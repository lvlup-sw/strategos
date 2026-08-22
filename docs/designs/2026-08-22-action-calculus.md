# The Action Calculus — Design Record and Roadmap Grounding

**Date:** 2026-08-22
**Tracked by:** #172 (epic) · #153 (master tracker, "The action calculus program")
**Source:** the *Action Calculus* design canvas — <https://claude.ai/code/artifact/e7aaeb81-d7e4-46b4-be2d-364c6c8b557e>
**Addresses:** #161 · #162 · #163 · #164 · #165 · #167 · #168 · #169 · #170 · #171
**Builds on:** #100 (bidirectional workflow IR) · #150 / #151 / #152 (contract shapes), all released in v2.10.0

This document is the durable record of two things the epic does not carry: the **grounding audit** against
source as it stood on 2026-08-22, and the **register** of questions the review raised, with their standing.
The live plan, sequencing and issue map live in #172 — this file does not restate them.

> **A note on the source canvas.** It spans four pages and three repositories. Pages 1, 2 and 4 are the
> calculus, the consumption model and the exarchos reading. **Page 3 is a separate design review for
> `lvlup-sw/timedat`** (a Convex/MCP closed-surface design) that shares the canvas. Read pages 1, 2 and 4
> for anything Strategos.

---

## The thesis

Everything is an action with a declared contract — including the acts of declaring, binding and projecting.
Actions compose. The compiler checks the composition, at every level.

```
Action@L = {
  requires : Predicate@L    -- what must hold before
  ensures  : Predicate@L    -- what holds after
  needs    : Capability     -- authority; an element of a lattice, not a literal
  touches  : Resource[]@L   -- the frame: what it acts on
}

L ∈ { M2, M1 }   -- M2 declares · M1 operates · M0 grounds the predicates and carries no actions
```

The level is a *parameter*, not a taxonomy. `declareObjectType(ns, name, props)` at M2 and `createTask(project, title)`
at M1 have the same four fields and differ only in where their predicates are grounded. That is what lets one
structure cover both running the system and changing it, and it is what removes the need for a second vocabulary
for meta-level work, a declared "consumer kind" field, and a separate version-compatibility framework.

---

## Grounding audit — Strategos as of 2026-08-22

Everything below was read from source at the commit this document lands on. It decays; re-verify before
estimating from it.

### What already exists

| Calculus element | In Strategos today | Where |
|---|---|---|
| `requires` | `ActionDescriptor.Preconditions` | `src/Strategos.Ontology/Descriptors/ActionDescriptor.cs` |
| `ensures` | `ActionDescriptor.Postconditions` | same |
| `⊓` (choice) | abstention contract, released v2.10.0 | #152 |
| `⁻¹` (edge) | fork/compensation DSL edge, released v2.10.0; lowered in #135 | #151 |
| the workflow join | `ActionBindingType.Workflow` + `BoundWorkflowName` | `Descriptors/ActionBindingType.cs` |
| contract substrate | TypeSpec, emitting JSON Schema + C# records | `src/Strategos.Contracts/main.tsp`, `Strategos.Contracts.Codegen` |
| retry safety, first cut | `ActionDescriptor.IsReadOnly` | `Descriptors/ActionDescriptor.cs` |
| the MCP projection's slot | `ToolAnnotations.IdempotentHint` | `src/Strategos.Ontology.MCP/ToolAnnotations.cs` |

### What is absent, and how absent

Four findings that change how the issues should be read.

**1. `needs` does not exist, and the name is taken.**
`ActionDescriptor` has no authority field. The only `Capability` type in the repository is
`src/Strategos/Orchestration/Capability.cs` — a `[Flags]` enum of *executor skills* (`WebSearch`,
`CodeGeneration`, `DatabaseQuery`, …) matched against tasks via `required & executor != 0`. It is
agent-to-task routing and has nothing to do with authority.

So #165 introduces both the vocabulary and its order in one move, and its proposed
`builder.Capability("space.write")` collides with an existing unrelated type. Either name the authority
concept distinctly, or rename the orchestration enum first, on its own, since it touches `TaskEntry` and
the orchestrator's selection path. Recorded on #165.

**2. `touches` does not exist on the action.**
`touched()` is defined in #60 and consumed there for exactly one purpose — gating parallel subagent
dispatch. It never reaches `ActionDescriptor`. #164 lifts it, and adds the frame-soundness rule #60 does
not need (an under-declared frame only makes parallel gating *more* permissive, but it makes sequential
composition and compensation unsound).

**3. The predicate payload is an unparsed string.**
`ActionPrecondition.Expression` is a `required string`. `OntologyQueryService` re-parses it at query time
via `TryParseComparison` (lines 771, 1120) to answer *satisfiability* against one object — a runtime A-Box
question. Implication between two actions' declarations is a different question and nothing asks it.

`PreconditionKind.Custom` has no arm in either switch. It falls to a wildcard, and the wildcard is
**permissive**:

```csharp
// OntologyQueryService.cs:805-806
_ => true, // Custom or unknown kinds are optimistically satisfiable
```

`GetValidActions` (line 147) filters with `.All(p => IsPreconditionSatisfiable(p, knownProperties))` at
line 165. So **an action guarded only by a `Custom` precondition is published as valid, unconditionally.**

For a discovery API answering "what *could* be legal here", an optimistic default is a defensible choice and
it is deliberately commented as one. Two things change that:

- Once `RelationHolds` (#162) lands, this switch carries *authorization* conditions. An unevaluated
  authorization precondition defaulting to `true` is a permissive default in the one place a permissive
  default is wrong. #162's acceptance criterion — `GetValidActions` excludes actions whose `RelationHolds`
  fails — has to be written against this switch specifically.
- Under costed opacity the correct behaviour is neither `true` nor `false`: an opaque predicate should be
  *evaluated at runtime* and *excluded from build-time composition checks*, and it should say which action
  dropped out. "Optimistically satisfiable" is a third thing, and it is silent.

And `PreconditionKind` (`PropertyPredicate | LinkExists | Custom`) and `PostconditionKind`
(`ModifiesProperty | CreatesLink | EmitsEvent`) are different vocabularies, so any implication check must
first write down the correspondence between them. All of this is #168.

**4. `Strategos.Contracts` has no decorators.**
The package is already TypeSpec, but there is not one `extern dec` in it. It carries structure only, so
every deontic fact — capability, relation, client exposure, confirm — has no way to be written in the
contract. This is why the operation map itself cannot land in `Strategos.Contracts` today, and a polyglot
domain can share its terms and not its verbs. #170, paired with #163.

---

## The register

Ten questions were raised across the review. The three that carry live Strategos work are marked.

| # | Question | Standing | Prior art | What remains |
|---|---|---|---|---|
| 1 | Relation composition and transitivity | settled · audit | Zanzibar `tuple_to_userset` · OpenFGA · Cedar `in` | Consumer-side audit; not Strategos work |
| 2 | Capability lattice as a product of two orders | **open · bites now** | Denning's lattice · Granule's coeffect products | The second axis. See finding 1 above → #165 |
| 3 | What a region is | settled | protobuf package · Cedar namespace · CODEOWNERS | Nothing. A region is a TypeSpec namespace |
| 4 | Version compatibility relation | settled | Buf breaking rules · Confluent compat modes | Nothing new. It is `author`'s postcondition plus the authority-matrix diff |
| 5 | Operation-to-symbol naming | settled | OpenAPI `operationId` · Smithy traits | Nothing. Rule plus binding overrides; emitter naming never enters the contract |
| 6 | Where the error-code union lives | settled | gRPC status codes · RFC 9457 · Smithy | Nothing. It is contract vocabulary |
| 7 | MCP projection under a partial binding | settled | — | `McpOps ⊆ dom(binding)` — the contract cannot promise an agent surface nobody serves |
| 8 | Retry safety for an agent surface | settled · part two later | RFC 9110 · Stripe idempotency keys | **The `Idempotent` field now → #171.** An idempotency key is a larger change, deferred |
| 9 | A-Box authority | closed, conditionally | Zanzibar zookie · the "new enemy" problem | Nothing, until a second implementing consumer |
| 10 | Interval relations in overlap detection | withdrawn | Allen's interval algebra (1983) | Nothing — measuring overlapped duration is the right tool, not classifying a relation |

Row 4 is worth restating because it deletes work: **contract `v+1` is a safe drop-in for `v` exactly when
`v+1 ⊑ v`**, which is the refinement law the calculus already carries. Backward compatibility needs no
separate framework. The one thing refinement cannot see is existential — *does the new contract admit a
principal the old one refused?* — and that is answered by diffing the emitted authority matrix, not by a
rival axis.

---

## Composition and its cost

Two things are worth keeping in this record because they are the parts most likely to be lost.

**The frame's second rule is the load-bearing one.** Soundness (`touches(A) ⊇ mutations(A)`) is the obvious
rule. Non-interference — *outside `touches(A)`, every predicate keeps its truth value across `A`* — is the
one that is easy to skip and expensive to lose. Without it a frame is documentation. With it, `A ; B` can
be checked at the seam alone rather than against everything the program has established so far.

**Opacity is permitted and costed, not forbidden.** A predicate language narrow enough to decide implication
is narrow enough to exclude things people will want. The exclusions — arithmetic over more than one term,
quantification, and transitive closure beyond the declared rewrites — belong written down beside the grammar,
so a request for one is answered from a list rather than rediscovered later as a limitation. An action with an
opaque precondition keeps working at runtime and drops out of every composition check, visibly, per action.
That is the difference between a restriction and a limitation, and it is the mitigation for this program's
real failure mode: a calculus too elegant to use.

**Measure whether anything actually composes.** `lvlup-sw/exarchos#1763` measures its own tool surface at
**13%** carrying a substantive output schema (16 of 124). Under this frame that is the fraction of a surface
that can appear in any composition check — an action with a vacuous `ensures` satisfies no downstream
`requires` and drops out silently. Report the same statistic here from the start.

---

## The asymmetry with exarchos

Worth recording because it affects what a shared IR may assume.

`lvlup-sw/exarchos` is not a candidate for this calculus — it is most of the way to it already, and its
coordination rule 6 asks that the shared IR be born speaking gate classes, fork edges, compensation edges and
abstention unions. Those are `requires`, `∥`, `⁻¹` and `⊓`: three of four operators, already named.

Its authority is an append-only log, so the inverse of any action is a fold that stops one event earlier.
**`A⁻¹` is the cheapest operator there and the most expensive one here**, because Strategos's authority is
state and its inverses must be authored where they cannot be derived. A shared IR that assumes derived
inverses are cheap is assuming exarchos's substrate. #169 says so explicitly.

Its v2.11 resolver also already ships `riskTier × designDepth` as two independent orders on one context,
resolved once on phase entry and evolving as one schema. That is the exact shape the register asks for on
`needs`, in production, reached independently for a different quantity. Read it before designing #165.

---

## Prior art

Hoare (1969) · Liskov and Wing behavioral subtyping (1994) · Back and Morgan's refinement calculus ·
O'Hearn and Reynolds separation logic (2001) · Garcia-Molina and Salem sagas (1987) · Denning's lattice
model · Zanzibar and OpenFGA · Cedar's deliberate restriction to a decidable fragment · RFC 9110 ·
RFC 9457 · Buf breaking-change rules · the MOF four-layer metamodel architecture.

None of this is invention. The program is applying it.
