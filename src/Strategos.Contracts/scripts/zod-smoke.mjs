// =============================================================================
// zod-smoke.mjs — T12 Zod-consumability smoke (#36).
//
// Proves the Exarchos derivation path works with NO manual post-processing:
//   1. dereference every emitted JSON Schema ($ref resolution via
//      @apidevtools/json-schema-ref-parser — the spike's known-issue mitigation);
//   2. convert each dereferenced schema to a Zod module (json-schema-to-zod);
//   3. write a barrel index.ts re-exporting every module.
//
// Usage:  node scripts/zod-smoke.mjs <out-dir>
// Exits non-zero if any schema fails to dereference or convert.
// =============================================================================

import { readdir, readFile, writeFile, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

import refParserPkg from "@apidevtools/json-schema-ref-parser";
import { jsonSchemaToZod } from "json-schema-to-zod";

const $RefParser = refParserPkg.default ?? refParserPkg;

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const schemaDir = path.join(projectRoot, "schemas", "json-schema");

const outDir = process.argv[2];
if (!outDir) {
  console.error("usage: node scripts/zod-smoke.mjs <out-dir>");
  process.exit(2);
}

async function main() {
  await mkdir(outDir, { recursive: true });

  const files = (await readdir(schemaDir)).filter((f) => f.endsWith(".json"));
  if (files.length === 0) {
    console.error(`no emitted schemas found in ${schemaDir} (did tsp compile run?)`);
    process.exit(1);
  }

  const moduleNames = [];

  for (const file of files) {
    const name = path.basename(file, ".json");
    const raw = JSON.parse(await readFile(path.join(schemaDir, file), "utf8"));

    // (1) dereference — resolve cross-file $refs against the schema dir so the
    // converter sees a self-contained document (no manual flattening).
    let dereferenced;
    try {
      dereferenced = await $RefParser.dereference(path.join(schemaDir, file), raw, {});
    } catch (err) {
      console.error(`dereference failed for ${file}: ${err?.message ?? err}`);
      process.exit(1);
    }

    // (2) convert to Zod — straight through, no post-processing.
    let zod;
    try {
      zod = jsonSchemaToZod(dereferenced, { module: "esm", name: `${name}Schema` });
    } catch (err) {
      console.error(`json-schema-to-zod failed for ${file}: ${err?.message ?? err}`);
      process.exit(1);
    }

    await writeFile(path.join(outDir, `${name}.ts`), zod, "utf8");
    moduleNames.push(name);
  }

  // (3) barrel index.
  const barrel =
    moduleNames
      .sort()
      .map((n) => `export * from "./${n}.js";`)
      .join("\n") + "\n";
  await writeFile(path.join(outDir, "index.ts"), barrel, "utf8");

  console.log(`zod-smoke: converted ${moduleNames.length} schema(s) -> ${outDir}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
