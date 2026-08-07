# Research: Deriving the design-rationale claim taxonomy from prior art

**Date:** 2026-08-07 · **Workflow:** `claim-taxonomy-derivation` (discovery) · **Feeds:** `strategos#157`, `strategos#115`, `basileus#236`, `lvlup-sw/exarchos#1745` P-1

> **Why this exists.** Two consecutive `/exarchos:ideate` → `/exarchos:plan` passes for the claim contract were refuted 3/3 by adversarial plan-review panels. Both panels, independently, found the taxonomy **asserted rather than derived**. That is precisely what `basileus#236` specifies: *"a committed research doc selecting/justifying the node + edge taxonomy against design-rationale prior art (IBIS, QOC, DRL, ADR ontologies), with a worked mapping of ≥3 existing Basileus ADRs/decisions onto it."* This report supplies it.

## 1. Method

Five questions were carried in from the refutations:

1. Derive a node + edge taxonomy from prior art rather than asserting one.
2. Validate it against ≥3 real basileus ADRs and ≥3 real exarchos decisions.
3. Resolve the trust-class arity problem — exarchos's `authored|observed|measured` is deterministic under their **C6** ("no LLM on the write path"); basileus's `declared|extracted` is LLM-inferred and lower-trust. Three values cannot carry four classes.
4. Decide what constitutes claim identity, given that cutting content-addressing in revision 2 accidentally left no identity at all.
5. Decide whether relations need reified edge objects carrying attributes, and specify per-relation cardinality.

Prior art was read from primary and survey sources; the corpora were read from `origin/main` in both consumer repos. Sources are listed in workflow state under `artifacts.sources`.

## 2. Prior-art survey

| Model | Nodes | Relations | Notable |
|---|---|---|---|
| **IBIS** (Kunz & Rittel 1970; gIBIS, Conklin & Begeman 1988) | Issue, Position, Argument, Other | 9 legal-move link types — `responds-to`, `supports`, `objects-to`, `generalizes`, `specializes`, `questions`, `is-suggested-by`, `replaces` | Only Positions may respond-to an Issue; Arguments attach to Positions. **Rebuttal is not representable** except as an opposing mutually-exclusive Position. An Issue can `replace` another — supersession, in embryo. **No Decision node** — a known deficiency. |
| **QOC** (MacLean, Young, Bellotti & Moran 1991) | Question, Option, Criterion (+ Assessment, Argument) | Assessments as **positive/negative** links from Criterion to Option, **backed by Arguments** referring to Data/Theories | The Criterion↔Option assessment is where Toulmin's *warrant* lives. Buckingham Shum's evaluation found authors could not reliably tell "what counts as an Option or Criterion". |
| **DRL** (Lee & Lai 1991/92, MIT CCS TR#121) | Alternative, Goal, Claim, Decision Problem; spaces: argument, alternative, evaluation, criteria, issue | `Achieves`, supports, denies, is-a-subgoal-of | Most expressive of the three, explicitly built to overcome IBIS/QOC limits. **A `Claim` carries attributes — plausibility, degree, evaluation** — and the `Achieves` relation between Alternative and Goal is *itself argued about*. |
| **Toulmin** (1958) | claim, data, warrant, backing, qualifier, rebuttal | data → qualified claim via warrant; backing supports warrant; rebuttal defeats | All three DR models claim kinship. **Qualifier** (how strongly held) is orthogonal to **warrant** (what licenses the inference) and to **data** (what grounds it). |
| **MADR / ADR** (Nygard; adr.github.io; Structured MADR) | one record: Context-and-Problem, Decision Drivers, Considered Options, Decision Outcome, Consequences (good/bad/neutral), Confirmation | `status` enum, `superseded by ADR-NNNN`, `related` | Status: `proposed \| rejected \| accepted \| deprecated \| superseded`. Structured MADR adds a JSON Schema with required `title`, `status`, `created`, `author`, and `x-` extension fields. |
| **PROV** (W3C PROV-O / PROV-DM) | Entity, Activity, Agent | `wasAttributedTo` (→Agent), `wasDerivedFrom` (→Entity), `wasGeneratedBy` (→Activity); **Qualification Pattern**: `qualifiedDerivation`, `qualifiedAttribution` | Attribution (component 3) and derivation (component 2) are **deliberately separate**. PROV-ISSUE-368 removed agents from derivation relations outright: *"we decouple components 2 and 3 in the data model."* |

### 2.1 The convergent core

Every argumentation-based model contains the same spine, under different names:

| Role | IBIS | QOC | DRL | MADR |
|---|---|---|---|---|
| the open question | Issue | Question | Decision Problem | Context and Problem Statement |
| a candidate | Position | Option | Alternative | Considered Option |
| the basis for judging | *(in Argument)* | **Criterion** | **Goal** | **Decision Driver** ("force") |
| the justification | **Argument** | Argument (backing an Assessment) | **Claim** | "Good/Bad, because …" |
| the choice | *(absent)* | *(implicit)* | *(evaluation result)* | **Decision Outcome** |
| what follows | *(absent)* | *(absent)* | *(absent)* | **Consequences** |

**Two findings fall straight out of this table, and both contradict the previous design revisions.**

**Finding A — the taxonomy was missing `Argument` entirely.** IBIS has Argument, QOC has Argument backing Assessments, DRL has Claim, Toulmin has warrant+backing. It is the single most universal element in the literature — the carrier of *why* — and neither previous revision had it. Typed body fields ("forces, alternatives, costs") are not a substitute: they attach justification to a claim rather than making it addressable, so an argument cannot be superseded, contradicted, or cited independently. This is a direct cause of both panels' "asserted, not derived" verdict.

**Finding B — `Criterion` and `Constraint` are different things and were conflated.** QOC's Criterion, DRL's Goal, and MADR's Decision Driver are *weighted evaluation bases* — soft, comparative, possibly in tension. A Constraint is a *binding rule* that admits no trade-off. Collapsing them (as both revisions did) destroys the comparison structure QOC and DRL exist to express, and it is why "forces" had no home except as prose in a body field.

### 2.2 Derived core vocabulary

Justified per element, with its prior-art warrant:

| Kind | Derived from | Why it must be its own kind |
|---|---|---|
| `Problem` | IBIS Issue · QOC Question · DRL Decision Problem · MADR Context | The addressable unit of "what is open". Universal across all four. |
| `Option` | IBIS Position · QOC Option · DRL Alternative · MADR Considered Option | Universal. Must be addressable so that non-selected options remain citable. |
| `Criterion` | QOC Criterion · DRL Goal · MADR Decision Driver | The comparison basis. **Distinct from Constraint** (Finding B). Carries weight. |
| `Argument` | IBIS Argument · QOC Argument · DRL Claim · Toulmin warrant/backing | The justification carrier (Finding A). Supports or objects-to. Carries a qualifier. |
| `Decision` | MADR Decision Outcome | IBIS's known gap; MADR supplies it. Selects an Option. |
| `Consequence` | MADR Consequences (good/bad/neutral) | Only MADR has it; it is what makes a decision auditable after the fact. |
| `Constraint` | MADR k.o. criterion · DRL mandatory Goal | A binding rule; no trade-off. `lifecycle: binding`. |
| `Rejection` | Toulmin **rebuttal** · MADR status `rejected` | **Now properly derived.** IBIS explicitly *cannot* represent rebuttal; that is a documented deficiency, not a design choice to copy. Making rejection addressable fixes it. |

`Pattern` and `Concept` are **not** in the argumentation prior art — they are knowledge-management kinds basileus wants. They belong in the extension space, not the derived core (see §6).

## 3. Worked mappings — basileus corpus

Corpus: 41 files under `docs/adrs/` on `origin/main`.

**(1) `ontological-data-fabric.md`** — "Exarchos ↔ Basileus Coordination & Ontological Data Fabric", Status: Accepted.
- → `Decision` (the fabric architecture), with `Supersedes` → 4 prior design docs **plus its own prior v2** (heterogeneous supersession targets).
- → `Argument` × N from "Research grounding (**credibility only**; content is inlined above)" — nine research docs cited explicitly as credibility-bearing rather than content-bearing. **This is Toulmin *backing*, in production, already labelled as such.**
- → cross-domain references to `basileus #112…#168`, `exarchos #1109/#1125`, `strategos #16…#48`.
- Note: its "Authority: this ADR is the single authoritative source of truth" is an authority-lattice assertion — basileus's open Q7.

**(2) `why-context-engine.md`** (design, but the live decision record) — supersedes "the code-symbol / SCIP / code-chunk-retrieval functionality of ODF v3.2", while explicitly *retaining and repointing* its intent/validation/ontology concepts.
- → `Decision` with a **partial** `Supersedes`: part of the target is superseded, part is retained. A boolean supersession edge cannot express this; it needs a scope qualifier on the relation — evidence for §7.

**(3) `distributed-sdlc-pipeline.md`** — 3 supersession references.
- → `Decision` + `Consequence`s; its DI-1/DI-2/DI-3 design invariants map to `Constraint` with `lifecycle: binding`, not to `Criterion`.

**Corpus finding — the `status` field does not mean what basileus's design doc says it means.** Actual values across the ADR corpus:

| Value | Count |
|---|---|
| Complete | 17 |
| Proposed | 5 |
| Implemented | 2 |
| "Partial Complete (Linear Saga Generation)" | 1 |
| "Milestone 15 Complete (Fork/Join Implementation…)" | 1 |
| "✅ Complete - Mock implementation with realistic data" | 1 |
| "Deferred pending Meta Wearables SDK expansion" | 1 |
| *(one value is an entire paragraph)* | 1 |

**Zero** ADRs carry `accepted` or `superseded` as a status value. The corpus is recording **implementation progress**, not decision lifecycle — while supersession, which *is* a decision-lifecycle fact, is expressed as an explicit `Supersedes:` list in the body (13 occurrences in ODF alone).

This vindicates exarchos's separation — *"the tracking half of an issue is not rationale, it is workflow state"* — and refutes the `status: proposed|accepted|superseded` axis that basileus's design doc claims and that both previous revisions faithfully copied. **Decision lifecycle should be derived from supersession/rejection relations, not from a status enum; work status belongs to workflow state.**

## 4. Worked mappings — exarchos corpus

From the DKG spec's "Decisions taken" table (D1–D6) — real decisions with explicit rationale:

- **D1** "Assertion is an event; the corpus is a projection" → `Decision`, selecting an `Option`, justified by `Argument`s (concurrency, provenance, replication) each `supports`-linked, with `Criterion` = *solved at the appender*.
- **D2** "The schema is authored in Strategos TypeSpec as a `DomainOntology`" → `Decision` whose supporting `Argument` cites `Constraint` **C7** (single-file binary) as binding. Note the derived model exposes what the flat form hid: D2's *conclusion* survives C7, but its *premise* about `DomainOntology` was falsified — separating Decision from Argument makes that failure addressable without retracting the decision.
- **D6** "`evidence` is not a new claim kind" → `Rejection` of an Option ("add an Evidence kind"), justified by an `Argument` citing a prior defect (P02-03). **This is exactly the case IBIS cannot represent** and Toulmin's rebuttal can.
- **DR-14** "No write-only claim kinds" → `Constraint`, `lifecycle: binding`, whose reachability predicate is a *closure rule over the graph* rather than a field.

## 5. Q3 — the trust-class arity problem, resolved

PROV settles this. **Attribution and derivation are separate components**, and W3C deliberately decoupled them (PROV-ISSUE-368: *"agents should not be mentioned in derivation relations… we decouple components 2 and 3"*). Toulmin adds a third, independent axis: the **qualifier** — how strongly a claim is held, which is neither who produced it nor how.

The arity conflict is an artifact of collapsing three axes into one enum:

| Axis | Prior art | Values | Answers |
|---|---|---|---|
| **Attribution** | PROV `wasAttributedTo` → Agent | `human` \| `agent` \| `system` | *who is responsible* |
| **Derivation** | PROV `wasDerivedFrom` (+ subtypes) | `asserted` \| `observed` \| `measured` \| `inferred` | *how it came to be* |
| **Qualifier** | Toulmin qualifier; DRL plausibility/degree | confidence value | *how strongly it is held* |

Both consumers then map cleanly, with no collision:

| Consumer value | Attribution | Derivation |
|---|---|---|
| exarchos `authored` | human/agent | `asserted` |
| exarchos `observed` | system | `observed` (reconciler, deterministic) |
| exarchos `measured` | system | `measured` (gate runner) |
| basileus `declared` | human | `asserted` (front-matter) |
| basileus `extracted` | agent | **`inferred`** (LLM) |

`inferred` gets its own value instead of colliding with `observed`. **exarchos's C6 ("no LLM on the write path") becomes structurally enforceable** — they refuse `derivation: inferred` at the boundary — rather than depending on a mapping convention. This is the mechanical enforcement the previous revisions lacked.

## 6. Q4 — claim identity, resolved

Prior art is unanimous that identity is an **assigned stable identifier**, not a content hash:

- MADR/ADR: numbered records; `superseded by ADR-0123`; Structured MADR's `related` matches `^[a-zA-Z0-9_-]+\.md$` and `title` is required.
- PROV: every Entity has an id; `wasDerivedFrom` relates *identified* entities; the derivation itself may carry an **optional** id.

Content-addressing and identity are **different roles**. A content hash answers "are these the same bytes" (dedup, integrity, reinforcement). An id answers "is this the same claim across revisions" — which a hash cannot do, because any edit changes the hash while the claim persists. Revision 2 cut the hash and accidentally cut identity with it; that was the error, not the hash removal itself.

**Resolution:** a required opaque stable `id` plus a required `title` (both consumers need it; basileus requires `title` in front-matter). A content digest is **optional and separate**, serving dedup/reinforcement only. This also repairs the orphaned `reinforced` lifecycle value: its producer is content-digest collapse, which becomes expressible again without the digest being load-bearing for identity.

## 7. Q5 — reification, resolved

**Unanimous: relations need attributes, and the prior art gives the exact pattern.**

- **DRL** — a `Claim` carries plausibility/degree/evaluation, and the `Achieves` relation between Alternative and Goal is itself argued about. The most expressive model reifies its central relation.
- **QOC** — Assessments are positive/negative links **backed by Arguments**; the link carries content.
- **PROV** — the **Qualification Pattern** exists for exactly this: keep the plain binary relation for the common case (`wasDerivedFrom`), and attach `qualifiedDerivation` when the relation needs attributes.
- **Strategos, already shipped** — `RationaleOntologyFixture` models `Supersedes`/`Motivates`/`ConflictsWith` as reified `ObjectKind.Association` objects carrying `rationale`/`weight`/`severity`.
- **basileus corpus** — the ODF→why-context-engine *partial* supersession (§3.2) cannot be expressed by a bare pointer.

So revision 2's "relations as bare typed body references" discarded a capability that every expressive model and our own shipped code provides. But revision 1's "all relations are standalone edge objects" over-corrected: exarchos's anti-drift property (*"edges are never separately authored, so they cannot drift from the types"*) is real and valuable.

**PROV's pattern is the synthesis, and it is a both/and:** a plain reference field for the common case, plus an optional qualified form when the relation carries attributes (weight, severity, scope, provenance-class). This satisfies exarchos's anti-drift for the simple case and basileus's `provenance: extracted` edge-tagging for the qualified case, without forcing either.

### Cardinality

| Relation | Direction | Cardinality | Warrant |
|---|---|---|---|
| `addresses` | Option → Problem | many-to-one | IBIS: Positions respond-to one Issue |
| `selects` | Decision → Option | one-to-one | MADR: one chosen option per outcome |
| `rejects` | Rejection → Option | one-to-one | Toulmin rebuttal targets one claim |
| `supports` / `objects-to` | Argument → {Option, Decision, Criterion} | many-to-many | IBIS/QOC: arguments accumulate |
| `assesses` | Criterion → Option | many-to-many, **qualified** (polarity + weight) | QOC Assessment |
| `supersedes` | Decision → Decision | many-to-many, **qualified** (scope: full \| partial) | ODF supersedes 4 docs + own prior version; why-context-engine supersedes *partially* |
| `conflicts-with` | Decision ↔ Decision | many-to-many, symmetric | basileus DI-2 |
| `constrains` | Constraint → {Option, Decision} | many-to-many | MADR k.o. criterion |
| `evidences` | *external ref* → {Argument, Decision} | many-to-many, **qualified** (provenance class) | Toulmin backing; ODF "research grounding (credibility only)" |
| `refines` | X → X (same kind) | many-to-one | IBIS generalize/specialize |
| `references` | any → any | many-to-many | basileus front-matter |

## 8. Implications for the next design pass

1. **Add `Argument` and split `Criterion` from `Constraint`.** Both are required by the prior art and absent from both revisions (§2.1).
2. **Drop the `status` enum; derive lifecycle from relations.** The corpus shows `status` in practice carries *work* progress, not decision lifecycle (§3). This also removes the conflict where basileus's design doc claims values its own corpus does not use.
3. **Three axes, not one enum:** attribution, derivation, qualifier (§5). This makes exarchos's C6 structurally enforceable.
4. **Identity is an assigned `id` + `title`; the content digest is optional and separate** (§6).
5. **Adopt PROV's Qualification Pattern** — plain reference by default, qualified form when the relation carries attributes (§7).
6. **Core vs extension:** the eight derived kinds are the argumentation core. `Pattern`, `Concept`, and basileus's untyped `Document` are extension-space, which the open-vocabulary question must serve — but note the emitter constraint found in plan-review round 2 (`RecordEmitter` is closed-world on both axes, and `ContractsJson` leaves `UnmappedMemberHandling = Skip`, silently dropping unknown members). **The extension mechanism is an emitter question, not a schema question**, and must be designed as such rather than asserted.

## 9. Open questions this did not settle

- **Whether the open vocabulary is achievable at all** without emitter work that the previous design forbade. §8.6 narrows it to a concrete emitter question; it does not answer it.
- **basileus's authority lattice (their Q7)** — ODF asserts "single authoritative source of truth" in prose. Prior art offers no standard; DRL's evaluation spaces are the nearest analogue.
- **Capture cost.** Buckingham Shum's QOC studies found designers could not reliably distinguish Options from Criteria, and that DR notations were "too cumbersome for design meetings". A richer taxonomy raises authoring cost — the trade-off exarchos's elicitation-forms approach is betting against, and it is unvalidated here.
