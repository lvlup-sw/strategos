# Spec: Strategy-Compiler Contract Layer (v2.10.0 bundle)

**Date:** 2026-07-06 · **Feature:** `strategy-compiler-contracts` · **Depth:** deep
**Inputs:** roadmap `lvlup-sw/strategos#153` (strategy-compiler program) · issues #100 · #150 · #151 · #145 · #152 · consumer trackers `lvlup-sw/basileus#182` (addendum 2026-07-05/06) and `lvlup-sw/exarchos#1599` (coordination rule 6) · in-session exploration reports (Contracts/TypeSpec pipeline, generator lowering surface, ontology MCP response shapes)

> One unified artifact: `## Requirements` is the DR-N source; `## Decomposition` maps tasks → DR-N within this same document.
> Ships as **v2.10.0 re-themed** to the contract layer; edge remnants #128/#130 are bumped out of the milestone (verified non-blocking, see Open Questions).


## Problem Statement

Both runtimes are converging on `Strategos.Contracts` as the shared IR spine (basileus consumes it as the U-8 source-generated saga layer; exarchos as the #1258 Workflow Builder IR target), but four contract capabilities are missing, and #153's contract-first acceptance rule requires them to land here before any consumer IR phase:

1. **The workflow IR is export-only** (#100). `ToContract()` projects builder → `WorkflowDefinitionV1`, but nothing can execute or round-trip a JSON-authored workflow — the strategy-compiler endpoint (exarchos authors IR → basileus executes generated sagas) has no import half. No moniker→CLR resolution exists anywhere (LB-2 is a doc-comment reservation only).
2. **Gates have no typed identity or reliability slot** (#150). Verifier FPR is the denominator of achievable autonomous horizon; basileus #392 (`horizon:now`) and exarchos #1646 (v2.12.0, priority:high) both measure it and need one shared taxonomy to declare it against. Nothing named `GateClass`/`GateDeclaration`/`ExecutionProfile` exists in code today.
3. **Fork/compensation is not a first-class DSL edge** (#151), and its base is unsound: fork-path `RequireConfidence`/`OnLowConfidence` reach the IR but are never lowered, and nested-`RepeatUntil` confidence config is dropped from the IR entirely — silently inert, structurally undiagnosable (#145, the AGWF022 `Deferred` debt).
4. **Ontology responses cannot express licensed abstention** (#152). A calibrated model must sometimes hallucinate closed-book; the system-level escape is retrieval plus a typed null decided by the retrieval layer. No citation/abstention shape exists (grep: zero hits), and there is no server-side event rail to make the coverage metric unskippable.

## Chosen Approach

**Compiler-first** (Exploration, Approach A). `FromContract()` is not a runtime API — it is a **build-time front-end to the existing source generator**: JSON `WorkflowDefinitionV1` files enter the consumer's compilation as `AdditionalFiles`, a wire-IR→`WorkflowModel` bridge rehydrates them into the same IR the C# fluent-chain re-parser produces, and the identical saga emitters lower them. Moniker→CLR resolution happens at compile time against the compilation's symbol table; failures are stable build diagnostics, and `lambda:true` delegate steps are rejected (imports are fully declarative). INV-1 is preserved by construction — the only thing that ever produces execution is the Roslyn SG.

The contract shapes land contract-first in TypeSpec (the canonical source; C# and JSON Schema are emitted projections): the `GateClass`/`GateDeclaration` family plus an additive `gates` slot on `WorkflowDefinitionV1` (#150), the fork/compensation edge with its closed trigger enum (#151), and the abstention response union (#152). The saga-lowering debt (#145) is paid first so #151's fork edge lowers onto sound machinery. The ontology union is implemented natively in `Strategos.Ontology.MCP` (INV-2: the core never references the Contracts package) with parity to the Contracts schema pinned by a mechanical conformance test, and the `ontology.abstained` event is emitted at the core decision site through a new minimal audit-sink abstraction — callers structurally cannot skip it.

## Requirements

The DR-N identifiers below are the single source the decomposition traces against.

### DR-1: `GateClass` closed enum in Contracts (#150)

A typed, closed gate-class identity — a type, never a string — shared by both runtimes.
TypeSpec enum with kebab wire names (`typecheck | lint | scoped-test | full-suite | mutation-adequacy | merge-gate | llm-judge`), emitted as a C# enum via the existing `[JsonStringEnumMemberName]` + `JsonStringEnumConverter<T>` path (the #98 precedent).

**Acceptance criteria:**
- `GateClass` model exists in a new `.tsp` under `src/Strategos.Contracts/`, imported from `main.tsp`; `tsp compile` + codegen emit the JSON Schema and the C# enum with kebab wire values.
- An unclassified gate is unrepresentable: `GateDeclaration.class` is non-optional and enum-typed in both projections.
- `ZodConsumabilityTests` / cross-product round-trip covers the new schema file.
- Member-list freeze is coordinated with basileus#392 + exarchos#1646 before merge (see Open Questions); additions later follow the additive-minor policy.

### DR-2: `GateDeclaration` with measured-reliability annotation (#150)

`GateDeclaration { class: GateClass, id, reliability?: { fpr, sampleSize, asOf, source } }` as a sealed emitted record.
`reliability` is data measured elsewhere (telemetry projections), never hand-authored: `source` carries the producing projection's provenance and is required whenever the annotation is present.

**Acceptance criteria:**
- TypeSpec model + emitted `sealed record` with `{ get; init; }` (INV-6/7, proven by the existing `EmitterShapeTests` reflection guard) and `InvariantGuardTests` coverage.
- `reliability.source` is schema-required within the reliability object; a reliability block without `source` fails schema validation (test).
- By-construction: no builder/DSL surface can author reliability values — the only way a reliability annotation exists is deserialization of telemetry-produced data; the #53 fixture corpus (builder-produced) contains zero reliability blocks (asserted).
- `JsonSchemaDiff` classifies the whole family as NON-BREAKING against the prior schema set (CI diff gate green).

### DR-3: Workflow IR is born speaking gate classes (#150 → #100)

`WorkflowDefinitionV1` gains an optional `gates?: GateDeclaration[]` and the existing `GateStep` wire arm gains an optional `gateId?` back-reference, both additive.
This is the composition point the roadmap names: declarations flow through the shared IR to both runtimes before either retrofits its own.

**Acceptance criteria:**
- Both fields optional; `JsonSchemaDiff` reports NON-BREAKING; existing #53 fixtures re-validate unchanged.
- A `gateId` referencing an id absent from `gates` fails contract validation (test at the `ContractsJson`/schema layer).
- The #100 import front-end (DR-12) accepts and preserves `gates` (rehydration keeps the declarations addressable; proven in the round-trip corpus).

### DR-4: Fork-path confidence gating lowers into the saga (#145 gap A)

`RequireConfidence`/`OnLowConfidence` declared on a fork-path step lower into the generated fork handlers, mirroring the top-level reference implementation (`StepCompletedHandlerEmitter.EmitConfidenceGatedHandler`).

**Acceptance criteria:**
- Below-threshold confidence on a fork-path step routes to the declared handler chain and (EventSourced mode) appends `{Pascal}LowConfidenceRouted` — behavioral proof on the real-host harness (`Strategos.Generators.Behavioral.Tests`), not golden-file.
- `StepConfigParityTests`: `RequireConfidence(fork-path)` and `OnLowConfidence(fork-path)` move `Deferred` → `Lowered`, each naming its behavioral proof.
- AGWF022 no longer fires for fork-path confidence (`DeclaredButInertTests` updated); the diagnostic remains for any still-inert surface.
- SagaDocument (non-EventSourced) output for workflows without fork confidence is byte-unchanged (regression guard).

### DR-5: Nested-`RepeatUntil` step config enters the IR and lowers (#145 gap B)

Loop-body steps are promoted from bare `StepInfo` to configured `StepModel` during step extraction (the configure lambda is parsed, not just the step name), `LoopModel` carries step models rather than name strings, and loop-body `OnLowConfidence` lowers into the loop's generated handlers.

**Acceptance criteria:**
- Loop-body confidence config reaches `StepModel.Confidence` in the IR (generator unit test) — the config is no longer structurally invisible.
- Behavioral proof: a nested-`RepeatUntil` body step with `OnLowConfidence` routes on low confidence on the real host.
- `StepConfigParityTests`: `OnLowConfidence(nested-RepeatUntil)` moves `Deferred` → `Lowered`; `docs/deferred-features.md` updated.
- Any loop-body config that remains unlowered (e.g. a deliberately deferred member) is now AGWF022-diagnosable — the "structurally undiagnosable" class is eliminated.

### DR-6: `RequireConfidence` composes instead of replacing (latent bug, #145)

`StepConfigurationBuilder.RequireConfidence` currently replaces the whole configuration object, making it order-dependent with other setters; it must merge.

**Acceptance criteria:**
- `RequireConfidence` after `WithRetry`/`Compensate`/etc. (and before) yields the same composed configuration (regression test both orders).

### DR-7: Fork/compensation edge as a first-class DSL construct (#151)

A workflow-level DSL edge declaring where a workflow may fork and what compensation it seeds — the declarative half of the basileus #394 diagnostic rollback-and-fork verb.
Naming follows INV-4 (concrete domain nomenclature): working name `AllowDiagnosticFork(...)`, finalized against the invariants skill at implementation.

**Acceptance criteria:**
- Builder surface expresses: anchor step(s), permitted triggers, compensation seed, `maxForks` bound; sealed builder types; `InvariantGuardTests` coverage.
- The construct is inexpressible without a trigger and evidence-ref declaration (compile error, not runtime check).
- XML docs state the lowering contract (what the SG emits) — no declared-but-inert introduction: the parity guard gains entries for every new declarable member in the same PR (`Lowered` or explicitly `Deferred` with an AGWF022 guarantee).

### DR-8: Closed fork-trigger contract with required evidence (#151)

`ForkTrigger` closed enum `{ ratification_failure, gate_contradiction, operator_explicit }` in Contracts TypeSpec, with per-trigger evidence-ref requirements (e.g. `ratification_failure` requires the provisional-stamp event id + a non-empty taint set).
The trigger-enum schema is versioned so a future budget-bounded `exploratory` member (basileus PR #401 rec L-4) is additive, not a redesign.

**Acceptance criteria:**
- TypeSpec union/enum + evidence-ref model emitted through the standard pipeline; Zod-consumable.
- An unjustified fork is unrepresentable: constructing/deserializing a fork declaration without its trigger's required evidence refs fails validation (schema + emitted-record guard tests).
- Schema carries an explicit version marker; the additive-evolution note is in the model doc-comment.

### DR-9: The fork edge lowers into the generated saga (#151)

The SG emits fork handling, compensation seeding, guard wiring, and events for the DR-7 edge — sagas are never hand-written (U-8/INV-1).

**Acceptance criteria:**
- Generated guard enforces `maxForks` (precedent: `LoopModel.MaxIterations` forced-exit); exceeding it routes to a blocked/human-escalation terminal, behavioral proof on the real host.
- EventSourced mode appends a `{Pascal}WorkflowForked` event carrying trigger + evidence refs at the single decision site (EventsEmitter pattern, `IsEventSourced`-gated); SagaDocument mode byte-unchanged for workflows without the edge.
- The edge round-trips through the wire IR (DR-10) and the import front-end compiles it back to an equivalent saga (corpus proof).
- Compensation seeded by the fork composes with the existing `Compensate`/`OnFailure` merged trigger site (#140) — proven behaviorally.

### DR-10: Fork edge on the wire (#151 → #100, exarchos #1648/#1258)

The declarative fork edge is a contract shape: an additive `DiagnosticForkDefinition` (anchor refs, triggers, evidence-ref schema, `maxForks`, compensation seed moniker) on `WorkflowDefinitionV1`, exportable by `ToContract()` and importable by the DR-12 front-end.
This is the shared combinator exarchos #1258 lowers to and basileus #394 consumes.

**Acceptance criteria:**
- `ToContract()` projects the DR-7 builder edge to the wire shape (extending `WorkflowDefinitionProjection`; the throwing `default` + exhaustiveness tests force explicit wiring).
- `JsonSchemaDiff`: NON-BREAKING addition; fixture corpus gains fork-edge fixtures (new combinator tag or within the existing 8).
- INV-8: all step/type references in the shape are string monikers, never CLR types.

### DR-11: Abstention response union in the ontology MCP surface (#152)

Closed response union `Answer { content, citations: RecordRef[] (non-empty) } | NoAnswerRecorded { nearestRecords: RecordRef[] }` in `Strategos.Ontology.MCP`, mechanically following the `QueryResultUnion` `[JsonPolymorphic]` pattern; `RecordRef` is the polyglot string identity pair (descriptor name + projected record id, à la `TraversalEndpoint`) — never a CLR type (INV-8).

**Acceptance criteria:**
- Sealed records, `_meta: ResponseMeta` carried (INV-3), discriminator emitted; `InvariantGuardTests` coverage.
- A free-text uncited answer is unrepresentable: `Answer` with empty/null citations cannot be constructed (guard clause) and fails schema validation (`minItems: 1` in the advertised output schema) — both tested.
- The null is decided by the retrieval layer: the union is produced server-side from retrieval results; no caller-facing constructor path yields `NoAnswerRecorded` with hidden results (test: empty retrieval ⇒ `NoAnswerRecorded` with nearest records populated).
- Lands independently of #115 (no descriptor-primitive changes) — verified by the ontology exploration; #115 stays open, unblocked.

### DR-12: JSON workflows compile — the import front-end (#100)

`WorkflowDefinitionV1` JSON files registered as `AdditionalFiles` are parsed by a wire-IR→`WorkflowModel` bridge in `Strategos.Generators` and lowered by the identical saga emitters as C#-authored workflows.
Scope: the fully-declarative subset (all wire step kinds the projection produces, plus structural transitions/branches/loops/forks and step configuration).

**Acceptance criteria:**
- A JSON-only workflow in a consumer compilation produces `Add{Pascal}Workflow()` / `Start{Pascal}Command` / saga / events identical in behavior to its C#-authored equivalent (real-host behavioral proof).
- The bridge feeds the existing `WorkflowModel` IR — zero forked emitter logic (architecture-guard: fork/loop/confidence emitters have a single call path).
- Incremental-generator correctness: editing the JSON invalidates and regenerates (cache test).
- Malformed JSON / schema-invalid input produces a stable diagnostic (next free AGWF id, monotonic — catalog updated via `AgwfCatalog.tsp`), never a generator crash (generator-driver test).

### DR-13: Compile-time moniker resolution with stable diagnostics (#100)

The wire contract's simple-name monikers resolve to CLR types via the compilation's symbol table.
Resolution failures are build-time diagnostics, allocated monotonically above the current AGWF022 ceiling: unresolvable moniker, ambiguous moniker (two+ types sharing the simple name), and delegate-step rejection (DR-14).

**Acceptance criteria:**
- Happy path: moniker resolves to exactly one accessible `INamedTypeSymbol` implementing the expected step contract.
- Unresolvable ⇒ diagnostic with the moniker + JSON file path; ambiguous ⇒ diagnostic listing all candidates (deterministic order); both have analyzer tests and catalog entries.
- INV-8 honored: resolution consumes the moniker string; nothing persists a CLR `Type` back into contract state.
- The one-step-CLR-type-per-workflow-definition constraint (CS0101 class) holds for imported workflows — violation surfaces as the same build error class as C#-authored duplicates, covered by a test.

### DR-14: Delegate steps are rejected at import — declarative-only contract (#100/LB-1)

`lambda: true` steps cannot execute from JSON (their bodies were dropped at export, by design).
The import front-end rejects them with a stable diagnostic; the lambda re-binding registry from #100's option list is explicitly deferred and documented.

**Acceptance criteria:**
- Importing a fixture containing a `delegate` step yields the rejection diagnostic naming the step and its `lambda` marker; no saga is emitted for that workflow.
- The deferral is recorded in `docs/deferred-features.md` with the #100 follow-on pointer.

### DR-15: Behavioral round-trip gate over the #53 corpus (#100)

Round-trip fidelity is proven behaviorally, both directions: for every declarative corpus fixture, C#-authored workflow → `ToContract()` JSON → DR-12 import → generated saga is behaviorally equivalent to compiling the C# directly.

**Acceptance criteria:**
- The gate runs over the `WorkflowCorpus` fixtures (≥100 across the combinator tags), skipping only `delegate`-bearing fixtures (which are asserted to hit DR-14 instead — the corpus partitions cleanly into imported-and-equivalent vs rejected-with-diagnostic, no third bucket).
- Equivalence is asserted on emitted-artifact semantics (saga phases, handlers, events) on the real-host harness for a representative subset, and on generated-source equivalence for the full corpus.
- The gate is CI-wired alongside the existing fixture-export tests.

### DR-16: Contracts twin + mechanical parity for the abstention union (#152)

The abstention union exists in Contracts TypeSpec (for basileus #155 and exarchos Zod derivation) and in `Strategos.Ontology.MCP` C# (INV-2 forbids the core referencing the Contracts package); divergence is prevented mechanically, not by convention.

**Acceptance criteria:**
- TypeSpec union emitted through the standard pipeline (discriminated-union path: abstract record + `[JsonDerivedType]`).
- A schema-conformance test serializes the Ontology.MCP union (both arms, edge cases) and validates the output against the Contracts-emitted JSON Schema — the build fails on drift in either direction.
- `JsonSchemaDiff`: NON-BREAKING addition.

### DR-17: `ontology.abstained` emitted server-side at the core chokepoint (#152)

A minimal audit-sink abstraction (`IOntologyAuditSink`, no-op default) in `Strategos.Ontology` core; the answer/abstain decision site emits the abstention record — callers structurally cannot skip the coverage metric.
The event's wire shape lands in Contracts `Events/` (envelope-compatible, `type: "ontology.abstained"`).

**Acceptance criteria:**
- Emission happens in the core decision path (not hosting, not the caller): any consumer of the core tool — MCP or direct — produces the event when abstaining (test through both entry points).
- The sink abstraction adds no new package dependencies to `Strategos.Ontology` (INV-2 posture preserved; `IsAotCompatible` unchanged).
- Hosting wires a concrete sink; the no-op default keeps existing consumers source- and behavior-compatible.
- Contracts event model emitted + Zod-consumable; the abstained payload carries `nearestRecords` counts, not record contents (no data exfiltration through audit).

### DR-18: Schema-evolution guardrail across the bundle (error/failure-mode requirement)

Every wire-visible addition in this bundle is additive and machine-verified as such; failure modes at every new boundary are typed, not stringly or silent.

**Acceptance criteria:**
- `JsonSchemaDiff` CI gate green across all schema deltas (#150/#151/#152/#100 shapes); any BREAKING classification blocks merge.
- Contracts package version bumps 0.3.0 → 0.4.0 (additive minor per its independent versioning); product ships v2.10.0.
- New public API surfaces trip RS0016/RS0017 and update `PublicAPI.Unshipped.txt` deliberately — no suppressions.
- Every new failure mode introduced by the bundle maps to a typed channel: build diagnostics (DR-12/13/14), guard-bounded escalation (DR-9), typed abstention (DR-11), schema-validation rejection (DR-2/8) — enumerated and each covered by at least one test.

## Technical Design

### Contracts (TypeSpec-first)

 All new shapes are `.tsp` models imported from `main.tsp`; the codegen auto-picks them up (closed enums → kebab-named C# enums; unions → `[JsonPolymorphic]` abstract records; optional annotation slots → additive fields). New families: `Gates/` (DR-1/2/3), the fork-edge shapes in `Workflow/` (DR-8/10), the abstention union + `ontology.abstained` event (DR-16/17). The emitter itself needs no changes — this is the pipeline working as designed.

### Generators

 Two workstreams share the `WorkflowModel` IR seam. First, the #145 debt: fork emitters gain the confidence-gated handler emission (mirroring `StepCompletedHandlerEmitter`), and step extraction promotes loop bodies to configured `StepModel`s (`LoopModel` shape change). Second, the #100 front-end: an `AdditionalFiles` provider + `WireIrReader` (System.Text.Json, `ContractsJson` options) + `WireToModelBridge` producing `WorkflowModel` — downstream emitters untouched. The DR-7/9 fork edge extends `WorkflowModel` (`DiagnosticForkModel`), lowers in the fork/compensation component emitters, and projects to the wire in `WorkflowDefinitionProjection`. New diagnostics allocate AGWF023+ via `AgwfCatalog.tsp` (monotonic, never reused).

### Ontology

 The union + `RecordRef` live beside `QueryResultUnion`; the audit sink is a single-method core abstraction with a no-op default, wired in `OntologyServerToolFactory`. No descriptor-primitive changes; #115 stays independent.

### Sequencing (contract-first, mirroring #153)

 DR-1/2/3 + DR-8 + DR-16/17 wire shapes land first (unblocks basileus #392 and exarchos #1646 immediately); #145 (DR-4/5/6) precedes the fork-edge lowering (DR-7/9/10); the import front-end (DR-12–15) closes the bundle. #152's implementation (DR-11/17) runs as a parallel track.

## Integration Points

- `src/Strategos.Contracts/main.tsp` + new `.tsp` models — gate family, fork triggers/edge, abstention union, abstained event
- `src/Strategos.Contracts/Workflow/WorkflowDefinitionV1.tsp` + `Structural.tsp` — additive `gates`, `gateId`, `DiagnosticForkDefinition`
- `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` — AGWF023+ allocations
- `src/Strategos/Contracts/WorkflowDefinitionProjection.cs` — fork-edge + gates export
- `src/Strategos/Builders/` (`StepConfigurationBuilder.cs`, new fork-edge builder) — DR-6/7
- `src/Strategos.Generators/Helpers/StepExtractor.cs` (:469/:520 loop-body promotion), `Models/LoopModel.cs`, `Models/WorkflowModel.cs` — DR-5, fork-edge model
- `src/Strategos.Generators/Emitters/Saga/SagaStepHandlersEmitter.cs`, `StepCompletedHandlerEmitter.cs`, `SagaCompensationComponentEmitter.cs`, `EventsEmitter.cs` — DR-4/9 lowering + events
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs` — AdditionalFiles front-end, AGWF022 update, new diagnostics
- `src/Strategos.Generators.Tests/Parity/StepConfigParityTests.cs` + `Diagnostics/DeclaredButInertTests.cs` — parity moves
- `src/Strategos.Tests/FixtureExport/` + `Strategos.Generators.Behavioral.Tests/` — DR-15 round-trip gate
- `src/Strategos.Ontology.MCP/` (union beside `QueryResultUnion.cs`), `src/Strategos.Ontology/` (audit sink), `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs` — DR-11/17
- `PublicAPI.Unshipped.txt` per touched project + `docs/deferred-features.md` + CHANGELOG

## Exploration

Divergent loop (designDepth: deep, 1 round to convergence). Three postures for #100 — the bundle's load-bearing fork — were scored across DIM-1..DIM-8 with the project invariants interleaved:

- **A — Compiler-first (chosen):** JSON as generator input via `AdditionalFiles`; same emitters; compile-time moniker resolution; delegate steps rejected. Won on DIM-3 (zero new public runtime API; wire unchanged), DIM-4 (behavioral proof on the existing real-host harness), DIM-6 (INV-1 by construction — only the SG produces execution), DIM-7 (failures are loud build diagnostics).
- **B — Audit-first runtime API:** `FromContract()` + moniker registry, non-executable by contract. Rejected: mints a versioned-forever public API with no current consumer, proves fidelity only structurally, and fails the exarchos unifying discipline (churn the SDK would undo — the endpoint still needs the build-time path).
- **C — Shared front-end (both):** endpoint-complete but XL surface; dual maintenance from day one with no consumer for the runtime half.

Sub-fork (#152 event rail): core audit sink (chosen — the chokepoint sits at the answer/abstain decision site, unskippable by any entry point) vs hosting-layer observer (bypassable by non-MCP consumers) vs shapes-only deferral (violates the issue's chokepoint-enforcement clause).

The opt-in discover bridge was surfaced and declined — the three in-session exploration reports (Contracts pipeline, generator lowering surface, ontology response shapes) grounded every approach in in-repo precedent; no external research pass was needed (no `correlationId` to cite).

## Alternatives considered

- **Runtime `FromContract()` API (Approach B)** — rejected above; revisit only when a consumer needs runtime rehydration that the build-time path cannot serve.
- **Shared front-end (Approach C)** — deferred, not rejected: the DR-12 bridge is internal, so exposing a read-only inspection API later is additive.
- **`GateClass` as an open string with a well-known-values list** — rejected: #150 mandates "a type, never a string"; closed enum + additive-minor evolution instead.
- **Hosting-layer observer for `ontology.abstained`** — rejected: leaves the coverage metric skippable for direct core consumers.
- **Lambda re-binding registry at import (#100 option)** — deferred with DR-14's explicit rejection contract; the registry is a consumer-driven follow-on.
- **Folding #128/#130 into this bundle** — declined at scoping: neither touches the contract seams (verified in exploration); they'd inflate job size without serving the strategy-compiler spine.

## Open Questions

- **`GateClass` member freeze:** confirm the initial member list with basileus#392 and exarchos#1646 owners before DR-1 merges (additions are additive-minor afterward, but a rename is breaking). Resolution: comment thread on #150 tagging both issues; freeze at plan-review.
- **Enum-member evolution policy:** adding a `GateClass`/`ForkTrigger` member is schema-relaxing on the write side but can break exhaustive consumers (Zod enums). Proposed policy (to ratify at plan-review): enum additions ride Contracts minor bumps with a consumer-notice line in the release notes — matches the additive-minor rule already governing `WorkflowDefinitionV1`.
- **`ontology.abstained` envelope naming:** confirm the `type` string + payload fields against basileus#155's coverage-metric consumer before DR-17 merges.
- **#128/#130 milestone placement:** bumped out of v2.10.0 (this spec re-themes it). Both verified non-blocking for every DR here. Needs the milestone edit + a note on each issue.
- **Fork-edge DSL verb name:** `AllowDiagnosticFork` is the working name; final INV-4 check at implementation (concrete domain nomenclature, no abstract structural terms).

## Decomposition

### Scope

**Target:** Full design (DR-1 … DR-18).
**Excluded:** None deferred out of the bundle. Two explicit in-design deferrals carry their own DR anchors: the lambda re-binding registry (DR-14 rejects instead) and the runtime inspection API (Alternatives — Approach C half, additive later).

**Repo conventions binding every task:** TUnit runner is `cd src && dotnet test <proj> -- --treenode-filter "/*/*/*/MethodName"` (bare `--filter` does not work here); assertions are `await`ed; every new public/internal record is appended to `InvariantGuardTests` (INV-6); public-surface changes update `PublicAPI.Unshipped.txt` + CHANGELOG in the same task (no RS0016/RS0017 suppressions); new diagnostic IDs are monotonic via `AgwfCatalog.tsp` regeneration; contracts regeneration runs `scripts/contracts-codegen.sh` and hand-edits to `schemas/`/`Generated/` are CI-rejected.

### Traceability matrix (DR-N → tasks)

| DR | Requirement | Tasks |
|----|-------------|-------|
| DR-1 | `GateClass` closed enum | 001 |
| DR-2 | `GateDeclaration` + reliability annotation | 002 |
| DR-3 | IR born speaking gate classes | 003, 019 |
| DR-4 | Fork-path confidence lowers | 008 |
| DR-5 | Loop-body config into IR + lowers | 009, 010 |
| DR-6 | `RequireConfidence` merge fix | 007 |
| DR-7 | Fork/compensation DSL edge | 011 |
| DR-8 | Closed fork-trigger contract | 004 |
| DR-9 | Fork edge lowers into saga | 012, 013 |
| DR-10 | Fork edge on the wire | 005, 014 |
| DR-11 | Abstention union in ontology MCP | 020 |
| DR-12 | JSON import front-end | 015, 017 |
| DR-13 | Compile-time moniker resolution | 016 |
| DR-14 | Delegate steps rejected at import | 018 |
| DR-15 | Behavioral round-trip gate | 019 |
| DR-16 | Contracts twin + mechanical parity | 006, 022 |
| DR-17 | `ontology.abstained` core chokepoint | 006, 021 |
| DR-18 | Schema-evolution guardrail | 023 (+ per-task RS0016/JsonSchemaDiff duties) |

### Tasks

Contracts-track tasks (001–006) serialize — they all touch `main.tsp` and regenerate `schemas/`/`Generated/` — but the track as a whole runs parallel to tracks G (007–010) and O (020–021).

### Task 001: `GateClass` closed enum in Contracts TypeSpec

Author the `GateClass` closed enum as a TypeSpec model and regenerate the contracts projections so both runtimes consume one typed gate identity.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-1
**Files:** `src/Strategos.Contracts/Gates/GateClass.tsp` (new), `src/Strategos.Contracts/main.tsp`, regenerated `schemas/` + `Generated/`, `src/Strategos.Contracts.Tests/Gates/GateClassSchemaTests.cs` (new)
**Verification:** `TspCompileTests`-family compile pass; emitted C# enum carries kebab `[JsonStringEnumMemberName]` values (shape test); `ZodConsumabilityTests` extended to the new schema; integration: cross-product round-trip script green.
**Dependencies:** None · **Parallelizable:** Yes (vs tracks G/O; serial within contracts track)

### Task 002: `GateDeclaration` + `GateReliability` models

Add the `GateDeclaration` record with its optional measured-reliability annotation — provenance-required, sealed, immutable — plus the corpus zero-reliability assertion.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-2
**Files:** `src/Strategos.Contracts/Gates/GateDeclaration.tsp` (new), regenerated projections, `src/Strategos.Contracts.Tests/Gates/GateDeclarationSchemaTests.cs` (new; schema-required `source`, sealed/init shape via `EmitterShapeTests`), `src/Strategos.Tests/` (fixture corpus asserts zero reliability blocks), `InvariantGuardTests`
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING assertion; integration: corpus re-validation.
**Dependencies:** 001 · **Parallelizable:** No (contracts track)

### Task 003: Workflow IR born speaking gate classes — additive `gates` + `gateId`

Extend the workflow wire IR with optional `gates` and `gateId` so gate declarations flow through the shared IR to both runtimes from birth.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-3
**Files:** `src/Strategos.Contracts/Workflow/WorkflowDefinitionV1.tsp`, `Workflow/StepDefinition.tsp`, `src/Strategos.Contracts.Tests/Workflow/GateSlotSchemaTests.cs` (new; dangling `gateId` contract-validation), regenerated projections
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING; existing #53 fixtures re-validate byte-unchanged; integration suite over Contracts.Tests.
**Dependencies:** 002 · **Parallelizable:** No (contracts track)

### Task 004: `ForkTrigger` closed enum + evidence-ref models

Author the closed `ForkTrigger` enum with per-trigger evidence-ref requirements and a versioned schema marker admitting a future `exploratory` member additively.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-8
**Files:** `src/Strategos.Contracts/Workflow/ForkTrigger.tsp` (new), version marker + evolution doc-comment, `src/Strategos.Contracts.Tests/Workflow/ForkTriggerSchemaTests.cs` (new; missing evidence refs fail), regenerated projections
**Verification:** scoped tests + kill-probe; schema version marker asserted; Zod consumability; `JsonSchemaDiff` NON-BREAKING.
**Dependencies:** 003 · **Parallelizable:** No (contracts track)

### Task 005: `DiagnosticForkDefinition` wire shape

Add the `DiagnosticForkDefinition` structural shape to the workflow wire IR as an additive, moniker-only contract (INV-8).
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-10 (wire half)
**Files:** `src/Strategos.Contracts/Workflow/Structural.tsp` (or new `.tsp`), `WorkflowDefinitionV1.tsp` additive slot, `src/Strategos.Contracts.Tests/Workflow/DiagnosticForkSchemaTests.cs` (new; INV-8: monikers only — no CLR-type-shaped fields)
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING.
**Dependencies:** 004 · **Parallelizable:** No (contracts track)

### Task 006: Abstention union + `ontology.abstained` event in Contracts

Author the abstention response union and the `ontology.abstained` event envelope as Contracts TypeSpec models on the discriminated-union emission path.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-16 (twin half), DR-17 (event shape)
**Files:** `src/Strategos.Contracts/Ontology/AbstentionResponse.tsp` (new — union via const-discriminator pattern), `Events/OntologyAbstained.tsp` (new), `main.tsp`, regenerated projections, `src/Strategos.Contracts.Tests/Ontology/AbstentionUnionSchemaTests.cs` (new; Zod consumability)
**Verification:** scoped tests + kill-probe; discriminated-union emission shape (`[JsonPolymorphic]` abstract record) asserted; `JsonSchemaDiff` NON-BREAKING.
**Dependencies:** 005 (main.tsp serialization only) · **Parallelizable:** No (contracts track)

### Task 007: `RequireConfidence` composes instead of replacing

**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-6
**Files:** `src/Strategos/Builders/StepConfigurationBuilder.cs`, both-orders regression tests in `src/Strategos.Tests/`
**Verification:** scoped tests + `check_test_adequacy` kill-probe.
**Dependencies:** None · **Parallelizable:** Yes

### Task 008: Fork-path confidence gating lowers into the saga

Lower fork-path `RequireConfidence`/`OnLowConfidence` into the generated fork handlers, mirroring the top-level confidence gate reference implementation.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-4
**Files:** `src/Strategos.Generators/Emitters/Saga/SagaStepHandlersEmitter.cs` (+ fork emitters), `src/Strategos.Generators.Behavioral.Tests/ForkPathConfidenceTests.cs` (new behavioral proof), `Parity/StepConfigParityTests.cs` (2 entries → `Lowered`), `Diagnostics/DeclaredButInertTests.cs`, byte-unchanged SagaDocument regression guard
**Verification:** real-host behavioral proof (below-threshold routes + `LowConfidenceRouted` appended in ES mode); kill-probe; full generator test suite.
**Dependencies:** None · **Parallelizable:** Yes (before 013 on the fork emitters)

### Task 009: Loop-body steps promoted to configured `StepModel` in the IR

**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-5 (IR half)
**Files:** `src/Strategos.Generators/Helpers/StepExtractor.cs` (`ParseLoopBody`/`ParseLoopBodyWithContext`), `Models/LoopModel.cs`, `Models/WorkflowModel.cs`, IR unit tests
**Verification:** scoped generator unit tests (config reaches `StepModel.Confidence`) + kill-probe; existing generator suite green (emission unchanged until 010).
**Dependencies:** None · **Parallelizable:** Yes

### Task 010: Nested-`RepeatUntil` confidence lowering

Parse loop-body configure lambdas and lower nested-`RepeatUntil` `OnLowConfidence` into the generated loop handlers, retiring the structurally-undiagnosable deferral.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-5 (lowering half)
**Files:** loop condition/handler emitters, `src/Strategos.Generators.Behavioral.Tests/NestedRepeatUntilConfidenceTests.cs` (new behavioral proof), `StepConfigParityTests` (`OnLowConfidence(nested-RepeatUntil)` → `Lowered`), `docs/deferred-features.md`
**Verification:** real-host behavioral proof; kill-probe; AGWF022 diagnosability test for any remaining inert loop-body member.
**Dependencies:** 009 · **Parallelizable:** No

### Task 011: Fork/compensation DSL edge — builder surface

Introduce the `AllowDiagnosticFork` builder surface expressing anchor, permitted triggers, evidence refs, compensation seed, and the `maxForks` bound.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-7
**Files:** `src/Strategos/Builders/DiagnosticForkBuilder.cs` (new) + abstractions, `src/Strategos.Tests/Builders/DiagnosticForkBuilderTests.cs` (new), `PublicAPI.Unshipped.txt`, `InvariantGuardTests`, compile-error inexpressibility tests (trigger + evidence refs mandatory), XML lowering-contract docs
**Verification:** scoped tests + kill-probe; INV-4 nomenclature check against the invariants skill; parity-guard entries added in the same PR (no silent declared-but-inert).
**Dependencies:** 004 (`ForkTrigger` contract type) · **Parallelizable:** Yes (after 004)

### Task 012: `DiagnosticForkModel` — generator IR + parser

**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-9 (IR half)
**Files:** `src/Strategos.Generators/Models/` (new model), `FluentDslParser.cs` + extractor for the new surface, IR unit tests
**Verification:** scoped tests + kill-probe.
**Dependencies:** 011 · **Parallelizable:** No

### Task 013: Fork-edge saga lowering — guard, events, compensation composition

Emit fork handling, the `maxForks` guard, the `WorkflowForked` event, and compensation seeding from the `DiagnosticForkModel` into the saga.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-9 (lowering half)
**Files:** saga component emitters (`SagaStepHandlersEmitter.cs`, `SagaCompensationComponentEmitter.cs`), `EventsEmitter.cs` (`{Pascal}WorkflowForked`, ES-gated), `src/Strategos.Generators.Behavioral.Tests/DiagnosticForkLoweringTests.cs` (new; maxForks blocked→human escalation; compensation composes with #140 merged trigger site), SagaDocument byte-unchanged guard
**Verification:** real-host behavioral proofs; kill-probe; full generator suite + Behavioral.Tests build (the real semantic check for generator output).
**Dependencies:** 012, 008 (same emitter files) · **Parallelizable:** No

### Task 014: Fork edge exports — `ToContract()` projection + corpus fixtures

Project the diagnostic fork edge through `ToContract()` and extend the fixture corpus with fork-edge fixtures.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-10 (projection half)
**Files:** `src/Strategos/Contracts/WorkflowDefinitionProjection.cs`, `ProjectionExhaustivenessTests`/`ProjectionStepKindMappingTests`, `src/Strategos.Tests/FixtureExport/ForkEdgeFixtureTests.cs` (new fork-edge fixtures)
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING; fixture corpus regenerates cleanly.
**Dependencies:** 011, 005 · **Parallelizable:** No

### Task 015: Import front-end — `AdditionalFiles` provider + `WireIrReader`

Register the `AdditionalFiles` provider and `WireIrReader` so JSON workflow definitions enter the incremental generator with typed failure diagnostics.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-12 (ingestion half)
**Files:** `src/Strategos.Generators/` (new reader + provider registration in `WorkflowIncrementalGenerator.cs`), `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` (malformed-input diagnostic, next free AGWF id) + regeneration, `src/Strategos.Generators.Tests/Import/WireIrReaderTests.cs` (new generator-driver tests: malformed JSON ⇒ diagnostic, never a crash)
**Verification:** scoped tests + kill-probe; incremental-generator cache test (JSON edit invalidates).
**Dependencies:** 006 (contracts track settled — wire shapes + catalog file) · **Parallelizable:** Yes (after contracts track)

### Task 016: Compile-time moniker resolution + diagnostics

Resolve wire monikers to CLR step types against the compilation symbol table with stable diagnostics for miss and ambiguity.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-13
**Files:** `src/Strategos.Generators/Import/WireMonikerResolver.cs` (new), `src/Strategos.Generators.Tests/Import/WireMonikerResolverTests.cs` (new), `AgwfCatalog.tsp` (unresolvable + ambiguous ids) + regeneration, analyzer tests (happy path, miss, deterministic ambiguity listing, CS0101-class duplicate coverage)
**Verification:** scoped tests + kill-probe; INV-8 assertion (no CLR `Type` persisted into contract state).
**Dependencies:** 015 · **Parallelizable:** No

### Task 017: `WireToModelBridge` — JSON → `WorkflowModel` → same emitters

Bridge the parsed wire IR into `WorkflowModel` so JSON-authored workflows lower through the identical saga emitters as C#-authored ones.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-12 (bridge half), DR-3 (gates preserved through import)
**Files:** `src/Strategos.Generators/Import/WireToModelBridge.cs` (new), `src/Strategos.Generators.Tests/Import/SingleLoweringPathGuardTests.cs` (new architecture guard), `src/Strategos.Generators.Behavioral.Tests/JsonWorkflowImportTests.cs` (new real-host proof)
**Verification:** real-host behavioral proof; kill-probe; full generator + Behavioral.Tests suites.
**Dependencies:** 016, 009 (LoopModel shape), 012 (fork model importable) · **Parallelizable:** No

### Task 018: Delegate-step rejection at import

**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-14
**Files:** bridge rejection path, `AgwfCatalog.tsp` (rejection id) + regeneration, `docs/deferred-features.md` (re-binding registry deferral → #100 follow-on), diagnostic tests (names step + `lambda` marker; no saga emitted)
**Verification:** scoped tests + kill-probe.
**Dependencies:** 017 · **Parallelizable:** No

### Task 019: Behavioral round-trip gate over the #53 corpus

Prove export→import→compile equivalence over the whole #53 corpus, partitioning cleanly into imported-and-equivalent vs delegate-rejected.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-15, DR-3 (gates round-trip)
**Files:** `src/Strategos.Tests/FixtureExport/RoundTripEquivalenceTests.cs` (new), `src/Strategos.Generators.Behavioral.Tests/RoundTripBehavioralTests.cs` (new), CI wiring alongside fixture-export tests
**Verification:** corpus partitions cleanly (declarative ⇒ imported-and-equivalent; delegate-bearing ⇒ DR-14 rejection; no third bucket); generated-source equivalence full corpus + real-host equivalence representative subset; integration suite.
**Dependencies:** 017, 018, 014 · **Parallelizable:** No

### Task 020: Abstention union + `RecordRef` in `Strategos.Ontology.MCP`

Implement the closed abstention union and `RecordRef` beside `QueryResultUnion`, with empty-citation unconstructibility and retrieval-decided nulls.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-11
**Files:** new records beside `QueryResultUnion.cs`, constructor guards (empty-citations unconstructible), advertised output schema (`minItems: 1`), `src/Strategos.Ontology.MCP.Tests/AbstentionUnionTests.cs` (new; empty retrieval ⇒ `NoAnswerRecorded` + nearest records), `PublicAPI.Unshipped.txt`, `InvariantGuardTests`
**Verification:** scoped tests + kill-probe; discriminator emission test; INV-8 (string identity pair, no CLR types); no new package refs (INV-2 posture).
**Dependencies:** None · **Parallelizable:** Yes

### Task 021: `IOntologyAuditSink` + core-chokepoint abstained emission

Add the minimal `IOntologyAuditSink` abstraction and emit the abstained record at the core decision site so no entry point can skip it.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-17 (emission half)
**Files:** `src/Strategos.Ontology/` (sink abstraction + no-op default), core decision-site emission, `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs` (wiring), `src/Strategos.Ontology.Tests/OntologyAuditSinkTests.cs` (new; direct core entry), `src/Strategos.Ontology.MCP.Hosting.Tests/AbstainedEmissionTests.cs` (new; MCP entry), `PublicAPI.Unshipped.txt`
**Verification:** scoped tests + kill-probe; integration across the hosting seam; `IsAotCompatible` + dependency-set unchanged assertions; payload carries counts, not record contents.
**Dependencies:** 020 · **Parallelizable:** No

### Task 022: Schema-conformance parity — C# union vs Contracts schema

Pin C#-union ↔ Contracts-schema parity mechanically: serialization of both arms validates against the emitted JSON Schema, red on drift in either direction.
**Risk Tier:** medium · **Boundary Touching:** true
**Implements:** DR-16 (parity half)
**Files:** `src/Strategos.Ontology.MCP.Tests/AbstentionSchemaConformanceTests.cs` (new; serializes both union arms + edge cases against the Contracts-emitted JSON Schema, fails on drift in either direction)
**Verification:** scoped tests + kill-probe (mutate one side ⇒ red).
**Dependencies:** 006, 020 · **Parallelizable:** Yes (after both)

### Task 023: Bundle close-out — version, CHANGELOG, guardrail sweep

Sweep the bundle close-out — Contracts version bump, CHANGELOG, deferred-features refresh — and confirm the contract-first sequencing (mirroring #153) held across the schema delta.
**Risk Tier:** low · **Boundary Touching:** false
**Implements:** DR-18
**Files:** `Strategos.Contracts.csproj` (`<ContractsVersion>` 0.3.0 → 0.4.0), CHANGELOG.md (bundle entry), `docs/deferred-features.md` final sweep, failure-mode enumeration doc-check (every DR-18 channel names its covering test), `JsonSchemaDiff` CI-run confirmation across the full schema delta
**Verification:** static analysis; CI schema-diff job green; publish-verify unaffected.
**Dependencies:** 019, 021, 022, 013 (all tracks landed) · **Parallelizable:** No (final)

### Parallelization

Three tracks run concurrently from the start; integration stays linear per wave (ff-merge discipline):

- **Wave 1 (parallel worktrees):** contracts track head (001→002→003→004→005→006, serialized internally), 007, 008, 009, 020
- **Wave 2:** 010 (after 009), 011 (after 004), 021 (after 020)
- **Wave 3:** 012→013 (013 also waits on 008), 014 (after 011+005), 022 (after 006+020), 015 (after 006)
- **Wave 4:** 016→017 (017 also waits on 009+012) →018
- **Wave 5:** 019 (after 014+017+018), then 023

**Critical path:** 001→…→006→015→016→017→018→019→023 (the contracts track feeds the import front-end; #145/#151 lowering and the ontology track hang off it in parallel).
