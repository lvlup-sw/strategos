# Implementation Plan — Strategos.Contracts Cross-Product Schema Substrate (0.2.0)

**Design:** `docs/designs/2026-05-24-contracts-schema-substrate.md`
**Milestone:** Strategos 2.8.0 — slice A (#36 events, #50 workflow IR) + #98 invariant models (fast-follow)
**Stack:** net10.0 · TUnit 1.2.11 · TypeSpec (`@typespec/compiler`, `@typespec/json-schema`) · NJsonSchema (fallback)
**Test invocation (TUnit):** `dotnet run --project <test.csproj> -- --treenode-filter "/*/*/*/Name"` — **not** `dotnet test --filter` (see memory `feedback_tunit_test_invocation`).

## Iron Law

> NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST.

For TypeSpec/JS pipeline tasks the "failing test" is a build-assertion: a test that compiles the `.tsp` source and asserts the emitted JSON Schema / generated C# shape — it fails before the model/emitter config exists.

## Traceability (design section → tasks)

| Design section | Tasks |
|---|---|
| Dependency direction (DIM-1) | T1, T32 |
| Build pipeline & C# emission (INV-6/7) | T1–T5 |
| Family 1 — events (#36) | T6–T12 |
| Family 2 — workflow IR (#50) | T13–T17 |
| LB-1 lambda (declarative-only) | T19, T26 |
| LB-2 moniker / export-only | T20, T21 |
| Equivalence gate / fixtures (#53) | T22–T24 |
| Family 3 — invariant catalog (#98) | T25–T29 |
| Resilience (versioning, diff, round-trip) | T13, T23, T30, T31 |
| Hygiene / #50 swap correction | T18, T21 (no `Definitions/*.cs` deletion) |

---

## Phase 0 — Pipeline foundation (SEQUENTIAL — blocks all families)

### Task 1: Scaffold `Strategos.Contracts` project
**Phase:** RED → GREEN → REFACTOR
1. [RED] Test: `Package_Identity_IsLevelUpStrategosContracts`
   - File: `src/Strategos.Contracts.Tests/PackagingTests.cs`
   - Expected failure: project + csproj do not exist.
2. [GREEN] Create `src/Strategos.Contracts/Strategos.Contracts.csproj` (PackageId `LevelUp.Strategos.Contracts`, net10.0), add to `strategos.sln`, add test project. CPM entries in `Directory.Packages.props`: `NJsonSchema`.
3. [REFACTOR] Align csproj with house props (copyright header, stylecop).

**Dependencies:** None · **Parallelizable:** No

### Task 2: TypeSpec toolchain + dual emitters
1. [RED] Test: `TspCompile_TrivialModel_EmitsJsonSchemaWithStableId`
   - File: `src/Strategos.Contracts.Tests/Pipeline/TspCompileTests.cs`
   - Expected failure: no `tspconfig.yaml`/`package.json`; nothing emitted.
2. [GREEN] Add `src/Strategos.Contracts/package.json` (`@typespec/compiler`, `@typespec/json-schema`, `@apidevtools/json-schema-ref-parser`), `tspconfig.yaml` with json-schema + C# emitters; MSBuild target runs `tsp compile` into `schemas/` + `Generated/`.
3. [REFACTOR] Cache `node_modules` restore; gate compile on `.tsp` change.

**Dependencies:** T1 · **Parallelizable:** No

### Task 3: C# emitter decision gate — INV-6/INV-7 shape (SPIKE → test)
1. [RED] Test: `GeneratedRecord_IsSealed_InitOnly_ReadOnlyCollections`
   - File: `src/Strategos.Contracts.Tests/Pipeline/EmitterShapeTests.cs`
   - Reflection asserts a generated record is `sealed`, all props `init`-only, collections `IReadOnlyList<T>`.
   - Expected failure: native emitter default shape is mutable `class`.
2. [GREEN] Prefer TypeSpec native C# emitter; if it cannot hit the shape, switch to NJsonSchema template tuned to `sealed record` + `init` + `IReadOnlyList`. **Record the chosen path in the plan + design.**
3. [REFACTOR] Single emitter-config source; document the decision in `src/Strategos.Contracts/README.md`.

**Dependencies:** T2 · **Parallelizable:** No · **Note:** gates INV-6/INV-7 for every downstream family.

### Task 4: JSON Schema embedded as NuGet content
1. [RED] Test: `Nupkg_Contains_SchemasUnderContentFiles`
   - File: `src/Strategos.Contracts.Tests/PackagingTests.cs`
   - Packs to a temp dir, unzips `.nupkg`, asserts `contentFiles/any/any/schemas/*.json` present.
   - Expected failure: no content-file wiring.
2. [GREEN] csproj `<Content>` glob for `schemas/**` → `contentFiles/any/any/schemas/`.
3. [REFACTOR] none.

**Dependencies:** T3 · **Parallelizable:** No

### Task 5: CI codegen-guard
1. [RED] Test/script: `Codegen_HandEdit_FailsGuard`
   - File: `.github/workflows/contracts-codegen-guard.yml` + `src/Strategos.Contracts.Tests/Pipeline/CodegenGuardTests.cs`
   - Regenerates, asserts `git diff --exit-code` over `Generated/` + `schemas/`; test simulates a hand-edit and expects failure.
2. [GREEN] Workflow step: `tsp compile` then `git diff --exit-code`.
3. [REFACTOR] Reuse the restore cache from T2.

**Dependencies:** T4 · **Parallelizable:** No

---

## Phase 1 — Events family #36 (PARALLEL-SAFE after Phase 0)

### Task 6: `SdlcEventEnvelope` model
1. [RED] `Envelope_Schema_HasRequiredFieldsAndSourceDiscriminator` → `Events/EnvelopeSchemaTests.cs`. Asserts `streamId`/`sequence:int32`/`timestamp`/`type` required, `source: exarchos|basileus`, unknown `type` allowed.
2. [GREEN] `Events/Envelope.tsp` (port from spike `main.tsp`).
3. [REFACTOR] shared enums file.

**Dependencies:** T5 · **Parallelizable:** Yes (group P1)

### Task 7: Consolidate in-repo event families
1. [RED] `ExistingFamilies_RoundTrip_FromInRepoSchemas` → `Events/ExistingFamiliesTests.cs` (coding-attempt lifecycle, task lifecycle).
2. [GREEN] `Events/Lifecycle.tsp`.
3. [REFACTOR] none.

**Dependencies:** T6 · **Parallelizable:** Yes (P1)

### Task 8: New ADR §4 — ontological record lifecycle
1. [RED] `OntologicalLifecycle_Schema_MatchesAdrSection4` → `Events/OntologicalLifecycleTests.cs` (`IntentProposed/Enriched/Completed`, `OntologicalRecordData`, `RecordStatus`, `DelegationPolicy`).
2. [GREEN] `Events/OntologicalLifecycle.tsp`.

**Dependencies:** T6 · **Parallelizable:** Yes (P1)

### Task 9: New ADR §4 — fabric query + remote delegation
1. [RED] `FabricAndDelegation_Schema_MatchesAdrSection4` → `Events/FabricDelegationTests.cs` (`FabricQueryData`, `FabricQueryType`, `TaskDelegatedRemoteData`, `CrossTierDependencyResolvedData`).
2. [GREEN] `Events/FabricDelegation.tsp`.

**Dependencies:** T6 · **Parallelizable:** Yes (P1)

### Task 10: NotificationEnvelope exclusion (ADR §2.3.4)
1. [RED] `Contracts_DoNotDefine_NotificationEnvelope` → `Events/ExclusionRegressionTests.cs`. Scans emitted schemas + generated types; fails if `NotificationEnvelope`/`FormattedNotification` present.
2. [GREEN] (assertion only — ensures no one adds it).

**Dependencies:** T6 · **Parallelizable:** Yes (P1)

### Task 11: C# records round-trip vs Basileus `ISdlcEvent`
1. [RED] `GeneratedRecords_RoundTrip_AgainstBasileusEventShapes` → `Events/BasileusCompatTests.cs`. Serializes a generated record, asserts JSON matches the known Basileus wire shape (validation, not migration).
2. [GREEN] Adjust `@encodedName`/casing until compatible.

**Dependencies:** T7–T9 · **Parallelizable:** Yes (P1)

### Task 12: Zod-consumability (no manual post-processing)
1. [RED] `EmittedSchema_GeneratesZod_WithoutManualPostProcessing` → `src/Strategos.Contracts.Tests/Pipeline/ZodConsumabilityTests.cs`. Runs dereference + `json-schema-to-zod` over emitted event schemas; asserts success + barrel index.
2. [GREEN] wire the dereference step (`@apidevtools/json-schema-ref-parser`) into a `generate:zod-smoke` script.

**Dependencies:** T6–T9 · **Parallelizable:** Yes (P1)

---

## Phase 2 — Workflow IR family #50 (PARALLEL-SAFE after Phase 0)

### Task 13: `WorkflowDefinitionV1` root + `schemaVersion` literal
1. [RED] `WorkflowIrRoot_HasSchemaVersionLiteral_1_0` → `Workflow/WorkflowIrRootTests.cs`. Asserts `schemaVersion: "1.0"` literal at root.
2. [GREEN] `Workflow/WorkflowDefinitionV1.tsp`.

**Dependencies:** T5 · **Parallelizable:** Yes (group P2)

### Task 14: `StepDefinition` discriminated + reserved `runtime`
1. [RED] `StepDefinition_Discriminates_FiveKinds_ReservesRuntime` → `Workflow/StepDefinitionSchemaTests.cs`. Asserts kinds `skill|handler|gate|delegate|approval`; `runtime?: exarchos|strategos|remote` default `exarchos`.
2. [GREEN] `Workflow/StepDefinition.tsp`.

**Dependencies:** T13 · **Parallelizable:** Yes (P2)

### Task 15: Structural sub-definitions (transitions / branch / loop / fork)
1. [RED] `StructuralDefs_Schema_MatchTransitionBranchLoopFork` → `Workflow/StructuralDefsTests.cs`.
2. [GREEN] `Workflow/Structural.tsp` (`TransitionDefinition`, `BranchPointDefinition`, `BranchPathDefinition`, `BranchCase`, `LoopDefinition`, `ForkPointDefinition`, `ForkPathDefinition`).

**Dependencies:** T13 · **Parallelizable:** Yes (P2)

### Task 16: Approval / failure / configuration sub-definitions
1. [RED] `ApprovalFailureConfig_Schema_MatchDefinitions` → `Workflow/ApprovalFailureConfigTests.cs`.
2. [GREEN] `Workflow/ApprovalFailureConfig.tsp` (`ApprovalDefinition`, `ApprovalEscalationDefinition`, `ApprovalRejectionDefinition`, `FailureHandlerDefinition`, `StepConfigurationDefinition`, `RetryConfiguration`, `CompensationConfiguration`, `ValidationDefinition`, `LowConfidenceHandlerDefinition`).

**Dependencies:** T13 · **Parallelizable:** Yes (P2)

### Task 17: All-18 schema completeness + stable `$id`
1. [RED] `WorkflowIr_Emits18Definitions_WithStableIds` → `Workflow/IrCompletenessTests.cs`. Asserts `workflow-definition-v1.schema.json` emitted with stable `$id` and all 18 sub-defs present.
2. [GREEN] tspconfig output naming.

**Dependencies:** T14–T16 · **Parallelizable:** No (joins P2)

### Task 18: `ToContract()` projection — happy path
1. [RED] `ToContract_SkillStepWorkflow_ProducesValidV1` → `src/Strategos.Tests/Contracts/ProjectionTests.cs`. Builds a simple `WorkflowDefinition<TState>` and asserts `.ToContract()` yields a populated `WorkflowDefinitionV1`.
2. [GREEN] `src/Strategos/Contracts/WorkflowDefinitionProjection.cs` — `ToContract(this WorkflowDefinition<TState>)` extension. **Builder IR untouched.**
3. [REFACTOR] map all structural collections.

**Dependencies:** T17 · **Parallelizable:** No (joins P2)

### Task 19: LB-1 — lambda step → `delegate` kind + `lambda` marker, body dropped
1. [RED] `ToContract_LambdaStep_EmitsDelegateKindWithMarker_NoCode` → `ProjectionTests.cs`. Builds a `CreateFromLambda` step; asserts projected step kind `delegate`, `lambda: true`, and **no executable/delegate field** on the wire object.
2. [GREEN] projection branch for `IsLambdaStep`.
3. [REFACTOR] none.

**Dependencies:** T18 · **Parallelizable:** No

### Task 20: LB-2 — `System.Type` → simple-name moniker
1. [RED] `ToContract_TypedStep_UsesSimpleNameMoniker_NotAssemblyQualified` → `ProjectionTests.cs`. Asserts `stepType == typeof(AnalyzeStep).Name`, contains no namespace/assembly qualifier.
2. [GREEN] projection uses `StepType.Name`.

**Dependencies:** T18 · **Parallelizable:** No

### Task 21: LB-2 — export-only guard (no `FromContract` surface)
1. [RED] `Projection_ExposesNoRehydrationApi_In_0_2_0` → `ProjectionTests.cs`. Reflection asserts no public `FromContract`/deserialize-to-`WorkflowDefinition<TState>` member exists (space reserved, not shipped).
2. [GREEN] keep projection one-way; add XML-doc note pointing to deferred V-next.
3. **Hygiene:** confirm `src/Strategos/Definitions/*.cs` retained (no deletion) — corrects #50.

**Dependencies:** T18 · **Parallelizable:** No

### Task 22: #53 fixture export — ≥100 cases via canonical serializer
1. [RED] `FixtureExport_Produces100PlusFixtures_AcrossAll8CombinatorTags` → `src/Strategos.Tests/FixtureExport/FixtureExportTests.cs` (`Category=FixtureExport`). Runs the 16 `Builders/*` corpus through `ToContract()` + the **Contracts canonical serializer** (no parallel `JsonSerializer.Serialize`); writes `artifacts/builder-fixtures/<cat>/<name>.json` + `index.json`.
2. [GREEN] exporter + manifest with combinator-coverage tags (`startWith`, `then`, `branch`, `repeatUntil`, `fork-join`, `awaitApproval`, `onFailure`, config variants).

**Dependencies:** T18–T21 · **Parallelizable:** No

### Task 23: Equivalence gate — every fixture validates against emitted JSON Schema
1. [RED] `EveryFixture_ValidatesAgainst_WorkflowDefinitionV1Schema` → `FixtureExport/FixtureSchemaValidationTests.cs`.
2. [GREEN] schema-validate each emitted fixture; wire into CI as the projection-drift gate.

**Dependencies:** T22 · **Parallelizable:** No

### Task 24: Partial-export rejection (atomicity)
1. [RED] `FixtureExport_SingleFailure_LeavesNoHalfWrittenArtifacts` → `FixtureExport/AtomicityTests.cs`.
2. [GREEN] write to temp dir, atomic move on full success.

**Dependencies:** T22 · **Parallelizable:** No

---

## Phase 3 — Invariant catalog #98 (fast-follow, PARALLEL-SAFE after Phase 0)

### Task 25: `CheckNode` recursive combinator tree
1. [RED] `CheckNode_Schema_SupportsRecursiveAllOfAnyOfNotScope` → `Diagnostics/CheckNodeTests.cs`. Asserts leaves (`grep`/`structural`/`heuristic` with `pattern`/`file-glob`/`threshold`) + recursive arms.
2. [GREEN] `Diagnostics/CheckNode.tsp`.

**Dependencies:** T5 · **Parallelizable:** Yes (group P3)

### Task 26: LB-1/INV-4 — no executable leaf (structural guarantee)
1. [RED] `CheckNode_Schema_CannotExpressExecutableLeaf` → `Diagnostics/SandboxGuaranteeTests.cs`. Asserts schema has **no** member admitting arbitrary code/exec; a fixture with an executable field fails validation.
2. [GREEN] keep leaves declarative-only.

**Dependencies:** T25 · **Parallelizable:** Yes (P3)

### Task 27: `Enforcement` discriminated (`check` | `audit`)
1. [RED] `Enforcement_DiscriminatesOnMode_CheckOrAudit` → `Diagnostics/EnforcementTests.cs`.
2. [GREEN] `Diagnostics/Enforcement.tsp`.

**Dependencies:** T25 · **Parallelizable:** Yes (P3)

### Task 28: `InvariantEntry` v3 + `@encodedName` kebab wire-names
1. [RED] `InvariantEntry_V3_AddsOptionalFields_PreservesKebabWireNames` → `Diagnostics/InvariantEntryTests.cs`. Asserts v2 fields + optional v3 (`phase-affinity`, `workflow-affinity`, `state-affinity`, `enforcement`, `severity`, `integrity-class`) and kebab-case wire names.
2. [GREEN] `Diagnostics/InvariantEntry.tsp` with `@encodedName`.

**Dependencies:** T25, T27 · **Parallelizable:** Yes (P3)

### Task 29: Round-trip exarchos v2 catalog + v3 fixture without loss
1. [RED] `InvariantCatalog_RoundTrips_V2AndV3_WithoutLoss` → `Diagnostics/CatalogRoundTripTests.cs`.
2. [GREEN] adjust encodings until lossless.

**Dependencies:** T28 · **Parallelizable:** No (joins P3)

---

## Phase 4 — Cross-cutting CI / release (after Phases 1–3)

### Task 30: Breaking-change JSON Schema structural diff
1. [RED] `SchemaDiff_DetectsBreakingChange_FailsCi` → `Pipeline/SchemaDiffTests.cs`. Removing a required field flags breaking; additive flags non-breaking.
2. [GREEN] CI step using a JSON Schema diff tool against the previous tag.

**Dependencies:** T12, T23, T29 · **Parallelizable:** No

### Task 31: Cross-product round-trip harness (exarchos#1247)
1. [RED] `CrossProduct_FixtureValidatesAgainstZod_AndZodIrValidatesHere` → `Pipeline/CrossProductRoundTripTests.cs`. Our fixture → exarchos Zod (pinned snapshot) and an exarchos-emitted IR → our schema.
2. [GREEN] harness consuming the generated Zod barrel; coordinate snapshot with exarchos#1247.

**Dependencies:** T23, T30 · **Parallelizable:** No

### Task 32: NuGet 0.2.0 packaging + publish wiring
1. [RED] `Package_Version_Is_0_2_0_WithEventsIrAndDiagnosticsContent` → `PackagingTests.cs`. Asserts version + all three families' schemas embedded + fixtures under `contentFiles/.../fixtures/`.
2. [GREEN] version via MinVer; **set `<MinVerSkip>true</MinVerSkip>` + `<PackageVersion>0.2.0</PackageVersion>`** if pinning explicitly (see memory `project_minver_version_override`). Wire into the existing Strategos release pipeline. **Do not publish until both events + IR land** (no 0.1.0).
3. [REFACTOR] CHANGELOG `## Cross-product breaking changes` section.

**Dependencies:** T30, T31 · **Parallelizable:** No

---

## Parallelization summary

- **Phase 0 (T1–T5):** strictly sequential — blocks everything.
- **Phases 1, 2, 3:** parallel-safe relative to each other (disjoint `.tsp` + test files); dispatch to separate worktrees.
  - Within P1: T6 first, then T7–T10 parallel, then T11–T12.
  - Within P2: T13 first, then T14–T16 parallel, then T17 → T18 → {T19,T20,T21} → T22 → {T23,T24}.
  - Within P3: T25 first, then T26–T28 parallel, then T29.
- **Phase 4 (T30–T32):** sequential, after Phases 1–3.

## Out of scope (deferred — design §Out of scope)

T4 cross-runtime dispatch; `FromContract()` rehydration; full 77+20 type absorption; consumer-side swaps (basileus#152, exarchos#1125, exarchos#1247).
