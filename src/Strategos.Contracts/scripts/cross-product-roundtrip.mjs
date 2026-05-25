// =============================================================================
// cross-product-roundtrip.mjs — T31 cross-product round-trip harness
// (design §Resilience item 2; exarchos#1247).
//
// Direction implemented here (offline, single-repo): our emitted workflow-IR
// fixtures must PARSE against a Zod schema. The Zod is generated from OUR OWN
// bundled JSON Schema via the same proven zod-smoke pipeline
// (@apidevtools/json-schema-ref-parser → json-schema-to-zod → zod runtime).
//
// -----------------------------------------------------------------------------
// EXTERNAL-COORDINATION SEAM (exarchos#1247) — READ BEFORE CHANGING:
//
//   The PRODUCTION cross-product gate must run our fixtures against EXARCHOS'S
//   PUBLISHED Zod snapshot — proving the two products agree on the wire shape,
//   not merely that our own schema round-trips with itself. Pinning that
//   snapshot (a versioned Exarchos Zod barrel, vendored or fetched at a pinned
//   rev) is coordinated in exarchos#1247 and is OUT OF SCOPE for this milestone.
//
//   The seam is the `--zod-source` flag:
//     --zod-source self      (default) derive Zod from our own JSON Schema —
//                            offline, what this milestone ships.
//     --zod-source <dir>     PRODUCTION: a directory holding Exarchos's pinned
//                            Zod barrel (index.mjs exporting WorkflowSchema).
//                            Wiring this to the exarchos#1247 snapshot is the
//                            follow-up; this harness already accepts it so the
//                            swap is a CI-config change, not a code change.
// -----------------------------------------------------------------------------
//
// Usage:
//   node scripts/cross-product-roundtrip.mjs --fixtures <dir> [--zod-source self|<dir>]
//
// Exits non-zero if any fixture fails to parse against the Zod schema.
// =============================================================================

import { readdir, readFile, writeFile, mkdir, rm } from "node:fs/promises";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";

import refParserPkg from "@apidevtools/json-schema-ref-parser";
import { jsonSchemaToZod } from "json-schema-to-zod";

const $RefParser = refParserPkg.default ?? refParserPkg;

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const bundledSchema = path.join(projectRoot, "schemas", "workflow-definition-v1.schema.json");
const perModelSchemaDir = path.join(projectRoot, "schemas", "json-schema");

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i >= 0 && i + 1 < process.argv.length ? process.argv[i + 1] : fallback;
}

const fixturesDir = arg("--fixtures");
const smqFixturesDir = arg("--smq-fixtures");
const zodSource = arg("--zod-source", "self");
if (!fixturesDir && !smqFixturesDir) {
  console.error(
    "usage: node scripts/cross-product-roundtrip.mjs (--fixtures <dir> [--zod-source self|<dir>] | --smq-fixtures <dir>)");
  process.exit(2);
}

/**
 * Resolve the Zod `WorkflowSchema` to validate against.
 *  - "self": derive it from our own bundled JSON Schema (offline, this milestone).
 *  - "<dir>": import Exarchos's pinned Zod barrel — the exarchos#1247 seam.
 */
async function resolveWorkflowZod() {
  if (zodSource !== "self") {
    // EXTERNAL SEAM: load Exarchos's pinned barrel (exarchos#1247). The barrel
    // must export `WorkflowSchema`. This path is intentionally unexercised in
    // this milestone (no Exarchos repo here) — present so the production swap
    // is a config change.
    const barrel = path.resolve(zodSource, "index.mjs");
    const mod = await import(pathToFileURL(barrel).href);
    if (!mod.WorkflowSchema) {
      throw new Error(`pinned Zod barrel ${barrel} does not export WorkflowSchema`);
    }
    return { schema: mod.WorkflowSchema, origin: `pinned Exarchos barrel (${barrel})` };
  }

  // Self path: dereference our bundled schema, convert to a Zod ESM module,
  // write it next to our node_modules so `import { z } from "zod"` resolves,
  // then import it and pull out WorkflowSchema.
  const raw = JSON.parse(await readFile(bundledSchema, "utf8"));
  const deref = await $RefParser.dereference(bundledSchema, raw, {});
  const code = jsonSchemaToZod(deref, { module: "esm", name: "WorkflowSchema", type: false });

  const tmpDir = path.join(projectRoot, ".roundtrip-zod.tmp");
  await mkdir(tmpDir, { recursive: true });
  const modPath = path.join(tmpDir, "workflow.mjs");
  await writeFile(modPath, code, "utf8");
  const mod = await import(pathToFileURL(modPath).href);
  return {
    schema: mod.WorkflowSchema,
    origin: "self (derived from our own JSON Schema)",
    cleanup: tmpDir,
  };
}

/**
 * S1–S4 SMQ round-trip: each fixture file is named `<TypeName>.json` and must
 * parse against a Zod schema derived from our own emitted per-model JSON Schema
 * `schemas/json-schema/<TypeName>.json` (sibling `$ref`s dereferenced). This is
 * the four-new-top-level-type extension of the workflow round-trip: same offline,
 * single-repo guarantee — our emitted bindings round-trip against Zod derived
 * from our own schema.
 */
async function runSmqRoundTrip(dir) {
  const fixtures = (await collectFixtures(dir)).filter((f) => f.endsWith(".json"));
  if (fixtures.length === 0) {
    console.error(`no SMQ fixtures found under ${dir}`);
    process.exit(1);
  }

  const tmpDir = path.join(projectRoot, ".roundtrip-smq.tmp");
  await mkdir(tmpDir, { recursive: true });

  const failures = [];
  try {
    for (const fixture of fixtures) {
      const typeName = path.basename(fixture, ".json");
      const schemaPath = path.join(perModelSchemaDir, `${typeName}.json`);
      let zodSchema;
      try {
        const deref = await $RefParser.dereference(schemaPath, {});
        const code = jsonSchemaToZod(deref, { module: "esm", name: "Schema", type: false });
        const modPath = path.join(tmpDir, `${typeName}.mjs`);
        await writeFile(modPath, code, "utf8");
        const mod = await import(pathToFileURL(modPath).href);
        zodSchema = mod.Schema;
      } catch (e) {
        failures.push(`${typeName}: schema/Zod derivation failed: ${e.message}`);
        continue;
      }

      const data = JSON.parse(await readFile(fixture, "utf8"));
      const result = zodSchema.safeParse(data);
      if (!result.success) {
        failures.push(`${typeName}: ${JSON.stringify(result.error.issues)}`);
      }
    }

    if (failures.length > 0) {
      console.error(`cross-product round-trip: ${failures.length} SMQ fixture(s) FAILED to parse against Zod:`);
      for (const m of failures) {
        console.error("  " + m);
      }
      process.exit(1);
    }

    console.log(`cross-product round-trip: ${fixtures.length} SMQ fixtures parsed against Zod. OK.`);
  } finally {
    await rm(tmpDir, { recursive: true, force: true });
  }
}

async function collectFixtures(dir) {
  const out = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      out.push(...(await collectFixtures(full)));
    } else if (entry.name.endsWith(".json") && entry.name !== "index.json") {
      out.push(full);
    }
  }
  return out;
}

async function main() {
  // SMQ mode (S1–S4): validate the four new top-level types against per-model
  // schema-derived Zod. Self-contained; does not touch the workflow path.
  if (smqFixturesDir) {
    await runSmqRoundTrip(smqFixturesDir);
    return;
  }

  const { schema, origin, cleanup } = await resolveWorkflowZod();
  console.log(`cross-product round-trip: Zod source = ${origin}`);

  try {
    const fixtures = await collectFixtures(fixturesDir);
    if (fixtures.length === 0) {
      console.error(`no fixtures found under ${fixturesDir}`);
      process.exit(1);
    }

    const failures = [];
    for (const f of fixtures) {
      const data = JSON.parse(await readFile(f, "utf8"));
      const result = schema.safeParse(data);
      if (!result.success) {
        failures.push(`${path.relative(fixturesDir, f)}: ${JSON.stringify(result.error.issues)}`);
      }
    }

    if (failures.length > 0) {
      console.error(`cross-product round-trip: ${failures.length} fixture(s) FAILED to parse against Zod:`);
      for (const m of failures.slice(0, 10)) {
        console.error("  " + m);
      }
      process.exit(1);
    }

    console.log(`cross-product round-trip: ${fixtures.length} fixtures parsed against Zod. OK.`);
  } finally {
    if (cleanup) {
      await rm(cleanup, { recursive: true, force: true });
    }
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
