# LevelUp.Strategos.Contracts

Cross-product schema substrate. **TypeSpec is the single canonical source.** The
build emits three artifacts from it:

- **JSON Schema** (`schemas/json-schema/*.json`) — language-neutral, embedded as
  NuGet content (`contentFiles/any/any/schemas/`).
- **C# records** (`Generated/*.g.cs`) — compiled into this DLL. Basileus
  references the DLL.
- **Zod modules** (`Generated/zod/*.ts`) — the TypeScript projection Exarchos
  pins (#219). Strategos owns every projection of the types it authors: one
  source, one emitter, one direction, one version.

```
main.tsp  (canonical)
   │ tsp compile  (@typespec/json-schema)
   ▼
schemas/json-schema/*.json
   ├─ Strategos.Contracts.Codegen  (raw JSON + INV-6/7 template)
   │  ▼
   │ Generated/*.g.cs   →   compiled into LevelUp.Strategos.Contracts.dll
   ├─ scripts/emit-zod.mjs  (+ x-strategos-references-v1)
   │  ▼
   │ Generated/zod/*.ts  →   consumed by Exarchos from the contracts-v* tag
   └─ x-strategos-* metadata → ContractOntologyCatalog (Ontology adapter)
```

Regenerate with `scripts/contracts-codegen.sh`. The emitted `schemas/` and
`Generated/` are **emitter-owned**: CI's codegen-guard (`.github/workflows/
contracts-codegen-guard.yml`) regenerates and fails on any hand-edit (DIM-6).

## C# emitter decision (T3 — INV-6 / INV-7 gate)

The generated records are consumer-facing contracts and must satisfy:

- **INV-6** — `sealed record`
- **INV-7** — every property `{ get; init; }`; collections as `IReadOnlyList<T>`

**Chosen path: NJsonSchema-backed custom template (the documented fallback), not
the native TypeSpec C# emitter.**

### Why not the native TypeSpec C# emitter

The only first-party TypeSpec C# emitter is `@typespec/http-client-csharp` (and
the Azure `@azure-tools/typespec-csharp`). Both are **HTTP service-client
generators**:

- They have hard peer dependencies on `@typespec/http` and
  `@azure-tools/typespec-client-generator-core` and expect operation/route
  definitions — they do not target plain `@jsonSchema` data models.
- Their model output is **mutable classes** (`public partial class` with
  `{ get; set; }`) plus client plumbing (pipelines, serialization helpers). They
  do not emit `sealed record` + `{ get; init; }` + `IReadOnlyList<T>`, and there
  is no configuration switch that produces that shape.

Shipping their default output would silently violate INV-6/INV-7 — exactly the
failure the decision gate exists to prevent.

### Why NJsonSchema + a custom template

NJsonSchema's own `CSharpClassStyle.Record` was also evaluated and rejected: in
11.6.1 it emits a `partial class` (not the `record` keyword), uses get-only
constructor-assigned properties (not `init` accessors), and adds a **mutable**
`AdditionalProperties` dictionary with a public setter — which violates the
init-only requirement.

So NJsonSchema is used **only as the parser / `$ref` resolver** (its robust
`JsonSchema` model), and `Strategos.Contracts.Codegen/RecordEmitter.cs` owns the
emitted shape with a template tuned to exactly `sealed record` + `{ get; init; }`
+ `IReadOnlyList<T>`. The shape is pinned by
`EmitterShapeTests.GeneratedRecord_IsSealed_InitOnly_ReadOnlyCollections`
(reflection over a generated record); if a future change regresses the shape,
that test goes red before anything ships.

**Implication for family tasks (T6+):** records are emitted by the in-repo
`Strategos.Contracts.Codegen` tool from JSON Schema, not by a TypeSpec emitter
plugin. New `.tsp` models flow through `scripts/contracts-codegen.sh`
automatically; no per-family emitter wiring is needed, but any new wire-name
encoding (e.g. `@encodedName` kebab-case for #98) must round-trip through the
`JsonPropertyName` the template emits.

### Emitter capabilities (extended in Family 1 / #36, Family 2 / #50, Family 3 / #98)

The emitter reads the raw JSON Schema directly (NJsonSchema is no longer on the
emit path) and classifies each document as enum / record / open-object:

- **String enums → C# `enum`.** Each member carries
  `[JsonStringEnumMemberName("<wire>")]` when the C# name diverges from the wire
  value (kebab/snake/camel/reserved-word), and the enum type carries
  `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` so it serializes to its
  **string** wire value — never a numeric ordinal — for Basileus and Zod. This
  is the `@encodedName` round-trip path #98 (kebab-case wire names) inherits.
- **`$ref` resolution.** A property `$ref`-ing another document resolves to the
  generated enum/record type (was `object`/`string`); enum refs are value types,
  record refs are reference types (so required record refs get the `= default!`
  INV-7 null-forgiving default).
- **Open objects** (`Record<unknown>`) are not emitted as standalone records;
  they surface as `object`-typed payload properties on their referrers.
- **Discriminated unions** (a top-level `anyOf` of `$ref` arms, the TypeSpec
  `union` form) → an `abstract record` base carrying
  `[JsonPolymorphic(TypeDiscriminatorPropertyName = "<discriminator>")]` and one
  `[JsonDerivedType]` per arm; each arm is a `sealed record` deriving from the
  base. The discriminator property is resolved **generically** — the const-pinned
  member shared by all arms, preferring `kind` — so the workflow-IR `kind` unions
  (#50) and the invariant `Enforcement` `mode` union (#98) both round-trip
  without hard-coding a single discriminator name.
- **Recursive types** (Family 3 / #98 `CheckNode`). A self-referential model (an
  arm whose payload `$ref`s the union it belongs to) needs no special
  handling: `$ref` resolution is **by document name**, not by recursive descent,
  so a cycle resolves to the generated type name (`IReadOnlyList<CheckNode>` /
  `CheckNode`) without infinite recursion. The combinator tree is declarative-only
  (LB-1 / INV-4) — no arm admits an executable member — a guarantee asserted
  structurally by `SandboxGuaranteeTests`.

`scripts/contracts-codegen.sh` now prunes stale `schemas/json-schema/*.json`
before `tsp compile`, so a removed/renamed TypeSpec model does not linger as an
orphan schema/record (the json-schema emitter does not prune its own output) —
keeping the codegen-guard diff honest across P2/P3 renames.

## Invariant catalogs (0.15.0)

`InvariantEntry` describes parsed catalog frontmatter entries. The entry stays open to
consumer metadata; the enforcement DSL and severity objects are closed. `citations`
is optional. The obsolete `axiom_overlap` contract field and C# property are removed.

The exact vocabularies are `InvariantAxis` (`substrate`, `authoring`), `InvariantLoadCost`
(`always-load`, `reference-only`, `archivable`), `InvariantIntegrityClass` (`substrate`,
`sdlc`, `authoring`, `user`), `InvariantPhase` (`plan`, `delegate`, `review`, `synthesize`),
and `InvariantWorkflow` (`feature`, `debug`, `refactor`, `discovery`, `oneshot`). Severity
uses `InvariantSeverityLevel` (`blocking`, `advisory`); context override keys remain open.

Migration from 0.14.0:

```json
{
  "severity": { "default": "blocking", "by-workflow": { "oneshot": "advisory" } },
  "enforcement": {
    "mode": "check",
    "check": {
      "scope": { "file-glob": "src/**", "phase": "review" },
      "node": { "not": { "kind": "grep", "pattern": "forbidden" } }
    }
  }
}
```

Replace combinator `kind`/`children`/`child` fields with `all-of`, `any-of`, `not`, or
`scope`/`node`. Leaf kinds remain `grep`, `structural`, `heuristic`; every leaf requires
`pattern` and allows optional `threshold` and `file-glob`. Empty arrays and scope
objects are valid. No evaluator is shipped. Exarchos must rename `fileGlob` to
`file-glob`, including nested scopes; it is not accepted as an alias.

Structural unions emit abstract C# bases with generated converters and sealed arms.
Closed schemas emit `JsonUnmappedMemberHandling.Disallow` and Zod `strictObject`.
The structural converter recognizes required keys and literals without inventing a
wire discriminator. Zod lowers `oneOf` only when its closed arms are provably disjoint.
Severity maps retain `IReadOnlyDictionary<string, InvariantSeverityLevel>` in C#.

`npm run check:invariants` validates the pinned corpus and live catalog against JSON
Schema and the built Zod package. `CatalogRoundTripTests` reads the same normalized
JSON and proves C# round-trip preservation. Frontmatter uses the locked YAML parser;
missing, malformed, empty, or incomplete fixtures fail the gate. The NuGet corpus is
separate from the workflow builder fixtures. Source revisions and the three-key Exarchos
migration are recorded in `fixtures/invariants/manifest.json`. Exarchos implementation
is tracked in [exarchos#1902](https://github.com/lvlup-sw/exarchos/issues/1902).

## Cross-product round-trip (T31, exarchos#1247)

`scripts/cross-product-roundtrip.mjs` is the offline equivalence harness
(design §Resilience item 2). It generates Zod from **our own** bundled JSON
Schema (`schemas/workflow-definition-v1.schema.json`) via the proven zod-smoke
pipeline and asserts every exported `#53` workflow-IR fixture parses against it;
the C# side (`CrossProductRoundTripTests`) additionally validates a
representative IR against our NJsonSchema schema.

**The external-coordination seam is closed by #219.** This harness was designed
when Exarchos owned the Zod derivation, so its production arm was "run our
fixtures against Exarchos's published, pinned Zod snapshot" — a gate that fires
in a different repository, at a different time, and protects the consumer rather
than the contract. Strategos now emits the Zod itself, so there is one artifact
and nothing to reconcile: `scripts/verify-zod-conformance.mjs` runs the corpus
against `Generated/zod`, the artifact Exarchos pins. The `--zod-source` flag
remains for pointing the harness at an arbitrary Zod barrel.

```
--zod-source self      # (default) derive Zod from our own JSON Schema — offline
--zod-source <dir>     # any external Zod barrel
```

## Zod / TypeScript projection (#219)

`scripts/emit-zod.mjs` reads the emitted JSON Schema and writes one Zod module
per document into `Generated/zod/`, plus a barrel `index.ts` and the shared
`_references.ts` runtime. The output is committed and diffed by the same
codegen guard as `Generated/*.g.cs`, so a hand-edit or a stale artifact fails
CI. Distribution for GA is the existing `contracts-v*` git-tag channel that
Exarchos already reads the schemas from; an npm package is a follow-up.

**The emitter is total.** Every JSON Schema keyword it meets is either lowered
or an error. This matters more than it looks: every fixture in the conformance
corpus is a VALID document, so a silently dropped constraint produces a Zod
schema that accepts documents the contract rejects and the corpus still passes.
`Emitter_FailsClosed_OnAJsonSchemaKeywordItDoesNotLower` pins the behavior.

Two keywords are read and deliberately NOT lowered, because JSON Schema draft
2020-12 asserts neither by default: `format` (lowering it would make the
TypeScript arm stricter than the contract) and `default` (lowering it would make
the parsed value differ from the input). Descriptions are not emitted either —
the prose lives in the schema and in the TypeSpec, and copying it would triple
the artifact.

**Referential rules.** `@references(collection, idField)` on a TypeSpec property
declares that its value must name an entry of a collection on the root document.
The decorator rejects bad authoring at `tsp compile` time — a collection that is
not a root-anchored JSON Pointer, a blank id field, or a target that is not a
string (a reference is a moniker, never a typed handle; INV-8) — and emits
`x-strategos-references-v1`. The emitter resolves the one root document that both
declares the collection and reaches the annotated shape, and lowers every rule
for that root into a single `superRefine`. Three rules ship today:

| Declaration | Rule |
|---|---|
| `GateStep.gateId` | must name a `gates[].id` on the workflow root (DR-3; the AGWF032 rule) |
| `TransitionDefinition.fromStepId` | must name a `steps[].stepId` |
| `TransitionDefinition.toStepId` | must name a `steps[].stepId` |

The check walks the instance structurally rather than following a fixed list of
paths, because the positions a gate step can occupy — a fork path, a loop body, a
low-confidence handler chain — are mutually recursive, so no finite path list
covers them.

**Conformance.** `scripts/verify-zod-conformance.mjs` compiles the emitted
modules with `tsc` and runs them: the whole `#53` builder corpus must parse, and
both reference rules must reject. The `gateId` arm runs the same two
hand-authored wire documents the generator's AGWF032 test runs
(`tests/Strategos.Generators.Tests/Import/ImportFixtures/`), so the declared rule
and the hand-coded check in `Import/WireToModelBridge.cs` are pinned to the same
verdict at the same position. Deriving the C# import front-end from the same
declaration is a follow-up; for now the two are pinned, not unified.

## Breaking-change schema diff (T30)

`scripts/contracts-schema-diff.mjs` + `.github/workflows/contracts-schema-diff.yml`
classify a removed / narrowed / newly-required property, and an added or changed
conditional (`if` / `then` / `else` / `dependentSchemas` / `dependentRequired`),
as **BREAKING**; an added optional property and a removed conditional are
**NON-BREAKING**. A breaking change advances the minor before 1.0 and the major
after 1.0. The classification rules are unit-tested in C# by `SchemaDiffTests` /
`JsonSchemaDiff` — the authoritative spec — and mirrored by the Node CI driver
over the packaged schema file set.

The workflow runs **two arms**, and both must pass:

1. **Published baseline** — the schema tree inside the latest package actually
   published to NuGet.
2. **Merge base** — the schema tree at the pull request's base commit, compared
   using the base and head `ContractsVersion` values. When the two versions are
   equal, any BREAKING change fails: the narrowing needs a version bump.

**The version increment alone does not accept a narrowing.** A pre-1.0 minor bump
permits *every* breaking change, so on its own it makes the gate unfailable. Each
BREAKING change must therefore also be named by an entry in
[`schemas/breaking-changes.allowlist.json`](schemas/breaking-changes.allowlist.json),
passed to the script as `--allowlist`:

```json
{
    "file": "json-schema/CompensationConfiguration.json",
    "path": "$.properties[\"compensationStepType\"]",
    "kind": "'minLength' increased from 0 to 1",
    "version": "0.12.0",
    "reason": "compensationStepType is a CLR simple-name moniker; an empty string names no type."
}
```

`file`, `path`, and `kind` are compared with **exact string equality** against
what the classifier reports, so a near-miss accepts nothing. `version` is the
Contracts version that introduced the narrowing; an entry whose version falls
outside the compared `(previous, candidate]` window is reported as a **stale
allowlist entry** and accepts nothing. An unmatched BREAKING change exits 1 and
prints the entry to add. Every entry also needs a line in
[`CHANGELOG.md`](CHANGELOG.md).

### Conditional keywords and validator support

`CompensationConfiguration` states the typed-inverse rule as a JSON Schema
conditional (`if` / `then`): when `inverseAction` is present, `requiredOnFailure`
must be `true`. **A Draft 2020-12 validator enforces this; NJsonSchema 11.6.1 —
the validator behind the in-repo equivalence gate — does not implement the
conditional applicators and accepts a violating document.** In-repo the rule is
therefore enforced by the analyzer (`AGWF044`); consumers validating against the
published schema with a conforming validator get it for free.
`ImportedWorkflowBindingProofTests.ImportedTypedCompensation_WireConditionalStatesTheAgwf044Rule`
pins that limitation and goes red if NJsonSchema gains the support.

### `WorkflowDefinitionV1.schemaVersion` identity

`schemaVersion` is a pinned literal `1.0`. While the Contracts package is pre-1.0
a minor may narrow this document in place and every narrowing must be listed in
`schemas/breaking-changes.allowlist.json`; after 1.0 a breaking change requires a
V2 root.

## Gate wire slots & the dangling-`gateId` rule (DR-3, #150 → #100)

The workflow wire IR carries gate declarations from birth so the same gate
vocabulary flows through the shared IR to both runtimes:

- `WorkflowDefinitionV1.gates?: GateDeclaration[]` — the workflow's declared
  gates (the DR-2 `GateDeclaration` / `GateClass` / `GateReliability` family).
- `GateStep.gateId?: string` — a gate step's optional back-reference to a
  declaration's `id`.

Both slots are **optional and additive** (a `JsonSchemaDiff` NON-BREAKING
change; a gate-less workflow omits them). Gate declarations are **consumer-plane
data** — the generated saga does not consume them; they exist so cross-product
consumers (Exarchos Zod, Basileus) and the visualization/diff tooling see the
gate model directly on the wire.

**Dangling `gateId`.** A `GateStep.gateId` that references an `id` **absent from
the workflow's `gates` list** is a *semantic* (referential-integrity) rule that
**JSON Schema cannot express** — JSON Schema validates each object's shape, not
cross-object id references within the document. This contract therefore does
**not** reject a dangling `gateId`; the slots validate structurally in
isolation. Enforcement lives with the *consumers of the schema*, not the schema:

- **Zod consumers** (Exarchos) refine it themselves — a `.superRefine` (or
  equivalent) that asserts every `gateId` resolves to a declared `gates[].id`.
- **The build-time import front-end** rejects a dangling `gateId` at import
  (forward-reference to DR-13 / task 018 / DR-15) — that is where a gate-bearing
  import is accepted and a dangling reference is turned into a build error. No
  rejection logic lives in this schema package.
- **The `@references` declaration** (#219) states the rule once, in TypeSpec.
  It emits as `x-strategos-references-v1` on the property, and the Zod emitter
  lowers it into a root-level check — so the TypeScript arm rejects the same
  document, at the same position, without a hand-written `superRefine`. The
  key is inert to a generic JSON Schema validator, which still accepts a
  dangling reference; the rule binds where a reader reads it.

## Versioning & publishing (T32)

This package versions at **0.13.0** (see `Strategos.Contracts.csproj`). Per the
repo convention, MinVer derives versions from the `v*` release tag; to pin the
contracts version explicitly — independent of the product line — we set
`<MinVerSkip>true</MinVerSkip>` + `<Version>` + `<PackageVersion>` (MinVer
silently overwrites a bare `<Version>` otherwise), driven by the single
`<ContractsVersion>` property and the `contracts-v*` publish tag.

**Version history:** 0.2.0 debuted events + workflow IR (no 0.1.0); 0.3.0 added
the semantic-merge-queue surface (`MergeGateDecision` / `JourneyResult` /
`WorkflowRef` / `WorkflowCatalog`, the `_meta.degraded` response envelope) and
the AGWF catalog; 0.4.0 added the strategy-compiler contract layer (the
`GateClass` gate taxonomy, the fork/compensation edge, and the licensed-abstention
union); 0.5.0–0.8.0 add workflow diagnostics; 0.9.0 adds contract-authored
ontology action metadata and the generated descriptor adapter; 0.10.0 adds the
versioned `ActionPredicateV1`/`ActionLiteralV1` wire vocabulary and replaces
legacy relation-only action metadata with typed `@requires`/`@ensures`
metadata. Integer and decimal predicate values use canonical strings so every
consumer preserves exact values. Unknown predicate discriminators are invalid,
never an implicit custom predicate. Closed enums accept only their exact
TypeSpec wire tokens, and schema-required fields fail deserialization when
omitted; 0.11.0 adds the optional, occurrence-scoped `ActionReferenceV1` on
workflow steps and `AGWF039`–`AGWF043` to the closed diagnostic vocabulary;
0.12.0 adds an optional `inverseAction: ActionReferenceV1` to compensation and
`AGWF044`–`AGWF045`. A closed authored inverse is required for static rollback
proof because Strategos cannot reconstruct prior authoritative state from a
property frame alone. The legacy compensation shape remains valid for
runtime-only workflows, but it cannot establish rollback safety. Typed inverse
metadata requires `requiredOnFailure` to be true because derived prefix rollback
is mandatory; 0.13.0 freezes the #193 workflow-definition kernel as additive
slots: the structural `contentHash`, the carried-not-proved `WorkflowAuthorityV1`
frame, the `TypedContractRefV1` slot that references a schema by `$id` instead of
inlining a CLR type, per-step `completion` and `authority`, the wire projection of
the authority lattice that was CLR-only, and the `ActionContractV1` /
`ActionCatalogV1` / `ProofCatalogV1` manifest, and adds the emitted Zod/TypeScript
projection (`Generated/zod/`) with the `@references` referential declarations it
lowers. Every 0.13.0 addition is optional,
so a 0.12.0 document parses unchanged and no allowlist entry is needed. Consumers must
upgrade before receiving one of the new diagnostic tokens. The package embeds all schema
families under
`contentFiles/any/any/schemas/` and the builder-fixture corpus under
`contentFiles/any/any/fixtures/` so Exarchos can extract both. See `CHANGELOG.md`
→ "Cross-product breaking changes".
