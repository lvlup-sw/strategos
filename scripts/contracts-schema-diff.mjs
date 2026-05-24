// =============================================================================
// contracts-schema-diff.mjs — T30 breaking-change JSON Schema structural diff.
//
// Compares a PREVIOUS set of emitted JSON Schemas against the CURRENT set and
// classifies each change as BREAKING or NON-BREAKING. Exits non-zero if any
// breaking change is found (the CI gate: a breaking change demands a major
// version bump per design §Resilience item 3).
//
// The classification rules mirror — and are kept honest by — the tested C#
// harness `Strategos.Contracts.SchemaDiff.JsonSchemaDiff` (see
// src/Strategos.Contracts.Tests/Pipeline/SchemaDiffTests.cs). The C# harness is
// the authoritative, unit-tested specification of these rules; this script is
// the offline, dependency-free CI driver over the *file set* (the C# tests
// exercise the rules over in-memory fixtures so they stay deterministic).
//
// Usage:  node scripts/contracts-schema-diff.mjs <previous-schemas-dir> <current-schemas-dir>
//
// A removed property, a newly-required property, or a narrowed property type is
// BREAKING; an added optional property or a relaxed `required` is NON-BREAKING.
// A schema present in CURRENT but absent in PREVIOUS is a new schema file
// (NON-BREAKING); a schema present in PREVIOUS but absent in CURRENT is a
// removed contract (BREAKING).
// =============================================================================

import { readdir, readFile } from "node:fs/promises";
import path from "node:path";

const [prevDir, nextDir] = process.argv.slice(2);
if (!prevDir || !nextDir) {
  console.error("usage: node scripts/contracts-schema-diff.mjs <previous-dir> <current-dir>");
  process.exit(2);
}

const BREAKING = "BREAKING";
const NON_BREAKING = "NON-BREAKING";

async function readSchemas(dir) {
  let files;
  try {
    files = (await readdir(dir)).filter((f) => f.endsWith(".json"));
  } catch {
    return new Map();
  }
  const map = new Map();
  for (const f of files) {
    map.set(f, JSON.parse(await readFile(path.join(dir, f), "utf8")));
  }
  return map;
}

function props(schema) {
  return schema && typeof schema.properties === "object" ? schema.properties : {};
}
function required(schema) {
  return new Set(Array.isArray(schema?.required) ? schema.required : []);
}
function typeOf(propSchema) {
  return propSchema && typeof propSchema.type === "string" ? propSchema.type : null;
}

function diffSchema(file, prev, next) {
  const changes = [];
  const pProps = props(prev);
  const nProps = props(next);
  const pReq = required(prev);
  const nReq = required(next);

  for (const name of Object.keys(pProps)) {
    if (!(name in nProps)) {
      changes.push({ severity: BREAKING, desc: `${file}: property '${name}' was removed` });
    }
  }
  for (const name of Object.keys(nProps)) {
    if (!(name in pProps)) {
      const nowRequired = nReq.has(name);
      changes.push({
        severity: nowRequired ? BREAKING : NON_BREAKING,
        desc: nowRequired
          ? `${file}: property '${name}' was added as required`
          : `${file}: optional property '${name}' was added`,
      });
    }
  }
  for (const name of Object.keys(pProps)) {
    if (!(name in nProps)) continue;
    const pt = typeOf(pProps[name]);
    const nt = typeOf(nProps[name]);
    if (pt && nt && pt !== nt) {
      changes.push({
        severity: BREAKING,
        desc: `${file}: property '${name}' changed type from '${pt}' to '${nt}'`,
      });
    }
  }
  for (const name of nReq) {
    if (name in pProps && !pReq.has(name)) {
      changes.push({ severity: BREAKING, desc: `${file}: property '${name}' became required` });
    }
  }
  for (const name of pReq) {
    if (!nReq.has(name) && name in nProps) {
      changes.push({ severity: NON_BREAKING, desc: `${file}: property '${name}' is no longer required` });
    }
  }
  return changes;
}

async function main() {
  const prev = await readSchemas(prevDir);
  const next = await readSchemas(nextDir);

  const changes = [];
  for (const [file, prevSchema] of prev) {
    if (!next.has(file)) {
      changes.push({ severity: BREAKING, desc: `${file}: schema file was removed` });
      continue;
    }
    changes.push(...diffSchema(file, prevSchema, next.get(file)));
  }
  for (const file of next.keys()) {
    if (!prev.has(file)) {
      changes.push({ severity: NON_BREAKING, desc: `${file}: new schema file added` });
    }
  }

  const breaking = changes.filter((c) => c.severity === BREAKING);
  for (const c of changes) {
    console.log(`[${c.severity}] ${c.desc}`);
  }
  if (changes.length === 0) {
    console.log("schema-diff: no structural changes.");
  }

  if (breaking.length > 0) {
    console.error(
      `\nschema-diff: ${breaking.length} BREAKING change(s) detected — a breaking ` +
        `schema change requires a MAJOR version bump (design §Resilience item 3).`,
    );
    process.exit(1);
  }
  console.log(`\nschema-diff: ${changes.length} change(s), all non-breaking. OK.`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
