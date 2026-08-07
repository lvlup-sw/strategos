# Spec: Claim Contract — the shared rationale contract for exarchos + basileus

**Date:** 2026-08-07 · **Feature:** `rationale-claim-ontology` · **Depth:** deep · **Revision:** 2 (clean break after plan-review refutation)
**Supersedes:** [`2026-08-07-rationale-claim-ontology.md`](./2026-08-07-rationale-claim-ontology.md) — refuted 3/3 by the plan-review panel; see Exploration for what was falsified.
**Inputs:** roadmap `lvlup-sw/strategos#153` · issues #157 · #115 · #153 · consumer specs `lvlup-sw/exarchos#1745` + [`exarchos DKG`](https://github.com/lvlup-sw/exarchos/blob/main/docs/specs/2026-08-05-design-knowledge-graph.md) · `lvlup-sw/basileus#155` + [`why-context-engine`](https://github.com/lvlup-sw/basileus/blob/main/docs/designs/2026-06-02-why-context-engine.md) · `lvlup-sw/basileus#236` (schema spike, open) · plan-review panel (3 voters, unanimous `refuted`, 2026-08-07)

> One unified artifact: `## Requirements` is the DR-N source; `## Decomposition` maps tasks → DR-N within this same document.

## Problem Statement

Two products need the same design-rationale semantics, consumed two ways: exarchos locally as JSON Schema/Zod (it ships a single-file binary and structurally **cannot** take .NET — their constraint **C7**), basileus remotely as .NET behind `IRagCollection<T>`. Both derived an IBIS/QOC vocabulary independently, two months apart, and drifted. Neither has frozen it: basileus **#236** is open with no rationale-ontology code written, and exarchos's P-1 is unbuilt. The divergence is a defect to close, not a constraint to design around.

The previous revision tried to close it by authoring an `Agentic.Ontology` `DomainOntology` in TypeSpec, as exarchos's DR-1 asks. **That is not a thing that can exist**, and the reason is mechanical:

1. **Emitted ontologies cannot carry actions.** A mechanically-emitted descriptor arrives with `DescriptorSource.Ingested`, and `ValidateIngestedIntentInvariant` throws `OntologyCompositionException` with Error-severity **AONT205** whenever an ingested descriptor populates `Actions`, `Events`, or `Lifecycle` — *"those are hand-authored intent"* (`OntologyBuilder.cs:231-277`, mirrored at graph freeze). exarchos's DR-1 requires `BoundToTool` actions for every read operation. Emission and actions are mutually exclusive by construction.
2. **CLR-free and polymorphic are mutually exclusive.** Union endpoints (`Motivates: {Problem,Constraint} → {Option,Decision}`) require a registered interface, and `InterfaceDescriptor` takes a non-nullable `Type InterfaceType`. The repo states the limit verbatim: *"a SymbolKey-ONLY interface fan-out is NOT expressible — an interface carries a CLR type, so a CLR-free (`ClrType == null`) descriptor cannot also be a polymorphic interface target."* The shipped parity proof **splits into two dimensions** precisely because they cannot be combined.
3. **The `DomainOntology` framing is itself the stale premise.** exarchos's ADR locates `Agentic.Ontology` in "the Agentic.Workflow repository" (Strategos's former identity) and describes "**source-generated** compile-time descriptors" — but `Strategos.Ontology.Generators` contains zero source generators. Their DR-1 asks for a `DomainOntology` because their ADR believed our ontology was the substrate. What they consume is **JSON Schema/Zod plus closed-loop provenance**; they build their own MCP surface and never needed us to declare their actions.

A fourth finding reframes the urgency: the CLR-free rationale-ontology substrate **already ships**. `RationaleOntologyFixture` builds decisions/constraints with `Supersedes`/`Motivates`/`ConflictsWith` as reified associations, every descriptor `ClrType = null`, SymbolKey-only; `RationaleCorpusParityTests` runs declare → relate → traverse → validate through both the in-memory evaluator and Npgsql (the G-18 edge-layer parity proof, #116, v2.9.0). Most of what #115 asked for is merged.

## Chosen Approach

**Author a claim *contract*, not an ontology.** One TypeSpec artifact in `Strategos.Contracts`, projected to JSON Schema (exarchos) and C# records (basileus) by the pipeline that already does exactly this. No emitted ontology, no emitted actions, no emitted polymorphic endpoints — so AONT205 and the interface limit are respected rather than fought. Ontology *wiring* stays hand-authored C# in the consumer that wants it, which is where AONT205 says intent belongs and where basileus, being .NET, can express it.

**Freeze the axes; leave the vocabulary open.** The previous revision froze a closed eight-kind union. That was the wrong thing to freeze, on three independent grounds. exarchos states it outright — *"That trichotomy — **not the eight-kind list** — determines reliability and who may produce a claim."* basileus requires a document with no front-matter to ingest gracefully as an untyped node, "never an ingestion failure," which a closed union forbids. And a closed union is what makes every future kind cost a Strategos release — the very risk that supplied the original urgency. So the frozen surface is the **axes**: reliability class, status, lifecycle, scope, provenance. The kind vocabulary ships as a *registered core* that consumers extend without a release.

**Cut content-addressing.** The previous revision pulled identity and canonical serialization forward as cheap-now/expensive-later. The panel showed the opposite: the address pre-image was never defined, `ClaimHash` was circular against non-optional provenance, JSON Schema cannot express a hash so exarchos would hand-write it from prose, and **exarchos already ships a `ContentAddressedStore`** with its own scheme — so we would be imposing a second hash notion on a consumer that has one. Cutting it is not a half-measure; shipping it would have been.

**Scope boundary (unchanged, and undisputed by the panel):** Strategos defines what is *intrinsic* to a claim. It defines nothing *adjudicative* — no promotion protocol, no reconciliation of exarchos's event-log authority against basileus's git+Marten authority.

## Requirements

The DR-N identifiers below are the single source the decomposition traces against.

### DR-1: Reliability class is the frozen axis

Every claim carries a producer class — `authored` | `observed` | `measured` — non-optional. This is the axis exarchos names as load-bearing: it determines who may produce a claim and how far it may be trusted. basileus needs the same distinction for its `provenance: extracted` vs `declared` gating.

**Acceptance criteria:**
- `reliability` is non-optional and closed-enum in both projections; a claim without one is unrepresentable.
- The enum carries `authored`, `observed`, `measured` with snake_case wire names (the `GateClass` casing precedent).
- basileus's `declared` vs `extracted` distinction maps onto it without a consumer-local field.
- A lower-trust class is never silently promoted: no projection coerces `observed` → `authored`.

### DR-2: Open kind vocabulary with a registered core

The eight core kinds — `Problem`, `Option`, `Decision`, `Rejection`, `Requirement`, `Constraint`, `Consequence`, `Pattern` — ship as a **registered core**, not a closed union. An unrecognized kind is carried, not rejected.

**Acceptance criteria:**
- A claim of an unregistered kind round-trips without loss and is readable as an untyped carrier — satisfying basileus's "graceful, never an ingestion failure" for the front-matter-less majority of its corpus.
- Adding a kind requires **no** Strategos release; a consumer registers one locally and it survives the round trip.
- `Concept` and `Document` are therefore not deferral decisions at all — they are expressible on day one. The previous revision's `Concept` deferral is withdrawn.
- The core eight are discoverable (enumerable from the schema) so a consumer can distinguish core from extension.

### DR-3: Status and lifecycle axes

`status` — `proposed` | `accepted` | `superseded` — and `lifecycle` — `binding` | `durable` | `reinforced` | `episodic`.

**Acceptance criteria:**
- `status` is present and closed-enum. basileus depends on it in four places (node state, index metadata filtering, live-vs-superseded selection, and "conflicts with **accepted** decisions"); `proposed` vs `accepted` is derivable from no relation, so it must be a field.
- `lifecycle` is present and closed-enum, ordered `binding > durable > reinforced > episodic` for exarchos's ranking and its response-budget truncation.
- An invariant maps to a `Constraint` claim with `lifecycle: binding` with no consumer-local extension.
- Superseded state is non-destructive: a superseded claim stays addressable.

### DR-4: Scope is on the claim

Every claim carries `scope: { repo, featureId, branch }`.

**Acceptance criteria:**
- All three fields present; `branch` is what makes exarchos's branch-scoped reads expressible without a consumer-local envelope.
- basileus's repo-scoping and its `commitSha` provenance are expressible from the same shape.
- Scope is data, not policy — this spec says nothing about *how* a consumer filters on it.

### DR-5: Typed body fields, never a prose blob

Claim bodies carry typed fields — forces, alternatives, costs, criteria, ordinal, cause — rather than free text (exarchos **C5**).

**Acceptance criteria:**
- No kind carries a bare `body: string`; narrative fields are explicitly named and never the sole carrier of a force, alternative, or cost.
- basileus's DI-1 is satisfiable from the schema alone: considerations are returnable *with their structure*, no prose parsing.
- The core eight each declare their typed body; an extension kind may carry an open body without weakening the core.

### DR-6: Relations are typed body references

Relations are expressed as **typed references in claim bodies**, not as standalone edge objects.

**Acceptance criteria:**
- exarchos's anti-drift property holds: *"edges are never separately authored, so they cannot drift from the types."* A superseding claim carries a backward `supersedes` pointer; the projection folds forward.
- basileus's declared-edge front-matter vocabulary — `supersedes`, `motivated-by`, `constrains`, `applies-to`, `refines`, **`references`** — maps onto the reference fields with nothing dropped. (`references` was silently lost in the previous revision.)
- Reference *targets* are claim ids, so no polymorphic CLR interface is required and the `InterfaceDescriptor` limit is never hit.
- Relation semantics — direction, cardinality, and what each asserts — are specified precisely enough to write deterministic traversal tests (basileus **#236** requires cardinality, which the previous revision omitted).

### DR-7: Provenance carriage

Schema-expressible provenance only: source, attribution, and the reference shape #61 declares.

**Acceptance criteria:**
- Provenance is non-optional on an asserted claim (the `GateReliability.source` precedent — an unattributable measurement is rejected at the schema boundary).
- Evidence is a **reference**, not a claim kind (exarchos **D6**), and is **not** typed against `GateClass`: basileus has no gate runner and its evidence is git blobs and commit SHAs, so gate-typing would make the field unpopulable for one consumer.
- No content address and no canonical byte-form is specified — see Alternatives for why.

### DR-8: Two projections, no emitted ontology

JSON Schema for exarchos, C# records for basileus, from one TypeSpec source via the existing `Strategos.Contracts` pipeline.

**Acceptance criteria:**
- The JSON Schema is consumable by exarchos's `#1125` pipeline; the Zod round-trip harness validates the same fixtures.
- The C# projection is plain records in `Strategos.Contracts` — **no** `ObjectTypeDescriptor` emission, so no ingested-descriptor path and no AONT205 exposure.
- `Strategos.Contracts` acquires **no** ProjectReference to `Strategos.Ontology`; the published schema package stays a leaf (it is `IsAotCompatible` with zero ProjectReferences today, and the previous revision had no viable output project).
- Nothing in this slice emits ontology actions, interfaces, or cross-domain links.

### DR-9: Additive evolution, honestly enforced

What actually enforces the contract is the CI schema-diff gate that already exists — not a build failure.

**Acceptance criteria:**
- The additive-evolution policy is stated: new kinds and new optional fields are non-breaking; narrowing an enum or requiring a new field is breaking and needs a version bump.
- `contracts-schema-diff` passes NON-BREAKING for this change; `contracts-codegen-guard` passes with regenerated artifacts committed.
- The claim that enforcement is a *build* failure is **not** made anywhere: codegen is invoked by no MSBuild target and runs only via `scripts/contracts-codegen.sh` in a path-filtered CI job doing `git diff --exit-code`. Enforcement is CI-scoped and the spec says so.
- `ContractsVersion` is bumped and `PackagingTests` updated in the same change — it hard-asserts the current version and would otherwise go red with no owner.

### DR-10: Failure modes

The failure class this design creates by serving two consumers on independent release cadences.

**Acceptance criteria:**
- **Unrecognized kind degrades, never errors** (DR-2) — the previous revision's hard error is withdrawn; it would have broken basileus's majority ingestion path.
- **Schema skew:** claims carry the taxonomy version that produced them, so a consumer detects and reports skew rather than mis-parsing.
- **Dangling references resolve later:** a reference to an unknown claim id is recorded as dangling and surfaced in diagnostics, **not** dropped, and resolves if the target later arrives — basileus's webhook ingestion is inherently out of order. JSON Schema cannot express cross-object referential integrity, so this is a stated contract obligation with a conformance fixture, not a schema constraint.
- **No silent coercion** anywhere: no fallback kind, no promoted reliability class, no dropped relation.

### DR-11: #115 rebased on what already shipped

The CLR-free substrate is largely delivered; this closes the remainder rather than rebuilding it.

**Acceptance criteria:**
- The v2.9.0 parity proof is cited as the existing evidence; no task re-derives it.
- The remaining gap is closed: the `.Requires()` predicate-DSL path and SCIP/source-extraction assumptions are deprecated mechanically (`[Obsolete]` with a named successor), not documentation-only.
- The documented expressibility limit (CLR-free ⊕ polymorphic) is recorded as a known limit of the descriptor model, with this contract's reference-based relations (DR-6) named as the reason it does not bind here.
- Public-API obligations are honored: `PublicAPI.Unshipped.txt` is updated in the same change as any public surface edit (RS0016/RS0017 are active and would otherwise fail the build).

### DR-12: Reconcile upstream, including our own docs

Under #153's coordination rule, correct the premises — on **both** sides, including ours.

**Acceptance criteria:**
- **Strategos's own docs are corrected first:** `docs/src/content/docs/reference/platform-architecture.md` and `ontology-theoretical-grounding.md` still describe `ComposedOntology` as "Source-generated in host assembly." exarchos's ADR faithfully mirrors *our* published docs, so fixing only their copy leaves the source of the error in place and it regenerates.
- exarchos's `system-index.md` and DKG DR-1 are corrected, and DR-1's `DomainOntology`/`BoundToTool`/`ComposedOntology` criteria are renegotiated against what this contract actually delivers.
- basileus's design doc is corrected too — not merely commented on. Its node list, `Decision.status` requirement, and front-matter vocabulary are the artifact its implementers read; leaving it stale reproduces on basileus the exact defect diagnosed in #157.
- The axis set (DR-1/DR-3/DR-4) and the registered core (DR-2) are posted to both consumer issues with a **stated objection window that closes before implementation starts** — the previous revision ran the window concurrently with implementation, giving it zero length.

## Technical Design

The contract lands as `.tsp` files under `src/Strategos.Contracts/Ontology/`, imported from `main.tsp`, emitted by the existing pipeline to JSON Schema (`schemas/json-schema`) and C# records (`Generated/`). Both outputs are checked in, and the two existing CI gates — `contracts-codegen-guard` and `contracts-schema-diff` — are what hold them honest. No new emitter, no new output project, no new reference edge.

This is deliberately *less* machinery than the previous revision. Every mechanism it added — the descriptor emitter, the cross-domain resolver, the canonicalizer, the address algorithm — was either unbuildable in the current project graph or duplicated something a consumer already ships.

Invariant posture: INV-2 holds trivially (nothing touches `Strategos.Ontology*` except DR-11's deprecations). INV-5 is not stretched — no new validation tier is claimed, and the enforcement story is the CI gate that exists. INV-6/INV-7 apply to the emitted records as to any Contracts type. INV-8 is unaffected here and closed by the already-shipped parity proof.

Release track: `contracts-v` for DR-1..DR-10 and DR-12; DR-11's deprecations touch `Strategos.Ontology`, which MinVer-versions off the **uncut `v2.10.0` product tag** — stated plainly this time, and a reason to keep DR-11 minimal.

## Integration Points

- `src/Strategos.Contracts/Ontology/*.tsp` — axes, registered core, typed bodies, reference relations, provenance
- `src/Strategos.Contracts/main.tsp` — imports
- `src/Strategos.Contracts/schemas/**`, `src/Strategos.Contracts/Generated/**` — regenerated, committed
- `src/Strategos.Contracts/Strategos.Contracts.csproj` — `ContractsVersion` bump
- `src/Strategos.Contracts.Tests/PackagingTests.cs` — version assertions
- `src/Strategos.Ontology/**` — DR-11 deprecations + `PublicAPI.Unshipped.txt`
- `docs/src/content/docs/reference/platform-architecture.md`, `ontology-theoretical-grounding.md` — DR-12

## Exploration

The divergent loop ran twice. Round 1 chose TypeSpec-first authoring of a `DomainOntology`; a 3-voter adversarial panel then refuted it unanimously, and the refutation is the input to Round 2.

**What the panel falsified.** The premise that the descriptor-first path is *silently* unvalidated — AONT205 (Error, builder-runtime) and AONT037 (analyzer tier) already cover it, and INV-5's tier 1 **is** builder-runtime, so a runtime tier the analyzer cannot reach is conformant. That premise was the sole ground for rejecting the C#-DSL alternative. Also falsified: that basileus encodes rejection as `Decision.status` (its statuses are `proposed|accepted|superseded`, with no `rejected`); that exarchos's P-1 gates their milestone (P-0 depends on taxonomy-v2, and their risk row says settle before **P-3**); that #115 needs an end-to-end proof (it shipped in v2.9.0); and that the work rides the fast `contracts-v` track (a third of it touched the uncut product track).

**Why not simply switch to the C#-DSL alternative.** With its rejection undercut, it was re-examined and still fails the binding constraint: exarchos cannot consume .NET (C7), so a C#-authored ontology reaches them only through an exporter — and the panel showed the export side is exactly where the previous design broke (JSON Schema cannot express algorithms; no JS artifact is published). The C# DSL also cannot express a CLR-free polymorphic ontology, which is a limit of the descriptor model, not of the authoring surface.

**Why "contract, not ontology" wins.** It is the only option under which AONT205 and the interface limit are constraints *respected* rather than *fought*, and it is what both consumers actually consume. It also makes the two hardest problems disappear rather than solving them: no emitted actions means no ingested-intent violation, and reference-based relations mean no polymorphic endpoints.

**What the panel did not touch,** and is therefore carried forward unchanged: the intrinsic/adjudicative scope boundary, and the core thesis that one canonical semantics serving both consumers is worth having while neither has frozen.

## Alternatives considered

- **TypeSpec-emitted `DomainOntology` (Revision 1).** Rejected on mechanism: AONT205 forbids actions on ingested descriptors; polymorphic endpoints require a CLR interface; the descriptor emitter had no viable output project (`Strategos.Contracts` is a zero-reference AOT-compatible leaf); and "fails the build" overstated a CI-only guard.
- **C#-DSL authoring plus a schema exporter.** Rejected: exarchos structurally cannot consume .NET, and the exporter is precisely where Revision 1 broke. Its original rejection rationale was wrong, so it was re-evaluated on merit and still loses on the binding constraint.
- **Content-addressed claim identity (Revision 1 DR-5/DR-6/DR-7).** Cut. The pre-image was never defined, `ClaimHash` was circular against non-optional provenance, JSON Schema cannot carry a hash function so exarchos would hand-write it from prose, and exarchos already ships a `ContentAddressedStore` — a second hash notion imposed on a consumer that has one. If cross-tier identity is later wanted, it is specifiable then against two corpora that exist.
- **Closed kind union (Revision 1 DR-1).** Cut. It forbade basileus's untyped-`Document` degradation path, froze the axis exarchos says is *not* load-bearing, and manufactured the release-per-kind cost it was meant to mitigate.

## Open Questions

- **Does exarchos accept a contract in place of a `DomainOntology`?** Their DR-1 AC1/AC3/AC5 name `DomainOntology`, `ComposedOntology`, and `BoundToTool` actions — all shown here to be unreachable by emission. DR-12 renegotiates those criteria; if exarchos insists on emitted actions, that is a design conflict to resolve before implementation, not during.
- **Who owns basileus's ontology wiring?** This contract gives basileus records; turning them into a queryable graph with actions is hand-authored C#. Assumed to be basileus-side (they own the service), and a reference wiring is out of scope. Worth confirming with them.
- **Relation cardinality.** DR-6 requires it specified; the specific cardinalities (may a `Decision` select two `Option`s?) are a decomposition decision to settle against both consumers' traversal needs.
- **Which store wins — deliberately unanswered.** Out of scope by construction; unchanged from Revision 1 and undisputed by the panel.

## Decomposition

> Authored by `/exarchos:plan`. DR-1 … DR-12 above are the decomposition source.
