# rollback-terminal-lifecycle-is-monotonic

## Why this obligation exists

At-least-once delivery guarantees stale and duplicate ingress. Generated guards treat finished,
Failed, and OutcomeUnknown as terminal/non-success and keep the saga/journal. Status checks appear
throughout `SagaCompensationComponentEmitter`, including lines 540, 627, 694, 1835, 1956, and 2029;
the timeout path writes `OutcomeUnknown` at line 2076.

## Failure scenario and boundary

An inverse times out after an uncertain external effect. A late completion or repeated failure trigger
then marks it successful or starts a second inverse. A related race occurs when worker failure and
forward timeout arrive in either order for the same execution: after one signal consumes rollback
authority, the other must be idempotent even though its exception kind differs. Collapsing
OutcomeUnknown into success is a false green; collapsing it into Failed loses operational distinction.

## Assigned proof

- Rung: R5, production-path integration.
- R4 backstops: inverse failure redelivery (`DerivedCompensationRuntimeTests.cs:882`), unmatched
  completion (`:911`), forward timeout/late completion (`:971`), diagnostic and approval/join stale
  controls (`:1808`, `:1891`, `:1965`, `:2056`), and both competing forward-terminal-signal arrival
  orders. The latter require one rollback dispatch, unchanged first-cause metadata, and no poison.
- Missing R5 artifact: real transport timeout/failure and delayed redelivery after persistence.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed the 1,915-test
  generator R4 matrix locally; final-revision/protected and real-host binding remain absent.

## Refutation attempts for Stage 3

1. Remove terminal guard from one ingress family.
2. Accept a late completion after OutcomeUnknown.
3. Reenter compensation from inverse failure.
4. Restore exception-kind matching for an already-consumed failure authority; one of the two
   competing-signal orders must poison rollback and fail the same-tree local fixture.

## Open questions

- No real-host failure/timeout redelivery fixture was found.

## Investigation Log

The Stage 1 docs survey found shorthand saying outcomes remain in `Failed`; code has a distinct
`OutcomeUnknown`. The obligation deliberately requires both to be retained non-success while
preserving the distinction.

## Stage 3 disposition

PF-7 distinguishes the existing generated state-by-ingress matrix from the Missing real-transport
timeout/failure redelivery fixture. COV-06 adds exact configured-timeout propagation upstream.

## Final-subject remediation

Independent review found the competing-signal race after the precursor evaluation. The final handler
still matches an unconsumed pending failure capability to its exact cause, but finds an already
consumed claim by execution/topology authority independent of a later cause. The first accepted cause
remains persisted. This closes the local instance without promoting the R5 obligation to `Verified`.
