---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 263cc5720818b13268214c5df84d7575fd74a6d7
base_revision: 362c45f1ebc812ba2e2abb419e47fef29cb1d622
target_ref: codex/169-derived-compensation
implementation_fingerprint: 24be1d97dfaedd88bebc7f33dda831d9b36b1ba1
implementation_fingerprint_command: git rev-parse 263cc5720818b13268214c5df84d7575fd74a6d7^{tree}
evidence_binding: Stage 0 scopes the immutable product subject only; no test, CI, review, package, downstream-adoption, release, or merge result is claimed here
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, persistence, wire, packaging, and documentation surface against merged-main issue-167 base 362c45f1ebc812ba2e2abb419e47fef29cb1d622
updated: 2026-09-08
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative derived-compensation intent and acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: v2.13 action-calculus program, operator ordering, and failure-mode context
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: immediately preceding typed-workflow-binding scope and verification handoff
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: typed predicate and sequential proof semantics consumed by inverse derivation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: dedicated downstream adoption coordination; implementation remains unproven
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: dedicated downstream adoption coordination; implementation remains unproven
---

# Stage 0 — Issue #169 mechanically derived compensation

## Scope answers — recorded word for word

The prior run recorded this exact answer and split the requested program into successive verification
subjects:

> Implement the next slice #167 and #169. Use [$verify-code](/home/reedsalus/.agents/skills/verify-code/SKILL.md) to review your changes. After applying any fixes, open PRs. You may do a single round of review with coderabbitai before pressing for my final authorization to merge.

The continuation request for this run is:

> Please complete the remaining work for 169 as planned.

This run verifies the complete #169 product diff over the post-#167 base. It does not infer merge
authorization beyond the authorization record maintained by the parent workflow, and it does not
treat the existence of Basileus #495 or Exarchos #1895 as evidence that either consumer has adopted
the contract.

## Cost control

**High.** The diff adds public inverse-calculus and rollback-plan APIs; changes an independently
published TypeSpec/JSON/C# contract package; adds enabled-by-default Error diagnostics in two analyzer
products; and generates a durable saga protocol whose persisted journal, authority claims, timeout
messages, correlation identities, and failure states cross process and storage boundaries. A false
positive can reject a consumer build. A false negative can execute an unsound inverse or erase the
only reconciliation record. The reverse dependency closure also reaches external Basileus and
Exarchos consumers. All five verification stages and all seven Stage 1 lenses are required, with
multiple independent refutations for every surviving correctness obligation.

## Immutable subject

- Product revision: `263cc5720818b13268214c5df84d7575fd74a6d7`
- Product tree: `24be1d97dfaedd88bebc7f33dda831d9b36b1ba1`
- Diff base: `362c45f1ebc812ba2e2abb419e47fef29cb1d622`
- Branch label: `codex/169-derived-compensation`

The thirteen product commits are `f1830ce`, `62f3b68`, `518a6ee`, `cb2f76e`, `70dbd6c`, `84892f0`,
`5f057b7`, `1bcba4c`, `453081d`, `2c24105`, `fe30d34`, `5a812d1`, and `263cc57`. The eight hardening
and delivery commits before the reconciliation commit close the review-cycle proof gaps: they expose
and validate durable rollback identity; pack the complete local dependency closure and exercise both shipped analyzers;
make the legal package probe warning-free; make competing forward terminal signals idempotent once
rollback has authority; fail closed on malformed, wrong-kind, or non-positive imported compensation
timeouts; and make the Basileus smoke restore hermetic against a fresh feed and empty package cache.

The branch was first reconciled against the issue-167 PR head and then rebased byte-identically onto
merged `origin/main` after PR #205 landed. The final tree remains
`24be1d97dfaedd88bebc7f33dda831d9b36b1ba1`; only commit ancestry and source revision changed. Rebase
conflicts in shared changelog, migration, package, and agents-API documentation were resolved against
the reviewed base rather than preserving duplicate or superseded text. The resulting composition
activated three issue-167 ratchets and each failure was corrected: rollback and legacy completion
event names now derive through `NamingHelper.GetCompletedEventName`; the exact legacy unvalidated
test-callsite ceiling for `ConfidenceLoweringTests` ratcheted down from six to five; and typed
`Compensate(WorkflowActionReference)` occurrence metadata is registered with named extraction and
wire-round-trip proofs. A separate independent-review P1 made AGWF044 and AGWF045
`NotConfigurable`, matching the fail-closed proof outcomes AGWF041 through AGWF043.

## Reverse dependency closure

1. **Inverse contract model and proof.** `ActionInverseAnalysis`, `ActionInverseContract`,
   obligation-specific failures, `ActionRollbackPlan`, and the `ActionCalculus` inverse/rollback
   entry points reach public API baselines, callers, graph freeze, analyzers, docs, and future
   workflow consumers.
2. **Ontology construction and source diagnostics.** Named `CompensatedBy` actions are resolved and
   proved during graph construction, while the netstandard ontology analyzer independently rejects
   statically visible disagreements as AONT216. The shared catalog/parser/finite solver and their
   SymbolKey-only behavior are therefore in scope.
3. **Typed workflow authoring.** `Compensate<T>(WorkflowActionReference)`, the immutable
   `CompensationConfiguration`, the one-declaration rule, every fluent continuation surface,
   source extraction, import, projection, and occurrence identity are in scope.
4. **Workflow compensation proof.** The workflow analyzer's inverse equivalence, required-on-failure
   rule, same-subject binding boundary, rollback-claim propagation, scope completeness, topology
   closure, and AGWF044/AGWF045 precedence are in scope.
5. **Topology and plan lowering.** Root, loop-iteration, branch-path, fork-lane, nested-scope,
   approval, confidence, failure, and terminal occurrence ownership; completed-prefix selection;
   fork quiescence; sequential reversal; and parallel noninterference are in scope.
6. **Generated durable runtime.** Persisted forward-dispatch and failure-trigger authority claims,
   completion journal entries, schema version/high-water invariants, stable execution/rollback IDs,
   forward and inverse worker messages, reducer ordering, timeout/redelivery behavior, terminal
   monotonicity, handler ordering, and saga retention are in scope.
7. **Wire and package contract.** TypeSpec `inverseAction`, generated C#, both JSON-schema forms,
   hand-written DTO/parser/bridge, diagnostic enum/catalog/docs, Contracts 0.12.0 packaging, generator
   analyzer packaging, and the isolated packed-consumer probe are in scope.
8. **Documentation, migration, and delivery.** The 2.13 changelog, API/calculus/diagnostic references,
   SagaDocument restriction, legacy separation, at-least-once/idempotency guidance, public API gates,
   downstream adoption issues, PR review, protected CI, release, and merge boundaries are in scope.

## Boundary and reversal record

- **Compiler boundary:** AONT216 and AGWF044/AGWF045 are build-rejecting diagnostics. Reverting source
  does not un-reject consumers that still reference the new analyzer package; they must rebuild or
  pin the earlier package.
- **Published-contract boundary:** Contracts 0.12.0 adds an optional inverse identity and two members
  to a closed diagnostic vocabulary. Previously valid compensation JSON with a nonblank
  `compensationStepType` and omitted `inverseAction` remains representable; empty or whitespace-only
  monikers are intentionally rejected to match importer/runtime identity rules. Consumers of the
  closed enum must upgrade before reading the new tokens.
- **Persistence boundary:** typed sagas persist compensation schema version 1, claims, topology keys,
  journal sequence, and rollback state. Reverting generated source cannot reinterpret an in-flight
  typed saga safely. The documented rollback is to drain/version workflows and retain unfamiliar or
  corrupt saga state for reconciliation rather than synthesize success.
- **External-effect boundary:** message delivery remains at-least-once. Reversal of the Strategos
  code does not reverse an external effect already performed by an inverse step; that implementation
  must be idempotent or durably deduplicate the stable rollback ID.
- **Consumer boundary:** Basileus #495 and Exarchos #1895 record the required package/schema/API
  adoption, but issue creation is coordination evidence only. Their implementation and compatibility
  suites stay outside this repository subject until separately bound.

## Initial run-wide questions

1. Do the runtime calculus, ontology analyzer, and workflow analyzer implement one inverse contract,
   including effective guarantees, bidirectional implication, semantic authority, exact frames,
   opacity, and diagnostic precedence?
2. Does every accepted typed compensation occurrence enter the same closed topology used by both
   proof and saga emission, or fail closed before generated rollback can claim safety?
3. Can any forged, stale, duplicated, reordered, cross-scope, or post-completion message acquire
   rollback authority without an exact persisted dispatch/completion claim?
4. Does a failure derive exactly the completed prefix of the innermost applicable scope, exclude the
   failed leaf, quiesce forks, and preserve enclosing-scope history for a later enclosing failure?
5. Are inverse failure, timeout, missing outcome, reducer failure, and corrupted persisted state all
   monotonic non-success states that retain the saga and journal for reconciliation?
6. Do TypeSpec, generated artifacts, hand-written import, fluent extraction, public API, analyzer
   tokens, package contents, and documentation agree without a permissive or stale representation?
7. Does the packaged analyzer actually enforce #169 in a fresh consumer with locally packed bytes,
   and does the verification harness distinguish restore/infrastructure indeterminacy from a product
   failure while rejecting generated nullable warnings?
8. Are the documented statements about a “proved inverse” scoped to declared action contracts, with
   the executable inverse step and external side effects kept as explicit implementation trust
   boundaries?
