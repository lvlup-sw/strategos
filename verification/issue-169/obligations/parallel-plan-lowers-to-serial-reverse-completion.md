# parallel-plan-lowers-to-serial-reverse-completion

## Why this obligation exists

COV-04 found the public-parallel/generated-serial refinement only in prose and a rejected candidate,
not an active correctness claim. It is distinct from interference analysis and fork quiescence.

## Failure scenario and boundary

After quiescence, generated rollback dispatches multiple lane inverses, chooses source order rather
than descending completion sequence, hides one reducer result from the next, or mutates/collapses the
public parallel plan.

## Assigned proof

- Rung: R4, compiled generated component under controlled effects.
- Proposed artifact: at least two completed lanes with an order discriminator, one-active-inverse
  assertion, reducer carrier between lanes, and unchanged public plan.
- Current disposition: Unproven pending a protected fixture and ordering mutant.

## Investigation Log

No unresolved question. Real transport concurrency is outside the narrowed component claim; arbitrary
external-effect ordering stays consumer-owned.
