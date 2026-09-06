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
structural diff in CI.

## [Unreleased]

### Added

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
  property as non-breaking, compared against the previous tag's schemas.
- **Cross-product round-trip harness (T31):** offline harness deriving Zod from
  our own JSON Schema and parsing every fixture against it. The external
  Exarchos pinned-Zod-snapshot step (exarchos#1247) is out of scope and marked
  at the harness `--zod-source` seam.

## Cross-product breaking changes

Schema (wire-contract) changes that would break Exarchos or Basileus consumers
are tracked here and gate a major version bump (per the T30 structural diff).

- **0.2.0:** None this release (initial published contract).
- **0.10.0:** Ontology action metadata uses typed versioned predicate arrays;
  legacy relation-only extensions are removed by the coordinated #168 contract
  migration.
