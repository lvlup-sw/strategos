// =============================================================================
// emit-zod.mjs — the Zod/TypeScript projection of Strategos.Contracts (#219).
//
// Strategos owns every projection of the types it authors. This is the fourth
// target on the `scripts/contracts-codegen.sh` path, alongside JSON Schema, the
// C# records and the AGWF reference: TypeSpec is compiled to JSON Schema, and
// this reads that JSON Schema and writes one Zod module per document into
// Generated/zod/. The output is committed, and the codegen guard regenerates it
// and diffs it, so a hand-edit or a stale artifact fails CI like every other
// emitted target.
//
// Two properties this emitter holds deliberately:
//
//   TOTAL, OR IT STOPS. Every JSON Schema keyword it meets is either lowered or
//   an error. It never skips a keyword it does not understand, because a
//   silently dropped constraint produces a Zod schema that accepts documents
//   the contract rejects — and no corpus of VALID fixtures can detect that.
//
//   REFERENCES ARE DECLARED, NOT HAND-WRITTEN. `x-strategos-references-v1`
//   (emitted by the `@references` decorator) carries the referential rules core
//   JSON Schema cannot express. This lowers each one into a root-level check on
//   the single root document that owns the collection, so no consumer writes a
//   `superRefine` for a dangling `gateId` or a transition to nowhere.
//
// Usage:  node scripts/emit-zod.mjs [out-dir]      (default: ../Generated/zod)
// =============================================================================

import { mkdir, readdir, readFile, rm, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const schemaDir = path.join(projectRoot, "schemas", "json-schema");
const outDir = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.join(projectRoot, "Generated", "zod");

const REFERENCE_KEY = "x-strategos-references-v1";

/**
 * Keywords this emitter reads and lowers. A keyword outside this set stops the
 * emit — see the header. `format` and `default` are listed as ANNOTATIONS: JSON
 * Schema draft 2020-12 asserts neither by default, so lowering them would make
 * the Zod arm stricter (`format`) or lossy (`default`) than the contract.
 */
const LOWERED = new Set([
  "type", "const", "enum", "$ref", "anyOf",
  "properties", "required", "items",
  "minLength", "maxLength", "pattern",
  "minimum", "maximum", "minItems", "minProperties",
  "unevaluatedProperties", "if", "then",
]);
const ANNOTATIONS = new Set(["$schema", "$id", "description", "format", "default", "title"]);

class EmitError extends Error {}

function fail(message) {
  throw new EmitError(message);
}

// -----------------------------------------------------------------------------
// Load
// -----------------------------------------------------------------------------

async function loadDocuments() {
  const files = (await readdir(schemaDir)).filter((file) => file.endsWith(".json")).sort();
  if (files.length === 0) {
    fail(`no emitted schemas in ${schemaDir} (did tsp compile run?)`);
  }

  const documents = new Map();
  for (const file of files) {
    const name = path.basename(file, ".json");
    documents.set(name, JSON.parse(await readFile(path.join(schemaDir, file), "utf8")));
  }
  return documents;
}

/** Every document name reached by a `$ref` anywhere inside `node`. */
function directRefs(node, into = new Set()) {
  if (Array.isArray(node)) {
    for (const item of node) {
      directRefs(item, into);
    }
    return into;
  }
  if (node === null || typeof node !== "object") {
    return into;
  }
  for (const [key, value] of Object.entries(node)) {
    if (key === "$ref") {
      into.add(refName(value));
    } else {
      directRefs(value, into);
    }
  }
  return into;
}

function refName(ref) {
  if (typeof ref !== "string" || !ref.endsWith(".json")) {
    fail(`unsupported $ref '${ref}': only same-directory '<Name>.json' references are emitted`);
  }
  return ref.slice(0, -".json".length);
}

// -----------------------------------------------------------------------------
// Cycles
//
// ESM evaluates a cyclic import group in one order, so a module that reads a
// sibling's binding at evaluation time fails with a temporal-dead-zone error on
// whichever member evaluates first. `z.lazy` defers that read to parse time.
// Only edges INSIDE a strongly-connected component can close a cycle, so only
// those are deferred — every other reference stays direct and keeps its inferred
// type.
// -----------------------------------------------------------------------------

function stronglyConnected(graph) {
  const index = new Map();
  const low = new Map();
  const onStack = new Set();
  const stack = [];
  const components = [];
  let counter = 0;

  const visit = (node) => {
    index.set(node, counter);
    low.set(node, counter);
    counter += 1;
    stack.push(node);
    onStack.add(node);

    for (const next of graph.get(node) ?? []) {
      if (!graph.has(next)) {
        continue;
      }
      if (!index.has(next)) {
        visit(next);
        low.set(node, Math.min(low.get(node), low.get(next)));
      } else if (onStack.has(next)) {
        low.set(node, Math.min(low.get(node), index.get(next)));
      }
    }

    if (low.get(node) === index.get(node)) {
      const component = [];
      let member;
      do {
        member = stack.pop();
        onStack.delete(member);
        component.push(member);
      } while (member !== node);
      components.push(component);
    }
  };

  for (const node of [...graph.keys()].sort()) {
    if (!index.has(node)) {
      visit(node);
    }
  }
  return components;
}

/** Maps each document to its cycle group id, for documents inside a cycle. */
function cycleGroups(graph) {
  const groups = new Map();
  let id = 0;
  for (const component of stronglyConnected(graph)) {
    const selfReferential =
      component.length === 1 && (graph.get(component[0]) ?? new Set()).has(component[0]);
    if (component.length > 1 || selfReferential) {
      for (const member of component) {
        groups.set(member, id);
      }
      id += 1;
    }
  }
  return groups;
}

// -----------------------------------------------------------------------------
// Reference rules
// -----------------------------------------------------------------------------

/**
 * Reads every `@references` declaration off the emitted schemas and resolves the
 * root document each one belongs to.
 *
 * The declaration names a collection by a pointer rooted at the document that
 * OWNS the collection (`/gates`), not at the document that carries the property.
 * Resolution finds the one root document that both declares the pointer's head
 * as an array and reaches the annotated document. Zero candidates or more than
 * one is an error: an ambiguous rule would attach the check to an arbitrary
 * root, and a rule attached nowhere is a rule that does not hold.
 */
function collectReferenceRules(documents, graph) {
  const rules = [];

  for (const [owner, document] of [...documents.entries()].sort()) {
    for (const [property, schema] of Object.entries(document.properties ?? {})) {
      const declaration = schema?.[REFERENCE_KEY];
      if (declaration === undefined) {
        continue;
      }

      const { collection, idField } = declaration;
      if (typeof collection !== "string" || typeof idField !== "string") {
        fail(`${owner}.${property}: malformed ${REFERENCE_KEY} (expected { collection, idField })`);
      }

      const pointer = collection.slice(1).split("/");
      const root = resolveReferenceRoot(documents, graph, owner, pointer, collection, property);

      rules.push({
        root,
        owner,
        property,
        collection: pointer,
        idField,
        ownerRequired: [...(document.required ?? [])].sort(),
        ownerConsts: constProperties(document),
      });
    }
  }

  // A declaration that never reaches a root is unreachable at the point it is
  // read. Nested carriers are rejected in resolveReferenceRoot; this catches the
  // remaining shape — an annotation on a document no root schema refers to.
  return rules;
}

function resolveReferenceRoot(documents, graph, owner, pointer, collection, property) {
  const reachable = reachableFrom(graph);
  const candidates = [];

  for (const [name, document] of documents) {
    const head = document.properties?.[pointer[0]];
    if (head === undefined || head.type !== "array") {
      continue;
    }
    if (name !== owner && !(reachable.get(name) ?? new Set()).has(owner)) {
      continue;
    }
    candidates.push(name);
  }

  if (candidates.length === 0) {
    fail(
      `${owner}.${property}: @references('${collection}') resolves to no root document ` +
        `(no emitted schema declares '${pointer[0]}' as an array and reaches ${owner})`);
  }
  if (candidates.length > 1) {
    fail(
      `${owner}.${property}: @references('${collection}') is ambiguous — it resolves to ` +
        `${candidates.sort().join(", ")}. A reference rule must name one owning document.`);
  }
  if (pointer.length > 1) {
    fail(
      `${owner}.${property}: @references('${collection}') is a nested pointer. ` +
        `Only a top-level collection on the owning document is lowered.`);
  }
  return candidates[0];
}

function reachableFrom(graph) {
  const memo = new Map();
  const walk = (node, seen) => {
    if (memo.has(node)) {
      return memo.get(node);
    }
    const result = new Set();
    memo.set(node, result);
    for (const next of graph.get(node) ?? []) {
      if (!graph.has(next) || seen.has(next)) {
        result.add(next);
        continue;
      }
      result.add(next);
      seen.add(next);
      for (const deep of walk(next, seen)) {
        result.add(deep);
      }
    }
    return result;
  };
  for (const node of graph.keys()) {
    walk(node, new Set([node]));
  }
  return memo;
}

/** The `const`-pinned properties of a document — a discriminator, in practice. */
function constProperties(document) {
  const consts = {};
  for (const [name, schema] of Object.entries(document.properties ?? {})) {
    if (schema !== null && typeof schema === "object" && "const" in schema) {
      consts[name] = schema.const;
    }
  }
  return consts;
}

// -----------------------------------------------------------------------------
// JSON Schema -> Zod expression
// -----------------------------------------------------------------------------

function compile(node, context) {
  if (node === null || typeof node !== "object" || Array.isArray(node)) {
    fail(`${context.where}: expected a schema object, got ${JSON.stringify(node)}`);
  }

  for (const key of Object.keys(node)) {
    if (!LOWERED.has(key) && !ANNOTATIONS.has(key) && !key.startsWith("x-strategos-")) {
      fail(`${context.where}: unsupported JSON Schema keyword '${key}'`);
    }
  }

  // The empty schema `{}` admits any value. It appears as the value shape of an
  // untyped record (`Record<unknown>`), and lowering it to anything narrower
  // would make the Zod arm reject documents the contract accepts.
  if (!Object.keys(node).some((key) => LOWERED.has(key))) {
    return "z.unknown()";
  }

  if ("$ref" in node) {
    return reference(refName(node.$ref), context);
  }
  if ("anyOf" in node) {
    const arms = node.anyOf.map((arm, i) =>
      compile(arm, { ...context, where: `${context.where}.anyOf[${i}]` }));
    if (arms.length < 2) {
      fail(`${context.where}: anyOf needs at least two arms`);
    }
    return `z.union([${arms.join(", ")}])`;
  }
  if ("const" in node) {
    return `z.literal(${JSON.stringify(node.const)})`;
  }
  if ("enum" in node) {
    if (node.type !== "string") {
      fail(`${context.where}: only string enums are lowered, got type '${node.type}'`);
    }
    return `z.enum([${node.enum.map((member) => JSON.stringify(member)).join(", ")}])`;
  }

  switch (node.type) {
    case "object":
      return objectExpression(node, context);
    case "array":
      return arrayExpression(node, context);
    case "string":
      return stringExpression(node, context);
    case "integer":
    case "number":
      return numberExpression(node, context);
    case "boolean":
      return "z.boolean()";
    case "null":
      return "z.null()";
    default:
      fail(`${context.where}: unsupported type '${JSON.stringify(node.type)}'`);
      return "";
  }
}

function reference(name, context) {
  if (!context.documents.has(name)) {
    fail(`${context.where}: $ref to '${name}.json', which is not an emitted schema`);
  }
  context.imports.add(name);
  const ownGroup = context.cycles.get(context.self);
  const targetGroup = context.cycles.get(name);
  const inCycle = ownGroup !== undefined && ownGroup === targetGroup;
  return inCycle ? `z.lazy(() => ${name}Schema)` : `${name}Schema`;
}

function objectExpression(node, context) {
  const required = new Set(node.required ?? []);
  for (const name of required) {
    if (!(name in (node.properties ?? {}))) {
      fail(`${context.where}: '${name}' is required but not declared`);
    }
  }

  const members = Object.entries(node.properties ?? {}).map(([name, schema]) => {
    const expression = compile(schema, { ...context, where: `${context.where}.${name}` });
    const suffix = required.has(name) ? "" : ".optional()";
    return `${JSON.stringify(name)}: ${expression}${suffix}`;
  });

  // A JSON Schema object with no `additionalProperties: false` admits unknown
  // members, and this contract never writes one. `looseObject` keeps them on the
  // parsed value rather than stripping them, so a consumer that reads a document
  // from a newer producer and writes it back does not silently drop its slots.
  const shape = members.length === 0 ? "{}" : `{\n    ${members.join(",\n    ")},\n  }`;
  let expression = `z.looseObject(${shape})`;

  if ("unevaluatedProperties" in node) {
    const value = compile(node.unevaluatedProperties, {
      ...context,
      where: `${context.where}.unevaluatedProperties`,
    });
    expression = `z.record(z.string(), ${value})`;
    if (members.length > 0) {
      fail(`${context.where}: unevaluatedProperties alongside declared properties is not lowered`);
    }
  }

  if ("minProperties" in node) {
    expression =
      `${expression}.refine((value) => Object.keys(value).length >= ${node.minProperties}, ` +
      `{ message: "must declare at least ${node.minProperties} member(s)" })`;
  }

  if ("if" in node || "then" in node) {
    expression = `${expression}${conditionalRefinement(node, context)}`;
  }

  return expression;
}

/**
 * Lowers the one conditional shape this contract uses: "if these members are
 * present, that member is pinned to a constant". Anything else stops the emit
 * rather than being dropped.
 */
function conditionalRefinement(node, context) {
  const condition = node.if;
  const consequence = node.then;
  const conditionKeys = Object.keys(condition ?? {});
  const consequenceKeys = Object.keys(consequence ?? {});

  if (conditionKeys.length !== 1 || conditionKeys[0] !== "required") {
    fail(`${context.where}: only 'if: { required: [...] }' is lowered, got ${conditionKeys.join(", ")}`);
  }
  if (consequenceKeys.length !== 1 || consequenceKeys[0] !== "properties") {
    fail(`${context.where}: only 'then: { properties: {...} }' is lowered, got ${consequenceKeys.join(", ")}`);
  }

  const trigger = condition.required
    .map((name) => `value[${JSON.stringify(name)}] !== undefined`)
    .join(" && ");

  const checks = Object.entries(consequence.properties).map(([name, schema]) => {
    const keys = Object.keys(schema ?? {});
    if (keys.length !== 1 || keys[0] !== "const") {
      fail(`${context.where}: only a 'const' consequence is lowered for '${name}'`);
    }
    return {
      name,
      literal: JSON.stringify(schema.const),
    };
  });

  const body = checks
    .map(
      (check) =>
        `    if (value[${JSON.stringify(check.name)}] !== ${check.literal}) {\n` +
        `      context.addIssue({\n` +
        `        code: "custom",\n` +
        `        path: [${JSON.stringify(check.name)}],\n` +
        `        message: ${JSON.stringify(
          `must be ${check.literal} when ${condition.required.join(" and ")} is present`)},\n` +
        `      });\n` +
        `    }`)
    .join("\n");

  return `.superRefine((value, context) => {\n  if (${trigger}) {\n${body}\n  }\n})`;
}

function arrayExpression(node, context) {
  if (!("items" in node)) {
    fail(`${context.where}: array without 'items' is not lowered`);
  }
  const item = compile(node.items, { ...context, where: `${context.where}[]` });
  let expression = `z.array(${item})`;
  if ("minItems" in node) {
    expression = `${expression}.min(${node.minItems})`;
  }
  return expression;
}

function stringExpression(node, context) {
  let expression = "z.string()";
  if ("minLength" in node) {
    expression = `${expression}.min(${node.minLength})`;
  }
  if ("maxLength" in node) {
    expression = `${expression}.max(${node.maxLength})`;
  }
  if ("pattern" in node) {
    // JSON Schema `pattern` is an unanchored search, which is what RegExp.test
    // does — so the pattern transfers verbatim, with no added anchors.
    expression = `${expression}.regex(new RegExp(${JSON.stringify(node.pattern)}))`;
  }
  void context;
  return expression;
}

function numberExpression(node, context) {
  let expression = node.type === "integer" ? "z.int()" : "z.number()";
  if ("minimum" in node) {
    expression = `${expression}.min(${node.minimum})`;
  }
  if ("maximum" in node) {
    expression = `${expression}.max(${node.maximum})`;
  }
  void context;
  return expression;
}

// -----------------------------------------------------------------------------
// Emission
// -----------------------------------------------------------------------------

const BANNER = `// <auto-generated>
//   Emitted by src/Strategos.Contracts/scripts/emit-zod.mjs from the TypeSpec
//   sources (#219). Do not edit: the contracts codegen guard regenerates this
//   and fails the build on any difference.
// </auto-generated>
`;

function moduleSource(name, document, context, rules) {
  const expression = compile(document, { ...context, self: name, where: name });

  const imports = [...context.imports].filter((target) => target !== name).sort();
  const importLines = imports.map((target) => `import { ${target}Schema } from "./${target}.js";`);
  if (rules.length > 0) {
    importLines.unshift(`import { referenceRules } from "./_references.js";`);
  }

  const lines = [BANNER, `import { z } from "zod";`];
  if (importLines.length > 0) {
    lines.push("", ...importLines);
  }
  lines.push("");

  const body = rules.length > 0
    ? `${expression}.superRefine(referenceRules(${JSON.stringify(rules, null, 2)
        .split("\n")
        .join("\n")}))`
    : expression;

  // A schema inside a reference cycle reads a sibling that reads it back, so its
  // own type cannot be inferred from its initializer. The annotation breaks that
  // loop; it is the reason these few schemas carry no `z.infer` alias below.
  const cyclic = context.cycles.get(name) !== undefined;
  if (cyclic) {
    lines.push(`export const ${name}Schema: z.ZodType<unknown> = ${body};`);
    lines.push("");
    lines.push(`// No inferred type alias: ${name} participates in a reference cycle.`);
  } else {
    lines.push(`export const ${name}Schema = ${body};`);
    lines.push("");
    lines.push(`export type ${name} = z.infer<typeof ${name}Schema>;`);
  }

  return `${lines.join("\n")}\n`;
}

const REFERENCE_RUNTIME = `${BANNER}
import type { z } from "zod";

/**
 * One declared referential rule — the lowering of a \`@references\` decorator.
 *
 * \`property\` on a document shaped like \`owner\` must name an entry of the root's
 * \`collection\`, matched on \`idField\`. \`ownerRequired\` and \`ownerConsts\` are how
 * a runtime walk recognizes that shape without a schema: every required member
 * is present, and every discriminator constant agrees.
 */
export interface ReferenceRule {
  readonly root: string;
  readonly owner: string;
  readonly property: string;
  readonly collection: readonly string[];
  readonly idField: string;
  readonly ownerRequired: readonly string[];
  readonly ownerConsts: Readonly<Record<string, unknown>>;
}

type Node = Record<string, unknown>;

function isNode(value: unknown): value is Node {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function matchesOwner(node: Node, rule: ReferenceRule): boolean {
  if (typeof node[rule.property] !== "string") {
    return false;
  }
  for (const member of rule.ownerRequired) {
    if (node[member] === undefined) {
      return false;
    }
  }
  for (const [member, expected] of Object.entries(rule.ownerConsts)) {
    if (node[member] !== expected) {
      return false;
    }
  }
  return true;
}

/** Every declared id in the root collection the rule points at. */
function declaredIds(root: Node, rule: ReferenceRule): Set<string> {
  let cursor: unknown = root;
  for (const segment of rule.collection) {
    cursor = isNode(cursor) ? cursor[segment] : undefined;
  }
  const ids = new Set<string>();
  if (Array.isArray(cursor)) {
    for (const entry of cursor) {
      if (isNode(entry) && typeof entry[rule.idField] === "string") {
        ids.add(entry[rule.idField] as string);
      }
    }
  }
  return ids;
}

/** Collects every position in the document where the rule's owner shape occurs. */
function occurrences(
  node: unknown,
  rule: ReferenceRule,
  path: (string | number)[],
  into: { value: string; path: (string | number)[] }[],
): void {
  if (Array.isArray(node)) {
    node.forEach((item, index) => occurrences(item, rule, [...path, index], into));
    return;
  }
  if (!isNode(node)) {
    return;
  }
  if (matchesOwner(node, rule)) {
    into.push({ value: node[rule.property] as string, path: [...path, rule.property] });
  }
  for (const [key, value] of Object.entries(node)) {
    occurrences(value, rule, [...path, key], into);
  }
}

/**
 * Builds the \`superRefine\` that enforces a document's declared reference rules.
 *
 * The walk is structural on purpose. A reference may sit anywhere the owning
 * shape can appear — a gate step nested in a fork path, in a loop body, or in a
 * low-confidence handler chain — and those positions are mutually recursive, so
 * there is no finite list of paths to check instead.
 */
export function referenceRules(
  rules: readonly ReferenceRule[],
): (value: unknown, context: z.RefinementCtx) => void {
  return (value, context) => {
    if (!isNode(value)) {
      return;
    }
    for (const rule of rules) {
      const ids = declaredIds(value, rule);
      const found: { value: string; path: (string | number)[] }[] = [];
      occurrences(value, rule, [], found);
      for (const occurrence of found) {
        if (!ids.has(occurrence.value)) {
          context.addIssue({
            code: "custom",
            path: occurrence.path,
            message:
              \`'\${occurrence.value}' does not name an entry of /\${rule.collection.join("/")} \` +
              \`(matched on '\${rule.idField}')\`,
          });
        }
      }
    }
  };
}
`;

async function main() {
  const documents = await loadDocuments();

  const graph = new Map();
  for (const [name, document] of documents) {
    graph.set(name, directRefs(document));
  }
  for (const [name, targets] of graph) {
    for (const target of targets) {
      if (!documents.has(target)) {
        fail(`${name}: $ref to '${target}.json', which is not an emitted schema`);
      }
    }
  }

  const cycles = cycleGroups(graph);
  const rules = collectReferenceRules(documents, graph);
  const rulesByRoot = new Map();
  for (const rule of rules) {
    const { root, ...rest } = rule;
    if (!rulesByRoot.has(root)) {
      rulesByRoot.set(root, []);
    }
    rulesByRoot.get(root).push({ root, ...rest });
  }

  // Stale-clean, for the same reason the schema emit does it: a removed or
  // renamed model must not linger as an orphan module the guard never notices.
  await rm(outDir, { recursive: true, force: true });
  await mkdir(outDir, { recursive: true });

  const names = [...documents.keys()].sort();
  for (const name of names) {
    const context = { documents, cycles, imports: new Set() };
    const source = moduleSource(name, documents.get(name), context, rulesByRoot.get(name) ?? []);
    await writeFile(path.join(outDir, `${name}.ts`), source, "utf8");
  }

  await writeFile(path.join(outDir, "_references.ts"), REFERENCE_RUNTIME, "utf8");

  const barrel = [
    BANNER,
    `export type { ReferenceRule } from "./_references.js";`,
    ...names.map((name) => `export * from "./${name}.js";`),
    "",
  ].join("\n");
  await writeFile(path.join(outDir, "index.ts"), barrel, "utf8");

  const ruleSummary = rules
    .map((rule) => `${rule.owner}.${rule.property} -> ${rule.root}/${rule.collection.join("/")}`)
    .join(", ");
  console.log(
    `emit-zod: ${names.length} module(s) -> ${path.relative(projectRoot, outDir)}` +
      ` (${rules.length} reference rule(s)${rules.length > 0 ? `: ${ruleSummary}` : ""})`);
}

main().catch((error) => {
  if (error instanceof EmitError) {
    console.error(`emit-zod: ${error.message}`);
    process.exit(1);
  }
  console.error(error);
  process.exit(1);
});
