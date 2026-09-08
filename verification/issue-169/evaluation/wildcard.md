---
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
lens: wildcard
evaluated_at: 2026-09-07T22:04:35-07:00
environment: local workspace, Linux, .NET SDK 10.0.202, git 2.43.0
scope_rule: question the inverse/compensation framing and look for cross-mechanism risks not reducible to proof-rung fit, ledger coverage, or ordinary premise/reachability/duplication attacks
evidence_binding: HEAD and HEAD tree matched the immutable Stage 0 subject; tracked product diff and index were clean before this report was written
skipped: other Stage 3 evaluation files were not used as evidence; a broad repository grep accidentally displayed several matching lines from coverage.md, which were discarded and are not relied on below
---

# Stage 3 wildcard evaluation — issue #169

## Boundary and question framing

I read the wildcard instructions and evidence-binding rules, then the complete Stage 0 record,
Stage 1 survey and detailed survey files, ledger, guards, mutation evidence, final artifact checks,
full-test checks, and the obligation files needed to distinguish these findings from already-recorded
proof boundaries. I inspected the exact product revision named above. I did not treat local tests,
packages, hosted checks, review, or downstream adoption as evidence for a different subject.

The fixed lenses ask whether each stated obligation has the right proof, whether the ledger covers the
scope set, and whether an obligation's premise or evidence can be refuted. The missing question is
more basic: **what mathematical and operational meaning does #169 assign to “inverse” and “restores”?**
The implementation proves equality between unary state predicates and equality between resource-name
sets. That can be useful compensation evidence, but it is not generally a left or right inverse of a
state transition. A second framing question follows from the documented at-least-once boundary:
**can an inverse implementation actually obtain the durable idempotency identity that the docs assign
to it through a named public contract?**

## Findings

### W-1 — `Proven` establishes return to the forward requirement set, not restoration of the concrete pre-state or frame

**Affected:** ledger-wide, especially
`inverse-contract-is-mechanically-derived`,
`authored-inverse-is-bidirectionally-equivalent`,
`inverse-subject-frame-and-authority-are-exact`,
`rollback-plan-is-immutable-and-subject-homogeneous`,
`executable-inverse-effects-remain-a-consumer-trust-boundary`, and
`docs-and-migration-build-and-state-exact-boundaries`.

**Concern.** The public surface repeatedly describes a Proven action as an “inverse,” the forward hard
requirement as “restored,” and the frame as the exact resource set the inverse “must restore.” The
actual logic has no old-state variable, transition relation, saved property value, or equality between
the state before the forward action and the state after compensation. It proves only that the inverse's
post-state belongs to the set denoted by the forward requirement. Even a compensation implementation
that perfectly satisfies its declared contract therefore need not restore the concrete prior state.
This is narrower and more fundamental than the existing boundary around arbitrary CLR bodies and
external effects: the counterexample remains valid when both bodies implement their unary contracts
exactly.

**Scope.** The semantic issue begins in `ActionCalculus.AnalyzeInverse`, continues through the public
`ActionInverseContract` and rollback-plan claims, and reaches generated runtime state. It also affects
event resources, whose append-only occurrence semantics cannot be inverted by equality of frame
tokens.

**Evidence.** At `src/Strategos.Ontology/Descriptors/ActionCalculus.cs:83-89`, the derived inverse is
constructed with requirement `forwardProof.EffectiveGuarantee`, guarantee
`forwardProof.Requirement`, and the same `ActionFrame`. Lines 235-252 then prove only bidirectional
implication between those unary formulas and the authored inverse formulas. The public XML contract
says the hard requirement is “restored after rollback” and calls the frame “the exact frame the
inverse must restore” at
`src/Strategos.Ontology/Descriptors/ActionInverseAnalysis.cs:93-103`; rollback leaves and plans repeat
the restoration wording at `src/Strategos.Ontology/Descriptors/ActionRollbackPlan.cs:47-51` and
`:215-216`. The same framing appears in
`docs/src/content/docs/reference/action-calculus.md:471-485` and
`docs/src/content/docs/reference/api/workflow.md:211-214`.

A closed counterexample follows directly from the supported integer language. Let a forward action
have:

```text
R_A: x > 0
D_A: x > 0
W_A: { x }
```

Then `Forget_x(R_A) = True`, so the forward effective guarantee is `G_A = x > 0`. An authored
compensator with requirement `x > 0`, guarantee `x > 0`, and the same frame is bidirectionally
equivalent and can be classified Proven. A forward implementation that maps every positive `x` to
`1`, followed by an inverse implementation that leaves `1` unchanged, satisfies both contracts. From
the concrete starting state `x = 5`, however, `A ; A^-1` finishes at `x = 1`, not `x = 5`. Frame
equality says only that both actions may touch `x`; it supplies no round-trip law.

The generated durable path does not add the missing relation. A completion journal entry has one
`RollbackState` (`src/Strategos.Generators/Emitters/Saga/SagaCompensationComponentEmitter.cs:90-110`),
initialized from the forward completion's state (`:268-288`). Before an inverse is dispatched it is
overwritten with the saga's current `State` (`:1765-1768`) and that current value is sent to the
inverse worker (`:1798-1802`). No generic pre-forward snapshot or delta is available from which the
runtime could establish concrete restoration. The real-host fixture uses singleton stage equalities
and checks invocation order; that useful example does not discriminate this non-singleton case
(`src/Strategos.Generators.Behavioral.Tests/Workflows/DerivedCompensationWorkflow.cs:36-64` and
`src/Strategos.Generators.Behavioral.Tests/CompensationBehaviorTests.cs:102-124`).

Event effects expose the same mismatch in a less repairable form. `.EmitsEvent<T>()` adds an
`EmitsEvent` postcondition and `ActionResource.Event(typeof(T).Name)` to the frame
(`src/Strategos.Ontology/Builder/ActionBuilderOfT.cs:230-237`), while the forward proof deliberately
derives no event predicate; the docs say exactly that at
`docs/src/content/docs/reference/action-calculus.md:159-170`. Exact frame equality can therefore
require an inverse to carry the same event-type token while proving no statement about event history.
Emitting the same append-only event does not undo it, while emitting a distinct compensating event
gives a different frame and is refuted by the exact-frame check at
`src/Strategos.Ontology/Descriptors/ActionCalculus.cs:211-221`. This is a modeled built-in effect, not
merely unknown user code.

**Suggested action.** Decide the public semantic contract before release. If #169 intends contractual
compensation only, rename or qualify the result and all “restore frame” claims: Proven means the
authored action re-establishes the forward requirement **region** and matches its declared authority
and may-touch set; it does not return the object or event history to the concrete pre-state. Add the
`x > 0` counterexample and an event-frame example as permanent claim-boundary tests/docs. If exact
reversal is intended, the model needs relational old/new predicates or a persisted pre-state/delta
that the inverse guarantee can reference, plus a round-trip proof. Until such semantics exist,
Event/External frames should not receive an unqualified Proven inverse classification; event effects
need an explicit compensating-event rule rather than same-event-type frame equality.

### W-2 — the documented durable rollback ID is hidden inside a tracing-only `CorrelationId`

**Affected:** `rollback-id-is-stable-distinct-and-injective`,
`executable-inverse-effects-remain-a-consumer-trust-boundary`, and
`docs-and-migration-build-and-state-exact-boundaries`.

**Concern.** The docs correctly assign at-least-once deduplication to the inverse implementation and
tell it to use “that rollback id” as a durable idempotency key. The `IWorkflowStep<TState>` call,
however, receives only state, `StepContext`, and cancellation. `StepContext` exposes neither
`RollbackId` nor `IsCompensation`; its only related member is a string `CorrelationId` documented as
being for distributed tracing. The generator silently places the rollback GUID's `N` representation
in that property. The identity is therefore physically reachable but absent from the named public
contract consumers are instructed to implement. This makes the most important mitigation for
at-least-once external effects dependent on reading generated source or guessing that a tracing ID is
a stable business idempotency key.

**Scope.** The mismatch crosses the generated inverse command, worker-to-step adapter, public
`StepContext` API/XML docs, action-calculus guidance, and the external-effect trust boundary.

**Evidence.** Generated inverse commands carry both `RollbackId` and `IsCompensation` and are filled
at `src/Strategos.Generators/Emitters/Saga/SagaCompensationComponentEmitter.cs:1798-1808`. The worker
adapter then creates `StepContext` and assigns
`CorrelationId = (command.RollbackId ?? command.StepExecutionId).ToString("N")` before calling the
step (`src/Strategos.Generators/Emitters/WorkerHandlerEmitter.cs:742-770`). The public record exposes
only `CorrelationId`, described as a distributed-tracing identifier, at
`src/Strategos/Steps/StepContext.cs:25-55`. The public API reference repeats that tracing-only meaning
and exposes no rollback field at `docs/src/content/docs/reference/api/workflow.md:282-295`, while the
compensation reference directs implementations to use the rollback ID at
`docs/src/content/docs/reference/action-calculus.md:562-570`. The emitted-source test checks the
hidden assignment, but no public-contract example shows an inverse implementation extracting and
durably deduplicating it.

This is not a claim that the generated GUID transform is unstable. The evidence shows a stable value
does reach the step as `CorrelationId`. The concern is that the consumer-owned safety control is not
identified as such by the API or documentation, so a conforming consumer has no explicit contract
that the tracing string is the rollback ID, stays stable across redelivery, or denotes compensation
rather than an ordinary forward execution.

**Suggested action.** Prefer an explicit immutable execution identity on `StepContext`, such as
`StepExecutionId`, nullable `RollbackId`, and `IsCompensation`, populated and tested at the worker
boundary. If compatibility requires retaining only `CorrelationId`, document it normatively as the
exact lowercase-`N` rollback GUID for inverse delivery and the forward execution GUID otherwise, and
add a public step example plus a redelivery test whose fake external sink deduplicates solely from the
context value. Correct the `StepContext` API table at the same time: it currently lists `Phase` and
`Metadata`, while the actual record has `CurrentPhase` and no `Metadata`.

## Passes

- The evaluation subject was bound correctly: `HEAD` was
  `42b4ed741c1f406a2de2fdcdaf602ca53dd91dab`, its tree was
  `2d0f3027308580376819e7b56649592cd0a784bc`, and tracked product/index checks were clean.
- The calculus does derive from the forward **effective** guarantee rather than raw authored
  guarantees, and it performs both implication directions for the authored requirement and
  guarantee. The concern in W-1 does not depend on weakening either implementation detail.
- Forward composition soundly declines to infer a state fact from `EmitsEvent`; the oddity arises
  only when the inverse layer subsequently calls equality of the event-bearing frame restoration.
- The durable runtime does keep a stable rollback GUID on the journal and generated command, and the
  worker maps it deterministically into `StepContext.CorrelationId`. W-2 is an API/contract
  reachability problem, not evidence that the value is randomized on every retry.
- The existing ledger explicitly separates static ontology-contract proof from arbitrary CLR and
  external-effect correctness. W-1 preserves that boundary and identifies the different case where
  even perfect satisfaction of the declared unary contracts is insufficient for concrete reversal.
- Local codegen, contract, package, and full-suite results remain bound in their dedicated evidence
  files with Pass/Fail/Indeterminate distinctions; this wildcard pass does not upgrade any of them to
  protected evidence.

## Uncertainties

- Product-owner intent is needed to decide whether “inverse” deliberately means only
  re-establishment of the forward requirement set. The implementation is coherent under that weaker
  definition, but current public names and restoration wording do not state the limitation.
- The intended semantics of `ActionResourceKind.Event` during inversion are not defined. Source and
  docs call the name an emitted event **type** and provide no event predicate; nothing says it instead
  denotes a reversible mutable channel. That decision controls whether event-frame actions must be
  rejected, weakened to contractual compensation, or given a distinct compensating-event relation.
- The worker-emitter comment suggests the `CorrelationId` encoding is intentional, but neither the
  public XML contract nor the user-facing API/compensation references make it normative. It is
  therefore unsettled whether this is the supported idempotency API or an internal tracing detail.
- No new executable fixture was added or run for this evaluation. The two findings are construction
  counterexamples derived from the exact source and public claims; the already-recorded exact-revision
  suites do not contain the discriminating non-singleton round-trip or public-context deduplication
  cases.
