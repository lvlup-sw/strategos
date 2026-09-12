// Validates the exact same normalized documents .NET consumes, against shipping schemas.
import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import { loadCorpus, parseCatalog, projectRoot } from "./invariant-corpus.mjs";

const ajv = new Ajv2020({ strict: false, allErrors: true, validateFormats: false });
const schemaRoot = path.join(projectRoot, "schemas/json-schema");
for (const name of await readdir(schemaRoot)) {
  if (name.endsWith(".json")) ajv.addSchema(JSON.parse(await readFile(path.join(schemaRoot, name), "utf8")));
}
const models = new Map();
const cases = await loadCorpus();
let rejected = 0;
for (const test of cases) {
  const validate = ajv.getSchema(`${test.model}.json`);
  assert.ok(validate, `Missing JSON Schema for ${test.model}`);
  assert.equal(validate(test.document), test.valid, `${test.name}: JSON Schema ${JSON.stringify(validate.errors)}`);
  if (!models.has(test.model)) models.set(test.model, await import(pathToFileURL(path.join(projectRoot, "dist", `${test.model}.js`))));
  const result = models.get(test.model)[`${test.model}Schema`].safeParse(test.document);
  assert.equal(result.success, test.valid, `${test.name}: Zod ${result.error?.message}`);
  if (result.success) assert.deepEqual(result.data, test.document, `${test.name}: Zod lost data`);
  else rejected++;
}
// Guard liveness: empty or malformed frontmatter cannot produce a green empty run.
assert.throws(() => parseCatalog("no frontmatter", "bad"));
assert.throws(() => parseCatalog("---\nschema-version: 3\ninvariants: []\n---\n", "empty"));
assert.throws(() => parseCatalog("---\nschema-version: 3\ninvariants: [\n---\n", "malformed"));
console.log(`invariant-conformance: ${cases.length} cases, ${rejected} rejected; JSON Schema and emitted Zod agree`);
