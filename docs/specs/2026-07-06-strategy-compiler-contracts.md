# Spec: Strategy-Compiler Contract Layer (v2.10.0 bundle)

**Date:** 2026-07-06 · **Feature:** `strategy-compiler-contracts` · **Depth:** deep · **Revision:** 2 (post plan-review round 1)
**Inputs:** roadmap `lvlup-sw/strategos#153` (strategy-compiler program) · issues #100 · #150 · #151 · #145 · #152 · consumer trackers `lvlup-sw/basileus#182` (addendum 2026-07-05/06) and `lvlup-sw/exarchos#1599` (coordination rule 6) · in-session exploration reports (Contracts/TypeSpec pipeline, generator lowering surface, ontology MCP response shapes) · plan-review round 1 (3-voter adversarial panel, all gaps folded in)

> One unified artifact: `## Requirements` is the DR-N source; `## Decomposition` maps tasks → DR-N within this same document.
> Ships as **v2.10.0 re-themed** to the contract layer; edge remnants #128/#130 are bumped out of the milestone (verified non-blocking, see Open Questions).


## Problem Statement

Both runtimes are converging on `Strategos.Contracts` as the shared IR spine (basileus consumes it as the U-8 source-generated saga layer; exarchos as the #1258 Workflow Builder IR target), but four contract capabilities are missing, and #153's contract-first acceptance rule requires them to land here before any consumer IR phase:

1. **The workflow IR is export-only** (#100). `ToContract()` projects builder → `WorkflowDefinitionV1`, but nothing can execute or round-trip a JSON-authored workflow — the strategy-compiler endpoint (exarchos authors IR → basileus executes generated sagas) has no import half. No moniker→CLR resolution exists anywhere (LB-2 is a doc-comment reservation only).
2. **Gates have no typed identity or reliability slot** (#150). Verifier FPR is the denominator of achievable autonomous horizon; basileus #392 (`horizon:now`) and exarchos #1646 (v2.12.0, priority:high) both measure it and need one shared taxonomy to declare it against. Nothing named `GateClass`/`GateDeclaration` exists in code today (#150 also names `ExecutionProfiles`; profile *composition* is consumer-side and explicitly out of scope here — see Open Questions).
3. **Fork/compensation is not a first-class DSL edge** (#151), and its base is unsound: fork-path `RequireConfidence`/`OnLowConfidence` reach the IR but are never lowered, and nested-`RepeatUntil` confidence config is dropped from the IR entirely — silently inert, structurally undiagnosable (#145, the AGWF022 `Deferred` debt).
4. **Ontology responses cannot express licensed abstention** (#152). A calibrated model must sometimes hallucinate closed-book; the system-level escape is retrieval plus a typed null decided by the retrieval layer. No citation/abstention shape exists (grep: zero hits), and there is no mechanism making the abstention coverage metric unskippable.

## Chosen Approach

**Compiler-first** (Exploration, Approach A). `FromContract()` is not a runtime API — it is a **build-time front-end to the existing source generator**: JSON `WorkflowDefinitionV1` files enter the consumer's compilation as `AdditionalFiles`, a wire-IR→`WorkflowModel` bridge rehydrates them into the same IR the C# fluent-chain re-parser produces, and the identical saga emitters lower them. Moniker→CLR resolution happens at compile time against the compilation's symbol table; every failure mode is a stable build diagnostic. The importable subset is the **runtime-bindable-behavior-free** subset: delegate steps (`lambda:true`), branch points and loops (their conditions live only in the compiled definition class, evaluated via the runtime condition registry), validation predicates, and approval context factories are all **rejected with per-carrier diagnostics** (DR-14) — the honest generalization of the LB-1 lossiness class; re-binding mechanisms are deferred. INV-1 is preserved by construction — the only thing that ever produces execution is the Roslyn SG. Because `Strategos.Generators` is an isolated netstandard2.0 analyzer that cannot reference the Contracts assembly, ingestion uses hand-authored internal wire-DTO twins plus a vendored minimal JSON reader, with the twins pinned to the Contracts-emitted JSON Schema by a conformance test (DR-12).

The contract shapes land contract-first in TypeSpec (the canonical source; C# and JSON Schema are emitted projections): the `GateClass`/`GateDeclaration` family plus an additive `gates` slot on `WorkflowDefinitionV1` (#150), the fork/compensation edge with its closed trigger enum split into **declaration** (permitted triggers + evidence-ref schema) and **occurrence** (runtime evidence values) shapes (#151), and the abstention response union (#152). The saga-lowering debt (#145) is paid first so #151's fork edge lowers onto sound machinery. The ontology union is implemented natively in `Strategos.Ontology.MCP` with parity to the Contracts schema pinned by a mechanical conformance test, and the abstention chokepoint is **composer-owned**: the union's constructors are internal and a single composer factory is the only producer, emitting `ontology.abstained` through an audit sink — bypassing the composer means being unable to construct the union at all (DR-11/17; core is untouched — plan-review established no core decision site exists).

## Requirements

The DR-N identifiers below are the single source the decomposition traces against.

### DR-1: `GateClass` closed enum in Contracts (#150)

A typed, closed gate-class identity — a type, never a string — shared by both runtimes.
TypeSpec enum with **snake_case** wire names (`typecheck | lint | scoped_test | full_suite | mutation_adequacy | merge_gate | llm_judge`), emitted as a C# enum via the existing `[JsonStringEnumMemberName]` + `JsonStringEnumConverter<T>` path (the #98 precedent).
Snake_case deviates from #150's illustrative kebab list deliberately: existing wire enum casings are snake_case/camelCase (nothing kebab), and `ForkTrigger` (DR-8) is snake_case by issue text — one casing feeds the shared Zod consumer.

**Acceptance criteria:**
- `GateClass` model exists in a new `.tsp` under `src/Strategos.Contracts/`, imported from `main.tsp`; `tsp compile` + codegen emit the JSON Schema and the C# enum with snake_case wire values.
- An unclassified gate is unrepresentable: `GateDeclaration.class` is non-optional and enum-typed in both projections.
- `ZodConsumabilityTests` / cross-product round-trip covers the new schema file.
- Member-list freeze: the proposal comment on #150 (tagging basileus#392 + exarchos#1646) is posted before implementation starts; absent objection by implementation start, the list above is the timeboxed default — later additions follow the DR-18 enum-evolution policy.

### DR-2: `GateDeclaration` with measured-reliability annotation (#150)

`GateDeclaration { class: GateClass, id, reliability?: { fpr, sampleSize, asOf, source } }` as a sealed emitted record.
`reliability` is data measured elsewhere (telemetry projections), never hand-authored: `source` carries the producing projection's provenance and is required whenever the annotation is present.

**Acceptance criteria:**
- TypeSpec model + emitted `sealed record` with `{ get; init; }` (INV-6/7, proven by the existing `EmitterShapeTests` reflection guard, which auto-covers all `Generated/` records).
- `reliability.source` is schema-required within the reliability object; a reliability block without `source` fails schema validation (test).
- No authoring channel can smuggle reliability: (a) no builder/DSL surface can author reliability values; (b) the #53 fixture corpus (builder-produced) contains zero reliability blocks (asserted); (c) **the import front-end rejects any gate declaration carrying a `reliability` block with a stable diagnostic** (reliability enters definitions only from telemetry projections, never from authored JSON) — this is #150's machine-check at the one authoring channel this bundle creates (DR-14 family).
- `JsonSchemaDiff` classifies the whole family as NON-BREAKING against the prior schema set (CI diff gate green).

### DR-3: Workflow IR is born speaking gate classes (#150 → #100)

`WorkflowDefinitionV1` gains an optional `gates?: GateDeclaration[]` and the existing `GateStep` wire arm gains an optional `gateId?` back-reference, both additive.
This is the composition point the roadmap names: declarations flow through the shared IR to both runtimes before either retrofits its own.
Gate declarations are **consumer-plane data** (resolvers/telemetry read them); the generated saga does not consume them.

**Acceptance criteria:**
- Both fields optional; `JsonSchemaDiff` reports NON-BREAKING; existing #53 fixtures re-validate unchanged.
- A dangling `gateId` (referencing an id absent from `gates`) is a **semantic** rule not expressible in JSON Schema: the import front-end rejects it with a stable diagnostic (DR-13 family), and the Contracts README documents it as a consumer-side semantic check (Zod consumers refine it themselves).
- The import front-end accepts gate-bearing definitions and the generated saga is unaffected: a gate-bearing JSON workflow imports identically to its gate-free twin (test), proven over **hand-authored JSON import fixtures** (gates are data-authored, never builder-produced, so the builder corpus cannot carry them — see DR-15's import-fixture family).

### DR-4: Fork-path confidence gating lowers into the saga (#145 gap A)

`RequireConfidence`/`OnLowConfidence` declared on a fork-path step lower into the generated fork handlers, mirroring the top-level reference implementation (`StepCompletedHandlerEmitter.EmitConfidenceGatedHandler`).

**Acceptance criteria:**
- Below-threshold confidence on a fork-path step routes to the declared handler chain and (EventSourced mode) appends `{Pascal}LowConfidenceRouted` — behavioral proof on the real-host harness (`Strategos.Generators.Behavioral.Tests`), not golden-file.
- `StepConfigParityTests`: `RequireConfidence(fork-path)` and `OnLowConfidence(fork-path)` move `Deferred` → `Lowered`, each naming its behavioral proof.
- AGWF022 no longer fires for fork-path confidence (`DeclaredButInertTests` updated); the diagnostic remains for any still-inert surface.
- SagaDocument (non-EventSourced) output for workflows without fork confidence is byte-unchanged (regression guard).

### DR-5: Nested-`RepeatUntil` step config enters the IR and lowers (#145 gap B)

Loop-body steps are promoted from bare `StepInfo` to configured `StepModel` during step extraction (the configure lambda is parsed, not just the step name), `LoopModel` carries the step models while keeping `FirstBodyStepName`/`LastBodyStepName` as computed projections (the `ForkPathModel` precedent — existing emitter call sites compile unchanged), and loop-body `OnLowConfidence` lowers into the loop's generated handlers.

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
- Builder surface expresses: anchor step(s), **permitted triggers with their evidence-ref schema** (which fields a future occurrence must carry — not runtime values, which don't exist at authoring time), compensation seed, `maxForks` bound; sealed builder types; sealed-type guard coverage.
- The construct is inexpressible without declaring at least one permitted trigger: required builder parameters (API-shape tests), plus compilation-refusal cases exercised through the existing Roslyn harness in `Strategos.Generators.Tests`.
- XML docs state the lowering contract (what the SG emits) — no declared-but-inert introduction: the parity guard gains entries for every new declarable member in the same PR (`Lowered` or explicitly `Deferred` with an AGWF022 guarantee).

### DR-8: Closed fork-trigger contract — declaration and occurrence split (#151)

`ForkTrigger` closed enum `{ ratification_failure, gate_contradiction, operator_explicit }` in Contracts TypeSpec.
Two shapes, deliberately distinct: the **declaration** side (in `DiagnosticForkDefinition`, DR-10) names permitted triggers and the evidence-ref *fields* each requires; the **occurrence** side (`ForkOccurrence`, the `{Pascal}WorkflowForked` payload analog and exarchos `workflow.forked` sibling) carries the runtime evidence *values* (e.g. `ratification_failure` ⇒ provisional-stamp event id + non-empty taint set).
Evidence values exist only at runtime, so completeness is enforced where occurrences are born: the generated guard (DR-9), never at workflow authoring.
The trigger-enum schema is versioned so a future budget-bounded `exploratory` member (basileus PR #401 rec L-4) is additive under the DR-18 enum-evolution policy, not a redesign.

**Acceptance criteria:**
- TypeSpec enum + `ForkOccurrence` model (snake_case wire values) emitted through the standard pipeline; Zod-consumable.
- An unjustified fork occurrence is unrepresentable at the only place occurrences are produced: the generated guard refuses to fork without the declared trigger's evidence refs (behavioral proof, DR-9); schema validation rejects an occurrence missing its required fields (test).
- Schema carries an explicit version marker; the additive-evolution note is in the model doc-comment.

### DR-9: The fork edge lowers into the generated saga (#151)

The SG emits fork handling, compensation seeding, guard wiring, and events for the DR-7 edge — sagas are never hand-written (U-8/INV-1).

**Acceptance criteria:**
- Generated guard enforces `maxForks` (precedent: `LoopModel.MaxIterations` forced-exit); exceeding it routes to a blocked/human-escalation terminal, behavioral proof on the real host.
- Generated guard requires the declared trigger + evidence refs before forking — a fork attempt without them is refused (behavioral proof); this is the DR-8 occurrence chokepoint.
- EventSourced mode appends a `{Pascal}WorkflowForked` event carrying the `ForkOccurrence` payload at the single decision site (EventsEmitter pattern, `IsEventSourced`-gated); SagaDocument mode byte-unchanged for workflows without the edge.
- The edge round-trips through the wire IR (DR-10) and the import front-end compiles it back to an equivalent saga (import-fixture proof).
- Compensation seeded by the fork composes with the existing `Compensate`/`OnFailure` merged trigger site (#140) — proven behaviorally.

### DR-10: Fork edge on the wire (#151 → #100, exarchos #1648/#1258)

The declarative fork edge is a contract shape: an additive `DiagnosticForkDefinition` (anchor refs, permitted triggers, per-trigger evidence-ref schema, `maxForks`, compensation seed moniker) on `WorkflowDefinitionV1`, exportable by `ToContract()` and importable by the DR-12 front-end.
This is the shared combinator exarchos #1258 lowers to and basileus #394 consumes; `ForkOccurrence` (DR-8) is its runtime companion in the Events family.

**Acceptance criteria:**
- `ToContract()` projects the DR-7 builder edge to the wire shape (extending `WorkflowDefinitionProjection`; the throwing `default` + exhaustiveness tests force explicit wiring).
- `JsonSchemaDiff`: NON-BREAKING addition; fixture corpus gains fork-edge fixtures (new combinator tag or within the existing 8).
- INV-8: all step/type references in the shape are string monikers, never CLR types.

### DR-11: Abstention response union with a composer-owned producer (#152)

Closed response union `Answer { content, citations: RecordRef[] (non-empty) } | NoAnswerRecorded { nearestRecords: RecordRef[] }` in `Strategos.Ontology.MCP`, mechanically following the `QueryResultUnion` `[JsonPolymorphic]` pattern; `RecordRef` is the polyglot string identity pair (descriptor name + projected record id, à la `TraversalEndpoint`) — never a CLR type (INV-8).
The union's constructors are **internal**: the only producer is a single composer (working name `OntologyAnswerComposer`, INV-4 check at implementation) whose factory takes retrieval results and decides `Answer` vs `NoAnswerRecorded` — the null is decided by the retrieval layer by construction, and the composer is where the abstained event is emitted (DR-17).
Strategos's existing tools return result sets, not answers; the composer is the primitive any answering surface (a future Strategos answer tool, or hosts embedding the ontology layer) builds on.

**Acceptance criteria:**
- Sealed records, `_meta: ResponseMeta` carried (INV-3), discriminator emitted; sealed-type guard coverage.
- A free-text uncited answer is unrepresentable: constructors internal; the composer refuses `Answer` with empty citations (guard clause) and the advertised output schema carries `minItems: 1` — both tested.
- Composer decision proof: empty retrieval ⇒ `NoAnswerRecorded` with nearest records populated; non-empty retrieval with citations ⇒ `Answer`; no code path yields `NoAnswerRecorded` while hiding results (tests).
- Lands independently of #115 (no descriptor-primitive changes; `Strategos.Ontology` core untouched) — verified by the ontology exploration; #115 stays open, unblocked.

### DR-12: JSON workflows compile — the import front-end (#100)

`WorkflowDefinitionV1` JSON files registered as `AdditionalFiles` are parsed by a wire-IR→`WorkflowModel` bridge in `Strategos.Generators` and lowered by the identical saga emitters as C#-authored workflows.
**Scope:** the runtime-bindable-behavior-free subset — linear/fork flows, retry/timeout/compensation/confidence step configuration, context-free approval points, gates, and diagnostic-fork edges. Definitions carrying delegate steps, branch points, loops, validation predicates, or approval context are rejected per DR-14.
**Ingestion mechanism:** `Strategos.Generators` is an isolated netstandard2.0 analyzer that cannot reference the Contracts assembly or its STJ-attributed records (the documented `AgwfCodes.g.cs` source-link rationale). The reader therefore uses hand-authored **internal wire-DTO twins** plus a **vendored minimal JSON reader** (zero analyzer package dependencies); the twins are pinned to the Contracts-emitted JSON Schema by a conformance test in `Strategos.Generators.Tests` (net-current, which *can* reference Contracts) — same mechanical-parity pattern as DR-16.

**Acceptance criteria:**
- A JSON-only workflow (importable subset) in a consumer compilation produces `Add{Pascal}Workflow()` / `Start{Pascal}Command` / saga / events behaviorally identical to its C#-authored twin (real-host proof).
- The bridge feeds the existing `WorkflowModel` IR — zero forked emitter logic (architecture-guard: fork/loop/confidence emitters have a single call path).
- Incremental-generator correctness: editing the JSON invalidates and regenerates (cache test).
- Malformed JSON, schema-invalid input, and **`schemaVersion` skew** (anything other than the supported `"1.0"`) each produce their own stable diagnostic (next free AGWF ids, monotonic — catalog updated via `AgwfCatalog.tsp`), never a generator crash (generator-driver tests).
- The DTO-twin conformance test fails when the twins drift from the Contracts schema in either direction.

### DR-13: Compile-time moniker resolution with stable diagnostics (#100)

The wire contract's simple-name monikers resolve to CLR types via the compilation's symbol table.
Resolution failures are build-time diagnostics, allocated monotonically above the current AGWF022 ceiling: unresolvable moniker, ambiguous moniker (two+ types sharing the simple name), dangling `gateId` (DR-3's semantic rule), and the DR-14 rejection family.

**Acceptance criteria:**
- Happy path: moniker resolves to exactly one accessible `INamedTypeSymbol` implementing the expected step contract.
- Unresolvable ⇒ diagnostic with the moniker + JSON file path; ambiguous ⇒ diagnostic listing all candidates (deterministic order); dangling `gateId` ⇒ diagnostic naming the id; all have analyzer tests and catalog entries.
- INV-8 honored: resolution consumes the moniker string; nothing persists a CLR `Type` back into contract state.
- The one-step-CLR-type-per-workflow-definition constraint (CS0101 class) holds for imported workflows — violation surfaces as the same build error class as C#-authored duplicates, covered by a test.

### DR-14: Runtime-bindable behavior is rejected at import — declarative-only contract (#100/LB-1 generalized)

The LB-1 lossiness class is bigger than delegate steps: branch conditions and loop exit conditions live only in the compiled definition class (evaluated via the runtime condition registry), validation predicates are descriptive strings on the wire, and approval context factories are dropped at export **silently** today.
This DR makes the whole class explicit: export marks what it drops, import rejects what it cannot bind.

**Acceptance criteria:**
- Export lossiness is marked: `ApprovalDefinition` gains an additive `hasContext: true` marker when a context factory was configured (mirroring `lambda: true`); branch points, loops, and validation predicates are detectable by shape presence and need no marker. A parity-style exhaustiveness test pairs every projection drop-site with its wire marker or presence rule — a new drop-site without one fails the guard.
- Import rejects each carrier with its own stable diagnostic (delegate step, branch point, loop, validation predicate, approval-with-context, reliability-bearing gate declaration per DR-2): diagnostic names the construct and the JSON path; no saga is emitted for that workflow.
- All markers are additive (`JsonSchemaDiff` NON-BREAKING).
- Every deferral (condition re-binding, lambda re-binding registry, context re-binding) is recorded in `docs/deferred-features.md` with the #100 follow-on pointer.

### DR-15: Behavioral round-trip gate over the #53 corpus (#100)

Round-trip fidelity is proven behaviorally where it is claimed, and rejection is proven where equivalence is impossible — with a mechanical two-bucket partition and no silent third bucket.

**Acceptance criteria:**
- **Partition assertion:** every `WorkflowCorpus` fixture (≥100) lands in exactly one bucket — (a) importable subset ⇒ export→import→compile succeeds, or (b) carrier-bearing (branch/repeatUntil/approval-with-context/…) ⇒ the specific DR-14 diagnostic fires. A fixture in neither bucket fails the gate. (The corpus's 26 branch, 26 repeatUntil, and context-bearing awaitApproval fixtures populate bucket (b) — the rejection proof is not vacuous.)
- **Behavioral equivalence:** for each importable combinator family, a hand-authored `[Workflow]` C# source twin and its exported JSON compile to behaviorally-identical sagas on the real host (the corpus itself is runtime builder invocations, not parseable literal source — twins are the honest baseline).
- **IR fidelity:** across the full importable partition, the bridge's `WorkflowModel` matches the JSON content field-for-field (steps, ordering, config values, gates, fork edges) — cheap structural assertion, full breadth.
- **Import-fixture family:** hand-authored JSON fixtures (distinct from the builder-produced corpus, whose charter forbids hand-written JSON) cover what the builder cannot produce: delegate-step rejection, gate-bearing definitions (DR-3), reliability-bearing rejection (DR-2), dangling `gateId`, `schemaVersion` skew.
- The gate is CI-wired alongside the existing fixture-export tests.

### DR-16: Contracts twin + mechanical parity for the abstention union (#152)

The abstention union exists in Contracts TypeSpec (for basileus #155 and exarchos Zod derivation) and in `Strategos.Ontology.MCP` C# (the ontology layer does not reference the Contracts package); divergence is prevented mechanically, not by convention — on **both** halves, the response union and the abstained event payload.

**Acceptance criteria:**
- TypeSpec union emitted through the standard pipeline (discriminated-union path: abstract record + `[JsonDerivedType]`).
- A schema-conformance test serializes the Ontology.MCP union (both arms, edge cases) and validates the output against the Contracts-emitted JSON Schema — the build fails on drift in either direction.
- The same conformance treatment covers the event half: the hosting-mapped abstained payload validates against the `Events/OntologyAbstained.tsp`-emitted schema (test).
- `JsonSchemaDiff`: NON-BREAKING addition.

### DR-17: `ontology.abstained` emitted at the composer chokepoint (#152)

A minimal audit-sink abstraction (`IOntologyAuditSink`, no-op default) in `Strategos.Ontology.MCP`, consumed exclusively by the DR-11 composer: when the composer produces `NoAnswerRecorded`, it emits the abstention record — and because the union's constructors are internal, **bypassing the composer means being unable to construct the union at all**. That is the mechanical chokepoint (plan-review corrected the earlier "core decision site" framing: core has no answer/abstain site and cannot reference the union).
The event's wire shape lands in Contracts `Events/` (envelope-compatible, `type: "ontology.abstained"`).
**Cross-runtime scope:** within any host using Strategos's composer, abstention coverage is unskippable; implementers coding directly against the wire schema (basileus #155's Why-Context service) enforce their own emission per the contract's server-side-emission clause — tracked on basileus#155, not enforceable from this repo.

**Acceptance criteria:**
- Emission happens inside the composer: every `NoAnswerRecorded` production emits through the sink, MCP-hosted or direct library use alike (tests through both entry points); no public API allows producing the union without traversing the composer.
- The sink abstraction adds no new package dependencies to `Strategos.Ontology.MCP` (its dependency set stays `Strategos.Ontology` + logging abstractions; `IsAotCompatible` unchanged); `Strategos.Ontology` core is untouched.
- Hosting wires a concrete sink; the no-op default keeps existing consumers source- and behavior-compatible.
- Contracts event model emitted + Zod-consumable; the abstained payload carries `nearestRecords` counts, not record contents (no data exfiltration through audit); the server-side-emission clause is stated in the model doc-comment.

### DR-18: Schema-evolution guardrail across the bundle (error/failure-mode requirement)

Every wire-visible addition in this bundle is additive and machine-verified as such — **including enums, which the current differ ignores** — and every failure mode at every new boundary is typed, not stringly or silent.

**Acceptance criteria:**
- `JsonSchemaDiff` (+ the CI driver) gains enum awareness: member removal or rename ⇒ BREAKING; member addition ⇒ flagged NOTICE (permitted on a Contracts minor bump with a consumer-notice release-notes line). Unit tests cover all three; the CI gate blocks BREAKING.
- Consumer-safety posture for closed enums is explicit: emitted converters stay strict (unknown member ⇒ `JsonException`, pinned by test), so enum additions require consumers to upgrade before producers emit new members — the upgrade-ordering rule is documented with the NOTICE policy. (A catch-all `unknown` member was rejected: it would un-close the closed set — see Alternatives.)
- `JsonSchemaDiff` CI gate green across all schema deltas (#150/#151/#152/#100 shapes); any BREAKING classification blocks merge.
- Contracts package version bumps 0.3.0 → 0.4.0 (additive minor per its independent versioning); product ships v2.10.0.
- New public API surfaces trip RS0016/RS0017 and update `PublicAPI.Unshipped.txt` deliberately — no suppressions; the three ontology projects (`Strategos.Ontology`, `.MCP`, `.MCP.Hosting`) currently lack PublicAPI tracking, so it is bootstrapped there first (DR-11/17 touch their public surface).
- Every new failure mode introduced by the bundle maps to a typed channel: build diagnostics (DR-12/13/14, incl. version skew and semantic rejections), guard-bounded escalation + evidence-required forking (DR-9), typed abstention (DR-11), schema-validation rejection (DR-2/8) — enumerated and each covered by at least one test.

## Technical Design

### Contracts (TypeSpec-first)

All new shapes are `.tsp` models imported from `main.tsp`; the codegen auto-picks them up (closed enums → snake_case-valued C# enums; unions → `[JsonPolymorphic]` abstract records; optional annotation slots → additive fields). New families: `Gates/` (DR-1/2/3), the fork-edge declaration + occurrence shapes (DR-8/10), the abstention union + `ontology.abstained` event (DR-16/17), and the DR-14 lossiness marker. The record **emitter** needs no changes; the **SchemaDiff tool** does — it gains enum awareness (DR-18), a tooling change beside the emitter, not to it.

### Generators

Two workstreams share the `WorkflowModel` IR seam. First, the #145 debt: fork emitters gain the confidence-gated handler emission (mirroring `StepCompletedHandlerEmitter`), and step extraction promotes loop bodies to configured `StepModel`s (`LoopModel` keeps computed name projections so existing call sites compile unchanged). Second, the #100 front-end: an `AdditionalFiles` provider + internal netstandard2.0 wire-DTO twins + a vendored minimal JSON reader (no analyzer package dependencies; twins schema-pinned from `Strategos.Generators.Tests`) + `WireToModelBridge` producing `WorkflowModel` — downstream emitters untouched. Import-time semantic checks (dangling `gateId`, reliability-bearing gates, carrier rejection, version skew) live in the bridge as diagnostics. The DR-7/9 fork edge extends `WorkflowModel` (`DiagnosticForkModel`), lowers in the fork/compensation component emitters with the evidence-required + `maxForks` guards, and projects to the wire in `WorkflowDefinitionProjection`. New diagnostics allocate AGWF023+ via `AgwfCatalog.tsp` (monotonic, never reused).

### Ontology

The union + `RecordRef` + `OntologyAnswerComposer` + `IOntologyAuditSink` all live in `Strategos.Ontology.MCP` beside `QueryResultUnion`; union constructors are internal, the composer is the sole producer and the emission chokepoint. Hosting wires a concrete sink in `OntologyServerToolFactory`. `Strategos.Ontology` core is untouched; no descriptor-primitive changes; #115 stays independent.

### Sequencing (contract-first, mirroring #153)

DR-1/2/3 + DR-8 + DR-16/17 wire shapes land first (unblocks basileus #392 and exarchos #1646 immediately); #145 (DR-4/5/6) precedes the fork-edge lowering (DR-7/9/10); the import front-end (DR-12–15) closes the bundle. #152's implementation (DR-11/17) runs as a parallel track.

## Integration Points

- `src/Strategos.Contracts/main.tsp` + new `.tsp` models — gate family, fork trigger/declaration/occurrence, abstention union, abstained event, approval `hasContext` marker
- `src/Strategos.Contracts/Workflow/WorkflowDefinitionV1.tsp` + `Structural.tsp` + `ApprovalFailureConfig.tsp` — additive `gates`, `gateId`, `DiagnosticForkDefinition`, `hasContext`
- `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` — AGWF023+ allocations
- `src/Strategos.Contracts/SchemaDiff/JsonSchemaDiff.cs` + `scripts/contracts-schema-diff.mjs` — enum awareness (DR-18)
- `src/Strategos/Contracts/WorkflowDefinitionProjection.cs` — fork-edge export + lossiness markers
- `src/Strategos/Builders/` (`StepConfigurationBuilder.cs`, new fork-edge builder) — DR-6/7
- `src/Strategos.Generators/Helpers/StepExtractor.cs` (`ParseLoopBody` :450 / `ParseLoopBodyWithContext` :500), `Models/LoopModel.cs`, `Models/WorkflowModel.cs` — DR-5, fork-edge model
- `src/Strategos.Generators/Emitters/Saga/SagaStepHandlersEmitter.cs`, `StepCompletedHandlerEmitter.cs`, `SagaCompensationComponentEmitter.cs`, `EventsEmitter.cs` — DR-4/9 lowering + events
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs` + new `Import/` (DTO twins, reader, resolver, bridge) — AdditionalFiles front-end, AGWF022 update, new diagnostics
- `src/Strategos.Generators.Tests/Parity/StepConfigParityTests.cs` + `Diagnostics/DeclaredButInertTests.cs` — parity moves
- `src/Strategos.Tests/FixtureExport/` + `Strategos.Generators.Behavioral.Tests/` — DR-15 round-trip gate, source twins, import-fixture family
- `src/Strategos.Ontology.MCP/` (union + composer + sink beside `QueryResultUnion.cs`), `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs` (sink wiring) — DR-11/17
- `PublicAPI.Unshipped.txt` per touched project (bootstrapped for the three ontology projects) + `docs/deferred-features.md` + CHANGELOG

## Exploration

Divergent loop (designDepth: deep). Three postures for #100 — the bundle's load-bearing fork — were scored across DIM-1..DIM-8 with the project invariants interleaved:

- **A — Compiler-first (chosen):** JSON as generator input via `AdditionalFiles`; same emitters; compile-time moniker resolution; runtime-bindable behavior rejected. Won on DIM-3 (zero new public runtime API; wire unchanged), DIM-4 (behavioral proof on the existing real-host harness), DIM-6 (INV-1 by construction — only the SG produces execution), DIM-7 (failures are loud build diagnostics).
- **B — Audit-first runtime API:** `FromContract()` + moniker registry, non-executable by contract. Rejected: mints a versioned-forever public API with no current consumer, proves fidelity only structurally, and fails the exarchos unifying discipline (churn the SDK would undo — the endpoint still needs the build-time path).
- **C — Shared front-end (both):** endpoint-complete but XL surface; dual maintenance from day one with no consumer for the runtime half.

Sub-fork (#152 event rail), two rounds: round 1 chose a core audit sink over a hosting-layer observer (bypassable) and shapes-only deferral (violates the chokepoint clause). **Plan-review round 1 refuted the core-sink premise** — no answer/abstain decision site exists in core, and core cannot reference the union — so the chokepoint moved to the **composer**: internal union constructors + a single producing factory that emits on abstention. The unskippable property survives in stronger form (bypassing the chokepoint = cannot construct the type); the layer moved.

Plan-review round 1 (3-voter adversarial panel, fresh-context, artifact-only) refuted revision 1 with 5 HIGH gaps — wire IR condition-lossiness (third bucket), the netstandard2.0 ingestion contradiction, the nonexistent core decision site, enum-blind schema diffing vs strict converters, and the import channel defeating reliability provenance — plus 10 MEDIUM/LOW. All are folded into this revision (DR-2/3/8/11/12/14/15/17/18 amended; tasks 024–026 added).

The opt-in discover bridge was surfaced and declined — the three in-session exploration reports (Contracts pipeline, generator lowering surface, ontology response shapes) grounded every approach in in-repo precedent; no external research pass was needed (no `correlationId` to cite).

## Alternatives considered

- **Runtime `FromContract()` API (Approach B)** — rejected above; revisit only when a consumer needs runtime rehydration that the build-time path cannot serve.
- **Shared front-end (Approach C)** — deferred, not rejected: the DR-12 bridge is internal, so exposing a read-only inspection API later is additive.
- **`GateClass` as an open string with a well-known-values list** — rejected: #150 mandates "a type, never a string"; closed enum + the DR-18 evolution policy instead.
- **Catch-all `unknown` enum member for forward compatibility** — rejected: it un-closes the closed set and turns every consumer switch into a silent default path; strict converters + machine-checked additive policy + upgrade ordering instead.
- **Core audit sink / hosting-layer observer for `ontology.abstained`** — both rejected: core has no decision site (plan-review round 1), and a hosting observer is bypassable; the composer-owned chokepoint subsumes both.
- **STJ packaged as an analyzer dependency for the import reader** — rejected: host-fragile (net472 VS hosts, compiler-host assembly resolution) and breaks the generator's documented isolated-analyzer posture; vendored minimal reader + schema-pinned DTO twins instead.
- **Lambda/condition/context re-binding registries at import (#100 options)** — deferred with DR-14's explicit rejection contract; re-binding is a consumer-driven follow-on.
- **Folding #128/#130 into this bundle** — declined at scoping: neither touches the contract seams (verified in exploration); they'd inflate job size without serving the strategy-compiler spine.

## Open Questions

- **`GateClass` member freeze:** proposal comment on #150 (tagging basileus#392 + exarchos#1646, proposing the DR-1 snake_case list) is posted as part of this plan's finalization; absent objection by implementation start, the DR-1 list is the timeboxed default — additions ride the DR-18 policy. *(Was "resolve at plan-review"; now executed with a default so the critical path cannot stall.)*
- **`ontology.abstained` envelope naming:** confirm the `type` string + payload fields against basileus#155's coverage-metric consumer before DR-17 merges (same #150-comment mechanism; non-blocking — the shape is additive).
- **ExecutionProfile narrowing (explicit deferral):** #150 says "ExecutionProfiles / workflow definitions"; `ExecutionProfile` exists only in docs, and profile *composition* is consumer-side (basileus #392 / the exarchos resolver). This bundle delivers the declaration layer (`GateDeclaration` + workflow-definition slots); the deferral is recorded on #150 at synthesis.
- **#128/#130 milestone placement:** bumped out of v2.10.0 (this spec re-themes it). Both verified non-blocking for every DR here. Needs the milestone edit + a note on each issue.
- **DSL/type verb names:** `AllowDiagnosticFork` and `OntologyAnswerComposer` are working names; final INV-4 check at implementation (concrete domain nomenclature, no abstract structural terms).

## Decomposition

### Scope

**Target:** Full design (DR-1 … DR-18).
**Excluded:** None deferred out of the bundle. Explicit in-design deferrals carry their own DR anchors: the re-binding registries (DR-14 rejects instead) and the runtime inspection API (Alternatives — Approach C half, additive later).

**Repo conventions binding every task:** TUnit runner is `cd src && dotnet test <proj> -- --treenode-filter "/*/*/*/MethodName"` (bare `--filter` does not work here); assertions are `await`ed; every new hand-written public/internal record is appended to the owning project's sealed-type guard (`InvariantGuardTests` variant — add one where the project lacks it; emitted `Generated/` records are auto-covered by `EmitterShapeTests` instead); public-surface changes update `PublicAPI.Unshipped.txt` + CHANGELOG in the same task (no RS0016/RS0017 suppressions); new diagnostic IDs are monotonic via `AgwfCatalog.tsp` regeneration; contracts regeneration runs `scripts/contracts-codegen.sh` and hand-edits to `schemas/`/`Generated/` are CI-rejected.

### Traceability matrix (DR-N → tasks)

| DR | Requirement | Tasks |
|----|-------------|-------|
| DR-1 | `GateClass` closed enum | 001 |
| DR-2 | `GateDeclaration` + reliability annotation | 002, 018 |
| DR-3 | IR born speaking gate classes | 003, 017, 018, 019 |
| DR-4 | Fork-path confidence lowers | 008 |
| DR-5 | Loop-body config into IR + lowers | 009, 010 |
| DR-6 | `RequireConfidence` merge fix | 007 |
| DR-7 | Fork/compensation DSL edge | 011 |
| DR-8 | Fork-trigger declaration/occurrence contract | 004 |
| DR-9 | Fork edge lowers into saga | 012, 013 |
| DR-10 | Fork edge on the wire | 005, 014 |
| DR-11 | Abstention union + composer | 020 |
| DR-12 | JSON import front-end | 026, 015, 017 |
| DR-13 | Compile-time moniker resolution | 016 |
| DR-14 | Runtime-bindable behavior marked + rejected | 024, 018 |
| DR-15 | Behavioral round-trip gate | 019 |
| DR-16 | Contracts twin + mechanical parity | 006, 022 |
| DR-17 | `ontology.abstained` composer chokepoint | 006, 021 |
| DR-18 | Schema-evolution guardrail | 025, 023 (+ per-task RS0016/JsonSchemaDiff duties) |

### Tasks

Contracts-track tasks (001–006, 024) serialize — they all touch `main.tsp` and regenerate `schemas/`/`Generated/` — but the track as a whole runs parallel to tracks G (007–010), O (020–021), and tooling (025).

### Task 001: `GateClass` closed enum in Contracts TypeSpec

Author the `GateClass` closed enum as a TypeSpec model and regenerate the contracts projections so both runtimes consume one typed gate identity.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-1
**Files:** `src/Strategos.Contracts/Gates/GateClass.tsp` (new), `src/Strategos.Contracts/main.tsp`, regenerated `schemas/` + `Generated/`, `src/Strategos.Contracts.Tests/Gates/GateClassSchemaTests.cs` (new)
**Verification:** `TspCompileTests`-family compile pass; emitted C# enum carries snake_case `[JsonStringEnumMemberName]` values (shape test); `ZodConsumabilityTests` extended to the new schema; integration: cross-product round-trip script green. Preconditions: the #150 member-list proposal comment is posted (timeboxed default = DR-1 list).
**Dependencies:** None · **Parallelizable:** Yes (vs tracks G/O/tooling; serial within contracts track)

### Task 002: `GateDeclaration` + `GateReliability` models

Add the `GateDeclaration` record with its optional measured-reliability annotation — provenance-required, sealed, immutable — plus the corpus zero-reliability assertion (the import-channel rejection lands in task 018).
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-2
**Files:** `src/Strategos.Contracts/Gates/GateDeclaration.tsp` (new), regenerated projections, `src/Strategos.Contracts.Tests/Gates/GateDeclarationSchemaTests.cs` (new; schema-required `source`, sealed/init shape via `EmitterShapeTests`), `src/Strategos.Tests/` (fixture corpus asserts zero reliability blocks)
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING assertion; integration: corpus re-validation.
**Dependencies:** 001 · **Parallelizable:** No (contracts track)

### Task 003: Workflow IR born speaking gate classes — additive `gates` + `gateId`

Extend the workflow wire IR with optional `gates` and `gateId` so gate declarations flow through the shared IR to both runtimes from birth; document the dangling-`gateId` semantic rule for schema-only consumers.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-3
**Files:** `src/Strategos.Contracts/Workflow/WorkflowDefinitionV1.tsp`, `Workflow/StepDefinition.tsp`, `src/Strategos.Contracts.Tests/Workflow/GateSlotSchemaTests.cs` (new), `src/Strategos.Contracts/README.md` (semantic-rule note for Zod consumers), regenerated projections
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING; existing #53 fixtures re-validate byte-unchanged; integration suite over Contracts.Tests. (Dangling-`gateId` rejection is import-side — task 018.)
**Dependencies:** 002 · **Parallelizable:** No (contracts track)

### Task 004: `ForkTrigger` enum + `ForkOccurrence` model

Author the closed `ForkTrigger` enum (declaration side) and the `ForkOccurrence` runtime-payload model (occurrence side) with a versioned schema marker admitting a future `exploratory` member additively.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-8
**Files:** `src/Strategos.Contracts/Workflow/ForkTrigger.tsp` (new), `src/Strategos.Contracts/Events/ForkOccurrence.tsp` (new), version marker + evolution doc-comment, `src/Strategos.Contracts.Tests/Workflow/ForkTriggerSchemaTests.cs` (new; occurrence missing required evidence fields fails schema), regenerated projections
**Verification:** scoped tests + kill-probe; schema version marker asserted; Zod consumability; `JsonSchemaDiff` NON-BREAKING.
**Dependencies:** 003 · **Parallelizable:** No (contracts track)

### Task 005: `DiagnosticForkDefinition` wire shape

Add the `DiagnosticForkDefinition` structural shape (permitted triggers + evidence-ref schema + `maxForks` + compensation seed moniker) to the workflow wire IR as an additive, moniker-only contract (INV-8).
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-10 (wire half)
**Files:** `src/Strategos.Contracts/Workflow/Structural.tsp` (or new `.tsp`), `WorkflowDefinitionV1.tsp` additive slot, `src/Strategos.Contracts.Tests/Workflow/DiagnosticForkSchemaTests.cs` (new; INV-8: monikers only — no CLR-type-shaped fields)
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING.
**Dependencies:** 004 · **Parallelizable:** No (contracts track)

### Task 006: Abstention union + `ontology.abstained` event in Contracts

Author the abstention response union and the `ontology.abstained` event envelope (with the server-side-emission clause in its doc-comment) as Contracts TypeSpec models on the discriminated-union emission path.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-16 (twin half), DR-17 (event shape)
**Files:** `src/Strategos.Contracts/Ontology/AbstentionResponse.tsp` (new — union via const-discriminator pattern), `Events/OntologyAbstained.tsp` (new), `main.tsp`, regenerated projections, `src/Strategos.Contracts.Tests/Ontology/AbstentionUnionSchemaTests.cs` (new; Zod consumability)
**Verification:** scoped tests + kill-probe; discriminated-union emission shape (`[JsonPolymorphic]` abstract record) asserted; `JsonSchemaDiff` NON-BREAKING.
**Dependencies:** 005 (main.tsp serialization only) · **Parallelizable:** No (contracts track)

### Task 007: `RequireConfidence` composes instead of replacing

Fix the replace-not-merge latent bug so confidence configuration composes with other setters in either order.
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

Promote loop-body extraction from bare `StepInfo` to configured `StepModel`, keeping `FirstBodyStepName`/`LastBodyStepName` as computed projections (ForkPathModel precedent) so emitters compile unchanged.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-5 (IR half)
**Files:** `src/Strategos.Generators/Helpers/StepExtractor.cs` (`ParseLoopBody` :450 / `ParseLoopBodyWithContext` :500), `Models/LoopModel.cs`, `Models/WorkflowModel.cs`, IR unit tests
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

Introduce the `AllowDiagnosticFork` builder surface expressing anchor, permitted triggers with their evidence-ref schema, compensation seed, and the `maxForks` bound.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-7
**Files:** `src/Strategos/Builders/DiagnosticForkBuilder.cs` (new) + abstractions, `src/Strategos.Tests/Builders/DiagnosticForkBuilderTests.cs` (new; API-shape/required-parameter tests), compilation-refusal cases in `src/Strategos.Generators.Tests/` (existing Roslyn harness), `PublicAPI.Unshipped.txt`, sealed-type guard, XML lowering-contract docs
**Verification:** scoped tests + kill-probe; INV-4 nomenclature check against the invariants skill; parity-guard entries added in the same PR (no silent declared-but-inert).
**Dependencies:** 004 (`ForkTrigger` contract type) · **Parallelizable:** Yes (after 004)

### Task 012: `DiagnosticForkModel` — generator IR + parser

Model the fork edge in the generator IR and parse the new builder surface into it.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-9 (IR half)
**Files:** `src/Strategos.Generators/Models/` (new model), `FluentDslParser.cs` + extractor for the new surface, IR unit tests
**Verification:** scoped tests + kill-probe.
**Dependencies:** 011 · **Parallelizable:** No

### Task 013: Fork-edge saga lowering — guards, events, compensation composition

Emit fork handling, the `maxForks` + evidence-required guards, the `WorkflowForked` event (ForkOccurrence payload), and compensation seeding from the `DiagnosticForkModel` into the saga.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-9 (lowering half)
**Files:** saga component emitters (`SagaStepHandlersEmitter.cs`, `SagaCompensationComponentEmitter.cs`), `EventsEmitter.cs` (`{Pascal}WorkflowForked`, ES-gated), `src/Strategos.Generators.Behavioral.Tests/DiagnosticForkLoweringTests.cs` (new; maxForks blocked→human escalation; fork-without-evidence refused; compensation composes with #140 merged trigger site), SagaDocument byte-unchanged guard
**Verification:** real-host behavioral proofs; kill-probe; full generator suite + Behavioral.Tests build (the real semantic check for generator output).
**Dependencies:** 012, 008 (same emitter files) · **Parallelizable:** No

### Task 014: Fork edge exports — `ToContract()` projection + corpus fixtures

Project the diagnostic fork edge through `ToContract()` and extend the fixture corpus with fork-edge fixtures.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-10 (projection half)
**Files:** `src/Strategos/Contracts/WorkflowDefinitionProjection.cs`, `ProjectionExhaustivenessTests`/`ProjectionStepKindMappingTests`, `src/Strategos.Tests/FixtureExport/ForkEdgeFixtureTests.cs` (new fork-edge fixtures)
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING; fixture corpus regenerates cleanly.
**Dependencies:** 011, 005, 024 (shared projection file) · **Parallelizable:** No

### Task 015: Import front-end — `AdditionalFiles` provider + ingestion diagnostics

Register the `AdditionalFiles` provider and wire the task-026 reader into the incremental generator with typed failure diagnostics for malformed input and `schemaVersion` skew.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-12 (ingestion half)
**Files:** `src/Strategos.Generators/` (provider registration in `WorkflowIncrementalGenerator.cs`), `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` (malformed-input + version-skew diagnostics, next free AGWF ids) + regeneration, `src/Strategos.Generators.Tests/Import/WireIrReaderTests.cs` (new generator-driver tests: malformed JSON ⇒ diagnostic, `schemaVersion` ≠ "1.0" ⇒ diagnostic, never a crash)
**Verification:** scoped tests + kill-probe; incremental-generator cache test (JSON edit invalidates).
**Dependencies:** 026 · **Parallelizable:** No

### Task 016: Compile-time moniker resolution + diagnostics

Resolve wire monikers to CLR step types against the compilation symbol table with stable diagnostics for miss and ambiguity.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-13
**Files:** `src/Strategos.Generators/Import/WireMonikerResolver.cs` (new), `src/Strategos.Generators.Tests/Import/WireMonikerResolverTests.cs` (new), `AgwfCatalog.tsp` (unresolvable + ambiguous ids) + regeneration, analyzer tests (happy path, miss, deterministic ambiguity listing, CS0101-class duplicate coverage)
**Verification:** scoped tests + kill-probe; INV-8 assertion (no CLR `Type` persisted into contract state).
**Dependencies:** 015 · **Parallelizable:** No

### Task 017: `WireToModelBridge` — JSON → `WorkflowModel` → same emitters

Bridge the parsed wire IR into `WorkflowModel` so JSON-authored workflows lower through the identical saga emitters as C#-authored ones; gate-bearing definitions import with the saga unaffected.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-12 (bridge half), DR-3 (gates tolerated, saga unaffected)
**Files:** `src/Strategos.Generators/Import/WireToModelBridge.cs` (new), `src/Strategos.Generators.Tests/Import/SingleLoweringPathGuardTests.cs` (new architecture guard), `src/Strategos.Generators.Behavioral.Tests/JsonWorkflowImportTests.cs` (new real-host proof; includes gate-bearing ≡ gate-free twin)
**Verification:** real-host behavioral proof; kill-probe; full generator + Behavioral.Tests suites.
**Dependencies:** 016, 009 (LoopModel shape), 012 (fork model importable) · **Parallelizable:** No

### Task 018: Carrier + semantic rejection at import

Reject every runtime-bindable-behavior carrier (delegate step, branch point, loop, validation predicate, approval-with-context) and every semantic violation (dangling `gateId`, reliability-bearing gate declaration) with per-case stable diagnostics.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-14 (rejection half), DR-2 (import-channel machine-check), DR-3 (dangling `gateId`)
**Files:** bridge rejection paths, `AgwfCatalog.tsp` (per-carrier + semantic ids) + regeneration, `docs/deferred-features.md` (re-binding deferrals → #100 follow-on), `src/Strategos.Generators.Tests/Import/ImportRejectionTests.cs` (new; each diagnostic names the construct + JSON path; no saga emitted)
**Verification:** scoped tests + kill-probe.
**Dependencies:** 017 · **Parallelizable:** No

### Task 019: Behavioral round-trip gate over the #53 corpus

Prove the two-bucket partition over the whole corpus (importable ⇒ equivalent; carrier-bearing ⇒ the specific rejection diagnostic; nothing in neither), with behavioral equivalence via hand-authored source twins and full-partition IR fidelity.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-15, DR-3 (gates import fixtures)
**Files:** `src/Strategos.Tests/FixtureExport/RoundTripEquivalenceTests.cs` (new; partition + IR fidelity — if the generator's internal `WorkflowModel` is needed, home the test in `Strategos.Generators.Tests` or extend its IVT rather than widening generator visibility), `src/Strategos.Generators.Behavioral.Tests/RoundTripBehavioralTests.cs` (new; per-family `[Workflow]` source twins), hand-authored import-fixture family (delegate, gates, reliability-bearing, dangling `gateId`, version skew), CI wiring alongside fixture-export tests
**Verification:** partition assertion (every fixture in exactly one bucket); twin behavioral equivalence per importable family; IR field-for-field fidelity across the importable partition; integration suite.
**Dependencies:** 017, 018, 014 · **Parallelizable:** No

### Task 020: Abstention union + `RecordRef` + composer in `Strategos.Ontology.MCP`

Implement the closed abstention union (internal constructors) with `RecordRef` beside `QueryResultUnion`, and the `OntologyAnswerComposer` as the sole producer with retrieval-decided nulls; bootstrap PublicAPI tracking for the three ontology projects.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-11
**Files:** new records + composer beside `QueryResultUnion.cs`, advertised output schema (`minItems: 1`), `src/Strategos.Ontology.MCP.Tests/AbstentionUnionTests.cs` (new; empty retrieval ⇒ `NoAnswerRecorded` + nearest records; empty-citation refusal; internal-ctor enforcement), PublicAPI tracking bootstrapped for `Strategos.Ontology`/`.MCP`/`.MCP.Hosting` + `PublicAPI.Unshipped.txt`, sealed-type guard
**Verification:** scoped tests + kill-probe; discriminator emission test; INV-8 (string identity pair, no CLR types); dependency-set unchanged (no Contracts reference).
**Dependencies:** None · **Parallelizable:** Yes

### Task 021: `IOntologyAuditSink` + composer-chokepoint abstained emission

Add the minimal `IOntologyAuditSink` abstraction (no-op default) beside the composer and emit the abstained record on every `NoAnswerRecorded` production — MCP-hosted or direct — with hosting wiring a concrete sink.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-17 (emission half)
**Files:** `src/Strategos.Ontology.MCP/` (sink abstraction + composer emission), `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs` (wiring), `src/Strategos.Ontology.MCP.Tests/AbstainedEmissionTests.cs` (new; direct composer use), `src/Strategos.Ontology.MCP.Hosting.Tests/AbstainedEmissionHostingTests.cs` (new; MCP entry), `PublicAPI.Unshipped.txt`
**Verification:** scoped tests + kill-probe; integration across the hosting seam; `IsAotCompatible` + dependency-set unchanged assertions; payload carries counts, not record contents; `Strategos.Ontology` core diff is empty.
**Dependencies:** 020 · **Parallelizable:** No

### Task 022: Schema-conformance parity — C# union + event payload vs Contracts schemas

Pin C#-union ↔ Contracts-schema parity mechanically on both halves: union arms against the response schema, and the hosting-mapped abstained payload against the `OntologyAbstained` event schema — red on drift in either direction.
**Risk Tier:** medium · **Boundary Touching:** true
**Implements:** DR-16 (parity half)
**Files:** `src/Strategos.Ontology.MCP.Tests/AbstentionSchemaConformanceTests.cs` (new; serializes both union arms + edge cases against the Contracts-emitted JSON Schema), `src/Strategos.Ontology.MCP.Hosting.Tests/AbstainedPayloadConformanceTests.cs` (new; event-half parity)
**Verification:** scoped tests + kill-probe (mutate one side ⇒ red).
**Dependencies:** 006, 021 · **Parallelizable:** Yes (after both)

### Task 023: Bundle close-out — version, CHANGELOG, guardrail sweep

Sweep the bundle close-out — Contracts version bump, CHANGELOG, deferred-features refresh, failure-mode enumeration — and confirm the contract-first sequencing (mirroring #153) held across the schema delta.
**Risk Tier:** low · **Boundary Touching:** false
**Implements:** DR-18 (sweep half)
**Files:** `Strategos.Contracts.csproj` (`<ContractsVersion>` 0.3.0 → 0.4.0), CHANGELOG.md (bundle entry), `docs/deferred-features.md` final sweep, failure-mode enumeration doc-check (every DR-18 channel names its covering test), `JsonSchemaDiff` CI-run confirmation across the full schema delta
**Verification:** static analysis; CI schema-diff job green; publish-verify unaffected.
**Dependencies:** 019, 021, 022, 013, 007, 010, 024, 025 (all tracks landed) · **Parallelizable:** No (final)

### Task 024: Export lossiness markers + drop-site exhaustiveness guard

Add the `hasContext` approval marker to the wire (additive), emit it (and verify presence rules for branch/loop/validation carriers) from the projection, and pin a parity-style exhaustiveness guard pairing every projection drop-site with its marker or presence rule.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-14 (marking half)
**Files:** `src/Strategos.Contracts/Workflow/ApprovalFailureConfig.tsp` (additive `hasContext`), regenerated projections, `src/Strategos/Contracts/WorkflowDefinitionProjection.cs` (marker emission), `src/Strategos.Tests/Contracts/LossinessMarkerTests.cs` (new; drop-site ↔ marker/presence exhaustiveness guard)
**Verification:** scoped tests + kill-probe; `JsonSchemaDiff` NON-BREAKING; corpus regenerates (context-bearing approval fixtures now carry the marker).
**Dependencies:** 006 (contracts track tail) · **Parallelizable:** No (contracts track; precedes 014 on the shared projection file)

### Task 025: `JsonSchemaDiff` enum awareness

Teach the schema differ and its CI driver to classify enum-member removal/rename as BREAKING and addition as flagged NOTICE, per the DR-18 evolution policy; pin the strict-converter `JsonException` posture.
**Risk Tier:** medium · **Boundary Touching:** false
**Implements:** DR-18 (machine-verification half)
**Files:** `src/Strategos.Contracts/SchemaDiff/JsonSchemaDiff.cs`, `scripts/contracts-schema-diff.mjs`, `src/Strategos.Contracts.Tests/Pipeline/SchemaDiffTests.cs` (removal/rename/addition cases), strict-converter unknown-member test
**Verification:** scoped tests + kill-probe; CI driver dry-run over the current schema set stays green.
**Dependencies:** None · **Parallelizable:** Yes

### Task 026: Wire-DTO twins + vendored JSON reader (netstandard2.0)

Hand-author the internal wire-DTO twins and the vendored minimal JSON reader inside the isolated netstandard2.0 generator (zero analyzer package dependencies), schema-pinned from the net-current test project.
**Risk Tier:** high · **Boundary Touching:** true
**Implements:** DR-12 (ingestion mechanism)
**Files:** `src/Strategos.Generators/Import/WireDtos.cs` (new; netstandard2.0-safe twins), `src/Strategos.Generators/Import/MinimalJsonReader.cs` (new; vendored, dependency-free), `src/Strategos.Generators.Tests/Import/WireDtoSchemaConformanceTests.cs` (new; twins validated against the Contracts-emitted JSON Schema — drift in either direction fails)
**Verification:** scoped tests + kill-probe; generator packaging unchanged (no new analyzer dependencies — packaging test); conformance test red on either-direction drift.
**Dependencies:** 006, 024 (twins must include the `hasContext` marker from birth — avoids a known-red conformance window) · **Parallelizable:** Yes (after contracts track)

### Parallelization

Four tracks run concurrently from the start; integration stays linear per wave (ff-merge discipline):

- **Wave 1 (parallel worktrees):** contracts track head (001→002→003→004→005→006, serialized internally), 007, 008, 009, 020, 025
- **Wave 2:** 024 (contracts track tail), 010 (after 009), 011 (after 004), 021 (after 020)
- **Wave 3:** 026 (after 024), 012→013 (013 also waits on 008), 014 (after 011+005+024), 022 (after 006+021)
- **Wave 4:** 015 (after 026), then 016→017 (017 also waits on 009+012) →018
- **Wave 5:** 019 (after 014+017+018), then 023

**Critical path:** 001→…→006→024→026→015→016→017→018→019→023 (the contracts track feeds the import front-end; #145/#151 lowering and the ontology track hang off it in parallel).
