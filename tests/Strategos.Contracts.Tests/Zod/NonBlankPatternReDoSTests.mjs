// Timing spec for the non-blank contract pattern.
//
// The published Zod modules and JSON Schemas accept a string when it contains
// one non-blank character. A blank string of this length must be rejected in
// about the time of one linear scan. The schemas are compiled from the emitted
// sources the package publishes, then called through safeParse and Ajv2020.
//
// Run from the repository root, after installing the contracts toolchain:
//
//   npm ci --ignore-scripts --prefix src/Strategos.Contracts
//   node --test --test-concurrency=1 tests/Strategos.Contracts.Tests/Zod/NonBlankPatternReDoSTests.mjs
//
// These tests fail while validation of a long blank is superlinear.

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import test from "node:test";
import { pathToFileURL } from "node:url";

const BLANK_LENGTH = 56_000;
const LINEAR_FACTOR = 30;
const SLACK_MS = 25;

const contractsDir = path.resolve(import.meta.dirname, "../../../src/Strategos.Contracts");
const buildDir = path.join(contractsDir, "node_modules/.cache/strategos-nonblank-redos");
const require = createRequire(path.join(contractsDir, "package.json"));
const Ajv2020 = require("ajv/dist/2020.js");

const spaces = " ".repeat(BLANK_LENGTH);
const tabs = "\t".repeat(BLANK_LENGTH);
const padded = "  x  ";
const blankOnly = " \t\n ";

function compileZod() {
  rmSync(buildDir, { recursive: true, force: true });
  mkdirSync(buildDir, { recursive: true });
  const tsc = path.join(contractsDir, "node_modules/typescript/bin/tsc");
  const result = spawnSync(
    process.execPath,
    [
      tsc,
      "--project", path.join(contractsDir, "tsconfig.json"),
      "--noEmit", "false",
      "--outDir", buildDir,
      "--pretty", "false",
    ],
    { cwd: contractsDir, encoding: "utf8" });
  if (result.status !== 0) {
    throw new Error(`tsc failed\n${result.stdout ?? ""}\n${result.stderr ?? ""}`);
  }
  writeFileSync(path.join(buildDir, "package.json"), `${JSON.stringify({ type: "module" })}\n`);
}

async function loadSchema(moduleName) {
  const imported = await import(pathToFileURL(path.join(buildDir, `${moduleName}.js`)).href);
  const schema = imported[`${moduleName}Schema`];
  if (schema === undefined) {
    throw new Error(`missing export ${moduleName}Schema`);
  }
  return schema;
}

function loadAjv() {
  const ajv = new Ajv2020({ strict: false, validateFormats: false });
  const schemaDir = path.join(contractsDir, "schemas/json-schema");
  for (const name of readdirSync(schemaDir)) {
    if (!name.endsWith(".json")) {
      continue;
    }
    ajv.addSchema(JSON.parse(readFileSync(path.join(schemaDir, name), "utf8")));
  }
  const bundle = JSON.parse(readFileSync(
    path.join(contractsDir, "schemas/workflow-definition-v1.schema.json"),
    "utf8"));
  const bundleAjv = new Ajv2020({ strict: false, validateFormats: false });
  bundleAjv.addSchema(bundle);
  return { ajv, bundle, bundleAjv };
}

function elapsedMs(run) {
  const started = process.hrtime.bigint();
  const value = run();
  return { value, ms: Number(process.hrtime.bigint() - started) / 1e6 };
}

function assertPatternIsLinear(label, run) {
  const baseline = elapsedMs(() => /\S/.test(run.input));
  assert.ok(
    baseline.ms < SLACK_MS,
    `${label}: linear baseline took ${baseline.ms.toFixed(1)}ms`);
  const actual = elapsedMs(run.validate);
  assert.equal(actual.value.rejected, true, `${label}: blank input was accepted\n${actual.value.detail}`);
  const budget = baseline.ms * LINEAR_FACTOR + SLACK_MS;
  assert.ok(
    actual.ms < budget,
    `${label}: validating ${run.input.length} blank characters took ${actual.ms.toFixed(1)}ms; ` +
      `one linear non-blank scan took ${baseline.ms.toFixed(1)}ms (budget ${budget.toFixed(1)}ms)\n${actual.value.detail}`);
}

function samePath(actual, expected) {
  return JSON.stringify(actual) === JSON.stringify(expected);
}

function zodRejection(result, issuePath) {
  if (result.success) {
    return { rejected: false, detail: "zod accepted the document" };
  }
  const issue = result.error.issues.find((candidate) =>
    candidate.code === "invalid_format" && samePath(candidate.path, issuePath));
  if (issue === undefined) {
    return {
      rejected: false,
      detail: `zod issues ${JSON.stringify(result.error.issues.map((candidate) => ({
        code: candidate.code,
        path: candidate.path,
      })))}`,
    };
  }
  return { rejected: true, detail: "" };
}

function ajvRejection(validate, document, instancePath) {
  const valid = validate(document);
  if (valid) {
    return { rejected: false, detail: "ajv accepted the document" };
  }
  const error = (validate.errors ?? []).find((candidate) =>
    candidate.keyword === "pattern" && candidate.instancePath === instancePath);
  if (error === undefined) {
    return { rejected: false, detail: `ajv errors ${JSON.stringify(validate.errors)}` };
  }
  return { rejected: true, detail: "" };
}

function emptyWorkflow(name) {
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

function skillStep(extra) {
  return {
    kind: "skill",
    stepId: "s1",
    stepName: "start",
    isTerminal: true,
    stepType: "Start",
    ...extra,
  };
}

function workflowWithStep(extra) {
  return {
    ...emptyWorkflow("workflow"),
    steps: [skillStep(extra)],
  };
}

const actionContract = (name, boundWorkflow) => ({
  subject: { domainName: "domain", objectTypeName: "object" },
  name,
  requires: [],
  ensures: [],
  touches: [],
  idempotent: false,
  ...(boundWorkflow === undefined ? {} : { boundWorkflow }),
});

const directCases = [
  {
    name: "ActionLinkPathSegmentV1",
    zodModule: "ActionLinkPathSegmentV1",
    schemaId: "ActionLinkPathSegmentV1.json",
    bundleRef: "#/definitions/ActionLinkPathSegmentV1",
    issuePath: [],
    instancePath: "",
    document: (value) => value,
  },
  {
    name: "ActionReferenceV1.domainName",
    zodModule: "ActionReferenceV1",
    schemaId: "ActionReferenceV1.json",
    bundleRef: "#/definitions/ActionReferenceV1",
    issuePath: ["domainName"],
    instancePath: "/domainName",
    document: (value) => ({ domainName: value, objectTypeName: "object", actionName: "act" }),
  },
  {
    name: "ActionReferenceV1.objectTypeName",
    zodModule: "ActionReferenceV1",
    schemaId: "ActionReferenceV1.json",
    bundleRef: "#/definitions/ActionReferenceV1",
    issuePath: ["objectTypeName"],
    instancePath: "/objectTypeName",
    document: (value) => ({ domainName: "domain", objectTypeName: value, actionName: "act" }),
  },
  {
    name: "ActionReferenceV1.actionName",
    zodModule: "ActionReferenceV1",
    schemaId: "ActionReferenceV1.json",
    bundleRef: "#/definitions/ActionReferenceV1",
    issuePath: ["actionName"],
    instancePath: "/actionName",
    document: (value) => ({ domainName: "domain", objectTypeName: "object", actionName: value }),
  },
  {
    name: "ActionSubjectV1.domainName",
    zodModule: "ActionSubjectV1",
    schemaId: "ActionSubjectV1.json",
    issuePath: ["domainName"],
    instancePath: "/domainName",
    document: (value) => ({ domainName: value, objectTypeName: "object" }),
  },
  {
    name: "ActionSubjectV1.objectTypeName",
    zodModule: "ActionSubjectV1",
    schemaId: "ActionSubjectV1.json",
    issuePath: ["objectTypeName"],
    instancePath: "/objectTypeName",
    document: (value) => ({ domainName: "domain", objectTypeName: value }),
  },
  {
    name: "ActionContractV1.name",
    zodModule: "ActionContractV1",
    schemaId: "ActionContractV1.json",
    issuePath: ["name"],
    instancePath: "/name",
    document: (value) => actionContract(value),
  },
  {
    name: "ActionContractV1.boundWorkflow",
    zodModule: "ActionContractV1",
    schemaId: "ActionContractV1.json",
    issuePath: ["boundWorkflow"],
    instancePath: "/boundWorkflow",
    document: (value) => actionContract("act", value),
  },
  {
    name: "ProofCatalogV1.catalogId",
    zodModule: "ProofCatalogV1",
    schemaId: "ProofCatalogV1.json",
    issuePath: ["catalogId"],
    instancePath: "/catalogId",
    document: (value) => ({ schemaVersion: "1.0", catalogId: value, actions: { actions: [] } }),
  },
  {
    name: "TypedContractRefV1.schemaId",
    zodModule: "TypedContractRefV1",
    schemaId: "TypedContractRefV1.json",
    bundleRef: "#/definitions/TypedContractRefV1",
    issuePath: ["schemaId"],
    instancePath: "/schemaId",
    document: (value) => ({ schemaId: value }),
  },
  {
    name: "WorkflowAuthorityStatementV1.statement",
    zodModule: "WorkflowAuthorityStatementV1",
    schemaId: "WorkflowAuthorityStatementV1.json",
    bundleRef: "#/definitions/WorkflowAuthorityStatementV1",
    issuePath: ["statement"],
    instancePath: "/statement",
    document: (value) => ({ statement: value }),
  },
  {
    name: "WorkflowDefinitionV1.name",
    zodModule: "WorkflowDefinitionV1",
    schemaId: "WorkflowDefinitionV1.json",
    bundleRoot: true,
    issuePath: ["name"],
    instancePath: "/name",
    document: (value) => emptyWorkflow(value),
  },
  {
    name: "AuthorityAxisV1.name",
    zodModule: "AuthorityAxisV1",
    schemaId: "AuthorityAxisV1.json",
    issuePath: ["name"],
    instancePath: "/name",
    document: (value) => ({ name: value, levels: [] }),
  },
  {
    name: "AuthorityCoordinateV1.axis",
    zodModule: "AuthorityCoordinateV1",
    schemaId: "AuthorityCoordinateV1.json",
    bundleRef: "#/definitions/AuthorityCoordinateV1",
    issuePath: ["axis"],
    instancePath: "/axis",
    document: (value) => ({ axis: value, level: "level" }),
  },
  {
    name: "AuthorityCoordinateV1.level",
    zodModule: "AuthorityCoordinateV1",
    schemaId: "AuthorityCoordinateV1.json",
    bundleRef: "#/definitions/AuthorityCoordinateV1",
    issuePath: ["level"],
    instancePath: "/level",
    document: (value) => ({ axis: "axis", level: value }),
  },
  {
    name: "AuthorityDescriptorV1.name",
    zodModule: "AuthorityDescriptorV1",
    schemaId: "AuthorityDescriptorV1.json",
    issuePath: ["name"],
    instancePath: "/name",
    document: (value) => ({ name: value, coordinates: [], explicitImplications: [] }),
  },
  {
    name: "CompensationConfiguration.compensationStepType",
    zodModule: "CompensationConfiguration",
    schemaId: "CompensationConfiguration.json",
    bundleRef: "#/definitions/CompensationConfiguration",
    issuePath: ["compensationStepType"],
    instancePath: "/compensationStepType",
    document: (value) => ({ compensationStepType: value }),
  },
  {
    name: "DomainAuthorityLatticeV1.domainName",
    zodModule: "DomainAuthorityLatticeV1",
    schemaId: "DomainAuthorityLatticeV1.json",
    issuePath: ["domainName"],
    instancePath: "/domainName",
    document: (value) => ({ domainName: value, lattice: { axes: [], authorities: [] } }),
  },
];

const nestedWorkflowCases = [
  {
    name: "WorkflowDefinitionV1.steps.configuration.compensation.compensationStepType",
    issuePath: ["steps", 0, "configuration", "compensation", "compensationStepType"],
    instancePath: "/steps/0/configuration/compensation/compensationStepType",
    document: (value) => workflowWithStep({
      configuration: { compensation: { compensationStepType: value } },
    }),
  },
  {
    name: "WorkflowDefinitionV1.steps.action.domainName",
    issuePath: ["steps", 0, "action", "domainName"],
    instancePath: "/steps/0/action/domainName",
    document: (value) => workflowWithStep({
      action: { domainName: value, objectTypeName: "object", actionName: "act" },
    }),
  },
  {
    name: "WorkflowDefinitionV1.steps.action.objectTypeName",
    issuePath: ["steps", 0, "action", "objectTypeName"],
    instancePath: "/steps/0/action/objectTypeName",
    document: (value) => workflowWithStep({
      action: { domainName: "domain", objectTypeName: value, actionName: "act" },
    }),
  },
  {
    name: "WorkflowDefinitionV1.steps.action.actionName",
    issuePath: ["steps", 0, "action", "actionName"],
    instancePath: "/steps/0/action/actionName",
    document: (value) => workflowWithStep({
      action: { domainName: "domain", objectTypeName: "object", actionName: value },
    }),
  },
  {
    name: "WorkflowDefinitionV1.steps.inputs.schemaId",
    issuePath: ["steps", 0, "inputs", "schemaId"],
    instancePath: "/steps/0/inputs/schemaId",
    document: (value) => workflowWithStep({ inputs: { schemaId: value } }),
  },
  {
    name: "WorkflowDefinitionV1.authority.invariants.statement",
    issuePath: ["authority", "invariants", 0, "statement"],
    instancePath: "/authority/invariants/0/statement",
    document: (value) => ({
      ...emptyWorkflow("workflow"),
      authority: { invariants: [{ statement: value }] },
    }),
  },
];

compileZod();
const { ajv, bundle, bundleAjv } = loadAjv();
const zodModules = new Map();
for (const moduleName of new Set(directCases.map((entry) => entry.zodModule))) {
  zodModules.set(moduleName, await loadSchema(moduleName));
}
const workflowZod = zodModules.get("WorkflowDefinitionV1");
const workflowAjv = ajv.getSchema("WorkflowDefinitionV1.json");
const bundleRoot = bundleAjv.getSchema(bundle.$id);
if (workflowAjv === undefined || bundleRoot === undefined) {
  throw new Error("WorkflowDefinitionV1 schema did not load");
}

function bundleDefinition(pointer) {
  return bundleAjv.compile({ $ref: `${bundle.$id}${pointer}` });
}

test("non-blank contract patterns reject a long blank in linear time", async (t) => {
  for (const entry of directCases) {
    const zodSchema = zodModules.get(entry.zodModule);
    const jsonSchema = ajv.getSchema(entry.schemaId);
    if (jsonSchema === undefined) {
      throw new Error(`missing JSON schema ${entry.schemaId}`);
    }

    await t.test(`zod ${entry.name}`, () => {
      assertPatternIsLinear(`zod ${entry.name}`, {
        input: spaces,
        validate: () => zodRejection(zodSchema.safeParse(entry.document(spaces)), entry.issuePath),
      });
    });

    await t.test(`json schema ${entry.name}`, () => {
      assertPatternIsLinear(`json schema ${entry.name}`, {
        input: spaces,
        validate: () => ajvRejection(jsonSchema, entry.document(spaces), entry.instancePath),
      });
    });

    if (entry.bundleRoot) {
      await t.test(`bundled workflow schema ${entry.name}`, () => {
        assertPatternIsLinear(`bundled workflow schema ${entry.name}`, {
          input: spaces,
          validate: () => ajvRejection(bundleRoot, entry.document(spaces), entry.instancePath),
        });
      });
    }

    if (entry.bundleRef) {
      const validateDefinition = bundleDefinition(entry.bundleRef);
      await t.test(`bundled definition ${entry.name}`, () => {
        assertPatternIsLinear(`bundled definition ${entry.name}`, {
          input: spaces,
          validate: () => ajvRejection(validateDefinition, entry.document(spaces), entry.instancePath),
        });
      });
    }
  }

  await t.test("zod WorkflowDefinitionV1.name tabs", () => {
    assertPatternIsLinear("zod WorkflowDefinitionV1.name tabs", {
      input: tabs,
      validate: () => zodRejection(workflowZod.safeParse(emptyWorkflow(tabs)), ["name"]),
    });
  });

  await t.test("json schema WorkflowDefinitionV1.name tabs", () => {
    assertPatternIsLinear("json schema WorkflowDefinitionV1.name tabs", {
      input: tabs,
      validate: () => ajvRejection(workflowAjv, emptyWorkflow(tabs), "/name"),
    });
  });

  await t.test("bundled workflow schema WorkflowDefinitionV1.name tabs", () => {
    assertPatternIsLinear("bundled workflow schema WorkflowDefinitionV1.name tabs", {
      input: tabs,
      validate: () => ajvRejection(bundleRoot, emptyWorkflow(tabs), "/name"),
    });
  });

  const bundleWorkflowDefinition = bundleDefinition("#/definitions/WorkflowDefinitionV1");
  await t.test("bundled definition WorkflowDefinitionV1.name", () => {
    assertPatternIsLinear("bundled definition WorkflowDefinitionV1.name", {
      input: spaces,
      validate: () => ajvRejection(bundleWorkflowDefinition, emptyWorkflow(spaces), "/name"),
    });
  });

  for (const entry of nestedWorkflowCases) {
    await t.test(`zod ${entry.name}`, () => {
      assertPatternIsLinear(`zod ${entry.name}`, {
        input: spaces,
        validate: () => zodRejection(workflowZod.safeParse(entry.document(spaces)), entry.issuePath),
      });
    });
    await t.test(`json schema ${entry.name}`, () => {
      assertPatternIsLinear(`json schema ${entry.name}`, {
        input: spaces,
        validate: () => ajvRejection(workflowAjv, entry.document(spaces), entry.instancePath),
      });
    });
    await t.test(`bundled workflow schema ${entry.name}`, () => {
      assertPatternIsLinear(`bundled workflow schema ${entry.name}`, {
        input: spaces,
        validate: () => ajvRejection(bundleRoot, entry.document(spaces), entry.instancePath),
      });
    });
  }
});

test("non-blank contract patterns keep their meaning for ordinary strings", async (t) => {
  for (const entry of directCases) {
    const zodSchema = zodModules.get(entry.zodModule);
    const jsonSchema = ajv.getSchema(entry.schemaId);

    await t.test(`${entry.name} accepts text with surrounding blanks`, () => {
      const zodResult = zodSchema.safeParse(entry.document(padded));
      assert.equal(zodResult.success, true, JSON.stringify(zodResult.error?.issues?.slice(0, 4)));
      assert.equal(jsonSchema(entry.document(padded)), true, JSON.stringify(jsonSchema.errors));
      if (entry.bundleRoot) {
        assert.equal(bundleRoot(entry.document(padded)), true, JSON.stringify(bundleRoot.errors));
      }
      if (entry.bundleRef) {
        const validateDefinition = bundleDefinition(entry.bundleRef);
        assert.equal(
          validateDefinition(entry.document(padded)),
          true,
          JSON.stringify(validateDefinition.errors));
      }
    });

    await t.test(`${entry.name} rejects a short blank`, () => {
      const zodResult = zodSchema.safeParse(entry.document(blankOnly));
      assert.equal(zodRejection(zodResult, entry.issuePath).rejected, true, zodRejection(zodResult, entry.issuePath).detail);
      assert.equal(
        ajvRejection(jsonSchema, entry.document(blankOnly), entry.instancePath).rejected,
        true,
        ajvRejection(jsonSchema, entry.document(blankOnly), entry.instancePath).detail);
    });
  }

  await t.test("a workflow step compensation moniker with surrounding blanks stays valid", () => {
    const document = nestedWorkflowCases[0].document(padded);
    const zodResult = workflowZod.safeParse(document);
    assert.equal(zodResult.success, true, JSON.stringify(zodResult.error?.issues?.slice(0, 4)));
    assert.equal(workflowAjv(document), true, JSON.stringify(workflowAjv.errors));
    assert.equal(bundleRoot(document), true, JSON.stringify(bundleRoot.errors));
  });
});
