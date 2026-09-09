# docs-and-migration-state-exact-boundaries

## Why this obligation exists

The diff changes public semantics and persisted saga state. Consumers need explicit guidance on
effective guarantees, typed versus legacy authoring, SagaDocument restriction, serial state folds,
retained Failed versus OutcomeUnknown, at-least-once external effects, stable rollback ID, and
drain/version migration for in-flight workflows.

## Failure scenario and boundary

A user treats rollback as exactly-once, expects fork inverses to mutate shared state concurrently,
downgrades an in-flight schema, or interprets OutcomeUnknown as success. A docs build only proves
syntax/links, not semantic honesty.

## Assigned proof

- Rung: R6, semantic human review.
- Existing candidate corpus: final claim audit across `migration-v2-13.md`, `action-calculus.md`, workflow API
  and diagnostic references, public XML, root/Contracts changelogs and README, including the W-1
  non-singleton/event-frame counterexamples.
- Current disposition: Unproven. Exact-final wording states the contract-region, may-touch-frame,
  durable-identity, at-least-once, retained-outcome, persistence, and external-effect boundaries; the
  final bound semantic review is absent.

## Refutation attempts for Stage 3

1. Remove the SagaDocument exclusion or legacy distinction.
2. Replace at-least-once with exactly-once.
3. Collapse OutcomeUnknown into successful/ordinary Failed wording.
4. Break one new cross-link and require docs build/link validation to fail.

## Open questions

None.

## Investigation Log

No unresolved question. Universal executable correctness is not a documentation claim and remains
separate under `executable-inverse-effects-remain-a-consumer-trust-boundary`.

## Stage 3 disposition

PF-4 split build/link integrity into `docs-and-migration-build-and-link` at R4. This file now owns
semantic truth, including that unary contract proof does not establish concrete pre-state or event-
history reversal. The final-subject wording is candidate evidence, not a human semantic verdict.

## Final-subject reconciliation

Rebase conflict resolution removed superseded agents API and package claims, aligned root and
Contracts changelogs on nonblank legacy input, and kept the v2.13 migration focused on this release's
actual action/compensation boundaries. The 80-page local docs build establishes mechanical inclusion
only; it does not promote this R6 claim to `Verified`.
