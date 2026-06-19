# v2.9.0 Close-Out Bundle — step-resilience completeness + schema-bootstrap footgun

**Date:** 2026-06-17 · **Workflow:** `v290-closeout-bundle`
**Addresses:** #143 (parity guard) · #142 (escape literal) · #141 (StartWith/Finally config) · #140 (OnFailure chain + Compensate interop) · #139 (multi-step OnLowConfidence) · #138 (EventSourced audit events) · #132 (batch schema bootstrap)
**Builds on:** #135 / PR #137 (step-resilience lowering, `docs/designs/2026-06-17-step-resilience-lowering.md`, DR-1..DR-10) · #129 (v2.9.0 edge layer)
**Invariant catalog:** `/strategos-design-invariants` (INV-1..INV-8)
**Defers to v2.10.0:** #128 (chained identity) · #130 R3a/R4/R5/R6/R8 — both already owned by `docs/designs/2026-06-16-edge-layer-v2100-followons.md`

---

## Problem Statement

v2.9.0 is **not yet tagged** (`git describe → v2.8.0-5-g0f5fa2c`); the edge layer (#127/#129/#136) and step-resilience lowering (#135/#137) are committed but unreleased. This is the **last close-out before the tag** — the place for the small completeness/correctness work that shouldn't ship half-done, *not* for the architectural follow-ons (#128/#130-heavy/#131/#126/#125/#113), which already have a v2.10.0 design + plan in the tree.

Two gap clusters remain after #137:

1. **The step-resilience DSL is lowered but not *complete*.** #137 made `WithRetry`/`WithTimeout`/`Compensate`/`RequireConfidence`/`WithContext` lower into the Wolverine+Marten saga and locked the five known capabilities with mutation-proven behavioral tests. But the surrounding surface still has declared-but-incomplete corners: the workflow-level `OnFailure` chain emits a worker command with **no handler** (a dead path, #140); `OnLowConfidence` lowers only a **single terminal** step (#139); `StartWith`/`Finally` can't take step config at all (#141); the terminal-failure / low-confidence **audit events** the design committed to are captured as document properties but **never appended to the Marten stream** (#138, the design's Open Question #1); and a validation-error message with a quote breaks the emitted literal (#142). Above all, the protection against *new* instances of the "declared-but-not-lowered" bug class is still **disciplinary, not structural** (#143).

2. **The schema-bootstrap default is a silent multi-registration footgun (#132).** `EnsureSchemaAsync<T>(descriptorName: null)` is correct until a `T` gains a second descriptor, then it's a hard startup crash — which is exactly how a downstream consumer (Basileus agent-host) crash-looped. There is no batch "ensure schemas for everything" counterpart, so every consumer hand-rolls a cross-service loop.

### What the exploration removed from scope

- **#130 R1 (reverse junction index) and R2 (vertex key-path unique index) are already shipped** (`SqlGenerator.cs:359-366` / `:271-287`, covered by `JunctionIndexTests` / `VertexIndexTests`). The "do the additive DDL while the schema is unreleased" argument is **moot** — there is no pre-tag DDL debt. R1/R2 are closed as already-complete; the remaining #130 items (R3a identifier guard, R4 batch relate, R5 prepared statements, R6 `RelateBatchAsync`, R8 pgvector knobs) stay in v2.10.0, where R3a is a P2 concern anyway.

## The organizing idea

The bundle's keystone is **#143's declared↔lowered parity guard**. Under this (Approach B) scope we *complete* the step-resilience surface rather than defer it, so #143 flips from "track five deferrals" to "**assert the whole `IStepConfiguration` surface is lowered and behaviorally proven**, with its deferred-set pointing only at the genuinely-out-of-scope v2.10.0 fork-path items." It converts "remember to lower + behaviorally test a new config member" from a discipline into a **build failure** — the permanent fix for the bug class #137 only patched five instances of.

---

## Invariant constraints (from `/strategos-design-invariants`, verdict: **conditional**)

| Invariant | Constraint this design adopts |
|---|---|
| **INV-1** (HIGH, lowering via SG) | #140/#139/#138 lower **through `SagaEmitter` / the `Saga/*` emitters onto Wolverine+Marten primitives** — no parallel runtime. #140's OnFailure worker handler is a generated saga `Handle`, not a hand-rolled dispatcher; #138's events append via the saga's existing `IDocumentSession`. #143's whole purpose is to *enforce* this seam mechanically. |
| **INV-5** (HIGH, stable diagnostic IDs) | #143's "declared-but-inert" diagnostic takes the **next-free monotonic `AGWF022`** (live ceiling `AGWF021` in `AgwfCatalog.tsp`; ids single-sourced there → `AgwfCodes.g.cs` + `WorkflowDiagnostics.cs`). No id reused/renumbered. #132 adds no analyzer diagnostic (runtime API only); if one is wanted it takes next-free `AONT213` (live ceiling AONT212). |
| **INV-6 / INV-7** (HIGH, sealed/immutable) | New IR/config records (#141 per-step config threaded onto `StartWith`/`Finally`; #139 multi-step handler chain model; #138 audit-event records; #132 any bootstrap result) are `sealed` `init`-only; each extends `InvariantGuardTests`. Retry/handler re-delivery keeps the same immutable envelope state (INV-7), unchanged from #137. |
| **INV-8** (HIGH, polyglot identity) | #132's batch path keys on the **resolved descriptor name** via `IOntologyQuery.GetObjectTypeNames<T>()` / `OntologyGraph.ObjectTypes`, never `typeof`. #140/#139 step references stay FQN-string descriptors (consistent with `StepModel.StepTypeName`), never CLR `Type`. |
| **INV-2** (HIGH, self-contained ontology) | #132 stays in `Strategos.Ontology` / `Strategos.Ontology.Npgsql`, raw Npgsql only — zero Marten/Wolverine. |
| **INV-3/INV-4** | Do not bind (no MCP-spec change; no new DSL nomenclature beyond mirroring existing concrete verbs `StartWith`/`Finally`/`Then`). |

---

## Requirements

Each `CL-N` is a close-out requirement; `/exarchos:plan` traces tasks to these anchors. CL-1/CL-2/CL-7 are independent; CL-3/CL-4/CL-5 are the heavier saga-emission items; **CL-6 (#143) lands last** as the closing forcing function over the now-complete surface.

### CL-1 — Escape the validation-error-message literal (#142)

`StepStartHandlerEmitter.cs:128` interpolates `stepModel.ValidationErrorMessage` (raw `Token.ValueText`) straight into a generated C# string literal; a message containing `"` or `\` breaks the emitted source.

**Acceptance criteria:**
- The message is emitted via `SymbolDisplay.FormatLiteral(msg, quote: true)` (or an escaped verbatim literal); generated source compiles for a message containing a double-quote and a backslash.
- A generator test asserts a quote/backslash-bearing `ValidateState` message round-trips (regression pin).

### CL-2 — `StartWith` / `Finally` accept step config (#141)

`StartWith<TStep>` (`IWorkflowBuilder.cs:51/74`) and `Finally<TStep>` (`:169`) are the only sequencing contexts lacking the `Action<IStepConfiguration<TState>>` overload, so the first and terminal steps can't declare resilience. The parse path already threads config (`StepExtractor.ExtractConfiguredResilience`), and `StepModel` already carries `Retry/Timeout/Compensation/Confidence` — this is overload + IR-threading, not new lowering.

**Acceptance criteria:**
- `StartWith<TStep>(Action<IStepConfiguration<TState>>)` and `Finally<TStep>(Action<IStepConfiguration<TState>>)` exist, mirroring `Then(configure)` at `IWorkflowBuilder.cs:129`; the interface stays sealed-by-composition (INV-6).
- A `StartWith`/`Finally` step declaring `.WithRetry(n)`/`.WithTimeout(t)`/`.Compensate<T>()` is lowered identically to a mid-chain `Then` step (proven by the #137 behavioral harness, extended).
- `PublicAPI.Unshipped.txt` updated (RS0016/RS0017 break by design — not suppressed); CHANGELOG entry.

### CL-3 — Wire the OnFailure worker handler + define Compensate↔OnFailure interop (#140)

Two coupled defects. (a) The workflow-level `OnFailure(flow => …)` chain's generated `ExecuteFailureHandler_…WorkerCommand` has **no worker handler** (`SagaFailureHandlerComponentEmitter.cs:131` dispatches it; nothing handles it) — the chain is dead. (b) `SagaCompensationComponentEmitter.cs:87` **no-ops when `model.HasFailureHandlers`** to avoid a duplicate `Handle(Trigger…)` (CS0111), so step-level `Compensate<T>` and a workflow-level `OnFailure` are today **mutually exclusive**.

The design call: define their composition explicitly. A step's `Compensate<T>` is **local unwind** of that step's effect; the workflow `OnFailure` chain is **terminal handling** for the workflow. When both are present they compose in a fixed order — **step compensation runs first, then the OnFailure chain** — emitted from a **single** `Handle(Trigger…)` site so there is no CS0111 collision (replacing the mutual-exclusion guard with a merged emission path).

**Acceptance criteria:**
- A workflow with `OnFailure(flow => flow.Then<NotifyFailure>())` and a failing step runs `NotifyFailure` (behavioral test) — the dead worker command is closed.
- A workflow with **both** a step `.Compensate<Rollback>()` and a workflow `OnFailure(…)`: on failure, `Rollback.ExecuteAsync` runs once, **then** the OnFailure chain runs; the saga reaches `Failed`; no CS0111 (single `Handle` site).
- The merged emission keeps phase-enum prefixing + durable checkpointing correct (INV-1); golden test pins the single-`Handle` shape.

### CL-4 — Multi-step and rejoining `OnLowConfidence` handlers (#139)

`StepExtractor.ExtractLowConfidenceHandlerStep` (`:1429-1455`) takes the **first** `Then<T>` and the completed handler is terminal (`MarkCompleted()`). Generalize it to a multi-step chain mirroring the failure-handler emission (`SagaFailureHandlerComponentEmitter.cs:52-72` loops over `model.FailureHandlers`), and support **rejoin-to-main-flow** after the handler (resume at the next main step) instead of always terminating.

**Acceptance criteria:**
- `OnLowConfidence(alt => alt.Then<A>().Then<B>())` lowers a two-step handler chain; both `A` and `B` execute in order (behavioral test).
- A rejoining handler (`…rejoin/continue` semantic) resumes the main flow at the step after the gated step; a terminating handler still completes (both shapes tested; the terminal default is unchanged for back-compat).
- New handler-chain model is `sealed` `init`-only (INV-6); emission goes through the saga emitters (INV-1).

### CL-5 — Emit `StepFailed` / `LowConfidenceRouted` as Marten stream events in EventSourced mode (#138)

#137 captures terminal-failure / low-confidence audit data as queryable saga **document properties + logs** (document mode) but never appends the **named stream events** the design committed to; they're only meaningful in `EventSourced` mode. The mechanism already exists — `StateApplicationHelper.cs:56-64` appends via `session.Events.Append(WorkflowId, evt)`, and the failure/confidence handlers already receive `IDocumentSession` when `IsEventSourced` (`SagaFailureHandlerComponentEmitter.cs:112-120`). This **resolves the step-resilience design's Open Question #1 (audit-event taxonomy)**.

**Acceptance criteria:**
- When `model.IsEventSourced`, the compensation-trigger handler appends `StepFailed` (failed step name + exception type) and the confidence-gated handler appends `LowConfidenceRouted` (step + score + threshold) to the workflow's Marten stream; document-mode posture is unchanged.
- Audit-event records are `sealed` `init`-only with a settled shape (resolve OQ#1: names `StepFailed` / `LowConfidenceRouted`; reuse vs. new decided in /plan).
- A new **EventSourced behavioral fixture** (the existing `WolverineHostFixture` is document-mode only) asserts each event lands in the stream; reverting the append makes it go RED (mutation-proof, per the #143 standard).

### CL-6 — Declared↔lowered parity guard (#143) — keystone, lands last

Make it **impossible to ship an `IStepConfiguration<TState>` member (or `StepConfigurationDefinition` field) that isn't lowered or explicitly, trackably deferred.** Two mechanisms:

- **(A) Parity allowlist test** (forcing function) in `Strategos.Generators.Tests`: reflect over the public surface of `IStepConfiguration<TState>` and the fields of `StepConfigurationDefinition`, asserting every member is in exactly one explicit set — **lowered** (membership backed by an emit assertion *and* a compile-run-saga behavioral test) or **deferred** (annotated with a tracking issue). A new member that is neither fails the build.
- **(B) Generator "declared-but-inert" diagnostic** (`AGWF022`, INV-5): when the parser populates a `StepModel` config field that **no emitter consumes for that step kind**, report the diagnostic at consumer compile time — turning the silent no-op into the earliest-tier signal.

Under Approach B the **lowered** set is the whole current surface (`WithRetry`/`WithTimeout`/`Compensate`/`RequireConfidence`/`OnLowConfidence`/`ValidateState`/`WithContext`, all lowered by #137 + CL-1..CL-5); the **deferred** set points only at the explicitly-out-of-scope v2.10.0 fork-path items (`RequireConfidence`/`OnLowConfidence` on fork branches, nested `RepeatUntil` in a branch — #134/v2.10.0 DR-17).

**Acceptance criteria:**
- Adding an `IStepConfiguration` member / `StepConfigurationDefinition` field that is neither lowered-with-behavioral-proof nor deferred-with-issue **fails** the parity test.
- A `StepModel` config field set by the parser but read by no emitter for a step kind raises `AGWF022` at consumer compile time.
- Backfill: the current surface passes the parity guard (every member lowered after CL-1..CL-5; deferred set = the v2.10.0 fork-path items only).
- The guard is documented as the standing contract: *shape-only tests are insufficient for lowering correctness; a config member is "done" only with a behavioral proof or a tracked deferral.*

### CL-7 — Batch / safe schema bootstrap (#132)

`PgVectorObjectSetProvider.EnsureSchemaAsync<T>(descriptorName: null)` throws for a multi-registered `T` (`:145`) — defensible alone, but there's no batch counterpart, so consumers hand-roll a cross-service loop over `IOntologyQuery.GetObjectTypeNames<T>()`. Add a graph-wide bootstrap and make the obvious call the safe one.

**Acceptance criteria:**
- `IObjectSetProvider` gains `Task EnsureAllSchemasAsync(CancellationToken)` — ensures a table for **every** descriptor in `OntologyGraph.ObjectTypes`; the Npgsql impl iterates the graph (INV-2, raw Npgsql; INV-8, keys on resolved descriptor name).
- `EnsureSchemaAsync<T>(CancellationToken)` (no descriptor name) ensures **all** descriptors registered for `T` (iterates `GetObjectTypeNames<T>()`) instead of throwing — the zero-arg call becomes the safe one; the existing single-descriptor overload is unchanged.
- The Basileus multi-registration crash scenario (two `Object<SemanticDocument>("…")` registrations) bootstraps cleanly under both new entry points — used as the validation fixture (design-on-merit; consumer is corpus, not dependency).
- `PublicAPI.Unshipped.txt` + CHANGELOG updated; any new result type `sealed` (INV-6).

---

## Sequencing & delegation shape

| Track | CL | Issue | Depends on | Notes |
|---|---|---|---|---|
| Escape literal | CL-1 | #142 | — | Tiny; parallel from t0 |
| StartWith/Finally config | CL-2 | #141 | — | Overload + IR thread; parse path exists |
| Batch schema bootstrap | CL-7 | #132 | — | Ontology; fully independent track |
| OnFailure + Compensate interop | CL-3 | #140 | — | Saga emission; merged `Handle` site |
| Multi-step OnLowConfidence | CL-4 | #139 | — | Mirror failure-handler chain + rejoin |
| EventSourced audit events | CL-5 | #138 | — | New EventSourced behavioral fixture |
| Parity guard | CL-6 | #143 | CL-1..CL-5 | **Last** — allowlist over the completed surface |

**Critical ordering:** CL-6 lands after CL-1..CL-5 so its allowlist reflects the now-complete surface and its backfill passes. CL-1/CL-2/CL-7 are quick parallel wins; CL-3/CL-4/CL-5 are the saga-emission core. **PR shape** (one stacked PR per CL vs. grouped — e.g. CL-3/CL-4/CL-5 saga-emission together, CL-1/CL-2/CL-6 generator-surface together, CL-7 standalone) is a /plan decision.

## Testing Strategy

- **Behavioral (mandatory, the #143 standard):** every lowering CL (CL-2 StartWith/Finally resilience, CL-3 OnFailure+compensation order, CL-4 multi-step/rejoin OnLowConfidence, CL-5 EventSourced stream events) gets a compile-run-saga test in `Strategos.Generators.Behavioral.Tests`, each **mutation-proven** to go RED when its lowering is removed. CL-5 needs a **new EventSourced host fixture** (current fixtures are document-mode).
- **Generator/shape:** CL-1 (escaped literal compiles), CL-3 (single-`Handle` golden shape, no CS0111), CL-6 (the parity allowlist + `AGWF022` analyzer positive/negative).
- **Ontology:** CL-7 against real Postgres+pgvector under `Strategos.Ontology.Npgsql.Tests` (DB-gated like the existing suites so the default build stays green without a database), asserting the two-registration bootstrap.
- **Sealed-guard (INV-6):** `InvariantGuardTests` extended for every new record.
- **No-config baseline:** the existing golden test still pins byte-for-byte-unchanged output for steps with no resilience config.
- TUnit invocation is `-- --treenode-filter`, not `--filter` (repo convention).

## Out of scope (→ v2.10.0, already designed)

- **#128** chained-identity / multi-registration traversal — the P2 junction keystone (v2.10.0 DR-11/G-11).
- **#130** R3a (identifier guard), R4 (batch relate), R5 (prepared statements), R6 (`RelateBatchAsync`), R8 (pgvector knobs) — v2.10.0 DR-13/G-13. **R1/R2 are already shipped and close as complete.**
- Fork-path `RequireConfidence`/`OnLowConfidence` and nested `RepeatUntil` in a fork branch — tracked deferrals in CL-6's allowlist (v2.10.0 DR-17 / #134).

## Open Questions

1. **OnFailure ↔ Compensate ordering (CL-3)** — confirm "step compensation, then workflow OnFailure" is the desired precedence (vs. OnFailure-only when both declared). Lean: compose in that order; resolve in /plan against the saga state machine.
2. **Rejoin semantics for OnLowConfidence (CL-4)** — does "rejoin" resume at the next main step, or re-run the gated step with the handler's output? Lean: resume at next step (handler is corrective, not a retry); confirm against a real low-confidence routing case.
3. **Audit-event taxonomy (CL-5)** — final names/shapes for `StepFailed` / `LowConfidenceRouted`, and whether `RetryExhausted` joins them now or stays deferred. Resolves step-resilience OQ#1 in /plan.
4. **#132 surface (CL-7)** — ship both `EnsureAllSchemasAsync` *and* the safe `EnsureSchemaAsync<T>(ct)`, or only the graph-wide bootstrap? Lean: both (the per-`T` safe overload removes the footgun at its source).
5. **Exact AGWF id** — `AGWF022` assigned against the live `AgwfCatalog.tsp` ceiling (AGWF021) at implementation.
