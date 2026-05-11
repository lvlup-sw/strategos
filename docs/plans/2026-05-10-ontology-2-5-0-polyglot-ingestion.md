# Implementation Plan: Strategos 2.5.0 — Polyglot Descriptors + IOntologySource + Drift Diagnostics

**Date:** 2026-05-10
**Design:** `docs/designs/2026-05-10-ontology-2-5-0-polyglot-ingestion.md`
**Feature ID:** `ontology-2-5-0-polyglot-ingestion`
**Closes:** strategos#37, strategos#48, strategos#43
**Iron Law:** **NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST**

## Cadence

Two PRs, sequenced:

- **PR-A** (Tasks 1–22): Schema + `IOntologySource` + provenance + AONT037 + test fixtures. Unblocks basileus.
- **PR-B** (Tasks 23–32): AONT200-series drift diagnostics + AONT041 retarget + `OntologyValidateTool.FindObjectType` retarget.

PR-B depends on PR-A being merged.

## Traceability matrix (design → tasks)

| DR | Title | Tasks |
|---|---|---|
| DR-1 | Polyglot descriptor schema | 1, 2, 3, 4 |
| DR-2 | AONT037 source-generator diagnostic | 18, 19 |
| DR-3 | `IOntologySource` extension point | 7, 12, 13 |
| DR-4 | `OntologyDelta` event vocabulary | 8 |
| DR-5 | Runtime `IOntologyBuilder` API | 9, 10, 11 |
| DR-6 | Field-level provenance metadata | 5, 6, 14, 15, 16 |
| DR-7 | AONT200-series graph-freeze diagnostics | 23, 24, 25, 26, 27, 28, 29, 30 |
| DR-8 | AONT041 retarget + `FindObjectType` retarget | 31, 32 |
| DR-9 | Test fixture infrastructure | 20, 21, 22 |
| DR-10 | Error handling and failure modes | 5, 16, 17, 27 |

## Conventions

- Test naming: `Method_Scenario_Outcome` (e.g., `Build_IngestedDescriptorWithActions_ThrowsAONT205`).
- Test framework: TUnit (Strategos convention). Invocation: `dotnet test src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj -- --treenode-filter "/*/*/*/<TestName>"`.
- Each task: [RED] failing test → [GREEN] minimum code → [REFACTOR] if needed.
- File paths absolute from repo root.

---

## PR-A: Schema + IOntologySource + Provenance + AONT037 + Test Fixtures

### Task 1: `DescriptorSource` enum

**Implements:** DR-6
**Phase:** RED → GREEN

1. [RED] Add test `DescriptorSource_DefaultValue_IsHandAuthored` to `src/Strategos.Ontology.Tests/Descriptors/DescriptorSourceTests.cs` asserting `default(DescriptorSource) == DescriptorSource.HandAuthored`.
   - Expected failure: `DescriptorSource` does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Descriptors/DescriptorSource.cs` with enum: `HandAuthored = 0, Ingested = 1`.

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 2: `ObjectTypeDescriptor` polyglot fields

**Implements:** DR-1
**Phase:** RED → GREEN → REFACTOR

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Descriptors/ObjectTypeDescriptorPolyglotTests.cs`:
   - `Ctor_HandAuthoredDefault_LanguageIdIsDotnet` — descriptor built via existing positional ctor `(Name, ClrType, DomainName)` has `LanguageId == "dotnet"`.
   - `Ctor_IngestedDescriptor_AcceptsNullClrTypeAndSymbolKey` — descriptor built with `SymbolKey != null, ClrType == null` compiles and round-trips.
   - `Ctor_BothNull_ThrowsInvalidOperationException` — descriptor with `ClrType == null` and `SymbolKey == null` throws at construction with message naming the invariant.
   - Expected failure: properties do not exist; positional ctor still requires `Type ClrType`.
2. [GREEN] Refactor `src/Strategos.Ontology/Descriptors/ObjectTypeDescriptor.cs`:
   - Change from positional record `(string Name, Type ClrType, string DomainName)` to property-init record with `Name`, `DomainName` required, `ClrType` optional, `SymbolKey`, `SymbolFqn`, `LanguageId` (default `"dotnet"`), `Source` (default `HandAuthored`), `SourceId`, `IngestedAt` optional.
   - Preserve backward-compat constructor `(string name, Type clrType, string domainName)` that calls the new init form with `ClrType = clrType, LanguageId = "dotnet"`.
   - Add invariant guard in the constructor body.
3. [REFACTOR] Update any test/sample call sites that broke (expected: zero — positional ctor preserved).

**Dependencies:** Task 1
**Parallelizable:** No
**testingStrategy:** unit

### Task 3: `LinkDescriptor` polyglot fields

**Implements:** DR-1
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Descriptors/LinkDescriptorPolyglotTests.cs`:
   - `Ctor_SymbolKeyTargetOnly_ParentTypeNullOk` — `LinkDescriptor` with `TargetSymbolKey != null` and `TargetType == null` constructs.
   - `Source_DefaultValue_IsHandAuthored`.
   - Expected failure: `TargetSymbolKey`, `Source` don't exist on `LinkDescriptor`.
2. [GREEN] Update `src/Strategos.Ontology/Descriptors/LinkDescriptor.cs`:
   - Add `string? TargetSymbolKey { get; init; }` parallel to existing `TargetTypeName`.
   - Add `DescriptorSource Source { get; init; } = HandAuthored`.

**Dependencies:** Task 1
**Parallelizable:** Yes (with Task 4)
**testingStrategy:** unit

### Task 4: `PropertyDescriptor` polyglot + provenance fields

**Implements:** DR-1, DR-6
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Descriptors/PropertyDescriptorPolyglotTests.cs`:
   - `Ctor_ReferenceTypeBySymbolKey_AcceptsNullClrType` — for reference-typed properties, `ReferenceSymbolKey != null` + `ReferenceType == null` constructs.
   - `Source_DefaultValue_IsHandAuthored`.
   - Expected failure: properties don't exist.
2. [GREEN] Update `src/Strategos.Ontology/Descriptors/PropertyDescriptor.cs`:
   - Add `string? ReferenceSymbolKey { get; init; }` parallel to existing `ReferenceType`.
   - Add `DescriptorSource Source { get; init; } = HandAuthored`.

**Dependencies:** Task 1
**Parallelizable:** Yes (with Task 3)
**testingStrategy:** unit

### Task 5: `OntologyCompositionException` aggregates diagnostics

**Implements:** DR-10
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Exceptions/OntologyCompositionExceptionTests.cs`:
   - `Ctor_WithDiagnostics_ExposesDiagnosticsProperty` — exception constructed with a list of `(DiagnosticId, Message, Severity)` exposes them via `Diagnostics: ImmutableArray<OntologyDiagnostic>`.
   - `Ctor_MessageContainsFirstDiagnosticId` — default message lists the first diagnostic ID.
2. [GREEN] Extend or create `src/Strategos.Ontology/Exceptions/OntologyCompositionException.cs` (check whether one exists; if so extend; if not create) with `Diagnostics` and `NonFatalDiagnostics` properties. Add `OntologyDiagnostic` record (`Id`, `Message`, `Severity`, `DomainName?`, `TypeName?`, `PropertyName?`).

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 6: `OntologyDiagnostic` record + `DiagnosticSeverity` mirror

**Implements:** DR-6, DR-10
**Phase:** RED → GREEN

1. [RED] Add test `OntologyDiagnostic_Construction_PreservesFields` to `src/Strategos.Ontology.Tests/Diagnostics/OntologyDiagnosticTests.cs` asserting field round-trip.
2. [GREEN] Create `src/Strategos.Ontology/Diagnostics/OntologyDiagnostic.cs`. Mirror Roslyn's `DiagnosticSeverity` enum locally to avoid runtime-binding to `Microsoft.CodeAnalysis` from the runtime assembly.

**Dependencies:** Task 5
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 7: `IOntologySource` interface

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `IOntologySource_ImplementedByTestSource_ExposesSourceId` to `src/Strategos.Ontology.Tests/Sources/IOntologySourceContractTests.cs`. Construct an inline test impl; assert `SourceId` returns the configured value.
   - Expected failure: `IOntologySource` namespace does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Sources/IOntologySource.cs` with the interface (`SourceId`, `LoadAsync`, `SubscribeAsync`).

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 8: `OntologyDelta` event vocabulary

**Implements:** DR-4
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Sources/OntologyDeltaTests.cs`:
   - `AddObjectType_Construction_RoundTrips` (and one test per variant for: `UpdateObjectType`, `RemoveObjectType`, `AddProperty`, `RenameProperty`, `RemoveProperty`, `AddLink`, `RemoveLink`).
   - `RenameProperty_IsSingleDelta_NotRemoveThenAdd` — assert via type assertion that a property-rename delta is `OntologyDelta.RenameProperty`, not a pair.
   - Expected failure: `OntologyDelta` does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Sources/OntologyDelta.cs` with the eight sealed-record variants under the abstract base.

**Dependencies:** Task 1, Task 7
**Parallelizable:** No
**testingStrategy:** unit

### Task 9: `IOntologyBuilder.ObjectTypeFromDescriptor`

**Implements:** DR-5
**Phase:** RED → GREEN

1. [RED] Add test `ObjectTypeFromDescriptor_IngestedDescriptor_AppearsInBuiltGraph` to `src/Strategos.Ontology.Tests/Builder/IOntologyBuilderDescriptorPathTests.cs`. Construct an `OntologyBuilder`, call `ObjectTypeFromDescriptor` with `Source = Ingested`, build, assert the descriptor is present with `Source == Ingested`.
   - Expected failure: method doesn't exist.
2. [GREEN] Add `ObjectTypeFromDescriptor(ObjectTypeDescriptor)` to `IOntologyBuilder` (`src/Strategos.Ontology/Builder/`). Implement on the concrete `OntologyBuilder` to register the descriptor in the builder's internal store.

**Dependencies:** Task 2, Task 7
**Parallelizable:** No
**testingStrategy:** unit

### Task 10: `IOntologyBuilder.ApplyDelta` — `AddObjectType` variant

**Implements:** DR-5
**Phase:** RED → GREEN

1. [RED] Add test `ApplyDelta_AddObjectType_RegistersDescriptor` to `src/Strategos.Ontology.Tests/Builder/IOntologyBuilderApplyDeltaTests.cs`. Builder applies an `OntologyDelta.AddObjectType` delta; built graph contains the type.
2. [GREEN] Implement `ApplyDelta(OntologyDelta)` on `OntologyBuilder` with the `AddObjectType` branch routing to `ObjectTypeFromDescriptor`.

**Dependencies:** Task 8, Task 9
**Parallelizable:** No
**testingStrategy:** unit

### Task 11: `IOntologyBuilder.ApplyDelta` — remaining seven variants

**Implements:** DR-5
**Phase:** RED → GREEN → REFACTOR

1. [RED] Add a test per variant to `IOntologyBuilderApplyDeltaTests.cs`:
   - `ApplyDelta_UpdateObjectType_OverwritesExisting`
   - `ApplyDelta_RemoveObjectType_DropsType`
   - `ApplyDelta_AddProperty_AppendsToParent`
   - `ApplyDelta_RenameProperty_PreservesIdentity`
   - `ApplyDelta_RemoveProperty_DropsByName`
   - `ApplyDelta_AddLink_AppendsToSourceType`
   - `ApplyDelta_RemoveLink_DropsByName`
2. [GREEN] Implement each variant branch in `ApplyDelta`. Use a switch expression over the sealed-record hierarchy.
3. [REFACTOR] Extract per-variant logic into private methods if the switch grows past ~50 lines.

**Dependencies:** Task 10
**Parallelizable:** No
**testingStrategy:** unit

### Task 12: `OntologyBuilderOptions.AddSource<T>` DI extension

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `AddSource_TestSource_RegistersAsTransient` to `src/Strategos.Ontology.Tests/Extensions/OntologyBuilderOptionsExtensionsTests.cs`. Configure DI via `services.AddOntology(opts => opts.AddSource<TestOntologySource>())`; resolve `IEnumerable<IOntologySource>`; assert one instance.
   - Expected failure: `AddSource<T>` does not exist.
2. [GREEN] Add extension method to `src/Strategos.Ontology/Extensions/OntologyBuilderOptionsExtensions.cs` (locate existing file under that name or `Strategos.Ontology/Configuration/`). Register `T : IOntologySource` as transient.

**Dependencies:** Task 7, Task 20 (TestOntologySource)
**Parallelizable:** No
**testingStrategy:** unit

### Task 13: `OntologyGraphBuilder.Build()` drains registered sources

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `Build_TwoSourcesContributingDifferentFields_BothAppearInGraph` to `src/Strategos.Ontology.Tests/OntologyGraphBuilderSourcesTests.cs`. Register two `TestOntologySource` instances; verify both contribute to the composed graph.
   - Expected failure: builder doesn't drain sources.
2. [GREEN] In `src/Strategos.Ontology/OntologyGraphBuilder.cs`, drain `IEnumerable<IOntologySource>` from DI; for each source, iterate `LoadAsync`, apply each delta via the existing builder. Run before composition.

**Dependencies:** Task 11, Task 12, Task 20
**Parallelizable:** No
**testingStrategy:** integration

### Task 14: `MergeTwo` lattice — identity fields

**Implements:** DR-6
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Merge/MergeTwoIdentityTests.cs`:
   - `MergeTwo_ClrType_HandWinsOverIngested`.
   - `MergeTwo_SymbolKey_IngestedWinsOverHand` — SCIP moniker is authoritative.
   - `MergeTwo_LanguageId_HandWins`.
   - Expected failure: `MergeTwo` doesn't exist or doesn't follow ADR §9.2 rules.
2. [GREEN] Implement `MergeTwo(ObjectTypeDescriptor hand, ObjectTypeDescriptor ingested) → ObjectTypeDescriptor` in `OntologyGraphBuilder.cs` (or new `src/Strategos.Ontology/Merge/MergeTwo.cs`). Follow the lattice rule from design §4 DR-6.

**Dependencies:** Task 2
**Parallelizable:** No
**testingStrategy:** unit

### Task 15: `MergeTwo` lattice — property + link union

**Implements:** DR-6
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Merge/MergeTwoPropertyLinkTests.cs`:
   - `MergeTwo_HandPropertyMissingFromIngested_BothPresent` — set union semantics.
   - `MergeTwo_PropertyConflict_HandWins` — per-name conflict resolves to hand.
   - `MergeTwo_IngestedOnlyProperty_TaggedIngested` — provenance preserved.
   - Same three for `Links`.
2. [GREEN] Implement `MergeProperties(hand, ingested)` and `MergeLinks(hand, ingested)` helpers. Tag conflict-loser side with the winner's Source.

**Dependencies:** Task 14
**Parallelizable:** No
**testingStrategy:** unit

### Task 16: AONT205 invariant check at delta-apply

**Implements:** DR-6, DR-10
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Builder/IOntologyBuilderInvariantTests.cs`:
   - `ApplyDelta_AddIngestedDescriptorWithActions_ThrowsOntologyCompositionException` — exception's `Diagnostics` contains AONT205.
   - `ApplyDelta_AddIngestedDescriptorWithLifecycle_ThrowsAONT205`.
   - `ApplyDelta_AddIngestedDescriptorWithEvents_ThrowsAONT205`.
   - Expected failure: invariant not enforced; descriptor passes through silently.
2. [GREEN] In `ApplyDelta`'s `AddObjectType`/`UpdateObjectType` branches, when `descriptor.Source == Ingested`, validate `Actions.Count == 0 && Events.Count == 0 && Lifecycle == null`. On violation, throw `OntologyCompositionException` with AONT205 diagnostic naming the offending field.

**Dependencies:** Task 5, Task 10
**Parallelizable:** No
**testingStrategy:** unit

### Task 17: Source-error propagation with `SourceId` context

**Implements:** DR-10
**Phase:** RED → GREEN

1. [RED] Add test `Build_SourceThrowsDuringLoadAsync_ExceptionMessageContainsSourceId` to `OntologyGraphBuilderSourcesTests.cs`. `TestOntologySource` that throws after yielding one delta; assert `Build()` throws with the source's `SourceId` in the message.
2. [GREEN] Wrap each source's drain in try/catch in `OntologyGraphBuilder.Build()`; re-throw with `OntologyCompositionException` whose message includes `SourceId`.

**Dependencies:** Task 13
**Parallelizable:** No
**testingStrategy:** unit

### Task 18: AONT037 diagnostic registration

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add test `Diagnostic_AONT037_IsRegistered` to `src/Strategos.Ontology.Generators.Tests/Analyzers/AONT037RegistrationTests.cs` asserting `OntologyDiagnostics` exposes a `DiagnosticDescriptor` with ID `"AONT037"`, Title contains `"polyglot"`, severity Error.
2. [GREEN] Add `AONT037 PolyglotInvariantViolated` to `OntologyDiagnosticIds.cs` and a matching `DiagnosticDescriptor` to `OntologyDiagnostics.cs`. Helper text: requires `ClrType` or `SymbolKey` non-null.
3. [GREEN] If `docs/reference/ontology-diagnostics.md` (or equivalent) exists, append AONT037 entry with title, severity, trigger description, and fix guidance.

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 19: AONT037 analyzer trigger

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Generators.Tests/Analyzers/AONT037AnalyzerTests.cs`:
   - `Analyze_DescriptorOverloadWithoutSymbolKey_FiresAONT037` — `obj.ObjectType("Foo", domainName: "Trading")` produces AONT037.
   - `Analyze_GenericObjectTypeCall_DoesNotFireAONT037` — `obj.ObjectType<TradeOrder>()` is clean.
2. [GREEN] In `OntologyDefinitionAnalyzer.cs`, register an action for `InvocationExpressionSyntax` matching `obj.ObjectType(name, …)` non-generic overload. If the call provides neither a `Type` argument nor a `symbolKey:` named argument, report AONT037.

**Dependencies:** Task 18
**Parallelizable:** No
**testingStrategy:** unit

### Task 20: `TestOntologySource` test fixture

**Implements:** DR-9
**Phase:** RED → GREEN

1. [RED] Add test `TestOntologySource_LoadAsync_YieldsConfiguredDeltas` to `src/Strategos.Ontology.Tests/TestInfrastructure/TestOntologySourceTests.cs`. Construct with three deltas; verify enumeration order matches.
2. [GREEN] Create `src/Strategos.Ontology.Tests/TestInfrastructure/TestOntologySource.cs` per design §4 DR-9 shape. `LoadAsync` yields configured deltas; `SubscribeAsync` completes immediately.

**Dependencies:** Task 7, Task 8
**Parallelizable:** No
**testingStrategy:** unit

### Task 21: Synthetic merge/diagnostic test matrix

**Implements:** DR-9, DR-6
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Merge/MergeMatrixTests.cs` using `TestOntologySource`:
   - `Merge_HandOnly_GraphMatchesHand`
   - `Merge_IngestedOnly_GraphMatchesIngested`
   - `Merge_HandOverridesIngestedProperty_HandWins`
   - `Merge_IngestedAddsLink_LinkAppearsWithIngestedProvenance`
   - `Merge_IdentityFields_FollowLatticeRule` — parameterized over each identity field per lattice.
2. [GREEN] Wire each test through `OntologyGraphBuilder` end-to-end. All assertions on graph-level shape; no internal-state inspection.

**Dependencies:** Task 13, Task 15, Task 20
**Parallelizable:** No
**testingStrategy:** integration

### Task 22: Roslyn SymbolKey round-trip integration test

**Implements:** DR-9
**Phase:** RED → GREEN

1. [RED] Add test `RoslynSymbolKey_RoundTripThroughBuilder_PreservesIdentity` to `src/Strategos.Ontology.Tests/Integration/RoslynSymbolKeyIntegrationTests.cs`. Compile a small `.cs` source string via `CSharpCompilation.Create`, extract a named type's `INamedTypeSymbol`, serialize via `SymbolKey.Create(symbol).ToString()`, build an `ObjectTypeDescriptor` with that `SymbolKey`, push through `OntologyBuilder.ApplyDelta`, assert the built graph contains a descriptor whose `SymbolKey` equals the serialized form.
   - Gate behind MSBuild property `SkipRoslynIntegrationTests` — skip if set.
   - Expected failure: test infra (Microsoft.CodeAnalysis.CSharp) not in test deps.
2. [GREEN] Add `Microsoft.CodeAnalysis.CSharp` (4.x) to `src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj` as `PrivateAssets="all"`. Implement the test.

**Dependencies:** Task 13, Task 20
**Parallelizable:** Yes (independent of Task 21)
**testingStrategy:** integration

---

## PR-A acceptance gate

Run before opening PR-A:

```
dotnet build strategos.sln  # 0 warnings, 0 errors
dotnet test src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj
dotnet test src/Strategos.Ontology.Generators.Tests/Strategos.Ontology.Generators.Tests.csproj
dotnet test src/Strategos.Ontology.MCP.Tests/Strategos.Ontology.MCP.Tests.csproj
```

Existing test counts hold (666 + 75 + 121 from PR #59). New test count delta: ~50 (synthetic merge matrix + each delta variant + invariant checks + AONT037).

---

## PR-B: AONT200-series + AONT041 retarget

PR-B begins after PR-A merges to main.

### Task 23: AONT201 (hand property missing from ingested)

**Implements:** DR-7
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Diagnostics/AONT201Tests.cs`:
   - `Build_HandDeclaresPropertyMissingFromIngested_AONT201Error` — `OntologyCompositionException` thrown; `Diagnostics[0].Id == "AONT201"`; message names the property.
   - `Build_HandDeclaresPropertyPresentInIngested_NoAONT201`.
2. [GREEN] In `OntologyGraphBuilder` graph-freeze phase (after merge, before return), iterate descriptors with mixed provenance (`hand.Properties ∩ ingested.Properties`); for each hand-side property not present in ingested by name, emit AONT201 error.

**Dependencies:** PR-A merged
**Parallelizable:** Yes (with 24, 25, 26, 28, 30)
**testingStrategy:** unit

### Task 24: AONT202 (property type mismatch hand vs ingested)

**Implements:** DR-7
**Phase:** RED → GREEN

1. [RED] Add tests to `AONT202Tests.cs`:
   - `Build_HandScalarVsIngestedReference_AONT202Warning` — `OntologyGraph.NonFatalDiagnostics` contains AONT202 warning.
   - `Build_HandAndIngestedAgree_NoAONT202`.
   - `Build_AONT202Fires_LoggerReceivesStructuredWarning` — verify `ILogger<OntologyGraphBuilder>` receives a `LogWarning` call with structured properties `{DiagnosticId, DomainName, TypeName, PropertyName}` (use TUnit-compatible logger fake or `Microsoft.Extensions.Logging.Testing.FakeLogger`).
2. [GREEN] Graph-freeze comparator: hand property kind vs ingested property kind; mismatch emits AONT202 warning to `NonFatalDiagnostics` **and** to `ILogger<OntologyGraphBuilder>.LogWarning` with structured properties `{DiagnosticId = "AONT202", DomainName, TypeName, PropertyName}`.

**Dependencies:** PR-A merged, Task 23 (shares graph-freeze plumbing)
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 25: AONT203 (ingested-only property missing under Strict)

**Implements:** DR-7
**Phase:** RED → GREEN

1. [RED] Add tests to `AONT203Tests.cs`:
   - `Build_StrictTypeMissingIngestedProperty_AONT203Warning`.
   - `Build_NonStrictTypeMissingIngestedProperty_NoAONT203` — opt-in only.
   - `Build_AONT203Fires_LoggerReceivesStructuredWarning` — verify structured `LogWarning` parallel to AONT202 (see Task 24).
2. [GREEN] Add `DomainEntityAttribute(Strict = true)` recognition in graph-freeze; for `Strict = true` descriptors, ingested-side properties missing on hand emit AONT203 to `NonFatalDiagnostics` **and** to `ILogger<OntologyGraphBuilder>.LogWarning` with structured properties `{DiagnosticId = "AONT203", DomainName, TypeName, PropertyName}`.

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 26: AONT204 (ingested type not referenced by hand)

**Implements:** DR-7
**Phase:** RED → GREEN

1. [RED] Add tests to `AONT204Tests.cs`:
   - `Build_IngestedTypeNoHandReference_AONT204Info` — info-level diagnostic on `NonFatalDiagnostics`.
   - `Build_IngestedTypeReferencedByHand_NoAONT204`.
2. [GREEN] Graph-freeze emits AONT204 for descriptors with `Source = Ingested` that have no incoming reference from a hand-authored descriptor (via Links, ParentType, KeyProperty references).

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 27: AONT205 graph-freeze surface (already enforced at delta-apply)

**Implements:** DR-7, DR-10
**Phase:** RED → GREEN

1. [RED] Add tests to `AONT205Tests.cs`:
   - `Build_TwoSourcesAttemptIntentContributionPostMerge_AONT205Error` — a stress case where two sources race intent fields; merge detects and emits AONT205. (Defensive — delta-apply enforces at write time; graph-freeze is the safety net.)
2. [GREEN] Add graph-freeze AONT205 check covering the post-merge state.

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 28: AONT206 (opt-in hygiene hint)

**Implements:** DR-7
**Phase:** RED → GREEN

1. [RED] Add tests to `AONT206Tests.cs`:
   - `Build_HandPropertyAlsoIngested_AONT206InfoWhenEnabled` — only fires when MSBuild prop `OntologyEnableHygieneHints=true`.
   - `Build_HandPropertyAlsoIngested_NoAONT206ByDefault`.
2. [GREEN] Wire opt-in flag into `OntologyGraphBuilderOptions`; gate AONT206 emission on it.

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 29: AONT207 registration-only with Skip

**Implements:** DR-7
**Phase:** RED → GREEN

1. [RED] Add registration test `Diagnostic_AONT207_IsRegisteredButSkipped` to `AONT207RegistrationTests.cs` asserting AONT207 is registered in the diagnostic catalog with severity Warning. Mark a trigger test with `[Skip("requires four-input fold")]` for documentation.
2. [GREEN] Register the diagnostic ID + descriptor only. No trigger logic.

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 30: AONT208 (LanguageId disagreement)

**Implements:** DR-7
**Phase:** RED → GREEN

1. [RED] Add tests to `AONT208Tests.cs`:
   - `Build_TwoSourcesDisagreeLanguageId_AONT208Error`.
2. [GREEN] Graph-freeze checks `MergeTwo`'s output for `LanguageId` agreement when both inputs supply it; mismatch emits AONT208.

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 31: AONT041 retarget to descriptor-name keying

**Implements:** DR-8
**Phase:** RED → GREEN → REFACTOR

1. [RED] Add tests to `src/Strategos.Ontology.Tests/OntologyGraphBuilderAONT041LinkTargetTests.cs` (existing file — append):
   - `AONT041_SymbolKeyKeyedMultiRegistration_InLinkParticipant_Fires` — ingested descriptor with `SymbolKey != null` registered twice as link participant trips AONT041.
   - `AONT041_ClrTypeMultiRegistrationInLink_StillFires` — existing behavior preserved.
2. [GREEN] In `OntologyGraphBuilder.cs`, find the AONT041 check; change the multi-registration lookup from `descriptor.ClrType`-keyed to `(descriptor.DomainName, descriptor.Name)`-keyed. Update the link-participant scan accordingly.
3. [REFACTOR] Extract the multi-registration lookup to a private helper `IsMultiRegistered(string domain, string name)`.

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 32: `OntologyValidateTool.FindObjectType` retarget

**Implements:** DR-8
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.MCP.Tests/Tools/OntologyValidateToolFindObjectTypeTests.cs`:
   - `FindObjectType_CrossDomainSameName_ResolvesByDomain` — two domains with a same-named type; lookup specifies domain + name; correct descriptor returned.
   - `FindObjectType_AmbiguousByNameOnly_Throws` — bare-name lookup against ambiguous catalog throws with diagnostic naming both candidates.
2. [GREEN] Update `FindObjectType` in `src/Strategos.Ontology.MCP/Tools/OntologyValidateTool.cs` to require `(domain, name)` or throw on ambiguity.

**Dependencies:** PR-A merged
**Parallelizable:** Yes
**testingStrategy:** unit

---

## PR-B acceptance gate

```
dotnet build strategos.sln  # 0 warnings, 0 errors
dotnet test  # all test projects pass
```

Test count delta from PR-B: ~30 new tests across diagnostics + retargets.

---

## Parallel groups

**PR-A:**
- **Sequential foundation:** Task 1 → Task 2 → Tasks 3, 4 (parallel) → Task 5
- **Parallel after foundation:**
  - Group α (sources): 7 → 8 → 20 → 21
  - Group β (builder): 9 → 10 → 11 → 13 → 17
  - Group γ (merge): 14 → 15 → 16
  - Group δ (analyzer): 18 → 19
  - Group ε (DI): 12 (after 7 + 20)
  - Group ζ (integration test): 22 (after 13 + 20)
- **Final:** Tasks 21, 22 (synthesis + Roslyn round-trip)

**PR-B:** Tasks 23–32 are mostly parallel after PR-A merges. Group:
- AONT201–AONT208 (Tasks 23–30) — parallel after Task 23 establishes graph-freeze plumbing
- AONT041 retarget (Task 31) — parallel
- FindObjectType retarget (Task 32) — parallel

## Open follow-ups (documented in design §5, not in scope)

- `OntologyGraph.NonFatalDiagnostics` shape — match `ConstraintViolationReport` from PR #59 for consistency (decided implicitly via Task 6's `OntologyDiagnostic`)
- `IOntologyVersionedCache` composition with `SubscribeAsync` for live invalidation — v2.6.0
- Branch-hand stream + four-input fold (`MergeFour`) — v2.6.0 (AONT207 unreachable until then)

## Plan-review delta annotations (folded gaps)

The following design-acceptance bullets are satisfied via in-task sub-steps rather than dedicated tasks:

- **DR-2 doc update** — folded into Task 18 step 3.
- **DR-6 `GetPropertyProvenance(name)` helper** — superseded by direct `PropertyDescriptor.Source` access (Task 4). The design's helper API is replaced by per-property `Source` field, eliminating a parallel-map drift risk. Functionally equivalent.
- **DR-10 ILogger emission for AONT202/AONT203** — folded into Tasks 24 step 2 and Task 25 step 2 as explicit `ILogger<OntologyGraphBuilder>.LogWarning` calls with structured properties.

## Estimated cost

- PR-A: ~22 tasks × ~30 min average = ~11 hours. Net ~600 lines impl + ~1500 lines test.
- PR-B: ~10 tasks × ~25 min average = ~4 hours. Net ~250 lines impl + ~800 lines test.
- Total: ~15 hours focused work across both PRs.
