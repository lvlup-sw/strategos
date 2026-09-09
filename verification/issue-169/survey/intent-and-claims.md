---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: claim inventory for the complete issue-169 diff and its immediate issue-167/168/172 contract context
updated: 2026-09-07
skipped: claim validation and verdict assignment; quoted statements remain claims until later stages bind evidence
lens: intent-and-claims
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative issue intent and acceptance language
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: action-calculus program ordering and machine-checking intent
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: exact prior scope answer and workflow identity handoff
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: predicate/effective-guarantee proof semantics assumed by inverse derivation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream adoption claim source, not implementation evidence
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream adoption claim source, not implementation evidence
---

# Stage 1 survey — intent and claim inventory

## Reading and epistemic rule

This file preserves user, issue, milestone, changelog, documentation, test-description, and delivery
statements as **claims**. Closing an issue, checking in a generated file, naming a test “proves,” or
opening a downstream adoption issue does not validate the claim. Stage 2 must turn each material claim
into an obligation, a scoped assumption, or an explicitly deferred question.

## User authorization and scope claims

### IC-USER-001 — prior program answer, exact text

Source: the exact answer recorded in issue #167 Stage 0.

> Implement the next slice #167 and #169. Use [$verify-code](/home/reedsalus/.agents/skills/verify-code/SKILL.md) to review your changes. After applying any fixes, open PRs. You may do a single round of review with coderabbitai before pressing for my final authorization to merge.

Claims carried forward:

- #169 is the remaining half of the already requested substantive program;
- `verify-code` applies to the change before delivery;
- fixes found by verification are authorized;
- PR creation and one CodeRabbit round were contemplated by that answer; and
- final merge authority is governed by the parent workflow's current authorization record, not
  expanded by this survey.

### IC-USER-002 — current continuation request, exact text

> Please complete the remaining work for 169 as planned.

This is a continuation, not a new scope definition. “As planned” imports the previously recorded #169
intent and delivery process. It does not by itself establish that the implementation meets that plan.

## Authoritative issue claims

### IC-169-001 — inverse laws

Source: issue #169, “What the calculus says instead.”

~~~text
requires(A⁻¹) = ensures(A)
ensures(A⁻¹)  = requires(A)
needs(A⁻¹)    = needs(A)
touches(A⁻¹)  = touches(A)

(A ; B)⁻¹ = B⁻¹ ; A⁻¹
(A ∥ B)⁻¹ = A⁻¹ ∥ B⁻¹

failure at C in A ; B ; C  ⇒  rollback plan is B⁻¹ ; A⁻¹
~~~

> Given a forward graph and a point of failure there is exactly one rollback plan, and it is computed rather than remembered.

Interpretive questions retained for Stage 2:

- In #168 terminology, issue text `ensures(A)` must mean the forward **effective guarantee**, not only
  the raw explicit `Ensures` collection, or preserved untouched requirements would be lost.
- `needs` maps to semantic authority and `touches` to the canonical frame; both require an equality
  relation, not merely containment.
- “Exactly one” is meaningful only after binding the completed prefix, concrete nested scope, and
  deterministic handling of fork completion order.

### IC-169-002 — propagation, nesting, and authored inverse

Source: issue #169, “Three properties that do not exist today.”

> A composite is compensable exactly when every part of it is. One non-compensable leaf makes every composite containing it non-compensable, and that travels upward on its own.

> A failure inside one rolls back the completed prefix of that scope, never the whole program, and an inner scope unwinds before the scope enclosing it.

> Where a compensating step is authored ... the derived inverse and the authored one can be compared, and a disagreement is a diagnostic rather than a production surprise.

### IC-169-003 — issue acceptance criteria

Source: issue #169, “Acceptance criteria.”

> - A composite containing one non-compensable leaf cannot be declared rollback-safe
> - The rollback plan for a failed prefix is computed, not authored
> - An authored compensation contradicting the derived inverse is a diagnostic
> - An inner scope's failure does not unwind its enclosing scope

These statements do not specify legacy compatibility, persistence mode, delivery semantics, message
authority, or the proof/executable boundary. The branch adds policies for those gaps; those policies
must be evaluated as new product claims, not silently attributed to the issue.

### IC-MILESTONE-001 — program ordering and machine-checking context

Source: v2.13.0 milestone metadata attached to #167/#168/#169, previously captured in the issue-167
intent survey.

> The action calculus — the operators. Earns composition on top of the object: the decidable predicate fragment and the seam check (#168), the typed workflow binding (#167), and derived compensation (#169). Ordered #168 → #167 → #169, and #164 gates #169. Gated: not to be started before v2.12.0 completes the object. This is a redesign of the action model and the workflow binding, not a refactor — see #172 for the honest cost and the failure mode.

Issue #172 additionally describes the operator program as machine checked. The branch's analyzer and
runtime proof surfaces are candidate evidence for that claim; their existence is not the proof.

## Product changelog claims

### IC-CHANGELOG-001 — public/source compatibility

Source: `CHANGELOG.md`, “Cross-product breaking changes.”

> Existing fluent call sites remain source-compatible, but external implementations of these interfaces and the exarchos builder-surface mirror must adopt the added members.

> Calling either `Compensate<T>()` overload after compensation was already configured now throws `InvalidOperationException` instead of silently replacing the earlier declaration.

The first claim is scoped: consumers calling existing overloads should compile, while implementers of
the expanded interface do break. The second intentionally changes observable builder behavior and
requires an API/behavioral baseline.

### IC-CHANGELOG-002 — mechanical inverse and diagnostics

Source: `CHANGELOG.md`, “Mechanically derived compensation (#169).”

> `ActionCalculus.AnalyzeInverse` derives `A^-1` from a closed forward contract and proves an authored inverse's subject, effective requirement/guarantee, frame, and semantic authority in both directions.

> The workflow generator proves `.Compensate<T>(WorkflowActionReference)` declarations and reports `AGWF044` for inverse disagreement or `AGWF045` when compensability does not propagate through a rollback-claimed scope.

The phrase “proves an authored inverse” is interpreted as proving the authored **ontology action
contract**. The analyzer cannot establish that arbitrary C# inside the named compensation step or its
external effects implement that contract; this distinction must remain explicit.

### IC-CHANGELOG-003 — durable completed-prefix rollback

Source: `CHANGELOG.md`, “Durable completed-prefix rollback (#169).”

> Generated sagas journal completed forward occurrences with stable topology and execution identity, backed by a persisted pre-dispatch authority claim that rejects forged and stale results.

> They derive the rollback prefix after failure, keep nested failures inside their concrete scope, and quiesce forks before rollback.

> Inverse completion/failure messages are distinct from forward flow; reducer-applied state is folded between inverses, and failed or timed-out inverse outcomes retain the saga for reconciliation.

> Typed derived compensation is restricted to saga-document persistence in v2.13: event-sourced workflows receive `AGWF045` because a consumer-defined `ApplyEvent` method cannot yet be proved to fold generated rollback state consistently during live handling and Marten replay.

“Persisted before dispatch” depends on the transactional semantics of the Wolverine/Marten host, not
only on source statement order. Stage 2 must state that environment assumption and demand an
integration rung rather than treating emitter text as durability proof.

### IC-CHANGELOG-004 — contract compatibility

Source: `CHANGELOG.md`, “Contracts package 0.12.0.”

> Compensation metadata adds the optional `inverseAction: ActionReferenceV1` field and the closed diagnostic vocabulary adds `AGWF044`–`AGWF045`. Legacy compensation JSON remains valid and omits the field, but the no-argument `.Compensate<T>()` form remains runtime-only and cannot establish a statically proved inverse.

> A typed inverse cannot set `requiredOnFailure` to `false`, because its derived completed-prefix rollback is mandatory.

This combines additive wire compatibility with a closed-enum consumer break and a semantic rejection
rule. Each needs separate evidence.

## Documentation claims

### IC-DOC-001 — exact inverse contract and identity rule

Source: `docs/src/content/docs/reference/action-calculus.md`, “Mechanically derived compensation.”

> Using the effective guarantee is important. It includes both explicit post-state facts and requirements preserved outside the forward frame. An authored inverse must be equivalent in both directions; merely accepting fewer states or promising a weaker restoration is not a valid inverse.

> A non-empty frame needs executable authored inverse code. An action with an empty frame may use the distinct identity inverse, provided it does not contain a broken `CompensatedBy` declaration.

> Graph freeze resolves named inverse actions and reports `AONT216` unless the full subject, requirement, guarantee, frame, and authority proof succeeds.

### IC-DOC-002 — rollback structure and noninterference

Source: the same reference.

> `ActionCalculus.DeriveRollbackPlan` accepts only the completed forward prefix, so a failed action is never included in its own rollback. Sequential plans reverse and flatten; parallel plans preserve independent branches; scoped plans retain nested compensation boundaries; and an empty plan is the subject-typed rollback identity.

> `DeriveParallelRollbackPlan` rejects branches whose aggregate frames overlap, or whose frame can change a resource read by another branch's inverse contract; shared reads alone remain valid.

The API cannot distinguish “completed” from “not completed” inside an arbitrary caller-provided
enumerable; the first sentence is a caller precondition plus workflow-lowering claim, not a runtime
type guarantee.

### IC-DOC-003 — typed proof closure

Source: the same reference, “Typed workflow compensation.”

> `AGWF044` rejects a missing, ambiguous, dynamic, opaque, or semantically different authored inverse. Once a workflow or one of its bound action specifications claims rollback, compensability propagates through the scope: every rollback-reachable leaf with a non-empty frame must have a proved inverse. `AGWF045` rejects the whole scope instead of emitting a partial plan.

> A workflow must not mix legacy, dynamic, and typed compensation into one derived program; such a program fails closed rather than running the provable subset.

### IC-DOC-004 — message authority and failure outcome

Source: the same reference, “Durable completed-prefix rollback.”

> A completion or pre-completion failure must consume that claim; topology-shaped messages that were never dispatched cannot claim rollback authority.

> Delivery remains at-least-once: inverse implementations that perform external effects must either be idempotent or use that rollback id as their durable idempotency key.

> An inverse failure, an unmatched outcome, or a timeout is never recursively compensated or assumed successful: the saga and its journal remain in `Failed` for reconciliation.

The generated model also carries an `OutcomeUnknown` state. Stage 2 must reconcile the prose's broad
“remain in Failed” shorthand with the actual failed-versus-unknown distinction and confirm both are
retained, monotonic, non-success outcomes.

### IC-DOC-005 — nested and parallel runtime refinement

Source: the same reference.

> a failure inside a nested branch or loop iteration unwinds only that concrete inner scope, while a later enclosing failure can include its completed descendant scopes;

> fork lanes remain parallel in the structural plan, but generated inverse workers fold their state updates serially in reverse completion order because generic workflow state has no sound merge operation. The workflow-binding proof has already established that the lane frames do not interfere.

This is more specific than issue #169. It requires evidence that serial reverse-completion execution
is a valid deterministic refinement of a noninterfering parallel plan and that enclosing-scope history
is preserved rather than deleted after an inner unwind.

### IC-DOC-006 — event-sourced exclusion

Source: the same reference.

> Typed derived compensation is restricted to `SagaDocument` persistence in v2.13.

> The source generator reports `AGWF045` for a typed inverse program declared with `PersistenceMode.EventSourced`; it does not accept a compile-only or no-op `ApplyEvent` method as rollback proof. Legacy untyped compensation keeps its existing event-sourced behavior.

## Contract-package claims

### IC-CONTRACT-001 — version and wire shape

Source: `src/Strategos.Contracts/CHANGELOG.md` and README.

- package version is claimed to be 0.12.0;
- `compensation.inverseAction` is claimed to use the existing exact three-name `ActionReferenceV1`;
- omission is claimed to preserve legacy documents;
- present malformed or incomplete identities are claimed to fail; and
- AGWF044/AGWF045 are claimed to be present in the closed generated enum, constants, JSON catalog,
  schema, Markdown, and live analyzer descriptors.

These are paraphrased to avoid turning parallel generated copies into presumed independent truth.

## Package and verification-harness claims

### IC-PACK-001 — analyzer loading and cause-specific failures

Source: comments and checks in `scripts/verify-generator-consumer-build.sh`.

> This proves the packaged analyzer is loaded and enforcing both #167 and #169 rather than merely present.

The script further claims its legal packed consumer compiles from locally packed artifacts, its
illegal seam fails exclusively with AGWF041, its contradictory inverse fails exclusively with
AGWF044, nullable warnings are rejected, dependency restore inability is `INDETERMINATE`, and a
consumer compilation failure is `FAIL`. Later verification must inspect and run the script before
adopting those claims.

## External adoption claims

Basileus #495 and Exarchos #1895 request Contracts 0.12, `inverseAction`, exact action identities,
legacy omission, AGWF044/AGWF045, and retention/reconciliation of failed or unknown sagas. The issues
also request fixtures against pinned wire shape. Their open state and issue text are coordination
evidence only. No code, package pin, compatibility result, or deployment from those repositories is
bound to this product revision by the present survey.

## Conflicts, ambiguities, and narrowed readings

1. **`ensures` versus effective guarantee.** The issue's algebra uses `ensures(A)`; implementation and
   docs use #168's effective guarantee. This is the sound narrowed reading and must be called out in
   obligations rather than presented as literal raw-collection swapping.
2. **“Authored, never derived” title language.** The issue title contrasts authored compensation with
   the desired calculus; the delivered design derives the contract and plan while requiring authored
   executable code for non-empty frames. The two meanings of “inverse” must stay distinct.
3. **“Exactly one rollback plan.”** Fork completion order, concrete scope occurrence, and journal
   contents are runtime inputs. Determinism is conditional on those exact inputs; it does not mean all
   schedules yield the same history.
4. **Parallel algebra versus serial runtime.** The structural calculus retains parallel nodes, while
   generated generic-state execution serializes folds. This is documented as a refinement and must
   not be misrepresented as literal concurrent execution.
5. **`Failed` versus `OutcomeUnknown`.** Both mean retained, non-success reconciliation state, but they
   are semantically distinct. Documentation and tests must preserve rather than collapse the split.
6. **Static proof versus executable behavior.** Contract equivalence proves declared ontology
   predicates, not arbitrary inverse-step C# or external side effects. The stable rollback ID and
   behavioral integration test mitigate but cannot erase this trust boundary.
7. **Durability wording.** Source order alone cannot prove transaction boundaries. A Wolverine/Marten
   integration test plus documented host semantics is the cheapest evidence for persisted-before-
   dispatch claims.
8. **Downstream completion.** Adoption issue creation satisfies coordination, not implementation or
   released interoperability.

## Claim-to-obligation seeds

1. Derive the inverse from the closed forward **effective** guarantee, hard requirement, semantic
   authority, exact frame, and subject.
2. Prove authored requirement and guarantee equivalence in both directions with deterministic
   witnesses; reject invalid, opaque, missing, wrong-name, wrong-subject, wrong-frame, and wrong-
   authority inverses with the intended status/diagnostic.
3. Propagate one noncompensable leaf through every sequence, parallel, and nested scope plan.
4. Compute rollback from exactly the completed forward prefix, reverse sequential order, exclude the
   failed occurrence, and isolate the innermost failed scope.
5. Reject parallel write/write and both write/read interference while allowing shared reads.
6. Preserve legacy no-identity authoring as runtime-only; reject mixing and typed opt-out.
7. Establish exact persisted authority before accepting completion/failure and make forged, stale,
   duplicate, corrupt, and cross-scope messages incapable of minting rollback work.
8. Make inverse results distinct from forward results, fold returned state before the next inverse,
   and retain failure/unknown/timeout state without recursive rollback.
9. Reject typed EventSourced programs and preserve legacy event-sourced behavior.
10. Bind the 0.12 TypeSpec, generated artifacts, hand parser/bridge, closed diagnostics, package, and
    public API to executable drift guards.
11. Compile a fresh consumer from exact packed bytes, prove legal typed authoring and cause-specific
    AGWF044 rejection, reject CS86xx output, and separate infrastructure indeterminacy from product
    failure.
12. Keep executable inverse correctness and external-effect idempotency as explicit assumptions,
    covered by behavioral and correlation evidence rather than overstated static proof.
13. Treat Basileus/Exarchos issue links as adoption coordination and leave their implementation/release
    evidence unproven in this Strategos subject.
