// =============================================================================
// contracts-schema-diff.mjs — fail-closed Contracts JSON Schema compatibility.
//
// Compares every packaged schema from a baseline schema tree (the preceding
// published Contracts package, or the pull request's merge base) with the
// candidate schema set. Missing/unreadable/empty inputs are INDETERMINATE and
// exit 2.
//
// A structural narrowing exits 1 unless BOTH of the following hold:
//   1. the candidate version carries the breaking-version increment required by
//      Contracts policy (pre-1.0 minor, post-1.0 major), and
//   2. the narrowing is named by an entry in the breaking-change allowlist
//      (`--allowlist`) whose `version` falls inside the compared window.
// The version increment alone is NOT sufficient: without (2) a pre-1.0 minor
// bump absorbs every narrowing and the gate can never fail for a product reason.
//
// The recursive rules mirror the authoritative C# classifier in
// Strategos.Contracts.SchemaDiff.JsonSchemaDiff.
// =============================================================================

import { readdir, readFile } from "node:fs/promises";
import path from "node:path";

const usage =
  "usage: node scripts/contracts-schema-diff.mjs " +
  "<previous-dir> <current-dir> <previous-version> <candidate-version> " +
  "[--allowlist <path>] [--allow-equal-versions]";

const DEFAULT_ALLOWLIST_PATH =
  "src/Strategos.Contracts/schemas/breaking-changes.allowlist.json";

function parseArguments(argv) {
  const positional = [];
  let allowlistPath = null;
  let allowEqualVersions = false;

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--allowlist") {
      const value = argv[index + 1];
      if (!value || value.startsWith("--")) return null;
      allowlistPath = value;
      index += 1;
    } else if (argument.startsWith("--allowlist=")) {
      allowlistPath = argument.slice("--allowlist=".length);
      if (!allowlistPath) return null;
    } else if (argument === "--allow-equal-versions") {
      allowEqualVersions = true;
    } else if (argument.startsWith("--")) {
      return null;
    } else {
      positional.push(argument);
    }
  }

  if (positional.length !== 4 || positional.some((value) => !value)) return null;
  return { positional, allowlistPath, allowEqualVersions };
}

const parsedArguments = parseArguments(process.argv.slice(2));
if (parsedArguments === null) {
  console.error(`[INDETERMINATE] ${usage}`);
  process.exit(2);
}

const [prevDir, nextDir, previousVersionText, candidateVersionText] =
  parsedArguments.positional;
const { allowlistPath, allowEqualVersions } = parsedArguments;

const BREAKING = "BREAKING";
const NOTICE = "NOTICE";
const NON_BREAKING = "NON-BREAKING";

const handledKeywords = new Set([
  "properties",
  "required",
  "type",
  "enum",
  "const",
  "$id",
  "$schema",
  "$ref",
  "minLength",
  "minItems",
  "minProperties",
  "pattern",
  "items",
  "anyOf",
  "oneOf",
  "allOf",
  "discriminator",
  "$defs",
  "definitions",
  "if",
  "then",
  "else",
  "dependentSchemas",
  "dependentRequired",
]);

const annotationKeywords = new Set([
  "$comment",
  "title",
  "description",
  "default",
  "examples",
  "deprecated",
  "readOnly",
  "writeOnly",
]);

class SchemaInputError extends Error {}

class ExactInteger {
  constructor(source) {
    this.value = BigInt(source);
  }
}

function parseJson(text) {
  return JSON.parse(text, (_key, value, context) => {
    if (
      typeof value === "number" &&
      Number.isInteger(value) &&
      context?.source &&
      /^-?(0|[1-9]\d*)$/.test(context.source) &&
      !Number.isSafeInteger(value)
    ) {
      return new ExactInteger(context.source);
    }

    return value;
  });
}

function parseVersion(value, label) {
  const match = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.exec(value);
  if (!match) {
    throw new SchemaInputError(
      `${label} version '${value}' is not a strict stable SemVer core (MAJOR.MINOR.PATCH)`,
    );
  }

  return {
    text: value,
    major: BigInt(match[1]),
    minor: BigInt(match[2]),
    patch: BigInt(match[3]),
  };
}

function compareVersions(left, right) {
  for (const component of ["major", "minor", "patch"]) {
    if (left[component] < right[component]) return -1;
    if (left[component] > right[component]) return 1;
  }

  return 0;
}

function permitsBreakingChange(previous, candidate) {
  if (previous.major === 0n) {
    return candidate.major > 0n ||
      (candidate.major === 0n && candidate.minor > previous.minor);
  }

  return candidate.major > previous.major;
}

function isObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function hasOwn(value, key) {
  return isObject(value) && Object.hasOwn(value, key);
}

function canonicalKey(value) {
  if (value instanceof ExactInteger) return `integer:${value.value};`;
  if (value === null) return "null;";
  if (Array.isArray(value)) return `array:[${value.map(canonicalKey).join("")}]`;
  if (isObject(value)) {
    return `object:{${Object.keys(value)
      .sort()
      .map((key) => `${JSON.stringify(key)}:${canonicalKey(value[key])}`)
      .join("")}}`;
  }

  return `${typeof value}:${JSON.stringify(value)};`;
}

function render(value) {
  if (value instanceof ExactInteger) return value.value.toString();
  return JSON.stringify(value, (_key, nested) =>
    nested instanceof ExactInteger ? nested.value.toString() : nested);
}

function equivalent(left, right) {
  return canonicalKey(left) === canonicalKey(right);
}

// The allowlist lives beside the schemas (and therefore ships inside the package
// content the baseline is extracted from), but it is policy data, not a schema:
// diffing it would make every allowlist edit register as a schema change.
const NON_SCHEMA_FILES = new Set(["breaking-changes.allowlist.json"]);

async function listSchemaFiles(root, current = root) {
  let entries;
  try {
    entries = await readdir(current, { withFileTypes: true });
  } catch (error) {
    throw new SchemaInputError(
      `schema directory '${current}' is unreadable: ${error.message}`,
    );
  }

  const files = [];
  for (const entry of entries) {
    const entryPath = path.join(current, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await listSchemaFiles(root, entryPath)));
    } else if (
      entry.isFile() &&
      entry.name.endsWith(".json") &&
      !NON_SCHEMA_FILES.has(entry.name)
    ) {
      files.push(path.relative(root, entryPath).split(path.sep).join("/"));
    }
  }

  return files.sort();
}

async function readSchemas(dir, label) {
  const files = await listSchemaFiles(dir);
  if (files.length === 0) {
    throw new SchemaInputError(`${label} schema directory '${dir}' contains no JSON schemas`);
  }

  const schemas = new Map();
  for (const file of files) {
    const schemaPath = path.join(dir, file);
    let schema;
    try {
      schema = parseJson(await readFile(schemaPath, "utf8"));
    } catch (error) {
      throw new SchemaInputError(`${label} schema '${schemaPath}' is unreadable: ${error.message}`);
    }

    if (!isObject(schema)) {
      throw new SchemaInputError(`${label} schema '${schemaPath}' is not a JSON object`);
    }

    schemas.set(file, schema);
  }

  return schemas;
}

function addChange(changes, severity, file, at, detail) {
  changes.push({
    severity,
    file,
    path: at,
    kind: detail,
    desc: `${file}: ${at}: ${detail}`,
  });
}

function properties(schema) {
  return isObject(schema?.properties) ? schema.properties : {};
}

function required(schema) {
  return new Set(
    Array.isArray(schema?.required)
      ? schema.required.filter((value) => typeof value === "string")
      : [],
  );
}

function diffProperties(file, prev, next, at, changes) {
  const prevProperties = properties(prev);
  const nextProperties = properties(next);
  const prevRequired = required(prev);
  const nextRequired = required(next);

  for (const name of Object.keys(prevProperties)) {
    if (!hasOwn(nextProperties, name)) {
      addChange(changes, BREAKING, file, at, `property '${name}' was removed`);
    }
  }

  for (const name of Object.keys(nextProperties)) {
    if (!hasOwn(prevProperties, name)) {
      const nowRequired = nextRequired.has(name);
      addChange(
        changes,
        nowRequired ? BREAKING : NON_BREAKING,
        file,
        at,
        nowRequired
          ? `property '${name}' was added as required`
          : `optional property '${name}' was added`,
      );
    }
  }

  for (const [name, previousProperty] of Object.entries(prevProperties)) {
    if (hasOwn(nextProperties, name)) {
      diffSchema(
        file,
        previousProperty,
        nextProperties[name],
        `${at}.properties[${JSON.stringify(name)}]`,
        changes,
      );
    }
  }

  for (const name of nextRequired) {
    const reportedAsNewRequiredProperty =
      !hasOwn(prevProperties, name) && hasOwn(nextProperties, name);
    if (!prevRequired.has(name) && !reportedAsNewRequiredProperty) {
      addChange(changes, BREAKING, file, at, `property '${name}' became required`);
    }
  }

  for (const name of prevRequired) {
    if (hasOwn(nextProperties, name) && !nextRequired.has(name)) {
      addChange(changes, NON_BREAKING, file, at, `property '${name}' is no longer required`);
    }
  }
}

function diffType(file, prev, next, at, changes) {
  const hadType = hasOwn(prev, "type");
  const hasType = hasOwn(next, "type");
  if (!hadType && !hasType) return;
  if (hadType && !hasType) {
    addChange(changes, NON_BREAKING, file, at, "declared type constraint was removed");
  } else if (!hadType && hasType) {
    addChange(changes, BREAKING, file, at, "declared type constraint was added");
  } else if (!equivalent(prev.type, next.type)) {
    addChange(
      changes,
      BREAKING,
      file,
      at,
      `declared type changed from ${render(prev.type)} to ${render(next.type)}`,
    );
  }
}

function diffEnum(file, prev, next, at, changes) {
  const hadEnum = hasOwn(prev, "enum");
  const hasEnum = hasOwn(next, "enum");
  if (!hadEnum && !hasEnum) return;
  if (!hadEnum || !hasEnum) {
    addChange(changes, BREAKING, file, at, "enum constraint was added or removed");
    return;
  }
  if ((!Array.isArray(prev.enum) && hadEnum) || (!Array.isArray(next.enum) && hasEnum)) {
    if (!equivalent(prev.enum, next.enum)) {
      addChange(changes, BREAKING, file, at, "enum constraint changed shape");
    }
    return;
  }

  const previous = prev.enum;
  const current = next.enum;
  const previousKeys = new Set(previous.map(canonicalKey));
  const currentKeys = new Set(current.map(canonicalKey));

  for (const value of previous) {
    if (!currentKeys.has(canonicalKey(value))) {
      addChange(changes, BREAKING, file, at, `enum member ${render(value)} was removed`);
    }
  }

  for (const value of current) {
    if (!previousKeys.has(canonicalKey(value))) {
      addChange(changes, NOTICE, file, at, `enum member ${render(value)} was added`);
    }
  }
}

function diffExactConstraint(file, prev, next, keyword, at, changes, removalIsBreaking) {
  const hadConstraint = hasOwn(prev, keyword);
  const hasConstraint = hasOwn(next, keyword);
  if (!hadConstraint && !hasConstraint) return;
  if (hadConstraint && hasConstraint && equivalent(prev[keyword], next[keyword])) return;

  if (hadConstraint && !hasConstraint && !removalIsBreaking) {
    addChange(changes, NON_BREAKING, file, at, `'${keyword}' constraint was removed`);
    return;
  }

  addChange(changes, BREAKING, file, at, `'${keyword}' constraint changed`);
}

function diffMinimum(file, prev, next, keyword, at, changes) {
  const hadMinimum = hasOwn(prev, keyword);
  const hasMinimum = hasOwn(next, keyword);
  if (!hadMinimum && !hasMinimum) return;

  const previous = hadMinimum ? prev[keyword] : 0;
  const current = hasMinimum ? next[keyword] : 0;
  const previousInteger = previous instanceof ExactInteger
    ? previous.value
    : Number.isSafeInteger(previous)
      ? BigInt(previous)
      : null;
  const currentInteger = current instanceof ExactInteger
    ? current.value
    : Number.isSafeInteger(current)
      ? BigInt(current)
      : null;
  if (
    previousInteger === null ||
    previousInteger < 0n ||
    currentInteger === null ||
    currentInteger < 0n
  ) {
    if (!equivalent(previous, current)) {
      addChange(changes, BREAKING, file, at, `'${keyword}' constraint changed shape`);
    }
    return;
  }

  if (currentInteger > previousInteger) {
    addChange(
      changes,
      BREAKING,
      file,
      at,
      `'${keyword}' increased from ${previousInteger} to ${currentInteger}`,
    );
  } else if (currentInteger < previousInteger) {
    addChange(
      changes,
      NON_BREAKING,
      file,
      at,
      `'${keyword}' decreased from ${previousInteger} to ${currentInteger}`,
    );
  }
}

function diffItems(file, prev, next, at, changes) {
  const hadItems = hasOwn(prev, "items");
  const hasItems = hasOwn(next, "items");
  if (!hadItems && !hasItems) return;
  if (!hadItems || !hasItems) {
    addChange(changes, BREAKING, file, at, "'items' schema was added or removed");
    return;
  }

  diffSchema(file, prev.items, next.items, `${at}.items`, changes);
}

function diffUnion(file, prev, next, keyword, at, changes) {
  const hadUnion = hasOwn(prev, keyword);
  const hasUnion = hasOwn(next, keyword);
  if (!hadUnion && !hasUnion) return;
  if (!hadUnion || !hasUnion || !Array.isArray(prev[keyword]) || !Array.isArray(next[keyword])) {
    if (!equivalent(prev[keyword], next[keyword])) {
      addChange(changes, BREAKING, file, at, `'${keyword}' union changed shape`);
    }
    return;
  }

  const previous = prev[keyword];
  const current = next[keyword];
  const previousKeys = new Set(previous.map(canonicalKey));
  const currentKeys = new Set(current.map(canonicalKey));

  for (const arm of previous) {
    if (!currentKeys.has(canonicalKey(arm))) {
      addChange(
        changes,
        BREAKING,
        file,
        at,
        `'${keyword}' union arm ${render(arm)} was removed or narrowed`,
      );
    }
  }

  for (const arm of current) {
    if (!previousKeys.has(canonicalKey(arm))) {
      const severity = keyword === "anyOf" ? NOTICE : BREAKING;
      addChange(
        changes,
        severity,
        file,
        at,
        `'${keyword}' union arm ${render(arm)} was added`,
      );
    }
  }
}

// `$defs` (2019-09+) and `definitions` (draft-07) are schema containers, not
// validation keywords: each named entry is a schema in its own right, so the
// classifier recurses per entry. An added definition is additive; a removed
// definition is breaking; a changed definition is classified by what changed
// inside it.
function diffDefinitions(file, prev, next, keyword, at, changes) {
  const hadDefinitions = hasOwn(prev, keyword);
  const hasDefinitions = hasOwn(next, keyword);
  if (!hadDefinitions && !hasDefinitions) return;
  if ((hadDefinitions && !isObject(prev[keyword])) || (hasDefinitions && !isObject(next[keyword]))) {
    if (!equivalent(prev[keyword], next[keyword])) {
      addChange(changes, BREAKING, file, at, `'${keyword}' container changed shape`);
    }
    return;
  }

  const previous = hadDefinitions ? prev[keyword] : {};
  const current = hasDefinitions ? next[keyword] : {};

  for (const name of Object.keys(previous)) {
    if (!hasOwn(current, name)) {
      addChange(changes, BREAKING, file, at, `definition '${name}' was removed`);
    }
  }

  for (const name of Object.keys(current)) {
    if (!hasOwn(previous, name)) {
      addChange(changes, NON_BREAKING, file, at, `definition '${name}' was added`);
    }
  }

  for (const [name, previousDefinition] of Object.entries(previous)) {
    if (hasOwn(current, name)) {
      diffSchema(
        file,
        previousDefinition,
        current[name],
        `${at}.${keyword}[${JSON.stringify(name)}]`,
        changes,
      );
    }
  }
}

// `if` / `then` / `else` / `dependentSchemas` / `dependentRequired` are the
// applicator keywords that make a document's validity depend on its own shape.
// Adding one, or changing one, can only reject documents the previous schema
// accepted, so it is a narrowing (BREAKING). Removing one only widens the
// accepted set (NON-BREAKING). The authoritative C# classifier
// (JsonSchemaDiff.DiffConditional) carries the same rule.
function diffConditional(file, prev, next, keyword, at, changes) {
  const hadConditional = hasOwn(prev, keyword);
  const hasConditional = hasOwn(next, keyword);
  if (!hadConditional && !hasConditional) return;
  if (hadConditional && hasConditional && equivalent(prev[keyword], next[keyword])) return;

  if (hadConditional && !hasConditional) {
    addChange(changes, NON_BREAKING, file, at, `'${keyword}' conditional was removed`);
    return;
  }

  addChange(changes, BREAKING, file, at, `'${keyword}' conditional was added or narrowed`);
}

function isAnnotation(keyword) {
  return annotationKeywords.has(keyword) || keyword.startsWith("x-");
}

function diffUnhandledKeywords(file, prev, next, at, changes) {
  const keywords = new Set([...Object.keys(prev), ...Object.keys(next)]);
  for (const keyword of keywords) {
    if (handledKeywords.has(keyword) || isAnnotation(keyword)) continue;
    if (!equivalent(prev[keyword], next[keyword])) {
      addChange(
        changes,
        BREAKING,
        file,
        at,
        `unsupported compatibility keyword '${keyword}' changed; safety cannot be proven`,
      );
    }
  }
}

function diffSchema(file, prev, next, at, changes) {
  if (equivalent(prev, next)) return;

  if (!isObject(prev) || !isObject(next)) {
    const relaxation = prev === false || next === true;
    addChange(
      changes,
      relaxation ? NON_BREAKING : BREAKING,
      file,
      at,
      "boolean or non-object schema changed",
    );
    return;
  }

  diffProperties(file, prev, next, at, changes);
  diffType(file, prev, next, at, changes);
  diffEnum(file, prev, next, at, changes);
  diffExactConstraint(file, prev, next, "$id", at, changes, true);
  diffExactConstraint(file, prev, next, "$schema", at, changes, true);
  diffExactConstraint(file, prev, next, "$ref", at, changes, true);
  diffExactConstraint(file, prev, next, "const", at, changes, true);
  diffExactConstraint(file, prev, next, "pattern", at, changes, false);
  diffExactConstraint(file, prev, next, "discriminator", at, changes, true);
  diffMinimum(file, prev, next, "minLength", at, changes);
  diffMinimum(file, prev, next, "minItems", at, changes);
  diffMinimum(file, prev, next, "minProperties", at, changes);
  diffItems(file, prev, next, at, changes);
  diffUnion(file, prev, next, "anyOf", at, changes);
  diffUnion(file, prev, next, "oneOf", at, changes);
  diffUnion(file, prev, next, "allOf", at, changes);
  diffDefinitions(file, prev, next, "$defs", at, changes);
  diffDefinitions(file, prev, next, "definitions", at, changes);
  diffConditional(file, prev, next, "if", at, changes);
  diffConditional(file, prev, next, "then", at, changes);
  diffConditional(file, prev, next, "else", at, changes);
  diffConditional(file, prev, next, "dependentSchemas", at, changes);
  diffConditional(file, prev, next, "dependentRequired", at, changes);
  diffUnhandledKeywords(file, prev, next, at, changes);
}

// -----------------------------------------------------------------------------
// Breaking-change allowlist.
//
// Each entry names one accepted narrowing by the exact triple the classifier
// reports — `file` (path relative to the schema root), `path` (the JSON-pointer-ish
// location `addChange` records) and `kind` (the reported message text) — plus the
// Contracts `version` that introduced it and a human `reason`. Matching is exact
// string equality on all three: a near-miss does not accept the change.
// -----------------------------------------------------------------------------

const ALLOWLIST_FIELDS = ["file", "path", "kind", "version", "reason"];

async function readAllowlist(allowlistFile) {
  if (!allowlistFile) return [];

  let text;
  try {
    text = await readFile(allowlistFile, "utf8");
  } catch (error) {
    throw new SchemaInputError(
      `breaking-change allowlist '${allowlistFile}' is unreadable: ${error.message}`,
    );
  }

  let document;
  try {
    document = JSON.parse(text);
  } catch (error) {
    throw new SchemaInputError(
      `breaking-change allowlist '${allowlistFile}' is not valid JSON: ${error.message}`,
    );
  }

  const entries = Array.isArray(document)
    ? document
    : isObject(document) && Array.isArray(document.entries)
      ? document.entries
      : null;
  if (entries === null) {
    throw new SchemaInputError(
      `breaking-change allowlist '${allowlistFile}' must be a JSON array of entries, ` +
        "or an object with an 'entries' array",
    );
  }

  return entries.map((entry, index) => {
    if (!isObject(entry)) {
      throw new SchemaInputError(
        `breaking-change allowlist '${allowlistFile}' entry ${index} is not a JSON object`,
      );
    }

    for (const field of ALLOWLIST_FIELDS) {
      if (typeof entry[field] !== "string" || entry[field].length === 0) {
        throw new SchemaInputError(
          `breaking-change allowlist '${allowlistFile}' entry ${index} is missing a ` +
            `non-empty string '${field}'`,
        );
      }
    }

    return {
      index,
      file: entry.file,
      path: entry.path,
      kind: entry.kind,
      version: parseVersion(entry.version, `allowlist entry ${index}`),
      reason: entry.reason,
      matched: false,
    };
  });
}

/** An entry applies only when the version that introduced it falls in
 * `(previous, candidate]` — the exact window this run compares. An entry outside
 * the window describes a narrowing that is either already baked into the baseline
 * or not yet shipped, so it cannot accept anything here. */
function entryIsInWindow(entry, previousVersion, candidateVersion) {
  return compareVersions(entry.version, previousVersion) > 0 &&
    compareVersions(entry.version, candidateVersion) <= 0;
}

function describeEntry(entry) {
  return `${entry.file}: ${entry.path}: ${entry.kind} (${entry.version.text}) — ${entry.reason}`;
}

async function main() {
  const previousVersion = parseVersion(previousVersionText, "previous");
  const candidateVersion = parseVersion(candidateVersionText, "candidate");
  const allowlistEntries = await readAllowlist(allowlistPath);
  const previousSchemas = await readSchemas(prevDir, "previous");
  const currentSchemas = await readSchemas(nextDir, "current");

  const versionOrder = compareVersions(candidateVersion, previousVersion);
  const equalVersions = versionOrder === 0;
  if (versionOrder < 0 || (equalVersions && !allowEqualVersions)) {
    console.error(
      `schema-diff: candidate version ${candidateVersion.text} must be greater than ` +
        `published baseline ${previousVersion.text}.`,
    );
    process.exitCode = 1;
    return;
  }

  const changes = [];
  for (const [file, previousSchema] of previousSchemas) {
    if (!currentSchemas.has(file)) {
      addChange(changes, BREAKING, file, "$", "schema file was removed");
      continue;
    }

    diffSchema(file, previousSchema, currentSchemas.get(file), "$", changes);
  }

  for (const file of currentSchemas.keys()) {
    if (!previousSchemas.has(file)) {
      addChange(changes, NON_BREAKING, file, "$", "new schema file was added");
    }
  }

  const breaking = changes.filter((change) => change.severity === BREAKING);
  const notices = changes.filter((change) => change.severity === NOTICE);
  for (const change of changes) console.log(`[${change.severity}] ${change.desc}`);
  if (changes.length === 0) console.log("schema-diff: no structural changes.");

  const allowlistLabel = allowlistPath ?? DEFAULT_ALLOWLIST_PATH;
  const applicableEntries = allowlistEntries.filter((entry) =>
    entryIsInWindow(entry, previousVersion, candidateVersion));
  for (const entry of allowlistEntries) {
    if (!applicableEntries.includes(entry)) {
      console.log(
        `[${NOTICE}] stale allowlist entry — version ${entry.version.text} is outside the ` +
          `compared window (${previousVersion.text}, ${candidateVersion.text}]: ` +
          describeEntry(entry),
      );
    }
  }

  if (breaking.length > 0) {
    if (permitsBreakingChange(previousVersion, candidateVersion)) {
      const increment = candidateVersion.major > previousVersion.major
        ? "major"
        : "pre-1.0 minor";
      const unmatched = [];
      for (const change of breaking) {
        const entry = applicableEntries.find((candidate) =>
          !candidate.matched &&
          candidate.file === change.file &&
          candidate.path === change.path &&
          candidate.kind === change.kind);
        if (entry) {
          entry.matched = true;
        } else {
          unmatched.push(change);
        }
      }

      for (const entry of applicableEntries) {
        if (entry.matched) console.log(`[ALLOWED] ${describeEntry(entry)}`);
      }

      if (unmatched.length > 0) {
        console.error(
          `\nschema-diff: ${unmatched.length} of ${breaking.length} BREAKING change(s) ` +
            `carry the ${increment} version increment ${previousVersion.text} -> ` +
            `${candidateVersion.text} but are NOT accepted by the breaking-change ` +
            "allowlist. For each one, add an entry to " +
            `${allowlistLabel} and a Contracts CHANGELOG line:`,
        );
        for (const change of unmatched) {
          console.error(
            `  - {"file": ${JSON.stringify(change.file)}, "path": ${JSON.stringify(change.path)}, ` +
              `"kind": ${JSON.stringify(change.kind)}, "version": ${JSON.stringify(candidateVersion.text)}, ` +
              '"reason": "<why this narrowing is correct>"}',
          );
        }

        process.exitCode = 1;
        return;
      }

      console.log(
        `\nschema-diff: ${breaking.length} BREAKING change(s) allowed by the ${increment} ` +
          `version increment ${previousVersion.text} -> ${candidateVersion.text}, each ` +
          `accepted by an entry in ${allowlistLabel}. OK.`,
      );
      return;
    }

    const requiredIncrement = previousVersion.major === 0n ? "MINOR" : "MAJOR";
    console.error(
      `\nschema-diff: ${breaking.length} BREAKING change(s) detected — a breaking ` +
        `schema change from ${previousVersion.text} requires a ${requiredIncrement} Contracts ` +
        `version bump; candidate is ${candidateVersion.text}. Bump ContractsVersion, then ` +
        `add an entry to ${allowlistLabel} and a Contracts CHANGELOG line.`,
    );
    process.exitCode = 1;
    return;
  }

  if (notices.length > 0) {
    console.log(
      `\nschema-diff: ${changes.length} change(s), no breaking — ${notices.length} ` +
        "NOTICE(s) require a consumer release-note and upgrade sequencing. OK.",
    );
    return;
  }

  console.log(`\nschema-diff: ${changes.length} change(s), all non-breaking. OK.`);
}

try {
  await main();
} catch (error) {
  console.error(`[INDETERMINATE] schema-diff: ${error.message}`);
  process.exitCode = 2;
}
