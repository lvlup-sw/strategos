# Implementation Plan: Ontology 2.5.0 — Dispatch Guarantees + Validation Surface

**Design:** `docs/designs/2026-05-08-ontology-2-5-0-dispatch-validation.md`
**Date:** 2026-05-08
**Tasks:** 23
**Tracks:** 6 (A: Foundational types; B: Interface seams; C: Reference impls; D: DSL + generators; E: MCP tool; F: #33 fold-ins)
**Closes:** strategos#39, #38, #42, #41
**Partially closes:** strategos#33 (Findings 1, 2, 4)

**Parallelization summary.**
- Track A is six independent small tasks — runnable concurrently.
- Track B depends on Track A (interfaces use the new types).
- Track C depends on Track B (impls implement interfaces).
- Track D depends on B1 only (`ActionDescriptor.IsReadOnly`); otherwise parallel with B/C.
- Track E depends on A + B + C (the validate tool composes everything).
- Track F is fully independent — runnable in parallel with all tracks.

---

## Test invocation note

This repo uses TUnit on Microsoft Testing Platform — `dotnet test --filter` will fail. All test runs in this plan use:

```bash
dotnet test --project src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj
dotnet test --project src/Strategos.Ontology.MCP.Tests/Strategos.Ontology.MCP.Tests.csproj
dotnet test --project src/Strategos.Ontology.Generators.Tests/Strategos.Ontology.Generators.Tests.csproj
dotnet test --project src/Strategos.Ontology.Npgsql.Tests/Strategos.Ontology.Npgsql.Tests.csproj
```

To filter to a single test/class: append `-- --treenode-filter "/*/*/ClassName/*"`.

---

## Track A: Foundational types

### Task A1: Promote `ConstraintEvaluation` to `Strategos.Ontology.Actions`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ConstraintEvaluation_ResolvedFromActionsNamespace_IsSameType`
   - File: `src/Strategos.Ontology.Tests/Actions/ConstraintEvaluationLocationTests.cs` (new)
   - Assert `typeof(Strategos.Ontology.Actions.ConstraintEvaluation) == typeof(Strategos.Ontology.Query.ConstraintEvaluation)` via reflection (will fail until the type-forwarder is in place).
   - Expected failure: type does not yet exist in `Strategos.Ontology.Actions`.

2. **[GREEN]** Move record to new namespace + add type-forwarder.
   - Move file: `src/Strategos.Ontology/Query/ConstraintEvaluation.cs` → `src/Strategos.Ontology/Actions/ConstraintEvaluation.cs`; change namespace to `Strategos.Ontology.Actions`.
   - Add `src/Strategos.Ontology/Query/ConstraintEvaluation.cs` (new) containing only:
     ```csharp
     [assembly: System.Runtime.CompilerServices.TypeForwardedTo(
         typeof(Strategos.Ontology.Actions.ConstraintEvaluation))]
     ```
   - Add `using Strategos.Ontology.Actions;` to `IOntologyQuery.cs`, `OntologyQueryService.cs`, `ActionConstraintReport.cs` if `ConstraintEvaluation` is referenced unqualified.

3. **[REFACTOR]** None.

**Dependencies:** None
**Parallelizable:** Yes (within Track A)

---

### Task A2: Add `ConstraintViolationReport` record
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ConstraintViolationReport_Construction_PreservesAllFields`
   - File: `src/Strategos.Ontology.Tests/Actions/ConstraintViolationReportTests.cs` (new)
   - Construct with action name, two hard `ConstraintEvaluation` items, one soft, suggested correction; assert all fields round-trip via `ToString()` and equality.
   - Expected failure: type does not exist.

2. **[GREEN]** Add record.
   - File: `src/Strategos.Ontology/Actions/ConstraintViolationReport.cs` (new)
   - Body matches design §4.2: `(string ActionName, IReadOnlyList<ConstraintEvaluation> Hard, IReadOnlyList<ConstraintEvaluation> Soft, string? SuggestedCorrection)`.

3. **[REFACTOR]** None.

**Dependencies:** A1
**Parallelizable:** Yes (independent of A3–A6)

---

### Task A3: Extend `ActionResult` with `Violations` field
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ActionResult_NewWithoutViolations_DefaultsToNull`
   - File: `src/Strategos.Ontology.Tests/Actions/ActionResultViolationsTests.cs` (new)
   - Construct `new ActionResult(false, null, "fail")`; assert `result.Violations is null`.
   - Plus test `ActionResult_NewWithViolations_PreservesReport`: construct with a `ConstraintViolationReport` populated; assert `result.Violations.Hard.Count` matches.
   - Expected failure: `Violations` member does not exist.

2. **[GREEN]** Edit `src/Strategos.Ontology/Actions/ActionResult.cs`: add fourth optional field `ConstraintViolationReport? Violations = null` to the primary constructor.

3. **[REFACTOR]** None.

**Dependencies:** A2
**Parallelizable:** Yes (independent of A4–A6)

---

### Task A4: Add `BlastRadius` + `BlastRadiusOptions` + `BlastRadiusScope` + `CrossDomainHop`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `BlastRadius_Construction_PreservesAllFields`
   - File: `src/Strategos.Ontology.Tests/Query/BlastRadiusTypesTests.cs` (new)
   - Construct each record/enum with sample data; assert equality and field access.
   - Expected failure: types do not exist.

2. **[GREEN]** Add files.
   - `src/Strategos.Ontology/Query/BlastRadius.cs` (new) — record with the 4 fields per design §4.6.
   - `src/Strategos.Ontology/Query/BlastRadiusScope.cs` (new) — enum `Local | Domain | CrossDomain | Global`.
   - `src/Strategos.Ontology/Query/BlastRadiusOptions.cs` (new) — record `(int MaxExpansionDegree = 16)`.
   - `src/Strategos.Ontology/Query/CrossDomainHop.cs` (new) — record `(string FromDomain, string ToDomain, OntologyNodeRef SourceNode, OntologyNodeRef TargetNode)`.

3. **[REFACTOR]** None.

**Dependencies:** None (parallel to A1–A3)
**Parallelizable:** Yes

---

### Task A5: Add `PatternViolation` + `ViolationSeverity`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `PatternViolation_Construction_PreservesAllFields`
   - File: `src/Strategos.Ontology.Tests/Query/PatternViolationTypesTests.cs` (new)
   - Construct violations at both severities; assert fields.
   - Expected failure: types do not exist.

2. **[GREEN]** Add files.
   - `src/Strategos.Ontology/Query/PatternViolation.cs` (new) — record per design §4.7.
   - `src/Strategos.Ontology/Query/ViolationSeverity.cs` (new) — enum `Warning | Error`.

3. **[REFACTOR]** None.

**Dependencies:** None
**Parallelizable:** Yes

---

### Task A6: Add `DesignIntent` + `ProposedAction` + `CoverageReport` + `IOntologyCoverageProvider`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `DesignIntent_Construction_PreservesAllFields`
   - File: `src/Strategos.Ontology.MCP.Tests/DesignIntentTypesTests.cs` (new)
   - Construct each record; assert fields per design §4.8.
   - Expected failure: types do not exist.

2. **[GREEN]** Add files in `Strategos.Ontology.MCP/`.
   - `DesignIntent.cs` — record per design §4.8.
   - `ProposedAction.cs` — record per design §4.8.
   - `CoverageReport.cs` — record `(int CoveredNodes, int TotalNodes, IReadOnlyList<OntologyNodeRef> Uncovered)`.
   - `IOntologyCoverageProvider.cs` — interface `CoverageReport? GetCoverage(DesignIntent intent)`.

3. **[REFACTOR]** None.

**Dependencies:** None
**Parallelizable:** Yes

---

## Track B: Interface seams

### Task B1: Add `ActionDescriptor.IsReadOnly` field
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ActionDescriptor_IsReadOnlyDefault_IsFalse` and `ActionDescriptor_IsReadOnlyTrue_FlowsThroughInit`
   - File: `src/Strategos.Ontology.Tests/Descriptors/ActionDescriptorReadOnlyTests.cs` (new)
   - Assert default `IsReadOnly == false`; assert `with { IsReadOnly = true }` produces an instance with `IsReadOnly == true`.
   - Expected failure: property does not exist.

2. **[GREEN]** Edit `src/Strategos.Ontology/Descriptors/ActionDescriptor.cs`: add `public bool IsReadOnly { get; init; }` (default false).

3. **[REFACTOR]** None.

**Dependencies:** None (descriptor change is independent of types in Track A)
**Parallelizable:** Yes (parallel to B2–B5 since other interface tasks reference different files)

---

### Task B2: Add `IActionDispatcher.DispatchReadOnlyAsync` via default interface implementation
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `DispatchReadOnlyAsync_OnReadOnlyAction_DelegatesToDispatchAsync`
   - File: `src/Strategos.Ontology.Tests/Actions/DispatchReadOnlyAsyncTests.cs` (new)
   - Use a fake `IActionDispatcher` that captures `DispatchAsync` calls; build an `ActionContext` whose descriptor has `IsReadOnly = true`; call `DispatchReadOnlyAsync`; assert the inner dispatch was called.
   - Plus `DispatchReadOnlyAsync_OnNonReadOnlyAction_ReturnsFailureWithoutCallingInner`.
   - Expected failure: method does not exist on `IActionDispatcher`.

2. **[GREEN]** Edit `src/Strategos.Ontology/Actions/IActionDispatcher.cs`: add the `DispatchReadOnlyAsync` method with the C# default interface implementation per design §4.3.

3. **[REFACTOR]** None.

**Dependencies:** B1 (uses `ActionDescriptor.IsReadOnly`), A3 (returns `ActionResult` with optional `Violations`)
**Parallelizable:** No (sequential after B1)

---

### Task B3: Add `IActionDispatchObserver` interface
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `IActionDispatchObserver_TypeShape_HasOnDispatchedAsync`
   - File: `src/Strategos.Ontology.Tests/Actions/ActionDispatchObserverContractTests.cs` (new)
   - Use reflection to assert `typeof(IActionDispatchObserver)` has a method `OnDispatchedAsync(ActionContext, ActionResult, CancellationToken)` returning `Task`.
   - Expected failure: type does not exist.

2. **[GREEN]** Add `src/Strategos.Ontology/Actions/IActionDispatchObserver.cs` (new) per design §4.5.

3. **[REFACTOR]** None.

**Dependencies:** None
**Parallelizable:** Yes

---

### Task B4: Add `IOntologyQuery.EstimateBlastRadius` (default throws)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `EstimateBlastRadius_OnNonImplementingQuery_ThrowsNotSupported`
   - File: `src/Strategos.Ontology.Tests/Query/IOntologyQueryDefaultsTests.cs` (new)
   - Define a minimal `IOntologyQuery` test double that does not override `EstimateBlastRadius`; call it; assert `NotSupportedException`.
   - Expected failure: method does not exist on the interface.

2. **[GREEN]** Edit `src/Strategos.Ontology/Query/IOntologyQuery.cs`: add `BlastRadius EstimateBlastRadius(IReadOnlyList<OntologyNodeRef> touchedNodes, BlastRadiusOptions? options = null) => throw new NotSupportedException(...);`.

3. **[REFACTOR]** None.

**Dependencies:** A4
**Parallelizable:** Yes (parallel to B5)

---

### Task B5: Add `IOntologyQuery.DetectPatternViolations` (default throws)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `DetectPatternViolations_OnNonImplementingQuery_ThrowsNotSupported`
   - Same file as B4.
   - Expected failure: method does not exist.

2. **[GREEN]** Edit `src/Strategos.Ontology/Query/IOntologyQuery.cs`: add `IReadOnlyList<PatternViolation> DetectPatternViolations(IReadOnlyList<OntologyNodeRef> affectedNodes, DesignIntent intent) => throw new NotSupportedException(...);`.

3. **[REFACTOR]** None.

**Dependencies:** A5, A6
**Parallelizable:** Yes

---

## Track C: Reference implementations

### Task C1: Implement `OntologyQueryService.EstimateBlastRadius` (BFS)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `EstimateBlastRadius_SingleDomainSeed_ReturnsLocalScope`
   - File: `src/Strategos.Ontology.Tests/Query/EstimateBlastRadiusTests.cs` (new)
   - Build a small graph (1 domain, 2 types, 1 link), seed with 1 node; assert `Scope == Local`, `DirectlyAffected.Count == 1`, `CrossDomainHops.Count == 0`.

2. **[RED]** Write test: `EstimateBlastRadius_MultipleObjectTypesOneDomain_ReturnsDomainScope`
   - Seed touches 2 different types in the same domain; assert `Scope == Domain`.

3. **[RED]** Write test: `EstimateBlastRadius_AcrossCrossDomainLink_ReturnsCrossDomainScope`
   - Build 2 domains with a `CrossDomainLink`; seed in domain A; assert `Scope == CrossDomain`, `CrossDomainHops.Count >= 1`.

4. **[RED]** Write test: `EstimateBlastRadius_FourDomains_ReturnsGlobalScope`
   - Build 4 domains with cross-domain links; assert `Scope == Global`.

5. **[RED]** Write test: `EstimateBlastRadius_SameInputs_DeterministicOutput`
   - Run twice; assert `result1 == result2` via record equality.

6. **[RED]** Write test: `EstimateBlastRadius_MaxExpansionDegree_StopsExpansion`
   - Build a long chain; pass `MaxExpansionDegree = 2`; assert `TransitivelyAffected.Count` capped.

7. **[RED]** Write test: `EstimateBlastRadius_GraphWithCycle_TerminatesAndReturnsBoundedSet`
   - Build a graph with a derivation cycle (A → B → A) or postcondition cycle; seed with one node; assert the call terminates within a reasonable time, returns a finite set, and `TransitivelyAffected` does not contain duplicates. Exercises the `HashSet<OntologyNodeRef>` cycle guard called out in design §4.6.

8. **[GREEN]** Implement `OntologyQueryService.EstimateBlastRadius`.
   - File: `src/Strategos.Ontology/Query/OntologyQueryService.cs`
   - Algorithm per design §4.6: BFS frontier with `HashSet<OntologyNodeRef>` for cycle protection; expand via `GetDerivationChain`, `TracePostconditions`, `GetIncomingCrossDomainLinks`; classify scope; cap by `MaxExpansionDegree`.
   - Determinism: order results by `(DomainName, NodeName)` before returning.

9. **[REFACTOR]** Extract `ClassifyScope(IReadOnlyList<OntologyNodeRef>, IReadOnlyList<CrossDomainHop>)` private helper.

**Dependencies:** B4
**Parallelizable:** Yes (parallel to C2)

---

### Task C2: Implement `OntologyQueryService.DetectPatternViolations` (4 patterns)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test per pattern, each in `src/Strategos.Ontology.Tests/Query/DetectPatternViolationsTests.cs` (new):
   - `DetectPatternViolations_WriteToComputedProperty_ReturnsErrorViolation` — intent that writes a `.Computed()` prop.
   - `DetectPatternViolations_LinkWithoutMatchingExtensionPoint_ReturnsErrorViolation`.
   - `DetectPatternViolations_PreconditionReferencesMissingProperty_ReturnsErrorViolation`.
   - `DetectPatternViolations_UnreachableInitialState_ReturnsWarningViolation`.
   - Plus negatives: `DetectPatternViolations_AllPatternsClean_ReturnsEmpty`.

2. **[GREEN]** Implement `OntologyQueryService.DetectPatternViolations`.
   - File: `src/Strategos.Ontology/Query/OntologyQueryService.cs`
   - One private method per pattern; combine results into a single ordered list.

3. **[REFACTOR]** Extract `IPatternDetector` private interface + 4 internal sealed classes (Computed, MissingExtensionPoint, MissingPrecondition, UnreachableInitial) for future v2 extensibility — but keep them `internal` and don't expose registry yet.

**Dependencies:** B5
**Parallelizable:** Yes (parallel to C1)

---

### Task C3: Reference dispatcher populates `ActionResult.Violations`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Dispatch_PreconditionFails_ReturnsActionResultWithHardViolations`
   - File: `src/Strategos.Ontology.Tests/Actions/DispatcherViolationsPopulationTests.cs` (new)
   - Use the existing reference dispatcher (locate via `grep "class.*ActionDispatcher"`); set up a precondition that fails; assert `result.Violations` is non-null and `result.Violations.Hard.Count >= 1` and `result.Violations.ActionName` matches.

2. **[RED]** Write test: `Dispatch_SoftConstraintWarning_ReturnsActionResultWithSoftViolations` — when a soft constraint is violated but action proceeds, assert `IsSuccess == true` and `result.Violations.Soft.Count >= 1`.

3. **[GREEN]** Edit the reference dispatcher to call `IOntologyQuery.GetActionConstraintReport` on dispatch entry and populate `ActionResult.Violations` accordingly.

4. **[REFACTOR]** Extract `BuildViolationReport(...)` helper.

**Dependencies:** A2, A3, B2
**Parallelizable:** Yes (parallel to C4)

---

### Task C4: Reference dispatcher fans out to `IActionDispatchObserver` instances
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Dispatch_AfterCompletion_InvokesAllRegisteredObservers`
   - File: `src/Strategos.Ontology.Tests/Actions/DispatcherObserverFanOutTests.cs` (new)
   - Register two observers; call `DispatchAsync`; assert both observers' `OnDispatchedAsync` was called once with the same `ActionContext` and the produced `ActionResult`.

2. **[RED]** Write test: `Dispatch_ObserverThrows_DoesNotFailDispatch`
   - One throwing observer + one tracking observer; assert dispatch returns success and the tracking observer was still invoked.

3. **[RED]** Write test: `DispatchReadOnlyAsync_OnSuccess_AlsoInvokesObservers` — same fan-out via the read-only path.

4. **[GREEN]** Edit reference dispatcher to take `IEnumerable<IActionDispatchObserver>` via DI; after dispatch, await `Task.WhenAll(observers.Select(o => SafeInvoke(o, context, result, ct)))` where `SafeInvoke` wraps in try/catch and logs.

5. **[REFACTOR]** None.

**Dependencies:** B3, B2
**Parallelizable:** Yes (parallel to C3)

---

## Track D: DSL + Source generators

### Task D1: Add `.ReadOnly()` to action DSL
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ActionBuilder_ReadOnly_FlagsBuilder` and `ActionBuilder_BuildDescriptor_PropagatesIsReadOnly`
   - File: `src/Strategos.Ontology.Tests/Builder/ActionBuilderReadOnlyTests.cs` (new)
   - Build an action via the public DSL and call `.ReadOnly()`; build the descriptor; assert `descriptor.IsReadOnly == true`. Negative case: without `.ReadOnly()`, descriptor's `IsReadOnly == false`.
   - Expected failure: `ReadOnly()` method does not exist.

2. **[GREEN]** Add `.ReadOnly()` method.
   - File: `src/Strategos.Ontology/Builder/IActionBuilder.cs` — add `IActionBuilder ReadOnly();`.
   - File: `src/Strategos.Ontology/Builder/ActionBuilder.cs` — implement: set `_isReadOnly = true`, return `this`. Wire `_isReadOnly` into the produced `ActionDescriptor.IsReadOnly`.
   - File: `src/Strategos.Ontology/Builder/IActionBuilderOfT.cs` and `ActionBuilderOfT.cs` — same on the generic variant.

3. **[REFACTOR]** None.

**Dependencies:** B1
**Parallelizable:** No (sequential after B1)

---

### Task D2: AONT036 source-generator diagnostic
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write tests in `src/Strategos.Ontology.Generators.Tests/AONT036Tests.cs` (new) per pattern:
   - `AONT036_ReadOnlyThenModifies_FiresDiagnostic` — `.ReadOnly().Modifies(...)` produces AONT036.
   - `AONT036_ReadOnlyThenCreatesLinked_FiresDiagnostic`.
   - `AONT036_ReadOnlyThenEmitsEvent_FiresDiagnostic`.
   - `AONT036_ReadOnlyAlone_NoDiagnostic`.
   - `AONT036_ModifiesAlone_NoDiagnostic`.
   - `AONT036_TwoActionsOneReadOnlyOneMutating_OnlyMutatingAttributesDoNotFireOnReadOnlyAction` — same object type declares two actions; one calls `.ReadOnly()` (clean), the other calls `.Modifies(...)` (clean). Verifies per-action chain isolation: the analyzer must reset its `seenReadOnly` flag per action and not cross-contaminate state between sibling actions.

2. **[GREEN]** Add diagnostic + analyzer logic.
   - File: `src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs` — add `public const string ReadOnlyConflictsWithMutation = "AONT036";`.
   - File: `src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnostics.cs` — add `DiagnosticDescriptor` for AONT036 with title, format, severity Error.
   - File: `src/Strategos.Ontology.Generators/Analyzers/OntologyDefinitionAnalyzer.cs` — extend the chain walk to track `seenReadOnly` per action; on encountering `Modifies`/`CreatesLinked`/`EmitsEvent` with `seenReadOnly == true`, report at the offending invocation site.

3. **[REFACTOR]** Extract `IsMutatingChainCall(InvocationExpressionSyntax)` helper.

**Dependencies:** D1 (recognizes `.ReadOnly()` chain calls; the analyzer must match the same DSL surface)
**Parallelizable:** No (sequential after D1)

---

## Track E: MCP tool

### Task E1: Add `ValidationVerdict` record
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ValidationVerdict_Construction_PreservesAllFields`
   - File: `src/Strategos.Ontology.MCP.Tests/ValidationVerdictTests.cs` (new)
   - Construct with hard violations, soft warnings, blast radius, pattern violations, optional coverage; assert all fields.
   - Expected failure: type does not exist.

2. **[GREEN]** Add `src/Strategos.Ontology.MCP/ValidationVerdict.cs` (new) per design §4.8.

3. **[REFACTOR]** None.

**Dependencies:** A1, A4, A5, A6
**Parallelizable:** No (sequential before E2)

---

### Task E2: Implement `OntologyValidateTool.Validate(DesignIntent)`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write tests in `src/Strategos.Ontology.MCP.Tests/OntologyValidateToolTests.cs` (new):
   - `Validate_NoViolations_ReturnsPassedTrue`.
   - `Validate_HardViolations_ReturnsPassedFalse`.
   - `Validate_OnlySoftWarnings_ReturnsPassedTrue`.
   - `Validate_PatternViolationAtErrorSeverity_ReturnsPassedFalse`.
   - `Validate_EmptyAffectedNodes_ReturnsTrivialVerdict`.
   - `Validate_NoCoverageProviderRegistered_CoverageIsNull`.
   - `Validate_CoverageProviderRegistered_CoveragePopulated`.

2. **[GREEN]** Implement `src/Strategos.Ontology.MCP/OntologyValidateTool.cs` (new).
   - Constructor: `(IOntologyQuery query, IOntologyCoverageProvider? coverage = null, OntologyGraph? graph = null)`.
   - `Validate(DesignIntent intent)`: composes hard/soft from `query.GetActionConstraintReport`, calls `query.EstimateBlastRadius` and `query.DetectPatternViolations`, calls `coverage?.GetCoverage(intent)`, returns `ValidationVerdict` with `Passed = HardViolations.Count == 0 && PatternViolations.All(p => p.Severity == ViolationSeverity.Warning)`.

3. **[REFACTOR]** Extract `EvaluateActions(IReadOnlyList<ProposedAction>)` private helper.

**Dependencies:** E1, C1, C2
**Parallelizable:** No (sequential after E1)

---

### Task E3: Register `ontology_validate` in `OntologyToolDiscovery` + annotations + `_meta`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write tests in `src/Strategos.Ontology.MCP.Tests/OntologyValidateRegistrationTests.cs` (new):
   - `Discover_IncludesOntologyValidateDescriptor` — assert one descriptor has `Name == "ontology_validate"`.
   - `OntologyValidateDescriptor_Annotations_AreReadOnlyIdempotentNonDestructive` — assert `ReadOnlyHint == true`, `IdempotentHint == true`, `DestructiveHint == false`, `OpenWorldHint == false`.
   - `OntologyValidateDescriptor_OutputSchemaMatchesValidationVerdict` — generate output schema; assert it round-trips a sample `ValidationVerdict` JSON.
   - `OntologyValidateDescriptor_OutputSchema_HandlesNullCoverage` — generate output schema; assert the `Coverage` field is declared optional/nullable; round-trip a `ValidationVerdict` with `Coverage = null`. Verifies the schema does not erroneously mark `Coverage` as required.
   - `OntologyValidateResponse_HasMetaOntologyVersion` — wire the tool through the existing tool-pipeline harness; assert response carries `_meta.ontologyVersion` matching `graph.Version`.

2. **[GREEN]** Edit registration.
   - File: `src/Strategos.Ontology.MCP/OntologyToolDiscovery.cs` — append `ontology_validate` descriptor with the matrix above; populate `OutputSchema` via the same `System.Text.Json.Schema` flow Slice A introduced.
   - Edit `OntologyValidateTool` to attach `_meta.ontologyVersion = graph.Version` to its response (mirror the pattern used by `OntologyQueryTool`).

3. **[REFACTOR]** None.

**Dependencies:** E2
**Parallelizable:** No (sequential after E2)

---

## Track F: #33 fold-ins (independent — runs in parallel with all other tracks)

### Task F1: AONT041 link-target name extension (#33 Finding 1)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write tests in `src/Strategos.Ontology.Tests/OntologyGraphBuilderAONT041Tests.cs` (new or extend existing):
   - `AONT041_LinkTargetWithExplicitDescriptorName_ThrowsCompositionException` — reproduces the #33 case (TradeOrder/`open_orders`).
   - `AONT041_LinkTargetWithDefaultDescriptorName_DoesNotThrow` — positive control.

2. **[GREEN]** Edit `src/Strategos.Ontology/OntologyGraphBuilder.cs` (locate via grep): in `ValidateMultiRegisteredTypesNotInLinks`, after the existing multi-registration check, iterate every `LinkDescriptor`; if `link.TargetType` is registered with `descriptor.Name != descriptor.ClrType.Name`, throw `OntologyCompositionException("AONT041: …")` per design §4.9.

3. **[REFACTOR]** None.

**Dependencies:** None
**Parallelizable:** Yes

---

### Task F2: `PgVectorObjectSetProvider` strict graph check (#33 Finding 2)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write tests in `src/Strategos.Ontology.Npgsql.Tests/PgVectorObjectSetProviderResolveTableTests.cs` (new or extend existing):
   - `ResolveTableNameForDefaultOverload_GraphPresentTypeUnregistered_Throws` — assert `InvalidOperationException` with message naming the type.
   - `ResolveTableNameForDefaultOverload_GraphAbsent_FallsBackToCamelCase` — preserves existing test-mode behavior.
   - `ResolveTableNameForDefaultOverload_GraphPresentTypeRegistered_ReturnsRegisteredName` — positive control.

2. **[GREEN]** Edit `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs:126-151`: when `graph is not null` and `TryGetValue` returns false, throw `InvalidOperationException` with the diagnostic from #33's "Fix" snippet.

3. **[REFACTOR]** None.

**Dependencies:** None
**Parallelizable:** Yes

---

### Task F3: `DiscoverWorkflowChains` domain-keyed lookup (#33 Finding 4)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write tests in `src/Strategos.Ontology.Tests/DiscoverWorkflowChainsTests.cs` (new or extend existing):
   - `DiscoverWorkflowChains_CrossDomainSimpleNameSharing_ResolvesToCorrectDomain` — register two domains where each has an object type with simple name `"Order"`; build workflow metadata referencing each; assert each lookup returns the descriptor from its declaring domain.
   - `DiscoverWorkflowChains_SingleDomainCommonCase_StillWorks` — regression guard for the existing single-domain case.

2. **[GREEN]** Edit:
   - `src/Strategos.Ontology/Builder/WorkflowMetadataBuilder.cs` (locate via grep): carry `string DomainName` alongside chain entries.
   - `src/Strategos.Ontology/OntologyGraphBuilder.cs` `DiscoverWorkflowChains`: replace `GroupBy(ot.Name).First()` lookup with `Dictionary<(string DomainName, string Name), ObjectTypeDescriptor>`.

3. **[REFACTOR]** None.

**Dependencies:** None
**Parallelizable:** Yes

---

## Integration & wrap-up

### Task X1: Composite acceptance check
**Phase:** RED → GREEN

1. **[RED]** Write integration test: `Strategos_OntologyDispatchAndValidation_FullFlow_E2E`
   - File: `src/Strategos.Ontology.MCP.Tests/EndToEndDispatchValidationTests.cs` (new)
   - Steps: build a small graph; declare an `ontology_validate` request via `DesignIntent`; assert verdict shape; dispatch the same proposed action via `IActionDispatcher.DispatchReadOnlyAsync`; assert read-only path returns success and an observer was invoked; dispatch a mutating action with a precondition failure; assert `result.Violations` populated.

2. **[GREEN]** Wire any missing DI registrations needed for the e2e harness; no new production logic expected.

**Dependencies:** All prior tracks
**Parallelizable:** No (final integration)

---

## Suggested execution order (when parallelization is available)

```text
┌─ Wave 1 (parallel) ──────────────────────────────────────────────┐
│  Track A: A1, A2, A3 (chained), A4, A5, A6                       │
│  Track B: B1, B3                                                 │
│  Track F: F1, F2, F3                                             │
└──────────────────────────────────────────────────────────────────┘
              │
              ▼
┌─ Wave 2 (parallel after Wave 1) ─────────────────────────────────┐
│  Track B: B2 (after B1+A3), B4 (after A4), B5 (after A5+A6)      │
│  Track D: D1 (after B1)                                          │
└──────────────────────────────────────────────────────────────────┘
              │
              ▼
┌─ Wave 3 (parallel after Wave 2) ─────────────────────────────────┐
│  Track C: C1 (after B4), C2 (after B5), C3 (after B2),           │
│           C4 (after B2+B3)                                       │
│  Track D: D2 (after D1)                                          │
└──────────────────────────────────────────────────────────────────┘
              │
              ▼
┌─ Wave 4 (sequential) ────────────────────────────────────────────┐
│  Track E: E1 → E2 → E3                                           │
│  Task X1                                                         │
└──────────────────────────────────────────────────────────────────┘
```

Solo execution (no worktrees): follow alphabetical order within each track, finish A before B, B before C and D, C+D before E, X1 last. Track F can interleave anywhere.

## Acceptance criteria (rolled up from design §7)

- All tests in this plan pass.
- Existing dispatchers and `IOntologyQuery` consumers compile without changes (DIM safety).
- `[TypeForwardedTo]` for `ConstraintEvaluation` keeps `Strategos.Ontology.Query.ConstraintEvaluation` resolvable.
- Slice A regression suite still passes (no `_meta`/version drift).
- `OntologyValidateTool.outputSchema` round-trips a sample `ValidationVerdict` JSON.
- AONT036 fires on the three conflict patterns; does not fire on `.ReadOnly()` alone or on `.Modifies()` alone.
- AONT041 (#33-1) extended check throws `OntologyCompositionException` on the documented reproduction.

## References

- Design — `docs/designs/2026-05-08-ontology-2-5-0-dispatch-validation.md`
- Issues — strategos#39, #38, #42, #41, #33
- Predecessor plan — `docs/plans/2026-04-19-mcp-surface-conformance.md` (Slice A)
- TUnit invocation memo — internal team note (non-repo artifact). Authoritative test-invocation examples live in this plan and in `CONTRIBUTING.md`; `dotnet test --filter` does not work in Strategos — use `dotnet test --project <test-project>.csproj -- --treenode-filter "/*/*/*/Name"`.
