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

The decomposition maps every task to one or more DR-N from the section above.

### Scope

**Target:** Full design — DR-1 … DR-12.

**Excluded, with rationale:**
- **Content-addressed identity and canonical byte-form.** Cut in this revision (see Alternatives). Not deferred-with-a-placeholder — no task, no DR, no half-specified field.
- **basileus's ontology wiring.** This contract delivers records; turning them into a queryable graph with actions is hand-authored C# in their service. Flagged in Open Questions for confirmation.
- **The authority question** — out of scope by construction, unchanged.

**Contract-file layout (deliberate).** The contract is three `.tsp` files, and the tasks authoring them are **serialized**, with the single `main.tsp` import edit batched into the first. The previous revision had five tasks each creating a `.tsp` with no import and a worktree note that contradicted their file lists; this shape removes the conflict rather than documenting it.

### Traceability matrix (DR-N → tasks)

| DR | Requirement | Tasks |
|----|-------------|-------|
| DR-1 | Reliability class is the frozen axis | 003 |
| DR-2 | Open kind vocabulary with a registered core | 004, 011 |
| DR-3 | Status and lifecycle axes | 003 |
| DR-4 | Scope is on the claim | 003 |
| DR-5 | Typed body fields | 004 |
| DR-6 | Relations are typed body references | 005 |
| DR-7 | Provenance carriage | 003 |
| DR-8 | Two projections, no emitted ontology | 006, 007 |
| DR-9 | Additive evolution, honestly enforced | 008, 009 |
| DR-10 | Failure modes | 010, 011, 012 |
| DR-11 | #115 rebased on what already shipped | 013, 014 |
| DR-12 | Reconcile upstream, including our own docs | 001, 002, 015, 016 |

### Tasks

### Task 001: Correct Strategos's own stale ontology docs

**Risk Tier:** low
**Test Layer:** unit
**Implements:** DR-12

**Files:**
- `docs/src/content/docs/reference/platform-architecture.md`
- `docs/src/content/docs/reference/ontology-theoretical-grounding.md`

**Verification:** low — static. Documentation correction; no code surface.

**Steps:**
1. Remove or correct every description of `ComposedOntology` as "Source-generated in host assembly" — the ontology is analyzer-only and `ComposedOntology` does not exist in code.
2. Correct any remaining `Agentic.*` naming to `Strategos.*`.
3. This runs **first**: exarchos's ADR mirrors these pages, so correcting their copy while ours stays stale lets the error regenerate.

**Dependencies:** None
**Parallelizable:** Yes

### Task 002: Post the axes and registered core, and close the objection window

**Risk Tier:** low
**Test Layer:** unit
**Implements:** DR-12

**Files:**
- `docs/coordination/2026-08-07-claim-contract-freeze.md`

**Verification:** low — static. The artifact is the coordination record.

**Steps:**
1. Write the freeze record: the DR-1/DR-3/DR-4 axes, the DR-2 registered core, the reference-relation model (DR-6), and the three cuts (emitted ontology, closed union, content-addressing) with their causes.
2. Post to `lvlup-sw/exarchos#1745`, `lvlup-sw/basileus#236`, `lvlup-sw/strategos#157` and `#115`, explicitly renegotiating exarchos's `DomainOntology`/`ComposedOntology`/`BoundToTool` criteria against what this contract delivers.
3. State a window with a **closing date that precedes implementation start**, and record the disposition of every objection in this file before Task 003 begins.
4. **This gates the contract-authoring tasks.** A window that runs concurrently with implementation has zero length and enforces nothing.

**Dependencies:** 001
**Parallelizable:** No — it is the freeze gate.

### Task 003: Claim envelope — reliability class, status and lifecycle axes, scope, provenance carriage

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** acceptance
**Implements:** DR-1, DR-3, DR-4, DR-7

**Files:**
- `src/Strategos.Contracts/Ontology/Claims.tsp`
- `src/Strategos.Contracts/main.tsp`
- `src/Strategos.Contracts.Tests/Ontology/ClaimEnvelopeTests.cs`

**Verification:** high — scoped tests + `check_test_adequacy` kill-probe + the Contracts round-trip suite. This is the north-star acceptance test for the frozen axes; it stays red until 004 and 005 land.

**Steps:**
1. Author the envelope: `reliability` (`authored|observed|measured`), `status` (`proposed|accepted|superseded`), `lifecycle` (`binding|durable|reinforced|episodic`), `scope {repo, featureId, branch}`, and provenance. All non-optional. snake_case wire names per the `GateClass` precedent.
2. Batch **all three** contract-file imports into `main.tsp` here, so 004 and 005 touch no shared file.
3. Test `Claim_WithoutReliabilityClass_Unrepresentable`.
4. Test `Claim_WithoutScope_Unrepresentable`.
5. Test `Claim_ObservedClass_NeverCoercedToAuthored`.
6. Test `Constraint_LifecycleBinding_ExpressibleWithoutConsumerExtension`.

**Dependencies:** 002
**Parallelizable:** No (foundation)

### Task 004: Open kind vocabulary with the registered core and typed bodies

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Acceptance Test Ref:** 003
**Implements:** DR-2, DR-5

**Files:**
- `src/Strategos.Contracts/Ontology/ClaimKinds.tsp`
- `src/Strategos.Contracts.Tests/Ontology/ClaimKindTests.cs`

**Verification:** high — scoped tests + kill-probe + round-trip.

**Steps:**
1. Author the eight core kinds as a **registered, enumerable** core — not a closed union — each with its typed body (forces, alternatives, costs, criteria, ordinal, cause).
2. Author the extension path so an unregistered kind is carried with an open body.
3. Test `Kind_Unregistered_RoundTripsWithoutLoss`.
4. Test `Kind_Unregistered_ReadableAsUntypedCarrier` — basileus's front-matter-less majority path.
5. Test `Kind_CoreEight_EnumerableFromSchema` — a consumer can tell core from extension.
6. Test `ClaimBody_NoKindCarriesBareBodyString`.

**Dependencies:** 003
**Parallelizable:** No

### Task 005: Reference relations with specified direction and cardinality

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Acceptance Test Ref:** 003
**Implements:** DR-6

**Files:**
- `src/Strategos.Contracts/Ontology/ClaimRelations.tsp`
- `src/Strategos.Contracts.Tests/Ontology/ClaimRelationTests.cs`

**Verification:** high — scoped tests + kill-probe + round-trip.

**Steps:**
1. Author relations as **typed reference fields on claim bodies**, targeting claim ids — never standalone edge objects, so exarchos's anti-drift property holds and no polymorphic CLR interface is implicated.
2. Cover basileus's full declared vocabulary including `references`, which the previous revision dropped.
3. Specify direction **and cardinality** for each relation (basileus #236 requires cardinality).
4. Test `Relation_SupersedesPointer_FoldsForwardDeterministically`.
5. Test `Relation_BasileusFrontMatterVocabulary_MapsWithoutLoss`.
6. Test `Relation_Cardinality_SpecifiedForEveryRelation`.

**Dependencies:** 004
**Parallelizable:** No

### Task 006: Regenerate and commit both projections

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Implements:** DR-8

**Files:**
- `src/Strategos.Contracts/schemas/**`
- `src/Strategos.Contracts/Generated/**`
- `src/Strategos.Contracts.Tests/Ontology/ProjectionParityTests.cs`

**Verification:** high — scoped tests + kill-probe + integration. The previous revision left `schemas/` and `Generated/` unowned while two CI gates watch them.

**Steps:**
1. Run `scripts/contracts-codegen.sh`; commit regenerated JSON Schema and C# records.
2. Confirm `contracts-codegen-guard` passes (a fresh run must produce no diff).
3. Test `Projection_JsonSchemaAndCSharp_ValidateSameFixtures`.
4. Test `Projection_NoObjectTypeDescriptorEmitted` — the C# projection is plain records, so no ingested-descriptor path and no AONT205 exposure.

**Dependencies:** 005
**Parallelizable:** No

### Task 007: Contracts stays a dependency-free leaf

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Implements:** DR-8

**Files:**
- `src/Strategos.Contracts.Tests/Ontology/PackageLeafTests.cs`

**Verification:** high — scoped tests + kill-probe. This pins the constraint that made the previous revision's emitter unbuildable.

**Steps:**
1. Test `Contracts_ProjectReferences_IsEmpty` — asserts the published schema package never acquires a dependency on `Strategos.Ontology`.
2. Test `Contracts_AotCompatibility_RemainsEnabled`.
3. Test `Contracts_Projection_EmitsNoOntologyActionsOrInterfaces`.

**Dependencies:** 006
**Parallelizable:** Yes

### Task 008: ContractsVersion bump and packaging assertions

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Implements:** DR-9

**Files:**
- `src/Strategos.Contracts/Strategos.Contracts.csproj`
- `src/Strategos.Contracts/CHANGELOG.md`
- `src/Strategos.Contracts.Tests/PackagingTests.cs`

**Verification:** high — scoped tests + kill-probe. `PackagingTests` hard-asserts the current version and goes red without this; the previous revision left it unowned.

**Steps:**
1. Bump `<ContractsVersion>` to the next minor (additive schema family) and update `CHANGELOG.md`.
2. Update `PackagingTests` version assertions in the same change.
3. Test `Packaging_NupkgVersion_MatchesContractsVersion` — `publish-contracts` fails closed when the tag disagrees.

**Dependencies:** 006
**Parallelizable:** Yes

### Task 009: Additive-evolution policy and the schema-diff gate

**Risk Tier:** medium
**Test Layer:** integration
**Implements:** DR-9

**Files:**
- `src/Strategos.Contracts/Ontology/EVOLUTION.md`
- `src/Strategos.Contracts.Tests/Ontology/EvolutionPolicyTests.cs`

**Verification:** medium — scoped tests + kill-probe.

**Steps:**
1. State the policy: new kinds and new optional fields are non-breaking; narrowing an enum or requiring a new field is breaking and needs a version bump.
2. Confirm `contracts-schema-diff` reports NON-BREAKING for this change.
3. Test `Evolution_NewRegisteredKind_IsNonBreaking` — the property that dissolves the release-per-kind cost.
4. State explicitly that enforcement is **CI-scoped**, not a build failure — codegen is invoked by no MSBuild target.

**Dependencies:** 006
**Parallelizable:** Yes

### Task 010: Failure modes — schema-skew detection

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Implements:** DR-10

**Files:**
- `src/Strategos.Contracts/Ontology/Claims.tsp`
- `src/Strategos.Contracts.Tests/Ontology/SchemaSkewTests.cs`

**Verification:** high — scoped tests + kill-probe + integration.

**Steps:**
1. Carry the taxonomy version on the claim.
2. Test `Read_SkewedTaxonomyVersion_DetectedAndReported`.
3. Test `Read_SkewedVersion_NeverMisParsedSilently`.

**Dependencies:** 006
**Parallelizable:** No — shares `Claims.tsp` with 003; sequenced after it.

### Task 011: Failure modes — unrecognized kinds degrade, never error

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Implements:** DR-2, DR-10

**Files:**
- `src/Strategos.Contracts.Tests/Ontology/GracefulDegradationTests.cs`

**Verification:** high — scoped tests + kill-probe + integration. This is the criterion whose inversion in the previous revision would have broken basileus's majority ingestion path.

**Steps:**
1. Test `Read_UnregisteredKind_DegradesGracefully_NeverThrows`.
2. Test `Read_UnregisteredKind_NeverCoercedToFallbackKind`.
3. Test `Read_UnregisteredKind_RelationsPreserved` — no silent relation loss.

**Dependencies:** 006
**Parallelizable:** Yes

### Task 012: Failure modes — dangling references resolve later

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Implements:** DR-10

**Files:**
- `src/Strategos.Contracts.Tests/Ontology/DanglingReferenceTests.cs`

**Verification:** high — scoped tests + kill-probe + integration. JSON Schema cannot express cross-object referential integrity, so this is a contract obligation pinned by conformance fixtures.

**Steps:**
1. Test `Reference_UnknownTargetId_RecordedAsDangling_NotDropped` — basileus's webhook ingestion is inherently out of order.
2. Test `Reference_TargetArrivesLater_Resolves`.
3. Test `Reference_Dangling_SurfacedInDiagnostics`.

**Dependencies:** 005
**Parallelizable:** Yes

### Task 013: Deprecate the code-symbol paths mechanically

**Risk Tier:** high
**Boundary Touching:** true
**Test Layer:** integration
**Implements:** DR-11

**Files:**
- `src/Strategos.Ontology/Builder/IActionBuilderOfT.cs`
- `src/Strategos.Ontology/PublicAPI.Unshipped.txt`
- `src/Strategos.Ontology.Tests/Deprecation/CodeSymbolDeprecationTests.cs`

**Verification:** high — scoped tests + kill-probe + integration. **Tiered high deliberately:** this is a published public-API change on `LevelUp.Strategos.Ontology` with RS0016/RS0017 active. The previous revision stamped the equivalent work `medium`, which the panel flagged as silently reducing verification depth.

**Steps:**
1. Mark `.Requires()` and the SCIP/source-extraction assumptions `[Obsolete]` with rationale and a **named** successor.
2. Update `PublicAPI.Unshipped.txt` in the same change — omitting it fails the build.
3. Test `Deprecation_RequiresPath_CarriesObsoleteWithNamedSuccessor`.
4. Note the consumer-notification obligation: this ships on the uncut `v2.10.0` product track, not `contracts-v`.

**Dependencies:** None
**Parallelizable:** Yes — touches no file any other task edits.

### Task 014: Record the CLR-free ⊕ polymorphic expressibility limit

**Risk Tier:** low
**Test Layer:** unit
**Implements:** DR-11

**Files:**
- `docs/src/content/docs/reference/ontology-polyglot-limits.md`

**Verification:** low — static. Documentation of an existing, already-proven limit.

**Steps:**
1. Record that a CLR-free descriptor cannot also be a polymorphic interface target, citing the shipped parity proof's two-dimension split as the evidence.
2. Name this contract's reference-based relations (DR-6) as the reason the limit does not bind here.
3. Cite the v2.9.0 parity proof as the existing #115 evidence so no task re-derives it.

**Dependencies:** None
**Parallelizable:** Yes

### Task 015: Correct exarchos's ADR and DKG DR-1

**Risk Tier:** low
**Test Layer:** unit
**Implements:** DR-12

**Files:**
- `docs/coordination/2026-08-07-exarchos-reconciliation.md` (the local record; the edits land cross-repo in `exarchos:docs/adrs/system-index.md` and `exarchos:docs/specs/2026-08-05-design-knowledge-graph.md`)

**Verification:** low — static.

**Steps:**
1. Correct `Agentic.Ontology` → `Strategos.Ontology`/`Strategos.Contracts` and the "source-generated compile-time descriptors" mechanism claim.
2. Remove the `ComposedOntology` reference.
3. Replace DR-1's `DomainOntology`/`BoundToTool` criteria with what this contract delivers, per the Task 002 disposition.
4. Update the `#1745` P-1 dependency to reference this spec.

**Dependencies:** 002
**Parallelizable:** Yes

### Task 016: Correct basileus's design doc

**Risk Tier:** low
**Test Layer:** unit
**Implements:** DR-12

**Files:**
- `docs/coordination/2026-08-07-basileus-reconciliation.md` (the local record; the edit lands cross-repo in `basileus:docs/designs/2026-06-02-why-context-engine.md`)

**Verification:** low — static.

**Steps:**
1. Reconcile the node list, the `Decision.status` requirement, and the DR-5 front-matter vocabulary against this contract.
2. Record the #236 disposition — answered by this contract, or a justified split.
3. Edit the doc, not merely a comment: leaving it stale reproduces on basileus the exact defect this spec diagnoses in #157.

**Dependencies:** 002
**Parallelizable:** Yes

### Parallelization

**Critical path:** 001 → 002 (objection window **closes**) → 003 → 004 → 005 → 006. The freeze gate is on the critical path by design; the previous revision ran it concurrently with implementation, which gave it zero length.

**Wave 0:** 001, then 002. Also 013 and 014, which touch no shared file and need no freeze.

**Wave 1 (contract authoring, strictly serial):** 003 → 004 → 005. Serialized because they form one coherent contract; all `main.tsp` imports are batched into 003 so 004 and 005 share no file.

**Wave 2 (projection + release):** 006, then 007 ∥ 008 ∥ 009 ∥ 011 ∥ 012 in parallel — each owns a distinct test file.

**Wave 3:** 010 after 006 (it shares `Claims.tsp` with 003, so it is sequenced, not parallel).

**Wave 4:** 015 ∥ 016 after 002.

**File-conflict audit:** `Claims.tsp` is touched by 003 and 010 (sequenced). `main.tsp` is touched only by 003. `schemas/**` and `Generated/**` only by 006. `PublicAPI.Unshipped.txt` only by 013. Every other task owns a distinct test file. No two tasks marked parallel share a file.

### Completion checklist

- [ ] Every DR-N in `## Requirements` maps to at least one task in the matrix
- [ ] Every task `Implements:` a DR-N that exists in this document
- [ ] Every task carries a `riskTier` stamp; public-API and schema surfaces are tiered `high`
- [ ] Medium/high-tier tasks carry adequacy-judged tests (test-after); low-tier tasks lean on static analysis
- [ ] No two parallel tasks share a file
- [ ] The objection window closes before contract authoring begins
- [ ] Open questions resolved OR explicitly deferred with rationale
- [ ] Ready for `plan-review`
