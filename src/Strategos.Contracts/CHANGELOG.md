# Changelog — LevelUp.Strategos.Contracts

All notable changes to the **cross-product schema substrate** package. This
package is **versioned independently** of the Strategos 2.x core line: the
contracts substrate is a new artifact and its first published
release is **0.2.0** — there is intentionally **no 0.1.0**.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html):
the wire contracts (JSON Schema + emitted C# records) are the public surface. A
breaking schema change advances the minor while this package is pre-1.0 and will
require a major bump after 1.0; additive-only minors are enforced by the T30
structural diff in CI. The version increment alone does not accept a narrowing:
every breaking change must also be named in
[`schemas/breaking-changes.allowlist.json`](schemas/breaking-changes.allowlist.json)
and carry a line here.

## [Unreleased]

### Added

The **#193 workflow-definition kernel**, frozen as additive slots on the shapes that already
exist rather than as a parallel vocabulary. Every field the kernel names either already
existed under another name, or is a slot the authority lattice (#165) and the typed predicate
fragment (#168) already typed. Nothing below narrows the wire, so a 0.12.0 document parses
against 0.13.0 unchanged.

- **`WorkflowDefinitionV1.contentHash`** — optional SHA-256 over the definition's structural
  fields, lowercase hex, prose excluded on the `OntologyGraphHasher` rules. The kernel's
  `identity.contentHash`. #204 reads it for tamper and skew detection across an assembly
  boundary.
- **`WorkflowDefinitionV1.authority`** — the kernel's authority frame as `WorkflowAuthorityV1`:
  optional `invariants`, `goals`, `assumptions`, `delegatedDecisions` and
  `escalationBoundaries`, each a list of `WorkflowAuthorityStatementV1 { id?, statement }`.
  **Carried, not proved.** Strategos serializes this block and checks none of it in 3.0.
  Statements are a model rather than a bare string so the fields a proved statement will need
  can be added additively later.
- **Step slots on the shared step common**, so every step kind carries them: `completion`
  (an `ActionGuaranteeV1`, the kernel's `tasks[].completion: Predicate`, in the #168 predicate
  vocabulary rather than a second one), `authority` (an `AuthorityRequirementV1`, the kernel's
  `tasks[].capabilities[]` as a coordinate rather than a name list), and `inputs` / `outputs`
  (a `TypedContractRefV1`, which references a schema by its `$id` and never inlines a CLR type).
- **The wire projection of the #165 authority lattice**, which was CLR-only: `AuthorityAxisV1`,
  `AuthorityCoordinateV1`, `AuthorityDescriptorV1`, `AuthorityLatticeV1`,
  `AuthorityRequirementV1` and the named `DomainAuthorityLatticeV1`. Before this, only the
  `x-strategos-authority` name string crossed the wire, so a consumer could not tell whether one
  authority dominated another. A coordinate is an ARRAY of `{ axis, level }` pairs ordered by
  `axis`, not a map: a JSON object has no canonical member order, so a map would let two
  producers emit two different `contentHash` values for one definition.
- **The action contract as a document**: `ActionSubjectV1` and `ActionContractV1`
  (`subject`, `name`, `requires`, `ensures`, `touches`, `authority`, `inverse`, `idempotent`),
  gathered by `ActionCatalogV1`. The wire previously carried only references to actions and
  the decorators on operations authored in this package.
- **`ProofCatalogV1`** — the portable proof manifest #204 emits from one assembly and reads
  from a referencing one. Frozen here, with the types it composes, so #204 is emission,
  consumption and proof with no further Contracts change.

Reserved composition-combinator kinds (sequence, parallel, choice, compensation,
host-continuation) are recorded **structurally** in `docs/architecture/kernel-v1.md` and add
no enum tokens. An enum token with no implementation is a consumer-breaking payload under the
strict converter.

Part of #193 (stage a, #209). Refs #204, #165, #168, #153.

The **Zod/TypeScript projection**, emitted here rather than derived downstream. Strategos
owns every projection of the types it authors: one source, one emitter, one direction, one
version. Exarchos consumes and pins; it does not derive, and it does not own the drift gate.
This is the second acceptance bullet of #193, closed inside this repository.

- **`Generated/zod/*.ts`** — one Zod module per emitted JSON Schema document, a barrel
  `index.ts`, and the shared `_references.ts` runtime. Emitted by
  `scripts/emit-zod.mjs` on the `scripts/contracts-codegen.sh` path, committed, and diffed by
  the codegen guard exactly like the C# records. Distribution for GA is the existing
  `contracts-v*` git-tag channel Exarchos already reads the schemas from; an npm package is a
  follow-up, not GA.
- **`@references(collection, idField)`** — a TypeSpec authoring decorator for the referential
  rules core JSON Schema cannot express. It rejects bad authoring at `tsp compile` time with
  its own library diagnostics (`contract-references-collection`,
  `contract-references-id-field`, `contract-references-target-type`), emits
  `x-strategos-references-v1` onto the property, and the emitter lowers it into a root-level
  check. Three rules ship: `GateStep.gateId` into `gates[].id` (the AGWF032 rule), and both
  `TransitionDefinition` endpoints into `steps[].stepId`.
- **The emitter is total.** An unlowered JSON Schema keyword stops the emit. A corpus of valid
  fixtures cannot detect a silently dropped constraint, so the emitter refuses to drop one.
  `format` and `default` are read and deliberately not lowered — draft 2020-12 asserts neither
  by default, so lowering them would make the TypeScript arm stricter, or lossier, than the
  contract.
- **Conformance.** `scripts/verify-zod-conformance.mjs` compiles the emitted modules with
  `tsc` and runs them against the whole #53 builder corpus, and against the same two
  hand-authored wire documents the generator's AGWF032 test runs — so the declared rule and
  the hand-coded check in `Import/WireToModelBridge.cs` are pinned to the same verdict at the
  same position (`$.steps[1].gateId`, naming `gX`).

No wire shape changed: `x-strategos-references-v1` is an annotation a generic validator
ignores, and the structural schema diff reports zero changes.

Part of #193 (acceptance 2, #219). Refs #209, #204, #153, exarchos#1901.

### Fixed

- **Four hand-authored wire fixtures declared a gate class the contract forbids.**
  `gates[].class` is the closed `GateClass` enum, whose wire values are the eight snake_case
  tokens; the fixtures carried `AntipatternDetection`, a pre-0.4.0 name. They are now `rules`.
  The import front-end reads `class` as an opaque string and accepted them, which is the gap
  this exposed — filed separately; nothing here changes generator behavior.

## [0.12.0] - 2026-09-09

The first Contracts release since 0.4.0. The 0.5.0 through 0.12.0 minors were pinned in
the tree between 2026-08-07 and 2026-09-08 and never tagged separately; every entry below
carries the minor it landed in. Ships alongside Strategos 3.0.0-rc.1, whose core package
depends on this version. Consumers upgrade first: the emitted converters throw on an
unknown member.

### Fixed

- **The schema-diff gate can fail again (tooling, no wire impact):** while the
  candidate `ContractsVersion` carried a pre-1.0 minor bump over the published
  baseline, `scripts/contracts-schema-diff.mjs` reported every BREAKING change as
  "allowed by the pre-1.0 minor version increment" and exited 0 — the failing exit
  was unreachable for a product reason, and the last CI run waved through eight
  narrowings without review. The version increment is now a necessary but not
  sufficient condition: each BREAKING change must also match an entry in
  `schemas/breaking-changes.allowlist.json` (exact `file` / `path` / `kind`, with a
  `version` inside the compared window), or the gate exits 1 and names the entry to
  add. An entry outside the window is reported as a stale allowlist entry.
  `contracts-schema-diff.yml` gained a second arm that diffs the pull request's
  merge-base schema tree against the head tree, so a narrowing introduced by the PR
  is compared against the versions the PR actually moves between rather than
  against a baseline several minors old.
- **Conditional keywords are classified rather than fenced off (tooling):** `if`,
  `then`, `else`, `dependentSchemas`, and `dependentRequired` were unhandled, so a
  change to one was reported as "safety cannot be proven". Both classifiers now
  carry the rule: adding or changing a conditional is BREAKING (it can only reject
  documents the previous schema accepted); removing one is NON-BREAKING.
- **Schema-diff classifier recurses `definitions` / `$defs` (tooling, no wire
  impact):** both the Node release gate (`scripts/contracts-schema-diff.mjs`)
  and the C# classifier (`JsonSchemaDiff`) treated the draft-07 `definitions`
  container as an unsupported keyword and reported the bundled
  `workflow-definition-v1.schema.json` as BREAKING ("safety cannot be proven")
  whenever any definition changed, including purely additive ones. The
  container is now diffed per entry: an added definition is non-breaking, a
  removed definition is breaking, and a changed definition is classified by
  what changed inside it. A test reads the Node keyword lists and requires them
  to equal the C# classifier's, so the two gates cannot drift again.

### Added

- **Compensation defaults and the inverse rule are on the wire (`0.12.0`):**
  `CompensationConfiguration.requiredOnFailure` now carries `"default": true` in
  the emitted schema, so a consumer reading the contract sees the value Strategos
  applies when the property is omitted rather than having to read the C# builder.
  The rule that a typed `inverseAction` requires `requiredOnFailure = true` — until
  now enforced only by the C# analyzer as `AGWF044` — is stated as a JSON Schema
  conditional (`if: { required: ["inverseAction"] }`,
  `then: { properties: { requiredOnFailure: { const: true } } }`) that any
  validator enforces. `timeout` documents the 300-second inverse deadline the
  generated runtime applies when the property is omitted. The conditional is a
  narrowing and is listed in `schemas/breaking-changes.allowlist.json` (#169).
- **`schemaVersion` narrowing policy is stated on the contract (`0.12.0`):**
  `WorkflowDefinitionV1.schemaVersion` is a pinned literal `1.0`. While the
  Contracts package is pre-1.0 a minor may narrow this document in place and every
  narrowing must be listed in `schemas/breaking-changes.allowlist.json`; after 1.0
  a breaking change requires a V2 root. The previous wording ("additive minors,
  breaking ⇒ V2") promised consumers something the pre-1.0 releases were not
  delivering (#169).
- **Typed workflow compensation (`0.12.0`):** the optional
  `CompensationConfiguration.inverseAction` field carries the ontology identity
  implemented by an authored compensation step. The field uses the existing
  `ActionReferenceV1` wire shape and remains omitted for legacy, runtime-only
  compensation. `AGWF044` reports an authored inverse that is missing,
  unresolved, or semantically different from the inverse contract derived from
  the forward action; `AGWF045` reports a rollback-safety claim whose executable
  nonempty-frame leaves are not all compensable. When `inverseAction` is present,
  `requiredOnFailure` must not be `false`: derived completed-prefix rollback is
  mandatory rather than a per-leaf opt-out (#169).
- **Compensation step identity is non-blank (`0.12.0`, narrowing):**
  `CompensationConfiguration.compensationStepType` gains `minLength: 1` and the
  `.*\S.*` pattern, and the generated record rejects empty or whitespace-only
  values. The two blank forms were **not** treated alike before this release. A
  whitespace-only moniker reached step-symbol resolution and failed the build. An
  **empty string** did not: `WireToModelBridge` guarded the compensation block
  with `!string.IsNullOrEmpty(compensationStepType)`, so an empty moniker made the
  importer **silently drop the entire compensation block** — the document
  validated, the build succeeded, and the generated saga carried no compensation
  at all. Importing that document is now a build error instead of a silent drop.
  The structural diff classifies the narrowing as breaking; it ships under the
  pre-1.0 minor-bump policy and is listed in
  `schemas/breaking-changes.allowlist.json` (#169).
- **Workflow step action identity (`0.11.0`):** `ActionReferenceV1` carries the
  ontology domain, object type, and action names on an optional `action` field
  shared by every workflow step kind. The field is occurrence-scoped and
  additive; legacy workflow JSON omits it byte-for-byte (#167).
- **Workflow name is non-blank (`0.11.0`, narrowing):** `WorkflowDefinitionV1.name`
  gains the `.*\S.*` pattern that every runtime and import lookup already
  required, and the generated record rejects a whitespace-only name on read and
  write. The structural diff classifies a new `pattern` as breaking; the change
  ships under the pre-1.0 minor-bump policy. A document with a blank workflow
  name was never loadable by Strategos, so no known producer is affected (#167).
- **Workflow binding diagnostics (`0.11.0`):** the closed `AgwfCode` vocabulary
  adds `AGWF039`–`AGWF043` for workflow lookup, occurrence action identity,
  refuted behavioral refinement, unprovable workflow contracts, and generated
  identity collisions. The schema
  change is additive, but generated enum converters reject unknown members, so
  Exarchos and Basileus must adopt 0.11.0 before Strategos emits these codes
  (#167).
- **Typed action contracts (`0.10.0`):** versioned, recursive
  `ActionPredicateV1` and `ActionLiteralV1` tagged unions; typed hard/soft
  `@requires` metadata; and explicit `@ensures` post-state guarantees. Integer
  and exact decimal literals are canonical base-10 strings, not JSON numbers.
  The closed discriminators reject unknown kinds rather than interpreting them
  as `custom` or `true` (#168).
- **Ontology action contracts:** TypeSpec `extern dec` decorators for object
  ownership, authority, relation paths, clients, confirmation, read-only, and
  idempotent semantics. The decorators emit language-neutral
  `x-strategos-*` JSON Schema metadata; the C# extension emits immutable
  `HandAuthoredContract` ontology descriptors. Additive, so the package moves
  0.8.0 → 0.9.0 (#170).
- **Diagnostics family — `AGWF038` (`DuplicateCompensationSeed`, severity `error`):** a new
  AGWF code for two diagnostic-fork edges whose compensation seeds sanitize to the same
  `DiagnosticForkCount_{seed}` key. Sharing a counter would let one edge's `maxForks`
  bound starve the other; the generator rejects the pair on C# extract and on JSON
  import. Additive (a new enum member + a new catalog entry), so it is a minor,
  non-breaking change; the package moves 0.7.0 → 0.8.0. **Consumers upgrade first:**
  the emitted `AgwfCode` converter throws on a member it does not know, so a consumer
  pinned to 0.7.0 cannot deserialize a payload carrying `AGWF038` (#156.3).
- **Diagnostics family — `AGWF037` (`DuplicatePermittedForkTrigger`, severity `error`):** a new
  AGWF code for two `PermitTrigger` declarations on one diagnostic-fork edge that name the
  same closed trigger. First-wins dedup would silently drop one evidence schema; the generator
  rejects the edge on C# extract and on JSON import. Additive (a new enum member + a new
  catalog entry), so it is a minor, non-breaking change; the package moves 0.6.0 → 0.7.0.
  **Consumers upgrade first:** the emitted `AgwfCode` converter throws on a member it does not
  know, so a consumer pinned to 0.6.0 cannot deserialize a payload carrying `AGWF037` (#156.2).
- **Diagnostics family — `AGWF036` (`PathEndTypeCollision`, severity `error`):** a new
  AGWF code for exclusive paths (fork path-ends, or branch cases) that share a step
  **type** under distinct instance names. Routing maps key by step type, so instance
  names do not disambiguate and the emitter would otherwise produce duplicate
  `Handle({Type}Completed)` overloads (CS0111). Additive (a new enum member + a new
  catalog entry), so it is a minor, non-breaking change; the package moves 0.5.0 →
  0.6.0. **Consumers upgrade first:** the emitted `AgwfCode` converter throws on a
  member it does not know, so a consumer pinned to 0.5.0 cannot deserialize a payload
  carrying `AGWF036` (#189, #190, #191).
- **Diagnostics family — `AGWF035` (`UnreachableTermination`, severity `error`):** a new
  AGWF code for a workflow whose main flow chains into a step that is reached only
  through its own construct — a fork path, a branch case, a failure or approval handler,
  or a low-confidence handler chain — so the saga runs past its declared termination
  instead of completing. Additive (a new enum member + a new catalog entry), so it is a
  minor, non-breaking change; the package moves 0.4.0 → 0.5.0. **Consumers upgrade
  first:** the emitted `AgwfCode` converter throws on a member it does not know, so a
  consumer pinned to 0.4.0 cannot deserialize a payload carrying `AGWF035` (#155).
- **Diagnostics family — `AGWF022` (`DeclaredButInert`, severity `warning`):** a new
  AGWF code for step configuration that is parsed into the IR but not lowered for the
  step's kind, so it is silently inert (first guarded case: confidence gating on a
  `Fork` path, deferred to v2.10.0 / DR-17, #134). Additive (a new enum member + a new
  catalog entry), so it is a minor, non-breaking change (#143, G-6).

### Changed

- **Approved breaking-version exception:** the Strategos v2.13 action-contract
  migration intentionally removes legacy string-parsed preconditions without a
  compatibility initializer. This source break was approved for the v2.13
  minor release; the independently versioned pre-1.0 contracts package moves
  from `0.9.0` to `0.10.0`.
- **Relation metadata:** `@relation` is now pure authoring sugar for a hard
  `relation-holds` predicate in `x-strategos-requires-v1`. The legacy
  `x-strategos-relation` / `x-strategos-link-path` pair is no longer emitted.
  Contract consumers must adopt `0.10.0` before receiving newly authored action
  schemas.
- **Closed wire contracts:** generated enum converters now accept and emit only
  the exact string wire tokens declared by TypeSpec. Quoted numeric values,
  CLR member spellings, case variants, numeric JSON tokens, and undefined
  outbound enum values are rejected. Generated record properties marked
  required by schema carry `JsonRequired`, so omitted fields fail
  deserialization instead of becoming CLR defaults.
- **AGWF003 remediation const:** the catalog string now names `EffectiveName` (the
  instance name, or the step type when none is given) so it matches
  `docs/diagnostics/agwf.md`. Downstream consumers that validate the previous
  literal see a changed value, not an addition.

## [0.2.0] - 2026-05-24

First published release of the cross-product schema substrate. **Must not
publish until both the events and workflow-IR families have landed** — they
have, in this milestone; that is why this is 0.2.0 and not an earlier preview.

### Added

- **Events family** — `SdlcEventEnvelope` + lifecycle/fabric/ontological event
  data, emitted as JSON Schema (NuGet content) and C# records (this DLL).
- **Workflow-IR family** — `WorkflowDefinitionV1` (the wire IR; `schemaVersion`
  pinned to `1.0`) + the 5-kind discriminated `StepDefinition` and structural
  sub-definitions, plus the bundled `workflow-definition-v1.schema.json`.
- **Diagnostics family** — `InvariantEntry` (v3) invariant catalog.
- **Embedded content (T32):** all three families' JSON Schemas under
  `contentFiles/any/any/schemas/` and the ≥100 `#53` builder fixtures under
  `contentFiles/any/any/fixtures/`, so Exarchos can extract both from the
  package.
- **Breaking-change schema diff (T30):** `JsonSchemaDiff` + CI workflow flag a
  removed / narrowed / newly-required property as breaking and an added optional
  property as non-breaking, compared against the complete schemas in the latest
  package actually published to NuGet.
- **Cross-product round-trip harness (T31):** offline harness deriving Zod from
  our own JSON Schema and parsing every fixture against it. The external
  Exarchos pinned-Zod-snapshot step (exarchos#1247) is out of scope and marked
  at the harness `--zod-source` seam.

## Cross-product breaking changes

Schema (wire-contract) changes that would break Exarchos or Basileus consumers
are tracked here and gate a minor version increment before 1.0 or a major
version increment after 1.0 (per the T30 structural diff).

- **0.2.0:** None this release (initial published contract).
- **0.10.0:** Ontology action metadata uses typed versioned predicate arrays;
  legacy relation-only extensions are removed by the coordinated #168 contract
  migration.
- **0.11.0:** None. Workflow-step action identity and `AGWF039`–`AGWF043` are
  additive. Older generated closed-enum consumers must still upgrade before
  receiving the new diagnostic codes.
- **0.12.0:** `CompensationConfiguration.compensationStepType` is narrowed to a
  non-empty, non-whitespace string, matching the importer and runtime identity
  rules. Typed inverse-action identity and `AGWF044`–`AGWF045` are additive.
  Older generated closed-enum consumers must still upgrade before receiving the
  new diagnostic codes.
