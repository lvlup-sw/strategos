#!/usr/bin/env node
/**
 * Generate Zod schemas from TypeSpec-emitted JSON Schema files.
 *
 * Pipeline: TypeSpec → JSON Schema → Zod
 *
 * Usage: node scripts/generate-zod.mjs
 */
import { readdir, readFile, writeFile, mkdir } from 'node:fs/promises';
import { join, basename } from 'node:path';
import { jsonSchemaToZod } from 'json-schema-to-zod';

const JSON_SCHEMA_DIR = join(import.meta.dirname, '..', 'tsp-output', 'json-schema');
const ZOD_OUTPUT_DIR = join(import.meta.dirname, '..', 'generated', 'zod');

async function main() {
  await mkdir(ZOD_OUTPUT_DIR, { recursive: true });

  const files = (await readdir(JSON_SCHEMA_DIR)).filter(f => f.endsWith('.json'));
  console.log(`Found ${files.length} JSON Schema files`);

  const exports = [];

  for (const file of files) {
    const schema = JSON.parse(await readFile(join(JSON_SCHEMA_DIR, file), 'utf-8'));
    const modelName = basename(file, '.json');

    try {
      const zodCode = jsonSchemaToZod(schema, {
        name: `${modelName}Schema`,
        module: 'esm',
        type: true,
      });

      const outFile = `${modelName}.ts`;
      await writeFile(join(ZOD_OUTPUT_DIR, outFile), zodCode);
      exports.push({ modelName, outFile });
      console.log(`  ✓ ${file} → ${outFile}`);
    } catch (err) {
      console.error(`  ✗ ${file}: ${err.message}`);
    }
  }

  // Generate barrel export
  const barrel = exports
    .map(e => `export { ${e.modelName}Schema, type ${e.modelName} } from './${e.modelName}.js';`)
    .join('\n');
  await writeFile(join(ZOD_OUTPUT_DIR, 'index.ts'), barrel + '\n');
  console.log(`\nGenerated ${exports.length} Zod schemas + barrel export`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
