# inverse-analysis-cancellation-propagates

## Why this obligation exists

COV-01 found cooperative cancellation in the surveyed public-calculus scope and a new entry test, but
no active claim. Cancellation is control flow, not a semantic inverse verdict.

## Failure scenario and boundary

A cancelled proof is caught and converted to Invalid/Opaque/Missing, or returns a partial Proven plan
after a solver observes cancellation. A caller can cache or execute a result that never completed.

## Assigned proof

- Rung: R4, public component behavior under controlled cancellation.
- Existing artifact: `ActionInverseTests.InverseAnalysisHonorsCancellation` at entry.
- Proposed artifact: deterministic mid-analysis/solver cancellation asserting
  `OperationCanceledException` and no result/rollback plan.
- Current disposition: Unproven pending protected propagation and anti-verdict evidence.

## Investigation Log

No unresolved question. Only inverse analysis is claimed cancellable; rollback factories without a
token are not silently included.
