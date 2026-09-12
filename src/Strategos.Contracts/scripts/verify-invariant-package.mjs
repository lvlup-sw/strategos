// Exercise package exports from an isolated installed tarball, not repository imports.
import { spawnSync } from "node:child_process";
import { mkdtemp, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { loadCorpus, projectRoot } from "./invariant-corpus.mjs";

function run(command, args, cwd) {
  const result = spawnSync(command, args, { cwd, encoding: "utf8" });
  if (result.status !== 0) throw new Error(`${command} failed: ${result.stdout}\n${result.stderr}`);
  return result.stdout;
}
const root = await mkdtemp(path.join(tmpdir(), "invariant-npm-consumer-"));
try {
  const packed = JSON.parse(run("npm", ["pack", "--json", "--pack-destination", root], projectRoot));
  if (packed.length !== 1) throw new Error("Expected exactly one package");
  await writeFile(path.join(root, "package.json"), JSON.stringify({ private: true, type: "module" }));
  run("npm", ["install", "--ignore-scripts", "--no-audit", "--no-fund", path.join(root, packed[0].filename)], root);
  await writeFile(path.join(root, "corpus.json"), JSON.stringify(await loadCorpus()));
  await writeFile(path.join(root, "probe.mjs"), `
    import assert from "node:assert/strict";
    import { readFile } from "node:fs/promises";
    import * as schemas from "@lvlup-sw/strategos-contracts";
    import { CheckNodeSchema } from "@lvlup-sw/strategos-contracts/CheckNode";
    assert.equal(CheckNodeSchema, schemas.CheckNodeSchema);
    const cases = JSON.parse(await readFile(new URL("./corpus.json", import.meta.url), "utf8"));
    for (const test of cases) {
      const result = schemas[test.model + "Schema"].safeParse(test.document);
      assert.equal(result.success, test.valid, test.name);
      if (result.success) assert.deepEqual(result.data, test.document, test.name);
    }
    console.log("invariant-package: " + cases.length + " installed-package cases passed");
  `);
  console.log(run("node", ["probe.mjs"], root).trim());
} finally {
  await rm(root, { recursive: true, force: true });
}
