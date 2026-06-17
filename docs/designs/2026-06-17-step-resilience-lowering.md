# Step Resilience Lowering — retry / timeout / compensation / confidence

**Date:** 2026-06-17 · **Epic:** [#135](https://github.com/lvlup-sw/strategos/issues/135) · **Type:** bug (non-functional shipped feature) · **Scope:** core engine (`Strategos.Generators`), not edge-layer
**Workflow:** `step-resilience-lowering` · **Invariant catalog:** `/strategos-design-invariants` (INV-1…INV-8) · **Research:** [`../research/2026-06-16-dsl-step-resilience-lowering-gap.md`](../research/2026-06-16-dsl-step-resilience-lowering-gap.md)
**Wolverine/Marten claims are grounded in primary docs — every emit mechanism is cited inline (see [Sources](#sources)).**

## Problem Statement

The workflow DSL exposes and documents step resilience — `WithRetry`, `WithTimeout`, `Compensate<T>`, `RequireConfidence`, `OnLowConfidence` — but **none of it lowers into the emitted Wolverine+Marten saga, for any step kind.** The builder captures the config and the projection serialises it into the declarative export wire contract (`WorkflowDefinitionProjection.cs:266-307` maps `ConfidenceThreshold`/`OnLowConfidence`/`Compensation`/`Retry`/`Timeout`), but it is **dropped before code generation**: `StepModel` (`Generators/Models/StepModel.cs:22-29`) carries only validation + context fields, and `StepExtractor.WalkInvocationChainForStepModelsInternal` never parses the resilience calls. So `.WithRetry(2)` compiles, validates, exports, is documented as functional — and the running saga has no retry. The worker handler catches and re-throws "to let Wolverine handle retry/dead-letter" (`WorkerHandlerEmitter.cs:249-250`) against a policy that **does not exist** anywhere in `src/`; the compensation `Trigger{Name}FailureHandlerCommand` is generated (`SagaFailureHandlerComponentEmitter.cs:80`) but **never published**; confidence is a span tag (`WorkerHandlerEmitter.cs:218`), never a gate. Every existing test is shape-only (asserts generated *source text*), so a `.WithRetry(2)` that never retries passes the suite.

Two scope corrections from the issue thread are folded in: **`WithContext`** (`ContextAssemblerEmitter`) is *also* declared-but-not-lowered — it exists, is ontology-wired (`IObjectSetProvider` / `ExecuteSimilarityAsync`), but is never invoked by `WorkflowIncrementalGenerator`; and fork-path `ValidateState` lowering already landed via #134.

## Dependency spine

```
                                 ┌─► DR-2 retry  ─────────────┐
DR-1  Resilience IR + parse  ────┼─► DR-4 timeout ────────────┤
(StepModel fields + StepExtractor├─► DR-5 confidence gate ────┼─► DR-9  Behavioral harness
 parse branches; INV-8 SymbolKey)└─► DR-3 compensation / OnFailure   (full Wolverine+Marten host —
        │                              (closes the orphan trigger;     ACCEPTANCE UNLOCK for DR-2..DR-6)
        │                               needs DR-2 retry-exhaustion)
        ├─► DR-8  INV-5 resilience diagnostics (rides DR-1's new config surface)
        └─► DR-7  Expressibility (Then<T>(cfg) on branch + failure contexts; fork landed #134)

DR-6  WithContext wire-in (independent track — ontology emitter already exists, just unwired)
```

**Keystone:** DR-1 is foundational — every emitter reads the IR. **Acceptance unlock:** DR-9 — retry/timeout are *Wolverine runtime behaviours*; they cannot be proven by source-text assertions, so the behavioral harness gates the acceptance of every emit DR.

## Invariant constraints (from `/strategos-design-invariants`, verdict: **conditional**)

| Invariant | Constraint this design adopts |
|---|---|
| **INV-1** (HIGH) | All resilience lowers **through `Strategos.Generators` onto Wolverine primitives** — never a parallel runtime, custom retry loop service, or hand-rolled saga store. Retry/compensation emit as a per-handler `Configure(HandlerChain)` on the **already-emitted** `{Step}Handler`; timeout emits as a saga `TimeoutMessage`; confidence as a saga-routing branch. No new `: Saga` class outside `SagaEmitter`; no Wolverine/Marten `PackageReference` added to a runtime project (only the **test** project, DR-9). |
| **INV-7** (HIGH) | Retry **re-delivers the same envelope** → the worker re-runs `_step.ExecuteAsync(command.State,…)` against the **same immutable state**; no mutation-across-attempts. Compensation/timeout handlers return `state with {…}`, never write through input. New saga audit fields (DR-3) are `init`-only. |
| **INV-5** (HIGH) | New validation takes the **next free monotonic `AGWF` id verified against the live ceiling `AGWF016`** (`Strategos.Contracts/Generated/AgwfCodes.g.cs`; the INV-5 reference's "AGWF001..010" is **stale**) → new ids start at **AGWF017**. No id reused/renumbered. The "declared-but-inert" silent no-op is converted into a real signal (DR-8). |
| **INV-6** (HIGH) | New `StepModel` fields, parser helpers, models (`RetryModel`, `TimeoutModel`, `CompensationModel`, `ConfidenceModel`), and any new emitter types are `sealed` records with `init`-only members; extend the sealed-guard test. |
| **INV-8** (MEDIUM) | `Compensate<TCompensation>()` threads the compensation step's **`SymbolKey`/type symbol** into the IR (consistent with how step types are already referenced), never a stringly-typed name. The projection's `CompensationStepType.Name` moniker (`WorkflowDefinitionProjection.cs:295`) is export-only and is **not** the generator's identity source. |

INV-2/3/4 do not bind (no ontology-analyzer change beyond DR-6's existing emitter; no MCP-spec change; no new DSL nomenclature).

## Approaches considered

The load-bearing fork is **where resilience lowering lives** — Wolverine's per-handler policy engine vs. generated saga control flow. (Behavioral-harness depth and the `WithContext` wire-vs-delete were settled separately: full integration host; wire in.)

### Option 1: Wolverine-native, maximal (simple but limited)

**Approach:** Lower retry/compensation as the thinnest per-handler `Configure(HandlerChain)` glue and delegate all control flow to Wolverine's policy engine; timeout via `TimeoutMessage`; confidence via saga branch.

**Pros:**
- Least generated code; rides Wolverine's tested retry/backoff/jitter
- INV-1 textbook — Wolverine primitives, zero parallel mechanism

**Cons:**
- Retry attempt count/timing live in Wolverine envelope internals, **not** the Marten audit log — erodes Strategos's time-travel/audit value-prop
- Behavior observable only by running a real host

**Best when:** the team trusts Wolverine's policy engine fully and does not need per-attempt history in the event log.

### Option 2: Saga-orchestrated (flexible but complex)

**Approach:** Generator emits explicit retry counters in saga state, re-dispatch loops, saga-scheduled timeout commands (the `ScheduleAsync` approval precedent), and an explicit failure-trigger publish. Wolverine is "just transport."

**Pros:**
- Every attempt/timeout/compensation is a Marten event — first-class audit + time-travel; deterministic and inspectable
- Testable without Wolverine's policy engine

**Cons:**
- Reinvents retry/backoff Wolverine gives free; largest SG blast radius
- Trips INV-1 **MEDIUM** (parallel abstraction fragmenting the runtime story)

**Best when:** audit fidelity dominates and the runtime must be fully replayable without Wolverine-internal state.

### Option 3: Right-tool-per-capability — hybrid (SELECTED)

**Approach:** Each capability lowers onto the cheapest *correct* Wolverine primitive: retry+compensation via per-handler `Configure`/`CompensatingAction`, **plus** a `StepFailed`/`RetryExhausted` audit event so the log still records terminal failure; timeout via saga `TimeoutMessage`; confidence via saga branch.

**Pros:**
- Cheapest correct primitive per capability; INV-1-clean
- Preserves the audit trail where it matters (Option 1's gap) without reinventing Wolverine retry (Option 2's cost)
- Smallest correct surface

**Cons:**
- Two lowering styles (handler-policy + saga-message) to learn
- The audit-event emission from `CompensatingAction` is a small extra bridge

**Best when:** you want INV-1 conformance *and* audit fidelity — the Strategos default. **Recommended and selected.**

## Chosen Approach

Settled in Phase 2: **Approach C / Option 3 — right-tool-per-capability (hybrid)**, full-epic scope, full integration-host behavioral harness, and **wire** `WithContext` in. Each capability lowers onto the cheapest *correct* Wolverine primitive the primary docs point to, while preserving Strategos's audit-trail value-prop where attempt/failure history matters.

- **Retry + compensation** → a generated static `Configure(HandlerChain)` companion on the `{Step}Handler`. Wolverine scopes error policy **per handler** via this method or method/class attributes — no global `WolverineOptions.Policies` needed (refuting the first archaeology pass). [E1] After retries exhaust, `.Then.CompensatingAction<TWorkerCmd>((cmd,ex,bus)=>bus.PublishAsync(new Trigger{Name}FailureHandlerCommand(…)))` **publishes** the existing-but-orphaned trigger command — closing the dead path with a real, Wolverine-native publish site. [E2]
- **Timeout** → the saga `TimeoutMessage` base class: the saga cascades `record {Step}Timeout(Guid WorkflowId) : TimeoutMessage(t)` on step-start and handles it later as a **deadline race** (did the `CompletedEvent` arrive before the timeout fired?). This mirrors the repo's existing approval-timeout `ScheduleAsync` precedent. [S1]
- **Confidence** → a saga-routing branch comparing `result.Confidence` to the declared threshold and dispatching the existing `OnLowConfidence` `IBranchBuilder`. Pure saga logic, no Wolverine infra.
- **Audit bridge:** `CompensatingAction` and the confidence branch emit `StepRetryExhausted` / `StepFailed` / `LowConfidenceRouted` events so the Marten log records terminal failure — the one place Approach A would have hidden history inside Wolverine's envelope.

## Requirements

### DR-1: Resilience IR + parse plumbing

Extend the generator IR and parser so resilience config survives to emit-time. Add to `StepModel` (`Generators/Models/StepModel.cs`) `sealed`/`init` fields: `RetryModel? Retry`, `TimeoutModel? Timeout`, `CompensationModel? Compensation`, `ConfidenceModel? Confidence`. Add parse branches + argument parsers to `StepExtractor.WalkInvocationChainForStepModelsInternal` (`Generators/Helpers/StepExtractor.cs:760-825`) for `.WithRetry`, `.WithTimeout`, `.Compensate`, `.RequireConfidence`, `.OnLowConfidence`. `Compensate<TCompensation>()` resolves the type-argument **symbol** (INV-8 `SymbolKey`), not a name string. No projection work — the wire contract already models all of it.

**Acceptance criteria:**
- Given a workflow step declaring `.WithRetry(2, TimeSpan.FromSeconds(5)).WithTimeout(t).Compensate<Rollback>().RequireConfidence(0.85)`, When the generator runs, Then the resulting `StepModel` carries a populated `Retry`/`Timeout`/`Compensation`/`Confidence`, and `Compensation` references `Rollback` by type symbol.
- Given the same in a `LoopBuilder` step, Then the config is parsed identically (the matrix gap closes for both `WorkflowBuilder` and `LoopBuilder`).
- New IR records are `sealed` with `init`-only members (INV-6 sealed-guard test extended).

### DR-2: Retry lowering (per-handler Wolverine policy)

Emit a static `public static void Configure(HandlerChain chain)` on the generated `{Step}Handler` when `Retry` is present. Map `WithRetry(n)` → `chain.OnAnyException().RetryTimes(n)`; `WithRetry(n, delay)` and the richer `RetryConfiguration` (`InitialDelay`/`BackoffMultiplier`/`MaxDelay`/`UseJitter`, already in the wire model) → `RetryWithCooldown(delays…)` with Wolverine jitter (`.WithExponentialJitter()` etc.) when `UseJitter`. [E1] The worker `catch { throw; }` stays — re-throw is what feeds Wolverine's policy.

**Acceptance criteria:**
- Given a step with `.WithRetry(2)` and a step that throws a transient exception on attempts 1–2 and succeeds on attempt 3, When the compiled saga runs in a real Wolverine host (DR-9), Then the step's `ExecuteAsync` is invoked exactly 3 times and the saga completes successfully.
- Given `.WithRetry(3, 5s)`, Then the emitted `Configure` contains `RetryWithCooldown` with three cooldown entries (not `RetryTimes`).
- Given no `.WithRetry`, Then **no** `Configure` method is emitted for that handler (no behavior change for non-resilient steps).

### DR-3: Compensation / OnFailure lowering (close the dead path)

When `Compensation` is present, append `.Then.CompensatingAction<{WorkerCommand}>((cmd, ex, bus) => bus.PublishAsync(new Trigger{Name}FailureHandlerCommand(cmd.WorkflowId, "{Step}", ex.Message, ex.GetType().Name, ex.StackTrace)), InvokeResult.Stop)` to the handler's `Configure` chain — supplying the **runtime publish site** the orphaned `SagaFailureHandlerComponentEmitter` output has always lacked. [E2] The compensation step itself runs as the failure-handler step chain that already exists. Emit a `StepRetryExhausted`/`StepFailed` event into the saga's Marten stream for audit.

**Acceptance criteria:**
- Given a step with `.WithRetry(2).Compensate<Rollback>()` and a step that always throws, When the saga runs (DR-9), Then after 2 attempts the `Trigger…FailureHandlerCommand` is published, `Rollback.ExecuteAsync` runs exactly once, and the saga transitions to `Failed`.
- Given `.Compensate<Rollback>()` **without** `.WithRetry`, Then compensation triggers on the first failure (retry-count defaults to 1 attempt).
- A `StepFailed` event is appended to the Marten event stream recording the failed step name + exception type.

### DR-4: Timeout lowering (saga TimeoutMessage deadline race)

When `Timeout` is present, emit `record {Step}Timeout(Guid WorkflowId) : TimeoutMessage(<t>)` and cascade it from the step-start handler; emit a saga `Handle({Step}Timeout, …)` that, **if the step has not already completed** (phase guard), routes to the failure/compensation path; otherwise it is a no-op (the `CompletedEvent` won the race). [S1] Durable cross-restart delivery requires the Marten outbox (`IntegrateWithWolverine()` + durable inbox/outbox); the design documents this as a deployment prerequisite. [S2][S3]

**Acceptance criteria:**
- Given a step with `.WithTimeout(50ms)` and a step that sleeps 500ms, When the saga runs (DR-9), Then the `{Step}Timeout` handler fires before completion and the saga routes to the timeout/failure path.
- Given a step with `.WithTimeout(5s)` that completes in 10ms, Then the timeout handler is a no-op (idempotent phase guard; saga not double-failed).
- **Documented constraint:** the timeout is a saga-level *deadline*, not hard cancellation of the in-flight handler; the handler's `CancellationToken` is passed for cooperative cancellation but enforcement is the deadline race.

### DR-5: Confidence gate lowering

When `Confidence` is present, after the step executes compare `result.Confidence` to `ConfidenceThreshold`; if below, route to the `OnLowConfidence` `IBranchBuilder` (already a lowering-capable construct) instead of the normal completion path; emit a `LowConfidenceRouted` audit event. Confidence ≥ threshold proceeds normally. Replaces the telemetry-only `activity?.SetTag("step.confidence", …)`.

**Acceptance criteria:**
- Given `.RequireConfidence(0.85).OnLowConfidence(alt => alt.Then<HumanReview>())` and a step returning `Confidence = 0.5`, When the saga runs, Then `HumanReview` executes and the primary continuation does not.
- Given the same step returning `Confidence = 0.9`, Then the normal next step runs and the low-confidence branch does not.
- `RequireConfidence` without `OnLowConfidence` fails the workflow on low confidence (no silent continue) and is flagged by DR-8.

### DR-6: WithContext wire-in (ontology-backed)

Invoke the existing `ContextAssemblerEmitter` (+ `ContextModelExtractor`) from `WorkflowIncrementalGenerator` so a `.WithContext(…)` step emits its `{Step}ContextAssembler` (an `IObjectSetProvider` consumer whose `FromRetrieval<TCollection>` lowers to `ExecuteSimilarityAsync`). This is the ontology-aligned path (no `Strategos.Rag` reference), advancing that library's retirement.

**Acceptance criteria:**
- Given a step with `.WithContext(ctx => ctx.FromState(…).FromRetrieval<Lib>(…).FromLiteral(…))`, When the generator runs, Then `WorkflowIncrementalGenerator` emits the `{Step}ContextAssembler` and wires it into the worker handler's execution.
- Given the saga runs (DR-9) with a stub `IObjectSetProvider`, Then the step receives assembled context (state + retrieval + literal) and the provider's `ExecuteSimilarityAsync` is invoked with the declared `TopK`/`MinRelevance`.
- No `Strategos.Rag` reference is introduced anywhere in the context path.

### DR-7: Expressibility on branch + failure-handler contexts

Add the `Then<TStep>(Action<IStepConfiguration<TState>>)` overload to the contexts still missing it — branch and failure-handler builders (fork landed via #134). Resilience config declared inside those contexts must reach DR-1's IR (e.g. `StepExtractor.ParseForkPathStepModels`-style parsing for branch/failure paths).

**Acceptance criteria:**
- Given a `.OnLowConfidence(alt => alt.Then<X>(s => s.WithRetry(2)))` declaration, When the generator runs, Then the branch step's `StepModel` carries the retry config and DR-2 emits its `Configure`.
- Public-API additions update the `PublicAPI.Unshipped.txt` baseline (RS0016/RS0017 — break by design, do not suppress).

### DR-8: INV-5 resilience diagnostics

Assign new `AGWF` ids (next-free from live ceiling **AGWF016** → start **AGWF017**), reported at the earliest tier (analyzer over builder-throw): invalid retry count (`< 1`), non-positive timeout, `Compensate<T>` where `T` is not a registered `IWorkflowStep<TState>`, `RequireConfidence` outside `[0,1]`, and `RequireConfidence` without `OnLowConfidence`. The pre-existing builder-runtime throws (e.g. `WithRetry` `ArgumentOutOfRangeException`) are mirrored as stable ids so consumers can suppress.

**Acceptance criteria:**
- Given `.Compensate<NotAStep>()`, When compiling, Then `AGWF0NN` (next-free) is reported at the call site.
- Given `.RequireConfidence(1.5)`, Then a diagnostic fires at analyzer tier.
- Every new id is the next unused monotonic value verified against `AgwfCodes.g.cs`; none reused/renumbered; ids added to `AgwfCodes.g.cs` + `WorkflowDiagnostics.cs`.

### DR-9: Behavioral-test harness (acceptance unlock)

New integration test project (`Strategos.Generators.Behavioral.Tests`) that compiles fixture workflows (SG runs at build), stands up a **real Wolverine `IHost` + Marten + Testcontainers Postgres** (`UseWolverine`, `AddMarten(...).IntegrateWithWolverine()`), sends the start command, and asserts runtime behavior via instrumented steps (attempt counters, timers). Adds `Wolverine`, `Marten`, `Npgsql`, `Testcontainers.PostgreSql` to `Directory.Packages.props` — currently **none** are referenced. Shared Postgres container via TUnit `[ClassDataSource]`/`IAsyncLifetime`; `[NotInParallel]` where a shared host is asserted (per `project_tunit_static_state_parallelism`).

**Acceptance criteria:**
- The harness can compile an emitted saga, run it against a real host, and observe: a step retried N times, a `TimeoutMessage` delivered, a compensation step executed, a low-confidence branch taken.
- A regression that reverts any DR-2..DR-6 lowering to a no-op makes the corresponding behavioral test **fail** (the property the shape-only suite lacks).
- Container lifecycle is shared across the suite (one Postgres per run, not per test).

### DR-10: Error handling, composition, and edge cases

Resilience features compose on a single step and across the saga; specify the interactions.

**Acceptance criteria:**
- **Ordering / composition:** Given a step with `.WithRetry(2).WithTimeout(t).Compensate<R>().RequireConfidence(c)`, Then retries are attempted first; the timeout deadline spans the whole step (including retries); compensation runs only after retries exhaust; a *successful* attempt then evaluated as low-confidence routes via `OnLowConfidence` (not compensation). The doc states this precedence explicitly.
- **INV-7 under retry:** Given a non-idempotent-looking step, When retried, Then each attempt receives the **same immutable `command.State`** (re-delivery), proven by asserting the input state is identical across attempts.
- **Timeout vs. retry race:** Given timeout fires mid-retry, Then the saga fails-by-timeout idempotently and a late `CompletedEvent` does not resurrect the saga (phase guard).
- **Durability:** Given the host restarts with a scheduled timeout pending and the Marten outbox enabled, Then the timeout still delivers (documented prerequisite [S2][S3]); without the outbox, the doc warns timeout/scheduled-retry are in-memory only.
- **No-config baseline:** Given a step with **no** resilience config, Then emitted saga/handler output is byte-for-byte unchanged from today (no `Configure`, no timeout message) — guarded by an existing golden/shape test.

## Technical Design

**Where each capability emits (all inside `Strategos.Generators`, INV-1):**

| Capability | Emit site | Wolverine primitive |
|---|---|---|
| Retry | `{Step}Handler.Configure(HandlerChain)` | `chain.OnAnyException().RetryTimes(n)` / `.RetryWithCooldown(delays).WithExponentialJitter()` [E1] |
| Compensation | same `Configure`, `.Then` | `.CompensatingAction<{WorkerCmd}>((cmd,ex,bus)=>bus.PublishAsync(new Trigger…FailureHandlerCommand(…)), InvokeResult.Stop)` [E2] |
| Timeout | `SagaEmitter` step-start + new handler | `record {Step}Timeout : TimeoutMessage(t)` cascaded; `Handle({Step}Timeout,…)` deadline race [S1] |
| Confidence | worker/saga completion routing | branch on `result.Confidence < threshold` → `OnLowConfidence` dispatch |
| Context (DR-6) | `WorkflowIncrementalGenerator` | invoke existing `ContextAssemblerEmitter` → `IObjectSetProvider.ExecuteSimilarityAsync` |

`Envelope.Attempts` is available to handlers by taking `Envelope` as a parameter, should a capability need the live attempt count. [E3] Retry redelivery is the same envelope → INV-7-clean by construction.

## Integration Points

- `Generators/Models/StepModel.cs` — new IR fields (DR-1).
- `Generators/Helpers/StepExtractor.cs:760-825` + `ParseForkPathStepModels` — parse branches (DR-1, DR-7).
- `Generators/Emitters/WorkerHandlerEmitter.cs` — emit `Configure` companion; replace confidence-tag with gate (DR-2/3/5).
- `Generators/Emitters/Saga/*`, `SagaEmitter.cs` — timeout message + handler; failure-handler trigger now actually published (DR-3/4).
- `Generators/WorkflowIncrementalGenerator.cs` — invoke `ContextAssemblerEmitter` (DR-6).
- `Diagnostics/WorkflowDiagnostics.cs` + `Contracts/Generated/AgwfCodes.g.cs` — AGWF017+ (DR-8).
- `Directory.Packages.props` + new `Strategos.Generators.Behavioral.Tests` (DR-9).
- Docs to reconcile once lowering ships: `docs/theory/agentic-workflow-theory.md`, `docs/deferred-features.md`, `docs/workflow-library-roadmap-v2.md`, `Abstractions/IStepConfiguration.cs` XML docs.

## Testing Strategy

Three tiers. **(1)** Generator unit tests (existing style) assert the new IR is parsed and the `Configure`/timeout source is emitted. **(2)** DR-9 behavioral suite proves the runtime properties (retry count, timeout fire, compensation run, confidence route) against a real Wolverine+Marten host — the mandatory close of the "shape-only tests can't catch this" failure. **(3)** A golden no-config test pins zero behavior change for steps without resilience. Per `feedback_tunit_test_invocation`, behavioral tests are invoked via `-- --treenode-filter`, not `--filter`.

## Open Questions

1. **Audit-event taxonomy** — exact event names/shapes for `StepRetryExhausted` / `StepFailed` / `LowConfidenceRouted` and whether they reuse existing failure events. (Resolve in /plan.)
2. **Timeout granularity** — is a per-step deadline sufficient, or is a per-attempt timeout also wanted? Design assumes per-step (spans retries) per DR-10; confirm.
3. **Durable-outbox default** — should `IntegrateWithWolverine()` + durable inbox/outbox be the emitted/recommended default for timeout correctness, or left to the consumer with a diagnostic/doc warning? Leaning: document prerequisite + sample, do not force.

## Sources

Authoritative Wolverine/Marten docs (pulled this session; do not infer beyond these):
- **[E1]** Per-handler error policy via static `Configure(HandlerChain)` and method/class attributes (`[MaximumAttempts]`, `[RetryNow]`, `[ScheduleRetry]`), `OnException`/`OnAnyException` → `RetryTimes`/`RetryWithCooldown`/`ScheduleRetry`/jitter — wolverinefx.net/guide/handlers/error-handling.html ; github.com/jasperfx/wolverine `docs/guide/handlers/error-handling.md`.
- **[E2]** `.Then.CompensatingAction<T>((msg,ex,bus)=>bus.PublishAsync(…), InvokeResult.Stop)` / `CustomAction` / `UserDefinedContinuation` — publishes a message after retries exhaust — same source as [E1].
- **[E3]** `Envelope.Attempts` via `Envelope` handler parameter — github.com/jasperfx/wolverine `docs/guide/handlers/index.md`.
- **[S1]** Saga `TimeoutMessage` base class + `MarkCompleted()` deadline pattern — wolverinefx.net/guide/durability/sagas.html.
- **[S2]** Scheduled messages (`ScheduleAsync`, `DelayedFor`, `ScheduledAt`) are in-memory by default; durability needs transactional outbox — wolverinefx.net/guide/messaging/message-bus.html.
- **[S3]** `AddMarten(…).IntegrateWithWolverine()` configures Marten as saga storage + durable inbox/outbox — wolverinefx.net/guide/durability/marten.html.
