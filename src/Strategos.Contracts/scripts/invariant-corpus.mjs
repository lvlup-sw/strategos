// Shared, lossless frontmatter normalization for the three contract projections.
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { parseDocument } from "yaml";

export const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const corpusRoot = path.join(projectRoot, "fixtures", "invariants");

export function parseCatalog(text, name) {
  const match = /^---\r?\n([\s\S]*?)\r?\n---(?:\r?\n|$)/.exec(text);
  if (!match) throw new Error(`${name}: missing YAML frontmatter`);
  const parsed = parseDocument(match[1], { uniqueKeys: true });
  if (parsed.errors.length) throw new Error(`${name}: ${parsed.errors.join("; ")}`);
  const catalog = parsed.toJS();
  if (![2, 3].includes(catalog?.["schema-version"]) ||
      !Array.isArray(catalog.invariants) || catalog.invariants.length === 0) {
    throw new Error(`${name}: expected a nonempty v2/v3 invariant catalog`);
  }
  return catalog.invariants;
}

export async function loadCorpus() {
  const manifest = JSON.parse(await readFile(path.join(corpusRoot, "manifest.json"), "utf8"));
  if (manifest.catalogs?.length !== 2 ||
      new Set(manifest.catalogs.map(x => x.repository)).size !== 2) {
    throw new Error("Expected both repository fixtures");
  }
  const cases = [];
  for (const fixture of manifest.catalogs) {
    if (!/^[a-f0-9]{40}$/.test(fixture.revision)) throw new Error("Missing pinned revision");
    const entries = parseCatalog(await readFile(path.join(corpusRoot, fixture.file), "utf8"), fixture.file);
    const ids = entries.map(entry => entry.id);
    if (JSON.stringify(ids) !== JSON.stringify(fixture.ids) || new Set(ids).size !== ids.length) {
      throw new Error(`${fixture.file}: fixture IDs do not match the manifest`);
    }
    entries.forEach(document => cases.push({ name: `${fixture.file}/${document.id}`, model: "InvariantEntry", valid: true, document }));
  }
  const live = parseCatalog(await readFile(path.join(projectRoot, "../../.exarchos/invariants.md"), "utf8"), "live Strategos catalog");
  if (new Set(live.map(entry => entry.id)).size !== live.length) throw new Error("Duplicate live catalog IDs");
  live.forEach(document => cases.push({ name: `live/${document.id}`, model: "InvariantEntry", valid: true, document }));
  const synthetic = JSON.parse(await readFile(path.join(corpusRoot, "cases.json"), "utf8"));
  if (!synthetic.some(x => x.valid) || !synthetic.some(x => !x.valid)) throw new Error("Missing positive or negative cases");
  cases.push(...synthetic);
  if (new Set(cases.map(x => x.name)).size !== cases.length) throw new Error("Duplicate conformance case names");
  return cases;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  console.log(JSON.stringify(await loadCorpus()));
}
