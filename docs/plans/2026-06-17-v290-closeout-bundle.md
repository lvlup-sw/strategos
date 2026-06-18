# Implementation Plan — v2.9.0 Close-Out Bundle

**Design:** `docs/designs/2026-06-17-v290-closeout-bundle.md` · **Workflow:** `v290-closeout-bundle`
**Scope:** step-resilience completeness (#142/#141/#140/#139/#138) + parity guard (#143) + schema bootstrap (#132). **Defers to v2.10.0:** #128, #130 R3a/R4/R5/R6/R8 (R1/R2 already shipped).

## Iron Law

> **NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST.** Every task is RED → GREEN → REFACTOR.

## Conventions for this plan

- **Test runner (TUnit):** `cd src && dotnet test <proj> -- --treenode-filter "/*/*/*/MethodName"`. Bare `--filter` does **not** work in this repo (`feedback_tunit_test_invocation`).
- **Assertions awaited:** `await Assert.That(x).IsEqualTo(y);`.
- **Behavioral suite:** `Strategos.Generators.Behavioral.Tests` (real Wolverine + Marten + Testcontainers Postgres). Lowering correctness is proven here, **mutation-proven** (reverting the lowering makes the test RED) — the #143 standard. Document-mode fixtures: `WolverineHostFixture`, `CompensationHostFixture`. **No EventSourced fixture exists yet** (built in G-5).
- **DB-gated suites:** `Strategos.Generators.Behavioral.Tests` + `Strategos.Ontology.Npgsql.Tests` need a live Postgres+pgvector; gate them like the existing Node/benchmark suites in publish-verify so default `dotnet build`/`dotnet test` stays green without a database.
- **Public-API drift:** any builder/provider signature change trips RS0016/RS0017 by design — update `PublicAPI.Unshipped.txt` + CHANGELOG in the same task; do **not** suppress.
- **Diagnostic ids:** new `AGWF` continue past live ceiling **AGWF021** → **AGWF022**, single-sourced in `Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` (regen `AgwfCodes.g.cs` — TypeSpec compile needs Node 22+, `project_contracts_tests_need_node_job`) + `WorkflowDiagnostics.cs`. Monotonic; never reuse. No `AONT` needed (CL-7 is runtime API only).
- **Sealed-guard (INV-6):** every new public/internal record is appended to `InvariantGuardTests` (`Strategos.Generators.Tests/InvariantGuardTests.cs:42`) in its REFACTOR step.

## Dependency spine (task groups)

```
G-1 (#142 escape)      ─┐
G-2 (#141 StartWith/Finally) ─┤  parallel from t0
G-7 (#132 schema bootstrap)  ─┘

G-3 (#140 OnFailure+Compensate) ─┐
G-4 (#139 multi-step OnLowConf)  ─┤─► G-5 (#138 EventSourced events) ─► G-6 (#143 parity guard, LAST)
                                  ┘     (extends both handlers)          (allowlist over completed surface)
```

**Critical path:** {G-3, G-4} → G-5 → G-6. **Parallel from t0:** G-1, G-2, G-7. **Worktree-isolation note:** G-3 and G-4 touch *different* emitters (G-3: `SagaFailureHandlerComponentEmitter` + `SagaCompensationComponentEmitter`; G-4: `StepExtractor` confidence + confidence emitter) so they parallelize; G-5 extends both failure and confidence handlers, so it lands **after** both; G-6 reflects the *finished* surface, so it lands **last**.

---

## G-1 — CL-1 (#142): Escape validation-error-message literal

### Task 1.1: Escaped literal compiles
1. **[RED]** `StepStartHandlerEmitter_ValidationMessageWithQuoteAndBackslash_EmitsCompilableLiteral`
   - File: `src/Strategos.Generators.Tests/Emitters/StepStartHandlerEmitterTests.cs`
   - Drive a workflow whose `ValidateState` message is `He said \"go\" \\ stop`; assert the generated source for the start handler compiles (Roslyn `CSharpCompilation` with no `CS1009`/`CS1010`), or that the emitted literal equals the `SymbolDisplay.FormatLiteral` form.
   - Expected failure: today `StepStartHandlerEmitter.cs:128` interpolates raw `Token.ValueText` → broken literal.
2. **[GREEN]** Replace the raw interpolation at `src/Strategos.Generators/Emitters/Saga/StepStartHandlerEmitter.cs:128` with `SymbolDisplay.FormatLiteral(stepModel.ValidationErrorMessage, quote: true)`.
3. **[REFACTOR]** Grep sibling emitters for other raw-message interpolations; factor a `LiteralOf(...)` helper if a second site exists.

**Dependencies:** None · **Parallelizable:** Yes

---

## G-2 — CL-2 (#141): `StartWith` / `Finally` accept step config

### Task 2.1: Builder overloads + IR threading (parse)
1. **[RED]** `StepExtractor_StartWithConfigure_PopulatesFirstStepResilience` and `StepExtractor_FinallyConfigure_PopulatesTerminalStepResilience`
   - File: `src/Strategos.Generators.Tests/Helpers/StepExtractorConfigTests.cs`
   - A workflow declaring `.StartWith<First>(s => s.WithRetry(2))` … `.Finally<Last>(s => s.WithTimeout(t))`; assert the resulting `StepModel`s carry `Retry`/`Timeout` (via the existing `ExtractConfiguredResilience` path).
   - Expected failure: overloads don't exist — won't compile / config dropped.
2. **[GREEN]** Add `StartWith<TStep>(Action<IStepConfiguration<TState>> configure)` (and the named-instance variant) and `Finally<TStep>(Action<IStepConfiguration<TState>> configure)` to `src/Strategos/Abstractions/IWorkflowBuilder.cs` (mirror `Then(configure)` at `:129`) + the concrete `WorkflowBuilder` impl; route the captured config through the same path `Then(configure)` uses so `StepExtractor` populates the IR.
3. **[REFACTOR]** No new record (reuses `StepModel` config). Confirm `ILoopBuilder.cs:100` parity unaffected.

**Dependencies:** None · **Parallelizable:** Yes

### Task 2.2: PublicAPI baseline
1. **[RED]** Build trips `RS0016` (new public members not in shipped baseline).
2. **[GREEN]** Add the four signatures to `src/Strategos/PublicAPI.Unshipped.txt`; add a CHANGELOG entry. Do not suppress.

**Dependencies:** Task 2.1 · **Parallelizable:** No

### Task 2.3: Behavioral proof
1. **[RED]** `Behavioral_StartWithStepWithRetry_RetriesIndependently` (start step throws twice, succeeds on 3rd → `ExecuteAsync` invoked 3×) and `Behavioral_FinallyStepWithTimeout_RoutesToTimeoutPath`
   - File: `src/Strategos.Generators.Behavioral.Tests/StartWithFinallyResilienceTests.cs` (reuse `WolverineHostFixture`)
   - Expected failure (pre-2.1): no `Configure` emitted for the first/terminal handler.
2. **[GREEN]** Verified by Task 2.1's emission; this task adds the mutation-proof (revert 2.1 → RED).

**Dependencies:** Task 2.1 · **Parallelizable:** No

---

## G-3 — CL-3 (#140): OnFailure worker handler + Compensate↔OnFailure interop

### Task 3.1: Close the dead OnFailure worker command
1. **[RED]** `Behavioral_WorkflowOnFailureChain_RunsHandlerStep`
   - File: `src/Strategos.Generators.Behavioral.Tests/FailureHandlerChainTests.cs` (new `FailureHandlerHostFixture` or reuse `CompensationHostFixture`)
   - Workflow `OnFailure(flow => flow.Then<NotifyFailure>())` + a step that always throws; assert `NotifyFailure.ExecuteAsync` runs once and saga reaches `Failed`.
   - Expected failure: `SagaFailureHandlerComponentEmitter.cs:131` dispatches `ExecuteFailureHandler_{id}_{step}WorkerCommand` but **no worker `Handle`** exists → chain never runs.
2. **[GREEN]** Emit the missing worker handler for `ExecuteFailureHandler_…WorkerCommand` (in `SagaFailureHandlerComponentEmitter` / the worker-handler emit path) so the dispatched command is handled and the failure-handler step executes.
3. **[REFACTOR]** Golden shape test for the new `Handle` in `Strategos.Generators.Tests`.

**Dependencies:** None · **Parallelizable:** Yes (vs G-4/G-1/G-2/G-7)

### Task 3.2: Compensate↔OnFailure interop (single Handle, ordered)
1. **[RED]** `Behavioral_StepCompensateAndWorkflowOnFailure_RunsCompensationThenFailureChain`
   - File: same fixture as 3.1
   - Workflow with step `.Compensate<Rollback>()` **and** `OnFailure(flow => flow.Then<NotifyFailure>())`; failing step → `Rollback.ExecuteAsync` runs once, **then** `NotifyFailure` runs; saga `Failed`.
   - Expected failure: `SagaCompensationComponentEmitter.cs:87` `if (model.HasFailureHandlers) return;` makes them mutually exclusive — compensation is skipped.
2. **[GREEN]** Replace the no-op guard with a **merged single `Handle(Trigger…)` emission** that dispatches compensation first, then the failure-handler chain — eliminating the CS0111 duplicate-`Handle` collision the guard was avoiding. Phase-enum prefixing + durable checkpoint stay correct.
3. **[REFACTOR]** Golden test pins exactly one `Handle(Trigger…)` site; confirm no CS0111 in the compile fixture.

**Dependencies:** Task 3.1 · **Parallelizable:** No (same emitters)

---

## G-4 — CL-4 (#139): Multi-step + rejoining `OnLowConfidence`

### Task 4.1: Multi-step handler chain
1. **[RED]** `Behavioral_OnLowConfidenceTwoStepChain_RunsBothInOrder`
   - File: `src/Strategos.Generators.Behavioral.Tests/LowConfidenceChainTests.cs`
   - `RequireConfidence(0.85).OnLowConfidence(alt => alt.Then<A>().Then<B>())` with a step returning `Confidence = 0.5`; assert `A` then `B` execute.
   - Expected failure: `StepExtractor.ExtractLowConfidenceHandlerStep` (`:1429-1455`) takes only the **first** `Then<T>` and terminates.
2. **[GREEN]** Generalize to `ExtractLowConfidenceHandlerChain` returning an ordered `IReadOnlyList<StepModel>`; emit the chain mirroring the failure-handler multi-step loop (`SagaFailureHandlerComponentEmitter.cs:52-72`). New `sealed` `init`-only chain model.
3. **[REFACTOR]** Add the chain model to `InvariantGuardTests`.

**Dependencies:** None · **Parallelizable:** Yes (vs G-3)

### Task 4.2: Rejoin-to-main-flow
1. **[RED]** `Behavioral_OnLowConfidenceRejoin_ResumesMainFlowAfterGatedStep` and `Behavioral_OnLowConfidenceTerminating_CompletesWorkflow`
   - File: same fixture
   - Rejoining handler resumes the main flow at the step **after** the gated step; terminating handler still `MarkCompleted()` (back-compat default).
   - Expected failure (pre-4.2): handler always terminates.
2. **[GREEN]** Add rejoin semantics — emit a continuation to the next main step instead of `MarkCompleted()` when the handler is declared rejoining; default stays terminal.
3. **[REFACTOR]** Golden shape for both routings.

**Dependencies:** Task 4.1 · **Parallelizable:** No

---

## G-5 — CL-5 (#138): EventSourced `StepFailed` / `LowConfidenceRouted` stream events

### Task 5.1: EventSourced behavioral fixture (infra)
1. **[RED]** `EventSourcedFixture_Smoke_AppendsAndReadsStream` — stand up an EventSourced-mode host (Marten event store) and assert a baseline workflow event round-trips.
   - File: `src/Strategos.Generators.Behavioral.Tests/Infrastructure/EventSourcedHostFixture.cs` (new; `WolverineHostFixture` is document-mode only)
   - Expected failure: no EventSourced fixture exists.
2. **[GREEN]** Build the fixture (Marten `AddMarten(...).IntegrateWithWolverine()`, EventSourced persistence mode, shared Testcontainers Postgres via `[ClassDataSource]`/`IAsyncLifetime`; `[NotInParallel]` where the shared host is asserted).
3. **[REFACTOR]** Share the Postgres container with the existing fixtures (one container per run).

**Dependencies:** None (infra) · **Parallelizable:** Yes, but **G-5 emit tasks below depend on G-3 + G-4**

### Task 5.2: `StepFailed` on terminal failure
1. **[RED]** `Behavioral_EventSourcedStepFailure_AppendsStepFailedEvent`
   - File: `src/Strategos.Generators.Behavioral.Tests/EventSourcedAuditEventTests.cs`
   - EventSourced workflow, failing step → a `StepFailed` event (failed step name + exception type) lands in the Marten stream.
   - Expected failure: #137 captures this only as document properties; no stream event.
2. **[GREEN]** In the compensation-trigger handler emit (`SagaFailureHandlerComponentEmitter`, EventSourced branch `:112-120`), append `StepFailed` via the existing `session.Events.Append(WorkflowId, evt)` mechanism (`StateApplicationHelper.cs:56-64`), guarded by `model.IsEventSourced` (`WorkflowModel.cs:84`). Define `sealed record StepFailed(...)`.
3. **[REFACTOR]** Document-mode golden output unchanged; add `StepFailed` to `InvariantGuardTests`.

**Dependencies:** Tasks 5.1, 3.2 · **Parallelizable:** No

### Task 5.3: `LowConfidenceRouted` on gated routing
1. **[RED]** `Behavioral_EventSourcedLowConfidence_AppendsLowConfidenceRoutedEvent`
   - File: same as 5.2
   - EventSourced workflow, low-confidence result → a `LowConfidenceRouted` event (step + score + threshold) lands in the stream.
2. **[GREEN]** In the confidence-gated handler emit, append `LowConfidenceRouted` when `IsEventSourced`. Define `sealed record LowConfidenceRouted(...)`. (Resolves step-resilience design **Open Question #1** audit-event taxonomy.)
3. **[REFACTOR]** Add `LowConfidenceRouted` to `InvariantGuardTests`; reconcile `docs/deferred-features.md`.

**Dependencies:** Tasks 5.1, 4.2 · **Parallelizable:** No

---

## G-6 — CL-6 (#143): Declared↔lowered parity guard — **LAST**

### Task 6.1: Parity allowlist test (forcing function)
1. **[RED]** `StepConfigParity_EveryMember_IsLoweredOrDeferred`
   - File: `src/Strategos.Generators.Tests/Parity/StepConfigParityTests.cs`
   - Reflect over the public surface of `IStepConfiguration<TState>` (`src/Strategos/Abstractions/IStepConfiguration.cs`) and the fields of `StepConfigurationDefinition` (`src/Strategos/Definitions/StepConfigurationDefinition.cs`); assert every member is in exactly one of two explicit sets — **Lowered** (with a referenced behavioral test) or **Deferred** (with a tracking issue #).
   - Expected failure: no allowlist exists; the reflection finds members not yet classified.
2. **[GREEN]** Author the `Lowered` set = the full current surface (`WithRetry`/`WithTimeout`/`Compensate`/`RequireConfidence`/`OnLowConfidence`/`ValidateState`/`WithContext`, all lowered after #137 + G-2..G-5) and the `Deferred` set = fork-path `RequireConfidence`/`OnLowConfidence` + nested `RepeatUntil` (→ #134 / v2.10.0 DR-17). Backfill passes.
   - **Precondition the guard enforces:** each `Lowered` member must reference a *behavioral* (compile-run-saga) test, not a shape test. If reflection surfaces a member whose only proof is shape-level (likely candidates: `ValidateState` from #134, `WithContext` from #137 DR-6), add the missing behavioral test in this step before the parity assertion can pass — this is the guard's whole point.
3. **[REFACTOR]** Add a guard that a new member with no classification fails (negative test via a synthetic surface).

**Dependencies:** G-1..G-5 complete (surface finished) · **Parallelizable:** No

### Task 6.2: `AGWF022` declared-but-inert diagnostic
1. **[RED]** `Generator_StepConfigFieldInertForStepKind_ReportsAgwf022`
   - File: `src/Strategos.Generators.Tests/Diagnostics/DeclaredButInertTests.cs`
   - Force a parsed `StepModel` config field that no emitter consumes for a given step kind; assert `AGWF022` is reported at the call site.
   - Expected failure: no such diagnostic.
2. **[GREEN]** Add `DeclaredButInert = "AGWF022"` to `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` (regen `AgwfCodes.g.cs` via `npx tsp compile` — Node-gated) + a descriptor in `WorkflowDiagnostics.cs`; emit it from the generator when a parsed config field is inert for the step kind.
3. **[REFACTOR]** Analyzer positive/negative tests; document the standing contract in `docs/deferred-features.md` (*"a config member is done only with a behavioral proof or a tracked deferral"*).

**Dependencies:** Task 6.1 · **Parallelizable:** No

---

## G-7 — CL-7 (#132): Batch / safe schema bootstrap

### Task 7.1: Safe `EnsureSchemaAsync<T>(ct)` (ensure-all-descriptors-for-T)
1. **[RED]** `EnsureSchemaAsync_MultiRegisteredType_EnsuresAllDescriptorTables`
   - File: `src/Strategos.Ontology.Npgsql.Tests/Schema/EnsureSchemaBootstrapTests.cs` (DB-gated)
   - Register a type under two descriptors (`Object<Doc>("a")`, `Object<Doc>("b")`); call `EnsureSchemaAsync<Doc>(ct)` (no descriptor name); assert **both** tables exist.
   - Expected failure: `PgVectorObjectSetProvider.cs:145` throws `InvalidOperationException` on multi-registration.
2. **[GREEN]** Add `EnsureSchemaAsync<T>(CancellationToken)` overload that iterates `GetObjectTypeNames<T>()` (`IOntologyQuery.cs:100`) and ensures each descriptor; the existing single-descriptor overload is unchanged. Keys on resolved descriptor name (INV-8); raw Npgsql (INV-2).
3. **[REFACTOR]** Mirror in the in-memory provider (trivial loop) for parity.

**Dependencies:** None · **Parallelizable:** Yes

### Task 7.2: Graph-wide `EnsureAllSchemasAsync(ct)`
1. **[RED]** `EnsureAllSchemasAsync_Graph_CreatesTableForEveryObjectDescriptor`
   - File: same as 7.1
   - A graph with several object descriptors → all vertex tables exist after one call.
   - Expected failure: method does not exist.
2. **[GREEN]** Add `Task EnsureAllSchemasAsync(CancellationToken)` to `src/Strategos.Ontology/ObjectSets/IObjectSetProvider.cs`; implement in `PgVectorObjectSetProvider` (iterate `OntologyGraph.ObjectTypes`, `OntologyGraph.cs:16`) and the in-memory provider.
3. **[REFACTOR]** Any new result type `sealed` (INV-6) — likely none.

**Dependencies:** Task 7.1 · **Parallelizable:** No

### Task 7.3: Validation-corpus + PublicAPI
1. **[RED]** `EnsureAllSchemasAsync_BasileusMultiRegistrationScenario_BootstrapsCleanly`
   - File: same as 7.1
   - Reproduce the Basileus scenario (two `SemanticDocument` registrations) and assert clean bootstrap under both new entry points (design-on-merit; consumer = corpus, not dependency).
2. **[GREEN]** Update `src/Strategos.Ontology/PublicAPI.Unshipped.txt` (+ `.Npgsql` if surfaced there) for the new members; CHANGELOG.
3. **[REFACTOR]** Update the multi-registration exception message at `:145` to point at the batch API.

**Dependencies:** Tasks 7.1, 7.2 · **Parallelizable:** No

---

## Task summary (delegation units)

| ID | Group | Issue | Title | Depends on | Parallel |
|---|---|---|---|---|---|
| 1.1 | G-1 | #142 | Escape validation-error literal | — | ✅ |
| 2.1 | G-2 | #141 | StartWith/Finally overloads + IR | — | ✅ |
| 2.2 | G-2 | #141 | PublicAPI baseline | 2.1 | — |
| 2.3 | G-2 | #141 | Behavioral proof | 2.1 | — |
| 3.1 | G-3 | #140 | OnFailure worker handler | — | ✅ |
| 3.2 | G-3 | #140 | Compensate↔OnFailure interop | 3.1 | — |
| 4.1 | G-4 | #139 | Multi-step OnLowConfidence | — | ✅ |
| 4.2 | G-4 | #139 | Rejoin-to-main-flow | 4.1 | — |
| 5.1 | G-5 | #138 | EventSourced fixture | — | ✅ |
| 5.2 | G-5 | #138 | StepFailed event | 5.1, 3.2 | — |
| 5.3 | G-5 | #138 | LowConfidenceRouted event | 5.1, 4.2 | — |
| 6.1 | G-6 | #143 | Parity allowlist test | G-1..G-5 | — |
| 6.2 | G-6 | #143 | AGWF022 inert diagnostic | 6.1 | — |
| 7.1 | G-7 | #132 | Safe EnsureSchemaAsync<T>(ct) | — | ✅ |
| 7.2 | G-7 | #132 | EnsureAllSchemasAsync | 7.1 | — |
| 7.3 | G-7 | #132 | Corpus + PublicAPI | 7.1, 7.2 | — |

**PR shape (for /delegate):** three natural PRs — **(A)** generator surface G-1/G-2/G-6 (escape + StartWith/Finally + parity guard), **(B)** saga-emission G-3/G-4/G-5 (OnFailure/Compensate + OnLowConfidence + EventSourced events), **(C)** ontology G-7 standalone. G-6 must land after G-1..G-5, so PR-A's parity-guard commit sequences after PR-B merges (or G-6 rides PR-B's tail). Confirm at delegation.

## Risks / call-outs

- **AGWF022 regen needs Node** (`npx tsp compile` on `AgwfCatalog.tsp`) — the implementer task for 6.2 runs in a Node-provisioned context (`project_contracts_tests_need_node_job`).
- **G-3/G-5 file overlap:** `SagaFailureHandlerComponentEmitter` is touched by 3.1/3.2 and 5.2 — sequence them (G-3 before G-5), don't run as parallel worktrees.
- **EventSourced durability:** timeout/scheduled-retry correctness needs the Marten outbox; the EventSourced fixture (5.1) enables `IntegrateWithWolverine()` (per the step-resilience design's [S2][S3]).
- **Open questions to settle in implementation:** OnFailure↔Compensate ordering (3.2), rejoin semantics (4.2), audit-event names/shapes (5.2/5.3) — design Open Questions 1–3.
