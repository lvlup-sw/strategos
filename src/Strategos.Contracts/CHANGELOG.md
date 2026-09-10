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
