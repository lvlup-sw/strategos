# Spec: Canonical Rationale/Claim Ontology

**Date:** 2026-08-07 · **Feature:** `rationale-claim-ontology` · **Depth:** deep
**Inputs:** roadmap `lvlup-sw/strategos#153` (strategy-compiler program) · issues #157 (DKG claim ontology) · #115 (generalize primitives for rationale ontologies) · #61 (`ProvenanceEnvelope`) · consumer specs `lvlup-sw/exarchos#1745` + [`exarchos:docs/specs/2026-08-05-design-knowledge-graph.md`](https://github.com/lvlup-sw/exarchos/blob/main/docs/specs/2026-08-05-design-knowledge-graph.md) (DR-1, D2, P-1) · `lvlup-sw/basileus#155` + [`basileus:docs/designs/2026-06-02-why-context-engine.md`](https://github.com/lvlup-sw/basileus/blob/main/docs/designs/2026-06-02-why-context-engine.md) §58 · `lvlup-sw/basileus#236` (schema spike, open) · in-session cross-repo source review (2026-08-07)

> One unified artifact: `## Design & Rationale` is the DR-N source; `## Decomposition` maps tasks → DR-N within this same document.

## Design & Rationale

### Problem Statement

Two products are independently building the same thing on top of Strategos, and neither has frozen it yet.

**exarchos** (`#1745`, milestone v3.2.0) makes design rationale a corpus of typed claims — `Problem`, `Option`, `Decision`, `Rejection`, `Requirement`, `Consequence`, `Constraint` — asserted as events and folded into a local, branch-scoped `dkg.db`. Its **D2** decides the schema "is authored in Strategos TypeSpec as a `DomainOntology`," consumed as generated JSON Schema/Zod and **never** the .NET assembly, because exarchos ships as a single-file binary (their constraint **C7**).

**basileus** (`#155`) builds a remote Why-Context Engine over a design-rationale ontology — nodes `Decision`, `Constraint`, `Pattern`, `Concept`, `Question`, `Rationale`; edges `Supersedes`, `Motivates`, `Constrains`, `Refines`, `ConflictsWith`, `Evidences`, `AppliesTo` — on "repointed `Strategos.Ontology` primitives," consumed as **.NET** behind `IRagCollection<T>`.

Both cite the same prior art (IBIS/QOC). They diverged because they were derived independently two months apart. The intent was always one semantics — exarchos consuming it locally, basileus remotely, so integration between the two apps is structural rather than negotiated. **The divergence is therefore a defect this spec exists to close, not a constraint to design around.**

Three facts make now the moment. exarchos's **P-1 is the only phase in their sequencing table with no dependencies**, and it *is* this work — their entire v3.2.0 milestone sits behind it. basileus **#236**, the spike that would fix its taxonomy, is **still open**, and no rationale-ontology code exists in basileus yet. And exarchos's own risk register says the cost of not settling kinds early is a Strategos release per added kind.

Meanwhile #157 as filed cannot be implemented as written. Its acceptance criteria describe a substrate that does not exist:

1. **`Agentic.Ontology` is a stale name for a stale mechanism.** exarchos's `docs/adrs/system-index.md` locates it in "the Agentic.Workflow repository" (`levelup-software/agentic-workflow` — Strategos's former identity) and describes "**source-generated** compile-time descriptors and cross-domain link validation." `Strategos.Ontology.Generators` contains **zero** `IIncrementalGenerator`/`ISourceGenerator`; it is `IsRoslynComponent` + `DevelopmentDependency`, packaged as *"Roslyn diagnostic analyzers… AONT001-AONT035"* (INV-2). `ComposedOntology` does not exist anywhere.
2. **"Fails the build on a dangling cross-domain reference" inverts today's behavior.** `AONT CrossDomainLinkUnverifiable` is a **Warning** reading *"targets '{1}' in domain '{2}' which cannot be validated at compile time."*
3. **Two of the three named link targets do not exist.** `Decision → Workflow` binds to `WorkflowDefinitionV1` ✓, but there is **no `GateVerdict` model** (`ValidationVerdict` is a comment reservation, with an untyped `validationVerdict?: Record<unknown>` placeholder) and **no first-class `Task` model** (only `Task*Data` event payloads).

Underneath all three sits the substrate gap #115 names. `IOntologyBuilder.ObjectTypeFromDescriptor()` already handles CLR-typeless registration — its own doc comment says *"ingested types may only be known by `SymbolKey`, with no loaded CLR type."* But the entire fluent DSL is CLR-generic (`Object<T>`, `Interface<T>`, `CrossDomainLink().From<T>()`, `ExtensionPoint.FromInterface<T>()`), and `OntologyDefinitionAnalyzer` validates by walking **C# invocation syntax**. A rationale ontology has no CLR types, so it must use the descriptor-first path — which routes *around* the analyzer tier. **For exactly the ontologies #115 and #157 describe, INV-5's compile-time validation silently does not apply.**

### Chosen Approach

Author **one canonical claim taxonomy in TypeSpec under `Strategos.Contracts`**, and emit two projections from it: JSON Schema (→ exarchos Zod via `#1125`) and .NET descriptor registrations (→ basileus). One authored artifact, two consumption modes, no translation layer between the products' type systems.

TypeSpec wins on a structural argument, not preference. The two consumers can never share a *language* — basileus needs .NET, exarchos structurally cannot take .NET (C7) — so the single source must be language-neutral. `Strategos.Contracts` already is exactly this pipeline and already ships on an independent, published track (`contracts-v0.4.0` is tagged; the product `v2.10.0` tag is **not**, so the ontology package would gate this work behind an uncut release). It also resolves the validation gap: emitting through `Strategos.Contracts.Codegen` — a standalone console app, not a Roslyn component, outside `Strategos.Ontology*` — makes dangling-reference detection an **emitter-tier** guard. That is INV-5 tier 3, a tier that already exists, so the build-failure criterion becomes reachable without inventing a fourth tier and without touching INV-2's analyzer-only guarantee.

**Scope boundary — intrinsic, not adjudicative.** Strategos defines everything intrinsic to a claim: its taxonomy, typed body fields, identity (content address), canonical serialization, and provenance carriage. Strategos defines nothing adjudicative: no promotion protocol, no reconciliation of exarchos's event-log authority against basileus's git+Marten authority, no cross-tier supersession propagation. Identity and canonical form are computable from a claim's own content, so they need no authority decision — but they are equally expensive to retrofit, so they are settled here. This keeps instance interchange *reachable* without requiring the two products, which both currently declare independence, to couple now.

### Requirements (DR-N)

The DR-N identifiers below are the single source the decomposition traces against.

#### DR-1: One canonical claim-kind vocabulary

The reconciled node set, IBIS/QOC-grounded, replacing both draft vocabularies:

| Kind | Role | Reconciles |
|---|---|---|
| `Problem` | the open issue under decision (IBIS Issue / QOC Question) | exarchos `Problem` ∪ basileus `Question` |
| `Option` | a candidate resolution (IBIS Position / QOC Option) | exarchos `Option` |
| `Decision` | the selected option | both |
| `Rejection` | a non-selected option, with cause | exarchos `Rejection` (see below) |
| `Requirement` | an obligation carrying an ordinal (DR-N records themselves) | exarchos `Requirement` |
| `Constraint` | a binding rule (invariants), QOC Criterion | both |
| `Consequence` | what follows from a decision | exarchos `Consequence` ∪ basileus `Rationale` |
| `Pattern` | a reusable solution shape | basileus `Pattern` |

Two calls are made explicitly. **`Rejection` is a first-class kind, not a `Decision.status`** — basileus's draft encodes it as status, which makes a rejected option indistinguishable from a live decision in a "what decisions bind here" query, working against their **DI-2** (surface conflicts, never hide them) and their hard rule that a superseded decision is *"reachable only as the target of a `Supersedes` edge, never returned as a live consideration."* It also makes exarchos's completeness gate — *"every non-selected option carries a `rejection`"* — mechanically checkable. **`Rationale` is not a kind**; it dissolves into `Consequence` plus DR-3's typed body fields, because a free-floating rationale node is precisely the prose blob exarchos's **C5** forbids.

**Acceptance criteria:**
- All eight kinds are authored in TypeSpec under `src/Strategos.Contracts/Ontology/`, imported from `main.tsp`; `tsp compile` + codegen emit both projections.
- The kind set is a **closed** discriminated union — an unknown kind is unrepresentable, not silently accepted.
- **Reachability rule (ported from exarchos DR-14):** every kind names its reader — an ontology action or a composition template. A kind with no reader fails the closure check. This is the guard against drift into general agent memory.
- `Concept` is **deferred, not dropped**, under that rule: it has no named reader in either consumer spec. The deferral and its cause are recorded in Open Questions.
- Round-trip: emitted JSON Schema and emitted C# validate the same fixture corpus.

#### DR-2: Closed link vocabulary and shared interfaces

The edge set is closed and directional, spanning both consumers' needs, plus the two shared interfaces `ISupersedable` and `IEvidenced`.

| Edge | Direction | Purpose |
|---|---|---|
| `Addresses` | `Option → Problem` | a candidate answers an issue |
| `Selects` | `Decision → Option` | the chosen candidate |
| `Rejects` | `Rejection → Option` | a discarded candidate, with cause |
| `Supersedes` | `Decision → Decision` | non-destructive replacement (exarchos **C4**) |
| `Motivates` | `{Problem,Constraint} → {Option,Decision}` | why this was considered |
| `Constrains` | `Constraint → {Option,Decision}` | a binding rule bears on a choice |
| `Refines` | `X → X` (same kind) | narrowing within a kind |
| `ConflictsWith` | `Decision ↔ Decision` (symmetric) | declared contradiction |
| `Evidences` | *external ref* `→ {Decision,Requirement}` | see below |
| `AppliesTo` | `any → scope/surface` | where a claim binds |

`Evidences` deliberately has **no internal `Evidence` node**. exarchos's **D6** is explicit that evidence is not a new claim kind — `admission.evidence-recorded` is already produced by the durable gate runner, and a second producer would reproduce a known defect. `IEvidenced` therefore carries a *reference* to an external verdict, typed against the already-shipped `GateClass` taxonomy (#150, plus `GateClass.Rules` from #158).

**Acceptance criteria:**
- The edge set is closed; an undeclared edge type is unrepresentable in both projections.
- Endpoint kinds are constrained in the type system, not by runtime convention — `Selects` cannot target a `Constraint`.
- `Supersedes` is non-destructive: the superseded claim remains addressable as the edge target.
- `ConflictsWith` is symmetric and its symmetry is enforced mechanically, not documented.
- `IEvidenced` references an external verdict; no `Evidence` claim kind is introduced.

#### DR-3: Typed body fields, never a prose blob

Claim bodies carry typed fields — forces, alternatives, costs, criteria — rather than free text. This is exarchos **C5** (*"a prose blob in a database is still prose"*) and it is what lets `Consequence` absorb basileus's `Rationale` node without losing structure.

**Acceptance criteria:**
- Each kind declares its typed body shape in TypeSpec; a single untyped `body: string` is not present on any kind.
- Prose that has no typed home is representable only in an explicitly-named narrative field, and that field is never the sole carrier of a force, alternative, or cost.
- basileus **DI-1** is satisfiable from the schema alone: a consumer can return considerations *with their structure* without parsing prose.

#### DR-4: TypeSpec authoring surface with dual emitters

The claim ontology is authored once in TypeSpec and projected twice. `Strategos.Contracts.Codegen` gains a descriptor emitter alongside the existing record emitter.

**Acceptance criteria:**
- JSON Schema emission is consumable by exarchos's `#1125` pipeline; the Zod round-trip harness (`contract/ir/` Ajv precedent) validates the same fixtures as the JSON Schema.
- .NET emission produces `ObjectTypeDescriptor` registrations consumable by `Strategos.Ontology` **without** requiring CLR types per claim kind (see DR-8).
- `Strategos.Ontology*` gains no Roslyn source generator and no Wolverine/Marten reference — INV-2 holds, verified by the existing grep-based check.
- The emitters are the *only* producers of both projections; no hand-authored claim type exists on either side.

#### DR-5: Claim identity as a published content address

The content-addressing scheme becomes a published contract rather than an implementation detail. exarchos already depends on it internally (their DR-10: *"identical claims from two processes collapse by content address into one row with reinforcement 2"*), and basileus needs the same function to recognize a claim it has seen before.

**Acceptance criteria:**
- The address is a pure function of the claim's canonical form (DR-6) — no store, clock, actor, or branch participates.
- Both emitted projections expose the algorithm, and a shared fixture set proves .NET and JS/TS compute **identical** addresses for identical claims.
- The algorithm is versioned; an address carries which version produced it.
- Explicitly **not** in scope: which store's copy is authoritative when two stores hold the same address (the authority question — see Open Questions).

#### DR-6: Canonical serialization

A byte-stable canonical form, without which DR-5's addresses disagree across the language boundary and exarchos's byte-identical composition requirement cannot hold.

**Acceptance criteria:**
- Field ordering, number formatting, string escaping, absent-vs-null, and Unicode normalization are all specified, not left to each emitter.
- A cross-language fixture suite proves .NET and JS/TS canonicalization are byte-identical.
- Round-trip: canonicalize(parse(canonicalize(x))) == canonicalize(x).

#### DR-7: Provenance carriage aligned with `ProvenanceEnvelope`

A claim carries provenance in the shape #61 already specifies — `EnvelopeReference { Id, ClaimHash }`, with `ProvenanceEnvelope.ancestry[]` available for cross-agent attribution. Carriage only: the claim records where it came from; nothing here decides whose record wins.

**Acceptance criteria:**
- `ClaimHash` is the DR-5 address — one identity notion, not two.
- Provenance is non-optional on an asserted claim; an anonymous claim is unrepresentable (mirroring the `GateReliability.source` precedent, where an unattributable measurement is rejected at the schema boundary).
- The shape is structurally compatible with #61 such that #61 can adopt it without a breaking change; if #61 lands first, this DR consumes it instead of redefining it.

#### DR-8: CLR-typeless ontologies become first-class (#115)

The descriptor-first path stops being a bypass and becomes a supported, validated authoring route, and the code-symbol assumptions the consumers no longer use are deprecated.

**Acceptance criteria:**
- A rationale ontology — no `ClrType`, edge-centric — can be declared, ingested, traversed, and validated through public primitives with no code-symbol assumptions, proven end-to-end on the DR-1 taxonomy as the fixture.
- `CrossDomainLink` acquires a non-generic source form; `From<T>()` is no longer the only way to name a source. INV-8's "`ClrType` **OR** `SymbolKey`, both first-class" holds on this path in fact, not incidentally.
- The `.Requires()` predicate-DSL path and SCIP/source-extraction assumptions are marked deprecated with rationale and a named successor.
- Deprecations are mechanical (`[Obsolete]` / analyzer diagnostic), not documentation-only.

#### DR-9: Dangling cross-domain references fail the build

The #157 criterion, relocated to a tier that can actually enforce it. Because the ontology is emitted rather than hand-registered, the emitter sees every cross-domain reference and can resolve it against the workflow IR at build time.

**Acceptance criteria:**
- A cross-domain link naming a non-existent target type **fails codegen** with a non-zero exit and a stable diagnostic identifier, following the `AONT*` numbering discipline (next unused ID, never reused).
- The failure names the offending link, the unresolvable target, and the file/line.
- `Decision → Workflow` binds to `WorkflowDefinitionV1` and is proven by a passing case plus a seeded-failure case.
- `Requirement → Task` and `Evidence → GateVerdict` have **no target type today**. Each is either introduced in this slice or explicitly deferred in Open Questions — silently emitting an unbindable link is not an option, since that is the exact class of defect this DR exists to prevent.
- The existing `AONT CrossDomainLinkUnverifiable` **Warning** is reconciled with this rule: its message currently asserts the opposite ("cannot be validated at compile time") and must be corrected, narrowed to the cases that remain genuinely unverifiable, or retired — whichever, the two must not contradict.

#### DR-10: Failure modes and schema skew

Error handling across the seam, covering the failure class the design creates by having two consumers on independent release cadences.

**Acceptance criteria:**
- **Unknown kind on the wire:** a consumer reading a claim of a kind its schema version does not know produces a typed, surfaced error — never a silent drop, and never a coerced fallback kind. Silent loss of a rejection or a conflict would defeat basileus **DI-2** directly.
- **Schema skew:** claims carry the taxonomy version that produced them; a consumer can detect and report skew rather than mis-parse.
- **Emitter failure is fail-closed:** any codegen error leaves no partial artifact on disk, so a broken emit cannot be mistaken for a successful one.
- **Cycle safety:** `Supersedes` and `Refines` cycles are rejected at emit or detected at fold, with a stated choice of which; an unbounded traversal is not an acceptable outcome.
- **Canonicalization failure** (un-normalizable input, e.g. invalid Unicode) is an error, not a best-effort pass-through that would silently break DR-5 addressing.

#### DR-11: Cross-repo reconciliation before the contract freezes

Under #153's coordination rule — *"every contract change names its consumers in both runtimes before it merges"* — this slice's premises must be corrected upstream, because both consumer specs currently reference a Strategos that does not exist.

**Acceptance criteria:**
- exarchos's `system-index.md` and DKG DR-1 are corrected: `Agentic.Ontology` → `Strategos.Ontology`/`Strategos.Contracts`, the "source-generated" mechanism claim, and the non-existent `ComposedOntology` type.
- basileus **#236** is answered by this taxonomy rather than run independently, or the divergence is recorded as a deliberate, justified split.
- The DR-1 kind list and DR-2 edge list are posted to both consumer issues for objection before implementation starts; absent objection by implementation start, the lists above are the timeboxed default (the #150 member-freeze precedent).
- Both consumers are named on the contract change per #153; the exarchos-side `#1745` P-1 dependency is updated to point at this spec.

### Technical Design

The claim ontology lands as new `.tsp` files under `src/Strategos.Contracts/Ontology/`, alongside the existing `AbstentionResponse.tsp`, imported from `main.tsp`. This is the same substrate that carries the workflow IR and event envelope — which is exactly the justification exarchos's D2 gives for putting it there.

`Strategos.Contracts.Codegen` currently emits C# records via `RecordEmitter.cs`. It gains a second emitter producing `ObjectTypeDescriptor` registrations, and a resolution pass that checks every cross-domain reference against the models in the same compilation — the mechanism behind DR-9. Because Codegen is a standalone console app rather than a Roslyn component, this adds no analyzer surface to `Strategos.Ontology*` and leaves INV-2 intact.

On the ontology side, `ICrossDomainLinkBuilder` gains a non-generic source form so a descriptor-first ontology can declare links without a CLR type, and the descriptor-first registration path (`ObjectTypeFromDescriptor` / `ApplyDelta`) becomes the supported route for emitted ontologies rather than an ingestion-only escape hatch.

The invariant posture is deliberate. INV-2 holds because no source generator or Wolverine/Marten reference enters `Strategos.Ontology*`. INV-5 holds because validation lands at the **emitter tier** (tier 3) rather than a new fourth tier — the analyzer tier is structurally unavailable here, since an emitted ontology has no C# invocation syntax for an analyzer to walk. INV-8 is strengthened from incidental to enforced by DR-8. INV-6 and INV-7 apply to the emitted descriptors as to any others.

Release track: this ships on `contracts-v` (0.4.0 published), independent of the uncut `v2.10.0` product tag.

### Integration Points

- `src/Strategos.Contracts/Ontology/*.tsp` — new claim kinds, link vocabulary, shared interfaces, typed bodies
- `src/Strategos.Contracts/main.tsp` — imports for the above
- `src/Strategos.Contracts.Codegen/RecordEmitter.cs` — existing record emission
- `src/Strategos.Contracts.Codegen/Program.cs` — new descriptor emitter + cross-domain resolution pass
- `src/Strategos.Ontology/Builder/ICrossDomainLinkBuilder.cs` + `CrossDomainLinkBuilder.cs` — non-generic source form
- `src/Strategos.Ontology/Descriptors/CrossDomainLinkDescriptor.cs` — `SourceType` becomes polyglot (`Type` **or** `SymbolKey`)
- `src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnostics.cs` — reconcile `CrossDomainLinkUnverifiable`
- `src/Strategos.Contracts.Tests/` — cross-language fixture suites (needs the Node-provisioned CI job)
- `src/Strategos.Contracts/CHANGELOG.md` + `ContractsVersion` — contracts version bump

### Exploration

The divergent loop ran in-session against primary sources in all three repositories rather than from the issue text, which is what surfaced the three broken premises in #157. The `/exarchos:discover` bridge was available at this depth and was **not** escalated: the open questions were answerable from the consumers' own specs and from Strategos source, so a research pass would have duplicated work already done.

**Round 1 — authoring surface.** Three candidates: TypeSpec in `Strategos.Contracts`; the existing C# fluent DSL in `Strategos.Ontology` plus a JSON Schema exporter; or a hybrid splitting shapes from wiring. The C#-first option was eliminated on two independent grounds — it contradicts exarchos **D2**, and it leaves #115's core case (CLR-typeless) still routing around the analyzer, so the validation gap it was meant to avoid persists anyway. The hybrid was eliminated as strictly worse than TypeSpec-first: it accepts a shape↔wiring seam that can drift silently, in exchange for analyzer coverage on wiring that TypeSpec-first gets at the emitter tier regardless.

**Round 2 — how far sharing goes.** Shared-schema/separate-stores versus full instance interchange. Interchange was not rejected on cost but on a structural obstacle: the two sides have **different authorities** — exarchos's corpus explicitly holds none (INV-1; *"no write path reaches the corpus except the projection fold"*), while basileus's authority is git plus Marten ingestion events (U-2). A promoted claim cannot arrive as data; it must arrive as a basileus ingestion event, meaning basileus re-authors it and provenance must survive re-authoring. Both products also currently declare independence.

**Convergence.** The chosen scope takes shared-schema/separate-stores, then pulls forward every part of interchange that does not require answering the authority question — identity, canonical serialization, provenance carriage — because those are computable from a claim's own content yet just as expensive to retrofit as the taxonomy. Interchange stays reachable; nobody is forced to couple now.

### Alternatives considered

- **C# DSL in `Strategos.Ontology` + schema exporter.** Single authoring surface and full analyzer coverage for CLR-typed claims. Rejected: contradicts exarchos D2; ships on the uncut `v2.10.0` track; and the CLR-typeless case — the entire point of #115 — still bypasses the analyzer, so the build-failure criterion stays unreachable. It would also derive the wire contract from CLR reflection, dragging C# nullability, casing, and generics into a schema exarchos must consume.
- **Hybrid: shapes in TypeSpec, wiring in C#.** Serves both consumers natively and keeps analyzer coverage on the wiring half. Rejected: splits one conceptual ontology across two languages with a seam that drifts silently, for a benefit TypeSpec-first already obtains at the emitter tier.
- **Full instance interchange now.** Rejected for this slice on the authority-reconciliation problem above; its cheap, authority-independent prerequisites are pulled forward into DR-5/6/7 so it remains reachable.
- **Two ontologies sharing a core.** Considered early and abandoned once the intent was clarified: it re-legitimizes the drift this spec exists to remove, and makes structural compatibility a negotiation rather than a property.

### Open Questions

- **`Requirement → Task` and `Evidence → GateVerdict` have no target types.** Neither a first-class `Task` model nor a `GateVerdict` exists in Contracts (`ValidationVerdict` is a comment reservation with an untyped `Record<unknown>` placeholder). Resolves in decomposition: introduce them here, or defer the links explicitly. DR-9 forbids the third option of emitting them unbindable.
- **`Concept` deferred under the reachability rule.** Neither consumer names a reader for it. Revisit if basileus #236 supplies one; adding a kind later is the expensive direction, so this is the deferral most worth re-examining before the freeze.
- **Where the taxonomy version lives.** DR-10 requires claims to carry the version that produced them; whether that is a field on every claim or a corpus-level stamp is a decomposition decision with a wire-size consequence.
- **Which store wins — deliberately unanswered.** The authority-reconciliation question is out of scope by construction. It becomes answerable once both corpora exist, and DR-5/6/7 are specified so that answering it later requires no re-freeze of the contract.
- **`CrossDomainLinkUnverifiable` disposition.** Correct, narrow, or retire. Depends on whether cases remain that are genuinely unverifiable at build time once emission is the authoring route.

## Decomposition

> Authored by `/exarchos:plan`. DR-1 … DR-11 above are the decomposition source.
