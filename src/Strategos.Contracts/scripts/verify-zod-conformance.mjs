// =============================================================================
// verify-zod-conformance.mjs — the #219 conformance gate for the emitted Zod.
//
// Two claims, both executed against the artifact that actually ships:
//
//   ACCEPTS. Every fixture in the #53 builder corpus (>= 100 WorkflowDefinitionV1
//   documents, produced from the real builder by the fixture-export test) parses
//   clean. The corpus is the same one the .NET arm validates, so a divergence
//   between the two arms shows up as a failure here rather than in a consumer.
//
//   REJECTS. A dangling `gateId` and a transition endpoint that names no step
//   are both rejected — by the emitted schema alone, with no consumer-written
//   `superRefine`. These are the rules `@references` declares and core JSON
//   Schema cannot express.
//
//   AGREES WITH THE .NET ARM. The gateId arm runs the same two hand-authored wire
//   documents the generator's AGWF032 test runs
//   (tests/Strategos.Generators.Tests/Import/ImportFixtures/, asserted by
//   HandAuthoredImportFixtureTests). The declared rule and the hand-coded check
//   in Import/WireToModelBridge.cs must reach the same verdict on the same bytes,
//   at the same position — `$.steps[1].gateId`, naming `gX`.
//
// The emitted modules are TypeScript. They are compiled here, into the project's
// own node_modules cache so `zod` resolves, and then executed: the artifact is
// type-checked and run, not read.
//
// Usage:  node scripts/verify-zod-conformance.mjs [fixtures-dir]
//         (default: <repo>/artifacts/builder-fixtures)
// =============================================================================

import { spawnSync } from "node:child_process";
import { mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(projectRoot, "..", "..");
const zodDir = path.join(projectRoot, "Generated", "zod");
const buildDir = path.join(projectRoot, "node_modules", ".cache", "strategos-zod");
const fixturesDir = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.join(repoRoot, "artifacts", "builder-fixtures");
const importFixturesDir = path.join(
  repoRoot, "tests", "Strategos.Generators.Tests", "Import", "ImportFixtures");

const failures = [];

function report(message) {
  failures.push(message);
  console.error(`  FAIL ${message}`);
}

/** Compiles Generated/zod into runnable ESM under the project's node_modules cache. */
async function build() {
  await rm(buildDir, { recursive: true, force: true });
  await mkdir(buildDir, { recursive: true });

  const result = spawnSync(
    "npx",
    [
      "tsc",
      "--project", path.join(projectRoot, "tsconfig.json"),
      "--noEmit", "false",
      "--outDir", buildDir,
    ],
    { cwd: projectRoot, encoding: "utf8" });

  if (result.status !== 0) {
    console.error(result.stdout ?? "");
    console.error(result.stderr ?? "");
    throw new Error("the emitted Zod projection does not type-check");
  }

  // tsc emits .js; NodeNext output is ESM, so the build dir declares it as such.
  // It sits inside node_modules so `import { z } from "zod"` resolves from the
  // project's own toolchain rather than a copy.
  await writeFile(
    path.join(buildDir, "package.json"), `${JSON.stringify({ type: "module" }, null, 2)}\n`, "utf8");
}

async function loadFixtures() {
  if (!existsSync(fixturesDir)) {
    throw new Error(
      `fixture corpus not found at ${fixturesDir}. Run the fixture-export test first:\n` +
        `  dotnet run --project tests/Strategos.Tests/Strategos.Tests.csproj ` +
        `-- --treenode-filter "/*/*/FixtureExportTests/*"`);
  }

  const fixtures = [];
  for (const tag of (await readdir(fixturesDir, { withFileTypes: true })).sort(byName)) {
    if (!tag.isDirectory()) {
      continue;
    }
    const tagDir = path.join(fixturesDir, tag.name);
    for (const entry of (await readdir(tagDir)).sort()) {
      if (!entry.endsWith(".json")) {
        continue;
      }
      fixtures.push({
        id: `${tag.name}/${entry}`,
        document: JSON.parse(await readFile(path.join(tagDir, entry), "utf8")),
      });
    }
  }
  return fixtures;
}

function byName(left, right) {
  return left.name < right.name ? -1 : left.name > right.name ? 1 : 0;
}

/** Deep structural clone that does not share any node with its source. */
function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

/** Rewrites the first value the predicate selects, returning the mutated copy. */
function mutate(document, select) {
  const copy = clone(document);
  return select(copy) ? copy : undefined;
}

/**
 * Runs the declared gateId rule over the AGWF032 fixture pair.
 *
 * `gate-bearing.workflow.json` is the document the import front-end accepts;
 * `dangling-gate.workflow.json` is the one it rejects with AGWF032 at
 * `$.steps[1].gateId`, naming `gX`. The emitted Zod must agree on both, and
 * must place the issue at the same position — a rule that fires somewhere else
 * is a different rule.
 */
async function gateIdArm(schema) {
  const load = async (name) =>
    JSON.parse(await readFile(path.join(importFixturesDir, name), "utf8"));

  const accepted = await load("gate-bearing.workflow.json");
  const acceptedResult = schema.safeParse(accepted);
  if (!acceptedResult.success) {
    report(
      `gate-bearing.workflow.json was rejected, but the import front-end accepts it: ` +
        JSON.stringify(acceptedResult.error.issues.slice(0, 3)));
  } else {
    console.log("zod-conformance: accepted gate-bearing.workflow.json (AGWF032-clean)");
  }

  const rejected = await load("dangling-gate.workflow.json");
  const rejectedResult = schema.safeParse(rejected);
  if (rejectedResult.success) {
    report("dangling-gate.workflow.json was accepted, but AGWF032 rejects it");
    return;
  }

  const issue = rejectedResult.error.issues.find(
    (candidate) => JSON.stringify(candidate.path) === JSON.stringify(["steps", 1, "gateId"]));
  if (issue === undefined) {
    report(
      `the dangling gateId was rejected, but not at $.steps[1].gateId where AGWF032 reports it: ` +
        JSON.stringify(rejectedResult.error.issues));
  } else if (!issue.message.includes("gX")) {
    report(`the dangling gateId issue does not name 'gX': ${issue.message}`);
  } else {
    console.log(
      "zod-conformance: rejected dangling-gate.workflow.json at $.steps[1].gateId, naming 'gX' " +
        "(the position and value AGWF032 reports)");
  }
}

async function main() {
  console.log("zod-conformance: building the emitted projection ...");
  await build();

  const { WorkflowDefinitionV1Schema } = await import(
    pathToFileURL(path.join(buildDir, "WorkflowDefinitionV1.js")).href);

  const fixtures = await loadFixtures();
  if (fixtures.length < 100) {
    report(`corpus has ${fixtures.length} fixtures; the #53 floor is 100`);
  }

  // (1) ACCEPTS — every corpus fixture.
  let accepted = 0;
  for (const fixture of fixtures) {
    const result = WorkflowDefinitionV1Schema.safeParse(fixture.document);
    if (result.success) {
      accepted += 1;
    } else {
      report(`${fixture.id} was rejected: ${JSON.stringify(result.error.issues.slice(0, 3))}`);
    }
  }
  console.log(`zod-conformance: accepted ${accepted}/${fixtures.length} corpus fixture(s)`);

  // (2) AGREES WITH AGWF032 — the same two hand-authored documents the generator
  //     test runs, with the same verdicts. The builder corpus cannot serve here:
  //     gates are consumer-plane data the DSL never emits (DR-3), so a gate step
  //     with a gateId exists only on the import path.
  await gateIdArm(WorkflowDefinitionV1Schema);

  // (3) REJECTS — a transition endpoint naming no step (the kernel's edges[]
  //     integrity case, on the construct the kernel maps a sequence onto).
  const transitionFixture = fixtures.find(
    (fixture) => Array.isArray(fixture.document.transitions) && fixture.document.transitions.length > 0);
  if (transitionFixture === undefined) {
    report("no corpus fixture declares a transition; the rejection arm cannot run");
  } else {
    for (const endpoint of ["fromStepId", "toStepId"]) {
      const dangling = mutate(transitionFixture.document, (copy) => {
        copy.transitions[0][endpoint] = "no-such-step";
        return true;
      });
      const result = WorkflowDefinitionV1Schema.safeParse(dangling);
      if (result.success) {
        report(`a dangling transition ${endpoint} was accepted (fixture ${transitionFixture.id})`);
      } else if (!JSON.stringify(result.error.issues).includes("no-such-step")) {
        report(`a dangling transition ${endpoint} was rejected for the wrong reason: ` +
          JSON.stringify(result.error.issues));
      } else {
        console.log(
          `zod-conformance: rejected a dangling transition ${endpoint} (from ${transitionFixture.id})`);
      }
    }
  }

  if (failures.length > 0) {
    console.error(`zod-conformance: ${failures.length} failure(s)`);
    process.exit(1);
  }
  console.log("zod-conformance: ok");
}

main().catch((error) => {
  console.error(`zod-conformance: ${error?.message ?? error}`);
  process.exit(1);
});
