// Timing spec for the non-whitespace contract pattern.
//
// The published Zod schemas and JSON Schema documents accept a string when it
// contains one non-whitespace character. A long run of whitespace must be
// rejected in linear time. These tests fail while that check is the
// unanchored pattern `.*\S.*`, and they pass once the same check scans the
// string once.
//
// Run from the repository root:
//   cd src/Strategos.Contracts && npm ci
//   node --test ../../tests/contracts-redos/nonwhitespace-pattern.test.mjs

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import test from "node:test";

const repoRoot = path.resolve(import.meta.dirname, "../..");
const projectRoot = path.join(repoRoot, "src/Strategos.Contracts");
const distDir = path.join(projectRoot, "dist");
const schemaDir = path.join(projectRoot, "schemas/json-schema");
const bundledSchemaPath = path.join(projectRoot, "schemas/workflow-definition-v1.schema.json");

// Doubling the length multiplies a quadratic scan by about four and a linear
// scan by about two. 2.5 sits between those ratios.
const SMALL = 8_000;
const LARGE = 16_000;
const QUADRATIC_RATIO = 2.5;
// Used only when the short input finishes so quickly that a ratio is noise.
const FAST_LARGE_MS = 4;

const subject = { domainName: "orders", objectTypeName: "Order" };
const lattice = { axes: [], authorities: [] };
const catalog = { actions: [] };

function workflow(name) {
  return {
    schemaVersion: "1.0",
    name,
    steps: [],
    transitions: [],
    branchPoints: [],
    loops: [],
    forkPoints: [],
    failureHandlers: [],
    approvalPoints: [],
  };
}

function actionContract(overrides) {
  return {
    subject,
    name: "ship",
    requires: [],
    ensures: [],
    touches: [],
    idempotent: false,
    ...overrides,
  };
}

function actionReference(overrides) {
  return {
    domainName: "orders",
    objectTypeName: "Order",
    actionName: "ship",
    ...overrides,
  };
}

/**
 * Each entry is one published document field whose value is attacker-controlled
 * text. `document` builds the smallest instance that still reaches that field.
 */
const fields = [
  {
    name: "WorkflowDefinitionV1.name",
    module: "WorkflowDefinitionV1",
    schemaId: "WorkflowDefinitionV1.json",
    document: (value) => workflow(value),
  },
  {
    name: "ActionContractV1.name",
    module: "ActionContractV1",
    schemaId: "ActionContractV1.json",
    document: (value) => actionContract({ name: value }),
  },
  {
    name: "ActionContractV1.boundWorkflow",
    module: "ActionContractV1",
    schemaId: "ActionContractV1.json",
    document: (value) => actionContract({ boundWorkflow: value }),
  },
  {
    name: "ActionReferenceV1.domainName",
    module: "ActionReferenceV1",
    schemaId: "ActionReferenceV1.json",
    document: (value) => actionReference({ domainName: value }),
  },
  {
    name: "ActionReferenceV1.objectTypeName",
    module: "ActionReferenceV1",
    schemaId: "ActionReferenceV1.json",
    document: (value) => actionReference({ objectTypeName: value }),
  },
  {
    name: "ActionReferenceV1.actionName",
    module: "ActionReferenceV1",
    schemaId: "ActionReferenceV1.json",
    document: (value) => actionReference({ actionName: value }),
  },
  {
    name: "ActionSubjectV1.domainName",
    module: "ActionSubjectV1",
    schemaId: "ActionSubjectV1.json",
    document: (value) => ({ domainName: value, objectTypeName: "Order" }),
  },
  {
    name: "ActionSubjectV1.objectTypeName",
    module: "ActionSubjectV1",
    schemaId: "ActionSubjectV1.json",
    document: (value) => ({ domainName: "orders", objectTypeName: value }),
  },
  {
    name: "ProofCatalogV1.catalogId",
    module: "ProofCatalogV1",
    schemaId: "ProofCatalogV1.json",
    document: (value) => ({ schemaVersion: "1.0", catalogId: value, actions: catalog }),
  },
  {
    name: "TypedContractRefV1.schemaId",
    module: "TypedContractRefV1",
    schemaId: "TypedContractRefV1.json",
    document: (value) => ({ schemaId: value }),
  },
  {
    name: "WorkflowAuthorityStatementV1.statement",
    module: "WorkflowAuthorityStatementV1",
    schemaId: "WorkflowAuthorityStatementV1.json",
    document: (value) => ({ statement: value }),
  },
  {
    name: "AuthorityAxisV1.name",
    module: "AuthorityAxisV1",
    schemaId: "AuthorityAxisV1.json",
    document: (value) => ({ name: value, levels: [] }),
  },
  {
    name: "AuthorityCoordinateV1.axis",
    module: "AuthorityCoordinateV1",
    schemaId: "AuthorityCoordinateV1.json",
    document: (value) => ({ axis: value, level: "read" }),
  },
  {
    name: "AuthorityCoordinateV1.level",
    module: "AuthorityCoordinateV1",
    schemaId: "AuthorityCoordinateV1.json",
    document: (value) => ({ axis: "role", level: value }),
  },
  {
    name: "AuthorityDescriptorV1.name",
    module: "AuthorityDescriptorV1",
    schemaId: "AuthorityDescriptorV1.json",
    document: (value) => ({ name: value, coordinates: [], explicitImplications: [] }),
  },
  {
    name: "CompensationConfiguration.compensationStepType",
    module: "CompensationConfiguration",
    schemaId: "CompensationConfiguration.json",
    document: (value) => ({ compensationStepType: value }),
  },
  {
    name: "DomainAuthorityLatticeV1.domainName",
    module: "DomainAuthorityLatticeV1",
    schemaId: "DomainAuthorityLatticeV1.json",
    document: (value) => ({ domainName: value, lattice }),
  },
  {
    name: "ActionLinkPathSegmentV1",
    module: "ActionLinkPathSegmentV1",
    schemaId: "ActionLinkPathSegmentV1.json",
    document: (value) => value,
  },
];

function spaces(length) {
  return " ".repeat(length);
}

function elapsedMs(fn) {
  const started = process.hrtime.bigint();
  fn();
  return Number(process.hrtime.bigint() - started) / 1e6;
}

function assertLinear(label, run) {
  run(1);
  const small = elapsedMs(() => run(SMALL));
  const large = elapsedMs(() => run(LARGE));
  if (small < 2) {
    const samples = [large, elapsedMs(() => run(LARGE)), elapsedMs(() => run(LARGE))];
    samples.sort((left, right) => left - right);
    const median = samples[1];
    assert.ok(
      median < FAST_LARGE_MS,
      `${label}: ${LARGE} spaces took ${median.toFixed(2)}ms; a linear scan of that length stays under ${FAST_LARGE_MS}ms`,
    );
    return;
  }

  const ratio = large / small;
  assert.ok(
    ratio < QUADRATIC_RATIO,
    `${label}: ${SMALL} spaces took ${small.toFixed(1)}ms and ${LARGE} spaces took ${large.toFixed(1)}ms (ratio ${ratio.toFixed(2)}; a linear scan stays under ${QUADRATIC_RATIO})`,
  );
}

let ajvValidate = new Map();
let bundledValidate;

test.before(async () => {
  const compiled = spawnSync(
    "npx",
    ["tsc", "--project", "tsconfig.build.json", "--pretty", "false"],
    { cwd: projectRoot, encoding: "utf8" },
  );
  if (compiled.status !== 0) {
    throw new Error(`zod build failed\n${compiled.stdout ?? ""}\n${compiled.stderr ?? ""}`);
  }

  const { default: Ajv2020 } = await import(
    pathToFileURL(path.join(projectRoot, "node_modules/ajv/dist/2020.js")).href
  );
  const ajv = new Ajv2020({ strict: false, allErrors: true, validateFormats: false });
  const { readdir } = await import("node:fs/promises");
  for (const file of await readdir(schemaDir)) {
    if (!file.endsWith(".json")) continue;
    ajv.addSchema(JSON.parse(await readFile(path.join(schemaDir, file), "utf8")));
  }
  ajvValidate = new Map(fields.map((field) => [field.schemaId, ajv.getSchema(field.schemaId)]));
  for (const [schemaId, validate] of ajvValidate) {
    assert.equal(typeof validate, "function", `missing JSON Schema validator for ${schemaId}`);
  }

  const bundled = new Ajv2020({ strict: false, allErrors: true, validateFormats: false });
  bundledValidate = bundled.compile(JSON.parse(await readFile(bundledSchemaPath, "utf8")));
});

async function zodSchema(moduleName) {
  const imported = await import(pathToFileURL(path.join(distDir, `${moduleName}.js`)).href);
  const schema = imported[`${moduleName}Schema`];
  assert.ok(schema, `missing ${moduleName}Schema`);
  return schema;
}

for (const field of fields) {
  test(`zod ${field.name} rejects whitespace in linear time and still accepts embedded text`, async () => {
    const schema = await zodSchema(field.module);

    assert.equal(schema.safeParse(field.document("  a  ")).success, true, `${field.name} rejected embedded text`);
    assert.equal(schema.safeParse(field.document("a b")).success, true, `${field.name} rejected text with a space`);
    assert.equal(schema.safeParse(field.document("   ")).success, false, `${field.name} accepted whitespace`);
    assert.equal(schema.safeParse(field.document("")).success, false, `${field.name} accepted an empty string`);

    assert.throws(() => schema.parse(field.document(spaces(32))), /./, `${field.name} parse() accepted whitespace`);

    assertLinear(`zod ${field.name}`, (length) => {
      schema.safeParse(field.document(spaces(length)));
    });
  });

  test(`ajv ${field.name} rejects whitespace in linear time and still accepts embedded text`, async () => {
    const validate = ajvValidate.get(field.schemaId);

    assert.equal(validate(field.document("  a  ")), true, `${field.name} rejected embedded text: ${JSON.stringify(validate.errors)}`);
    assert.equal(validate(field.document("a b")), true, `${field.name} rejected text with a space: ${JSON.stringify(validate.errors)}`);
    assert.equal(validate(field.document("   ")), false, `${field.name} accepted whitespace`);
    assert.equal(validate(field.document("")), false, `${field.name} accepted an empty string`);

    assertLinear(`ajv ${field.name}`, (length) => {
      validate(field.document(spaces(length)));
    });
  });
}

test("zod ActionReferenceV1 rejects three whitespace fields in one document in linear time", async () => {
  const schema = await zodSchema("ActionReferenceV1");
  const document = (length) => ({
    domainName: spaces(length),
    objectTypeName: spaces(length),
    actionName: spaces(length),
  });

  assert.equal(schema.safeParse(document(8)).success, false);
  assertLinear("zod ActionReferenceV1 three fields", (length) => {
    schema.safeParse(document(length));
  });
});

test("ajv bundled workflow-definition-v1 schema rejects a whitespace name in linear time", async () => {
  assert.equal(bundledValidate(workflow("  a  ")), true, JSON.stringify(bundledValidate.errors));
  assert.equal(bundledValidate(workflow("   ")), false);
  const nameError = bundledValidate.errors?.some((error) => error.instancePath === "/name" && error.keyword === "pattern");
  assert.equal(nameError, true, JSON.stringify(bundledValidate.errors));

  assertLinear("ajv workflow-definition-v1.schema.json name", (length) => {
    bundledValidate(workflow(spaces(length)));
  });
});

test("zod and ajv reject a workflow name that is only tabs and newlines", async () => {
  const schema = await zodSchema("WorkflowDefinitionV1");
  const blank = "\t\n\r\f";
  assert.equal(schema.safeParse(workflow(blank)).success, false);
  assert.equal(bundledValidate(workflow(blank)), false);
});
