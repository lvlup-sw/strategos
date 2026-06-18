# Investigation Spike: Workflow Step Resilience Config Is Declared But Never Lowered Into the Saga

- **Date:** 2026-06-16
- **Workflow:** `dsl-resilience-lowering-spike` (discovery; deliverable = document, no code)
- **Trigger:** during DR-17 (#134) implementation, the implementer found that fork-path `Then(configure)` could not lower `WithRetry`/`WithTimeout`/`Compensate` because nothing lowers them for *any* step kind — only the declarative export carries them.
- **Method:** two independent read-only code-archaeology passes over `src/Strategos` + `src/Strategos.Generators` + `src/Strategos.Contracts*` + `**/*Tests*` — one mapping the capability×context lowering matrix, one checking runtime/export/test reality. Both converged.
- **Status:** **CONFIRMED, very high confidence.** A shipped, documented DSL feature (step-level resilience) is non-functional. Findings below scope the follow-up epic.

---

## 1. BLUF

The Strategos workflow DSL exposes `IStepConfiguration` capabilities — `WithRetry`, `WithTimeout`, `Compensate<T>`, `RequireConfidence`, `OnLowConfidence` — and documents them as runtime behavior. **None of them lower into the emitted Wolverine+Marten saga, for any step kind.** They are parsed only far enough to reach the declarative export wire contract, then dropped before code generation. The `OnFailure`/compensation path is inert for the same root reason: the saga generates a failure-handler trigger command that is **never published at runtime**. The entire test suite is shape-only, so it passes while the feature does nothing. The XML docs and design docs advertise these as working.

**Only `ValidateState` (validation guard) lowers to the saga.** *(Correction, verified 2026-06-16 during the #134 fold-in: `WithContext`'s emitter `ContextAssemblerEmitter` exists with unit tests but is **never invoked** by `WorkflowIncrementalGenerator` — only test call sites — so RAG context does **not** lower in production either; it joins the declared-not-enforced set. The two tracer passes saw the emitter exists but did not verify it is wired into the generation pipeline.)* Approval timeouts (`AwaitApproval(...).OnTimeout(...)`) lower too, but that is a *separate* feature, not step `WithTimeout`.

This corroborates and extends the DR-17 finding: the gap is single-rooted, total, and masked.

---

## 2. Scope of the gap — the capability × context matrix

The generator re-parses the builder lambda from syntax and recognizes only a fixed method set, so it never sees a per-step config object. The lowering verdict is therefore **uniform across all contexts**; only *expressibility* differs.

| Capability | WorkflowBuilder | LoopBuilder | ForkPath | Branch | Approval | Failure |
|---|---|---|---|---|---|---|
| `WithRetry` / `WithTimeout` / `Compensate` / `RequireConfidence` / `OnLowConfidence` | EXPORT-ONLY | EXPORT-ONLY | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE |
| `ValidateState` | **LOWERS** | **LOWERS** | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE |
| `WithContext` | NOT LOWERED † | NOT LOWERED † | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE | NOT-EXPRESSIBLE |

† **Correction (2026-06-16):** `ContextAssemblerEmitter` is never invoked by `WorkflowIncrementalGenerator` (test-only call sites) — `WithContext` does **not** lower in production. Only `ValidateState` does.

- Only top-level + loop accept a step-config lambda: `WorkflowBuilder.Then<TStep>(Action<IStepConfiguration<TState>>)` (`WorkflowBuilder.cs:133`, attaches at `:149`); `LoopBuilder.Then<TStep>(...)` (`LoopBuilder.cs:85`, `:96`). DR-17 (#134) just added the same overload to `IForkPathBuilder` (still export-only / inert for resilience, since lowering doesn't exist).
- The other contexts have no config-lambda overload: `IForkPathBuilder.cs:36,60` (pre-#134), `IBranchBuilder.cs:38,60`, `IFailureBuilder.cs:36,48`; `IApprovalBuilder` has no step `Then` at all. So even `ValidateState`/`WithContext` are not-expressible there.

---

## 3. The single-rooted mechanism — where the config dies

The drop is **by omission of fields in the generator IR**:

1. **`StepModel` (`StepModel.cs:22-29`)** carries only `ValidationPredicate`, `ValidationErrorMessage`, `Context`. There is no retry/timeout/compensation/confidence field — the emitter has nowhere to read them from. **Root cause.**
2. **`StepExtractor.WalkInvocationChainForStepModelsInternal` (`StepExtractor.cs:760-825`)** branches only on `RepeatUntil`/`Fork`/`Branch`/`Join`/`Then`/`ValidateState`. `.WithRetry`/`.WithTimeout`/`.Compensate`/`.RequireConfidence`/`.OnLowConfidence` fall through the receiver-recursion (`:810-824`) and are silently skipped.
3. **No emitter can emit them.** Saga emitters read only `StepModel.HasValidation`/`ValidationPredicate` (`StepStartHandlerEmitter.cs:78,111`) and `StepModel.Context` (`ContextAssemblerEmitter.cs:82,117`).

The config is **EXPORT-ONLY** (not lost everywhere) because the runtime builder fully captures it in `StepConfigurationDefinition.cs:30-74` and `WorkflowDefinitionProjection.ProjectConfiguration` (`WorkflowDefinitionProjection.cs:266-282`, `ProjectRetry:300`, `ProjectCompensation:292`, `ProjectLowConfidence:284`) serializes all of it into the wire contract. It survives to the export and dies before the saga.

---

## 4. Runtime reality — inert beyond export

- **No retry/timeout wrapping in the emitted saga.** `StepStartHandlerEmitter.cs:78-86` wraps only a validation guard; otherwise a bare handler (`EmitStandardHandler:144`) transitions phase + dispatches the worker. No retry loop, no timeout.
- **The worker re-throws to a handler that doesn't exist.** `WorkerHandlerEmitter.cs:249-250` catches and re-throws with the comment *"let Wolverine handle retry/dead-letter"* — but a solution-wide grep for `OnException`/`RetryWithCooldown`/`ScheduleRetry`/`MaximumAttempts`/`.Policies`/`Polly`/`WolverineOptions` across `Strategos.Infrastructure`, `Strategos.Agents`, `Strategos/Configuration` returns **zero** retry config. No per-step and no global message retry, anywhere.
- **Compensation / `OnFailure` is also inert.** The saga generates `Trigger{Name}FailureHandlerCommand` + handler (`SagaFailureHandlerComponentEmitter.cs:75-102`), but a grep for `new Trigger…`/`PublishAsync`/`SendAsync`/`InvokeAsync` referencing it (outside the generator that defines it) finds nothing — **it is never published**. The worker catch only re-throws.
- **Confidence is telemetry, not a gate.** `WorkerHandlerEmitter.cs:218,231` set `step.confidence` as a span tag + write it to the completed event; nothing compares against `ConfidenceThreshold` or dispatches the `OnLowConfidence` path.

---

## 5. What DOES lower (bounds the gap exactly)

- **`ValidateState`** → real guard-then-dispatch: `StepStartHandlerEmitter.EmitValidationHandler (:88-129)` emits `if(!(<pred>)){ Phase=…ValidationFailed; yield return …; yield break; }`, backed by `PhaseEnumEmitter.cs:96-101` + `EventsEmitter.cs:169-173`.
- ~~**`WithContext`**~~ → **correction:** `ContextAssemblerEmitter (:82-157)` exists but is **never invoked** by `WorkflowIncrementalGenerator` (only test call sites). RAG context does **not** lower in production — move it to the declared-not-enforced set, not here.
- **Approval timeout** (`AwaitApproval(...).OnTimeout(...)`) → `CommandsEmitter.EmitTimeoutCommand (:374)` + `SagaApprovalHandlersEmitter.EmitTimeoutHandler (:155)`; sets `Deadline = …Add(evt.Timeout)`. **Approval-specific — not step `WithTimeout`.**

---

## 6. Test-coverage gap — fully masked

No test asserts retry/timeout/compensation **behavior**. The closest, all shape-only:
- `Definitions/RetryConfigurationTests.cs`, `CompensationConfigurationTests.cs` — value-object construction.
- `Builders/StepConfigurationBuilderTests.cs:87-124` — assert config lands on `Configuration.Retry/.Compensation` (definition presence).
- `Contracts.Tests/Workflow/IrCompletenessTests.cs` — projection/wire carries config (export shape).
- `Generators.Tests/OnFailureIntegrationTests.cs` — closest to behavioral, but asserts only generated **source text** `Contains("Trigger…FailureHandlerCommand")`; never compiles+runs the saga, never asserts anything publishes the trigger.

The suite is **structurally incapable** of catching a `.WithRetry(2)` that never retries — it only ever checks that config is *present in the definition/export*.

---

## 7. Blast radius

- **Advertised as shipped:** `IStepConfiguration.cs:21-36` lists "Compensation steps for rollback / Retry policies for transient failures / Execution timeouts," with an example `.Compensate<RollbackAssessment>().WithRetry(3, …).WithTimeout(…)`. Method docs read as runtime guarantees (`:64,72,93`). `StepConfigurationDefinition.cs:18-20`, `CompensationConfiguration.cs:9-18` describe rollback/retry/timeout as behavior.
- **Consumer impact:** any workflow author writing `.WithRetry(n)`/`.WithTimeout(t)`/`.Compensate<T>()` gets a silent no-op — it compiles, validates, exports, and is documented as functional, but the running saga has no retry, no timeout, no compensation. This is precisely the reliability expression that downstream consumers (e.g. lvlup-sw/basileus#277's per-analyst resilience) assumed worked.
- **Docs to reconcile:** `docs/theory/agentic-workflow-theory.md`, `docs/deferred-features.md`, `docs/workflow-library-roadmap-v2.md`, plus the contracts design docs (a focused pass should separate "advertised working" from "listed deferred" — `docs/deferred-features.md` may already hedge some).

---

## 8. Scope of the fix (the follow-up epic) + relation to #134

The gap is single-rooted, so the fix is well-bounded. The epic should cover:

1. **IR + parse:** add retry/timeout/compensation/confidence fields to `StepModel`; add parse branches (+ argument parsers) to `StepExtractor` (the wire contract already models all of it — no projection work).
2. **Emit (the INV-1 design):** decide how each maps onto Wolverine+Marten and emit it — retry → Wolverine retry/error policy or a saga retry loop; timeout → scheduled-message/`Deadline`; **compensation → actually publish `Trigger{Name}FailureHandlerCommand` on step failure** (close the existing dead path); confidence-gate → emit the threshold comparison + `OnLowConfidence` alternate-path dispatch.
3. **Expressibility:** add `Then<TStep>(Action<IStepConfiguration>)` to the still-missing contexts (branch, failure; fork landed via #134) so config is reachable where it makes sense.
4. **Behavioral tests (mandatory):** compile+run the emitted saga and assert a step is *actually* retried N times / times out / runs its compensation — so the gap cannot recur. The current shape-only posture is the deeper failure.
5. **Docs:** until lowering ships, mark step resilience as "declared, not yet enforced" rather than implying runtime behavior.

**Relation to #134 / DR-17:** #134's `Then(configure)` overload closes a real builder-completeness wart and is green, but on its own it only adds *export-only* surface for resilience (consistent with all other contexts). The genuinely *functional* fork-path win is the separate `ParseForkPathStepModels` fix (fork-path `ValidateState` was being dropped — and it DOES lower for other step kinds) — folded into #134 and shipped. (`WithContext` does **not** lower for any step kind — §5 correction — so it was not part of that fix.) The retry/timeout/compensation enforcement for fork (and every other context), plus wiring `WithContext`'s dead emitter, belongs to this epic.

---

## 9. Sources

`Abstractions/IStepConfiguration.cs`; `Builders/{StepConfigurationBuilder,WorkflowBuilder:133-149,LoopBuilder:85-96}.cs`; `Abstractions/{IWorkflowBuilder,ILoopBuilder,IForkPathBuilder,IBranchBuilder,IApprovalBuilder,IFailureBuilder}.cs`; `Definitions/{StepConfigurationDefinition,StepDefinition,CompensationConfiguration}.cs`; `Contracts/WorkflowDefinitionProjection.cs:121-314`; `Contracts/Generated/{CompensationConfiguration,StepConfigurationDefinition}.g.cs`; `Generators/Models/StepModel.cs:22-29`; `Generators/Helpers/{StepExtractor.cs:760-825,1002-1048,ContextModelExtractor,ApprovalExtractor:177-205}.cs`; `Generators/Emitters/{SagaEmitter,Saga/StepStartHandlerEmitter:70-129,ContextAssemblerEmitter,CommandsEmitter:243-380,Saga/SagaApprovalHandlersEmitter,Saga/SagaFailureHandlerComponentEmitter:75-102,ApprovalIntegrationHandlerEmitter,WorkerHandlerEmitter:181-253,EventsEmitter:169-236,PhaseEnumEmitter:96-101}.cs`; tests `Strategos.Tests/Definitions/{RetryConfiguration,CompensationConfiguration}Tests.cs`, `Strategos.Tests/Builders/StepConfigurationBuilderTests.cs:87-124`, `Strategos.Contracts.Tests/Workflow/IrCompletenessTests.cs`, `Strategos.Generators.Tests/OnFailureIntegrationTests.cs`; solution-wide greps over `Generators/` (`Retry|Timeout|Compensat|Confidence`) and over `src/` (`OnException|RetryWithCooldown|Polly|WolverineOptions|new Trigger`).

**Confidence:** very high — two independent passes converged on the same single-rooted mechanism with concrete `file:symbol` evidence; the conclusion is corroborated by the absence (grep-verified) of any retry policy or trigger-publish anywhere in the runtime.
