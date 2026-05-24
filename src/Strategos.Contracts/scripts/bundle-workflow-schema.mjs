// =============================================================================
// bundle-workflow-schema.mjs — T17 / T23 workflow IR schema bundler (#50/#53).
//
// The json-schema emitter writes one document per model. For the equivalence
// gate (T23) a fixture must validate against the *whole* workflow IR as a
// single self-contained document. This script walks the per-model schemas
// reachable from WorkflowDefinitionV1, inlines them under $defs, rewrites every
// cross-file `$ref` (`Foo.json` → `#/$defs/Foo`), and writes one bundled
// document with a stable `$id`:
//
//   schemas/workflow-definition-v1.schema.json
//
// Run by scripts/contracts-codegen.sh after `tsp compile`, so the bundle is
// emitter-owned and covered by the codegen-guard diff.
// =============================================================================

import { readFile, writeFile, readdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const ROOT = "WorkflowDefinitionV1";
const STABLE_ID =
  "https://schemas.levelup.software/strategos/workflow-definition-v1.schema.json";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const schemaDir = path.join(projectRoot, "schemas", "json-schema");
const outPath = path.join(projectRoot, "schemas", "workflow-definition-v1.schema.json");

// Use `definitions` (not `$defs`) as the local store: it is the keyword the
// broadest set of validators — including NJsonSchema, the in-repo C# validator
// behind the equivalence gate (T23) — resolve `#/definitions/*` against
// reliably, including sibling references inside a discriminated-union arm.
const DEFS_KEY = "definitions";

/** Rewrite every `<Name>.json` $ref to a local `#/definitions/<Name>` pointer. */
function rewriteRefs(node) {
  if (Array.isArray(node)) {
    return node.map(rewriteRefs);
  }
  if (node && typeof node === "object") {
    const out = {};
    for (const [k, v] of Object.entries(node)) {
      if (k === "$ref" && typeof v === "string" && v.endsWith(".json")) {
        out.$ref = `#/${DEFS_KEY}/${path.basename(v, ".json")}`;
      } else {
        out[k] = rewriteRefs(v);
      }
    }
    return out;
  }
  return node;
}

/** Collect the set of document names reachable via $ref from a starting doc. */
function collectRefs(node, acc) {
  if (Array.isArray(node)) {
    node.forEach((n) => collectRefs(n, acc));
  } else if (node && typeof node === "object") {
    for (const [k, v] of Object.entries(node)) {
      if (k === "$ref" && typeof v === "string" && v.endsWith(".json")) {
        acc.add(path.basename(v, ".json"));
      } else {
        collectRefs(v, acc);
      }
    }
  }
}

async function loadSchema(name) {
  return JSON.parse(await readFile(path.join(schemaDir, `${name}.json`), "utf8"));
}

async function main() {
  const available = new Set(
    (await readdir(schemaDir))
      .filter((f) => f.endsWith(".json"))
      .map((f) => path.basename(f, ".json")),
  );
  if (!available.has(ROOT)) {
    console.error(`bundle: root ${ROOT}.json not found in ${schemaDir} (did tsp compile run?)`);
    process.exit(1);
  }

  // Transitive closure of everything reachable from the root.
  const reachable = new Set([ROOT]);
  const queue = [ROOT];
  while (queue.length > 0) {
    const name = queue.shift();
    const doc = await loadSchema(name);
    const refs = new Set();
    collectRefs(doc, refs);
    for (const ref of refs) {
      if (available.has(ref) && !reachable.has(ref)) {
        reachable.add(ref);
        queue.push(ref);
      }
    }
  }

  // Build definitions (drop per-doc $schema/$id; rewrite refs to local pointers).
  const defs = {};
  for (const name of [...reachable].sort()) {
    const doc = rewriteRefs(await loadSchema(name));
    delete doc.$schema;
    delete doc.$id;
    defs[name] = doc;
  }

  // Inline the root schema's body at the top level (rather than a bare `$ref`
  // root) so validators resolve `#/definitions/*` against this document without
  // a separate registration step. The root's own definition is also retained
  // under definitions for completeness.
  const rootBody = defs[ROOT];

  const bundle = {
    $schema: "https://json-schema.org/draft/2020-12/schema",
    $id: STABLE_ID,
    ...rootBody,
    [DEFS_KEY]: defs,
  };

  await writeFile(outPath, JSON.stringify(bundle, null, 4) + "\n", "utf8");
  console.log(`bundle: wrote ${outPath} (${reachable.size} definitions)`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
