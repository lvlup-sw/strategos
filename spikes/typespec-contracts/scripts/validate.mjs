#!/usr/bin/env node
/**
 * Validate that generated JSON Schema files are valid and non-empty.
 * Also spot-checks that key models are present.
 */
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

const JSON_SCHEMA_DIR = join(import.meta.dirname, '..', 'tsp-output', 'json-schema');

const EXPECTED_MODELS = [
  'SdlcEventEnvelope',
  'WorkflowStartedData',
  'PhaseTransitionData',
  'TaskCompletedData',
  'GateExecutedData',
  'ReviewFindingData',
  'TeamSpawnedData',
  'TeammateInfo',
];

async function main() {
  const files = (await readdir(JSON_SCHEMA_DIR)).filter(f => f.endsWith('.json'));
  console.log(`Found ${files.length} JSON Schema files\n`);

  let errors = 0;

  // Check each file is valid JSON with expected structure
  for (const file of files) {
    try {
      const content = await readFile(join(JSON_SCHEMA_DIR, file), 'utf-8');
      const schema = JSON.parse(content);

      if (!schema.type && !schema.$ref && !schema.enum && !schema.oneOf) {
        console.error(`  ✗ ${file}: no type, $ref, enum, or oneOf`);
        errors++;
      } else {
        console.log(`  ✓ ${file} (${schema.type || 'enum/ref'})`);
      }
    } catch (err) {
      console.error(`  ✗ ${file}: ${err.message}`);
      errors++;
    }
  }

  // Check expected models are present
  console.log('\nExpected model check:');
  for (const model of EXPECTED_MODELS) {
    const found = files.some(f => f.includes(model));
    if (found) {
      console.log(`  ✓ ${model}`);
    } else {
      console.error(`  ✗ ${model} NOT FOUND`);
      errors++;
    }
  }

  if (errors > 0) {
    console.error(`\n${errors} error(s) found`);
    process.exit(1);
  } else {
    console.log(`\nAll checks passed`);
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
