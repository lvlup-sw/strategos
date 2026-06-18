# Implementation Plan — Step Resilience Lowering

**Design:** [`docs/designs/2026-06-17-step-resilience-lowering.md`](../designs/2026-06-17-step-resilience-lowering.md)
**Epic:** [#135](https://github.com/lvlup-sw/strategos/issues/135) · **Scope:** core engine (`Strategos.Generators`) · **Workflow:** `step-resilience-lowering`

## Iron Law

NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST. Every task starts `[RED]`, states the expected failure, then `[GREEN]` minimum code, then `[REFACTOR]`.

> **Test invocation (TUnit):** `dotnet test --filter` does NOT work in this repo. Run a project's suite with `dotnet run --project <proj> -c Debug`, and filter with `-- --treenode-filter "/*/*/*/Method_*"`.

## Diagnostic-ID note (INV-5)

The INV-5 reference doc's "AGWF001..010" is **stale**. Live ceiling is **AGWF016** (verified against `src/Strategos.Contracts/Generated/AgwfCodes.g.cs`; gaps exist at 005–008, 011, 013). Provisional next-free ids below are **AGWF017+**; the implementer **verifies next-free against `AgwfCodes.g.cs` at GREEN time** and documents each id there + in `WorkflowDiagnostics.cs`. No existing id is removed/renumbered.

## Wolverine-API note (grounding)

Emit targets are pinned to primary docs (design [Sources]): retry/compensation → per-handler `static Configure(HandlerChain)` with `OnAnyException().RetryTimes/RetryWithCooldown` + `.Then.CompensatingAction<T>(…, InvokeResult.Stop)`; timeout → saga `record …Timeout : TimeoutMessage(t)`; durability → `AddMarten(…).IntegrateWithWolverine()` + durable inbox/outbox. Implementers must not substitute global `WolverineOptions.Policies` for the per-handler form.

## Traceability matrix (DR-N → tasks)

| DR | Requirement | Tasks |
|----|-------------|-------|
| DR-1 | Resilience IR + parse plumbing (keystone) | T1, T2, T3, T4 |
| DR-9 | Behavioral-test harness (acceptance unlock) | T5, T6 (+ all Track D/H behavioral) |
| DR-2 | Retry lowering (per-handler `Configure`) | T7, T11 |
| DR-3 | Compensation / OnFailure (close orphan trigger) | T8, T12 |
| DR-4 | Timeout (saga `TimeoutMessage` deadline race) | T9, T13 |
| DR-5 | Confidence gate (saga branch) | T10, T14 |
| DR-6 | WithContext wire-in (ontology-backed) | T15, T16 |
| DR-7 | Expressibility on branch + failure contexts | T17 |
| DR-8 | INV-5 resilience diagnostics (AGWF017+) | T18 |
| DR-10 | Composition / error / edge cases | T19, T20, T21, T22 |
| INV-6 | Sealed-guard extension | T23 |
| Docs | Integration-points doc reconciliation | T24 |

## Parallelization

```
Group 1 (start immediately, parallel — disjoint projects):
  ├─ Track A: T1 → T2 → T3 → T4         (DR-1 IR+parse keystone — blocks C, F, G, H)
  └─ Track B: T5 → T6                   (DR-9 harness scaffolding — new project, disjoint)

Group 2 (after Track A):
  ├─ Track C: T7 → T8 ; T9 ; T10        (emit — T7→T8 share WorkerHandlerEmitter chain; T9/T10 sequence on saga emitters)
  ├─ Track E: T15 → T16                 (DR-6 — WorkflowIncrementalGenerator entry; parallel w/ C)
  ├─ Track G: T18                       (DR-8 diagnostics — WorkflowDiagnostics/analyzer; parallel w/ C)
  └─ Track F: T17                       (DR-7 expressibility — branch/failure builders + parse path)

Group 3 (after Track B + matching Track C task — new test project, disjoint files → parallel):
  └─ Track D: T11 ∥ T12 ∥ T13 ∥ T14     (behavioral proof per capability)

Group 4 (after Track C + D):
  └─ Track H: T19 → T20 ; T21 ; T22 ; T23   (composition, races, immutability, golden baseline, sealed-guard)

Final: T24 (docs reconciliation)
```

Worktree isolation: Track A (`StepModel.cs`/`StepExtractor.cs` + new IR models) and Track B (`Strategos.Generators.Behavioral.Tests` new project) touch disjoint files — worktree-parallel-safe. Within Track C, T7→T8 share `WorkerHandlerEmitter.cs` (sequential); T9/T10 touch `SagaEmitter`/`Saga/*` emitters. Track D test files are one-per-capability (disjoint) once the harness (T6) exists.

---

## Track A — DR-1: Resilience IR + parse (keystone)

### Task 1: Resilience IR models + StepModel fields
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-1 · **testingStrategy:** unit (propertyTests: no, benchmarks: no)

1. [RED] `StepModel_WithResilienceConfig_CarriesRetryTimeoutCompensationConfidence`
   - File: `src/Strategos.Generators.Tests/Models/StepModelResilienceTests.cs`
   - Expected failure: `StepModel` has no `Retry`/`Timeout`/`Compensation`/`Confidence` members; `RetryModel`/`TimeoutModel`/`CompensationModel`/`ConfidenceModel` don't exist.
2. [GREEN] Add `sealed record` models (`RetryModel`, `TimeoutModel`, `CompensationModel`, `ConfidenceModel`) under `Generators/Models/`; add `init`-only fields to `StepModel` (`StepModel.cs:22-29`) + the `Create` factory + validation.
3. [REFACTOR] XML docs; keep existing positional params backward-compatible (new params default `null`).

**Dependencies:** None · **Parallelizable:** No (Track A head)

### Task 2: StepExtractor parses `.WithRetry` / `.WithTimeout`
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-1 · **testingStrategy:** unit

1. [RED] `WalkInvocationChain_StepWithWithRetryAndTimeout_PopulatesRetryAndTimeoutModels`
   - File: `src/Strategos.Generators.Tests/Helpers/StepExtractorResilienceTests.cs`
   - Expected failure: `WalkInvocationChainForStepModelsInternal` (`StepExtractor.cs:760-825`) ignores `.WithRetry`/`.WithTimeout`; resulting `StepModel.Retry`/`.Timeout` are null.
2. [GREEN] Add parse branches + argument parsers for `WithRetry(int)`, `WithRetry(int, TimeSpan)`, `WithTimeout(TimeSpan)` mapping to the IR (incl. the richer `RetryConfiguration` fields when present).
3. [REFACTOR] Extract a `ParseResilienceCall(...)` helper.

**Dependencies:** T1 · **Parallelizable:** No

### Task 3: StepExtractor parses `.Compensate<T>` (INV-8 SymbolKey) + confidence
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-1 (INV-8) · **testingStrategy:** unit

1. [RED] `WalkInvocationChain_CompensateOfT_CarriesCompensationStepSymbolKey` + `WalkInvocationChain_RequireConfidenceOnLowConfidence_PopulatesConfidenceModel`
   - File: `src/Strategos.Generators.Tests/Helpers/StepExtractorResilienceTests.cs`
   - Expected failure: `.Compensate<T>`/`.RequireConfidence`/`.OnLowConfidence` unparsed; no compensation type symbol captured.
2. [GREEN] Parse the type-argument **symbol** for `Compensate<T>` into `CompensationModel` (SymbolKey, not name-string); parse confidence threshold + `OnLowConfidence` branch reference.
3. [REFACTOR] Assert INV-8: no `typeof`/name-string identity for the compensation target on this path.

**Dependencies:** T2 · **Parallelizable:** No

### Task 4: Loop + fork-path parse parity
**Phase:** RED → GREEN · **Implements:** DR-1 · **testingStrategy:** unit

1. [RED] `WalkInvocationChain_WithRetryInsideLoopStep_PopulatesRetryModel`
   - File: `src/Strategos.Generators.Tests/Helpers/StepExtractorResilienceTests.cs`
   - Expected failure: loop/fork step parse path does not thread resilience config (matrix gap: `LoopBuilder` column).
2. [GREEN] Thread the DR-1 parsing into the loop and fork-path step extraction so config is captured identically.

**Dependencies:** T3 · **Parallelizable:** No

---

## Track B — DR-9: Behavioral-test harness scaffolding

### Task 5: New behavioral test project + package backbone
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-9 · **testingStrategy:** integration (propertyTests: no, benchmarks: no)

1. [RED] `PostgresFixture_StartsContainer_ConnectionOpens`
   - File: `src/Strategos.Generators.Behavioral.Tests/Infrastructure/PostgresFixtureSmokeTests.cs`
   - Expected failure: project, packages, and `PostgresFixture` don't exist (no `Wolverine`/`Marten`/`Npgsql`/`Testcontainers.PostgreSql` in `Directory.Packages.props`).
2. [GREEN] Create `Strategos.Generators.Behavioral.Tests.csproj`; add the four packages to `src/Directory.Packages.props`; implement a `PostgresFixture : IAsyncLifetime` (Testcontainers) shared via TUnit `[ClassDataSource]`. Add to `Strategos.sln`. Mark `[NotInParallel]` where a shared host is asserted (per `project_tunit_static_state_parallelism`).
3. [REFACTOR] Centralize the connection-string accessor.

**Dependencies:** None · **Parallelizable:** Yes (with Track A — disjoint files)

### Task 6: Wolverine + Marten host fixture over a compiled fixture workflow
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-9 · **testingStrategy:** integration

1. [RED] `Host_FixtureWorkflow_StartsAndCompletesHappyPath`
   - File: `src/Strategos.Generators.Behavioral.Tests/Infrastructure/HostFixtureTests.cs`
   - Expected failure: no `UseWolverine` host + `AddMarten(...).IntegrateWithWolverine()` wiring; no SG-compiled fixture workflow with instrumentable steps.
2. [GREEN] Add a fixture workflow (steps with injectable behaviors — counters/delays/throw-on-attempt) compiled by the SG in this project; build the Wolverine+Marten host fixture using `PostgresFixture`; send the start command via `IMessageBus`; assert happy-path completion.
3. [REFACTOR] Expose a `RunWorkflowAsync(start)` + step-instrumentation helper reused by Track D.

**Dependencies:** T5 · **Parallelizable:** No

---

## Track C — emit lowering (needs Track A)

### Task 7: Retry emit — per-handler `Configure(HandlerChain)`
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-2 · **testingStrategy:** unit

1. [RED] `Emit_StepWithWithRetry2_GeneratesConfigureWithRetryTimes` + `Emit_StepWithWithRetryAndDelay_GeneratesRetryWithCooldown`
   - File: `src/Strategos.Generators.Tests/Emitters/RetryLoweringTests.cs`
   - Expected failure: `{Step}Handler` has no `Configure(HandlerChain)`; `WorkerHandlerEmitter` emits no retry policy.
2. [GREEN] In `WorkerHandlerEmitter`, when `StepModel.Retry` is set, emit `public static void Configure(HandlerChain chain)` → `chain.OnAnyException().RetryTimes(n)` (no delay) or `.RetryWithCooldown(delays…)` (+ jitter when `UseJitter`). Keep `catch { throw; }`.
3. [REFACTOR] Guard: no `Configure` emitted when `Retry`/`Compensation` both absent.

**Dependencies:** T4 · **Parallelizable:** No (shares the `WorkerHandlerEmitter` Configure chain; must precede the compensation emit)

### Task 8: Compensation emit — `CompensatingAction` publishes the orphan trigger
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-3 · **testingStrategy:** unit

1. [RED] `Emit_StepWithCompensate_ConfigureChainPublishesTriggerFailureHandlerCommand`
   - File: `src/Strategos.Generators.Tests/Emitters/CompensationLoweringTests.cs`
   - Expected failure: `Trigger{Name}FailureHandlerCommand` is still never published; no `.Then.CompensatingAction` in emitted `Configure`.
2. [GREEN] Append `.Then.CompensatingAction<{WorkerCommand}>((cmd,ex,bus)=>bus.PublishAsync(new Trigger{Name}FailureHandlerCommand(cmd.WorkflowId,"{Step}",ex.Message,ex.GetType().Name,ex.StackTrace)), InvokeResult.Stop)` to the chain; emit a `StepFailed`/`RetryExhausted` event append into the saga stream.
3. [REFACTOR] Share the `Configure`-chain builder with the retry emit; `Compensate` without `WithRetry` ⇒ single attempt then compensate.

**Dependencies:** T7 · **Parallelizable:** No

### Task 9: Timeout emit — saga `TimeoutMessage` + deadline-race handler
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-4 · **testingStrategy:** unit

1. [RED] `Emit_StepWithTimeout_GeneratesTimeoutMessageRecordAndSagaHandler`
   - File: `src/Strategos.Generators.Tests/Emitters/TimeoutLoweringTests.cs`
   - Expected failure: no `record {Step}Timeout : TimeoutMessage`; step-start handler doesn't cascade it; no `Handle({Step}Timeout,…)`.
2. [GREEN] In `SagaEmitter`/`Saga/*`, emit `record {Step}Timeout(Guid WorkflowId) : TimeoutMessage(<t>)`, cascade it from the step-start handler, and emit a `Handle({Step}Timeout,…)` that routes to failure/compensation **only if the step phase hasn't completed** (idempotent guard).
3. [REFACTOR] Reuse the approval-timeout emit helpers where shared.

**Dependencies:** T4 · **Parallelizable:** Yes (different emitter file from the retry/compensation chain; sequences with the confidence emit on the saga emitters)

### Task 10: Confidence gate emit — saga branch
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-5 · **testingStrategy:** unit

1. [RED] `Emit_StepWithRequireConfidence_GeneratesConfidenceThresholdBranchDispatch`
   - File: `src/Strategos.Generators.Tests/Emitters/ConfidenceLoweringTests.cs`
   - Expected failure: emitter only sets a `step.confidence` span tag (`WorkerHandlerEmitter.cs:218`); no comparison to threshold, no `OnLowConfidence` dispatch.
2. [GREEN] Emit a saga-routing branch: when `result.Confidence < ConfidenceThreshold`, dispatch the `OnLowConfidence` branch; else normal continuation. Emit a `LowConfidenceRouted` event. Keep the telemetry tag.
3. [REFACTOR] `RequireConfidence` without `OnLowConfidence` ⇒ fail path (covered by the DR-8 diagnostic task).

**Dependencies:** T4 · **Parallelizable:** Yes (sequences with the timeout emit on the saga emitters)

---

## Track D — behavioral proof (needs Track B + matching Track C task)

### Task 11: Retry actually retries
**Phase:** RED → GREEN · **Implements:** DR-2, DR-9 · **testingStrategy:** integration

1. [RED] `Saga_StepWithWithRetry2_InvokesStepExactlyTwiceThenSucceeds`
   - File: `src/Strategos.Generators.Behavioral.Tests/RetryBehaviorTests.cs`
   - Expected failure: without DR-2 emit, the step runs once and the saga dead-letters/throws.
2. [GREEN] Fixture step throws transiently on attempts 1–2, succeeds on 3; assert `step.Attempts == 3` and saga completes. (Verifies the property the shape suite cannot.)

**Dependencies:** T6, T7 · **Parallelizable:** Yes (disjoint behavioral test files across the capability suites)

### Task 12: Compensation runs after retries exhaust
**Phase:** RED → GREEN · **Implements:** DR-3, DR-9 · **testingStrategy:** integration

1. [RED] `Saga_RetryExhaustedWithCompensate_RunsCompensationOnceAndTransitionsToFailed`
   - File: `src/Strategos.Generators.Behavioral.Tests/CompensationBehaviorTests.cs`
   - Expected failure: compensation step never runs (trigger never published) pre-DR-3.
2. [GREEN] Fixture step always throws with `.WithRetry(2).Compensate<Rollback>()`; assert `Rollback.Runs == 1`, saga phase `Failed`, and a `StepFailed` event in the Marten stream.

**Dependencies:** T6, T8 · **Parallelizable:** Yes

### Task 13: Timeout fires; idempotent when step wins the race
**Phase:** RED → GREEN · **Implements:** DR-4, DR-9 · **testingStrategy:** integration

1. [RED] `Saga_StepExceedsTimeout_RoutesToTimeoutPath` + `Saga_StepCompletesBeforeTimeout_TimeoutHandlerIsNoOp`
   - File: `src/Strategos.Generators.Behavioral.Tests/TimeoutBehaviorTests.cs`
   - Expected failure: no timeout enforcement pre-DR-4.
2. [GREEN] One fixture step sleeps past a 50ms timeout (routes to failure); another completes in 10ms with a 5s timeout (handler no-op, saga not double-failed). Durable inbox/outbox enabled on the host.

**Dependencies:** T6, T9 · **Parallelizable:** Yes

### Task 14: Low confidence routes to the alternate branch
**Phase:** RED → GREEN · **Implements:** DR-5, DR-9 · **testingStrategy:** integration

1. [RED] `Saga_LowConfidence_RoutesToOnLowConfidenceBranch` + `Saga_HighConfidence_ProceedsOnPrimaryPath`
   - File: `src/Strategos.Generators.Behavioral.Tests/ConfidenceBehaviorTests.cs`
   - Expected failure: confidence is telemetry-only pre-DR-5; both cases take the primary path.
2. [GREEN] Fixture step returns `Confidence=0.5` (asserts `HumanReview` ran, primary did not) and `0.9` (asserts primary ran).

**Dependencies:** T6, T10 · **Parallelizable:** Yes

---

## Track E — DR-6: WithContext wire-in

### Task 15: Wire `ContextAssemblerEmitter` into the generator
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-6 · **testingStrategy:** unit

1. [RED] `Generate_StepWithWithContext_EmitsContextAssembler`
   - File: `src/Strategos.Generators.Tests/Emitters/ContextWireInTests.cs`
   - Expected failure: `WorkflowIncrementalGenerator` never invokes `ContextAssemblerEmitter`; no `{Step}ContextAssembler` in generated output.
2. [GREEN] Invoke `ContextAssemblerEmitter` (+ `ContextModelExtractor`) from `WorkflowIncrementalGenerator` for `.WithContext` steps; wire the assembler into the worker handler. No `Strategos.Rag` reference.
3. [REFACTOR] Assert `using Strategos.Ontology.ObjectSets` only; confirm `IObjectSetProvider` dependency.

**Dependencies:** T4 · **Parallelizable:** Yes (with Track C — generator-entry file, disjoint from emitters)

### Task 16: WithContext assembles ontology context at runtime
**Phase:** RED → GREEN · **Implements:** DR-6, DR-9 · **testingStrategy:** integration

1. [RED] `Saga_StepWithContext_AssemblesContextAndInvokesExecuteSimilarity`
   - File: `src/Strategos.Generators.Behavioral.Tests/ContextBehaviorTests.cs`
   - Expected failure: no assembler invoked pre-DR-6.
2. [GREEN] Stub `IObjectSetProvider`; assert the step receives assembled context (state+retrieval+literal) and `ExecuteSimilarityAsync` is called with the declared `TopK`/`MinRelevance`.

**Dependencies:** T6, T15 · **Parallelizable:** Yes

---

## Track F — DR-7: Expressibility on branch + failure contexts

### Task 17: `Then<TStep>(Action<IStepConfiguration>)` on branch + failure builders
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-7 · **testingStrategy:** unit

1. [RED] `BranchBuilder_ThenWithConfig_StepModelCarriesRetry` + `FailureHandlerBuilder_ThenWithConfig_StepModelCarriesRetry`
   - File: `src/Strategos.Generators.Tests/Builders/BranchFailureConfigExpressibilityTests.cs`
   - Expected failure: the configure-overload is absent on branch/failure builders (fork landed via #134); declared config doesn't reach the IR.
2. [GREEN] Add the overload to the branch + failure-handler builders; thread config through their parse paths (`ParseForkPathStepModels`-style). Update `PublicAPI.Unshipped.txt` (RS0016/RS0017 break by design — do not suppress).
3. [REFACTOR] De-dupe the configure-capture across fork/branch/failure.

**Dependencies:** T3 · **Parallelizable:** Yes (after Track A; touches builder files + parse path)

---

## Track G — DR-8: INV-5 resilience diagnostics

### Task 18: Analyzer diagnostics for invalid resilience config (AGWF017+)
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-8 · **testingStrategy:** unit (analyzer)

1. [RED] `Analyze_CompensateNonStepType_FiresNextFreeAgwf` + `Analyze_RequireConfidenceOutOfRange_Fires` + `Analyze_RequireConfidenceWithoutOnLowConfidence_Fires` + conformant negative cases
   - File: `src/Strategos.Generators.Tests/Diagnostics/ResilienceDiagnosticsTests.cs`
   - Expected failure: no analyzer rule covers these; live ceiling AGWF016, next-free unused.
2. [GREEN] Add descriptors at next-free ids (verify against `AgwfCodes.g.cs`): `Compensate<T>` not an `IWorkflowStep<TState>`, `RequireConfidence` ∉ [0,1], `RequireConfidence` without `OnLowConfidence`, retry `<1`, non-positive timeout. Report at analyzer tier (earliest). Mirror builder-runtime throws as stable ids.
3. [REFACTOR] Document each new id in `AgwfCodes.g.cs` + `WorkflowDiagnostics.cs`.

**Dependencies:** T4 · **Parallelizable:** Yes (with Track C — diagnostics/analyzer files disjoint from emitters)

---

## Track H — DR-10: Error handling, composition, and edge cases

This track covers DR-10's error handling, composition precedence, failure-mode races, the INV-7 immutability edge case, and the no-config baseline.

### Task 19: Composition precedence and error-handling across composed resilience
**Phase:** RED → GREEN · **Implements:** DR-10 (error handling, composition, edge cases) · **testingStrategy:** integration

1. [RED] `Saga_StepWithAllResilience_RetriesThenTimeoutSpansRetriesThenCompensates_AndLowConfidenceRoutesNotCompensates`
   - File: `src/Strategos.Generators.Behavioral.Tests/CompositionBehaviorTests.cs`
   - Expected failure: precedence undefined until all emit DRs compose.
2. [GREEN] Assert: retries first; timeout deadline spans all retries; compensation only after retries exhaust; a *successful-but-low-confidence* attempt routes via `OnLowConfidence`, **not** compensation.

**Dependencies:** T11, T12, T13, T14 · **Parallelizable:** No (Track H head)

### Task 20: Timeout-vs-retry race + late-completion idempotency
**Phase:** RED → GREEN · **Implements:** DR-10 · **testingStrategy:** integration

1. [RED] `Saga_TimeoutDuringRetry_FailsByTimeoutIdempotently_LateCompletedDoesNotResurrect`
   - File: `src/Strategos.Generators.Behavioral.Tests/CompositionBehaviorTests.cs`
   - Expected failure: a late `CompletedEvent` after a fired timeout could resurrect/double-process the saga.
2. [GREEN] Assert the saga fails-by-timeout once and a late completed event is ignored (phase guard).

**Dependencies:** T19 · **Parallelizable:** No

### Task 21: INV-7 — same immutable input across retry attempts
**Phase:** RED → GREEN · **Implements:** DR-10 (INV-7) · **testingStrategy:** integration (propertyTests: yes)

1. [RED] `Saga_StepRetried_EachAttemptReceivesIdenticalImmutableState`
   - File: `src/Strategos.Generators.Behavioral.Tests/RetryBehaviorTests.cs`
   - Expected failure: no assertion that re-delivery preserves identical input across attempts.
2. [GREEN] Capture the input state reference/value per attempt; assert all attempts received the same immutable state (no mutation-across-attempts). Property test over varied state shapes.

**Dependencies:** T11 · **Parallelizable:** Yes

### Task 22: No-config golden baseline
**Phase:** RED → GREEN · **Implements:** DR-10 · **testingStrategy:** unit (golden)

1. [RED] `Emit_StepWithoutResilience_GeneratedOutputUnchangedFromBaseline`
   - File: `src/Strategos.Generators.Tests/Emitters/NoConfigBaselineTests.cs`
   - Expected failure: assert no `Configure`, no `TimeoutMessage`, no confidence branch for a config-free step; pin via Verify snapshot.
2. [GREEN] Confirm the emit guards (T7–T10) leave config-free steps byte-identical to the pre-change baseline.

**Dependencies:** T7, T8, T9, T10 · **Parallelizable:** Yes

### Task 23: INV-6 sealed-guard extension
**Phase:** RED → GREEN · **Implements:** INV-6 · **testingStrategy:** unit

1. [RED] `InvariantGuard_ResilienceIrAndModelTypes_AreSealedRecords`
   - File: `src/Strategos.Generators.Tests/InvariantGuardTests.cs` (extend existing)
   - Expected failure: new types not asserted sealed/`init`-only.
2. [GREEN] Add the new IR/model/emitter types to the sealed-guard reflection assertion.

**Dependencies:** T1 · **Parallelizable:** Yes

---

## Track I — Docs

### Task 24: Reconcile docs to "enforced" (no longer "declared, not lowered")
**Phase:** Documentation (no test — explicitly non-TDD) · **Implements:** DR-8 / Integration Points

- Update `Abstractions/IStepConfiguration.cs` XML docs, `docs/theory/agentic-workflow-theory.md`, `docs/deferred-features.md`, `docs/workflow-library-roadmap-v2.md` to reflect that step resilience now lowers (retry/timeout/compensation/confidence) + the durable-outbox prerequisite for timeout. Note the WithContext ontology-backed wire-in.

**Dependencies:** T11–T16 (land after behavior proven) · **Parallelizable:** Yes
**Note:** Documentation-only task — exempt from the Iron Law (no production code).
