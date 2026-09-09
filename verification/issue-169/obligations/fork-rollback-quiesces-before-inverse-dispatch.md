# fork-rollback-quiesces-before-inverse-dispatch

## Why this obligation exists

Fork lanes can fail, complete, or start under different schedules. The generated saga records lane
terminality and pending failure, stops successors, and begins rollback only after quiescence. The
public plan preserves parallel structure, while generic state folds are serialized after selection.

## Failure scenario and boundary

One lane fails and rollback begins while another forward worker remains live. Its later completion
adds work after prefix selection or overwrites restored state. Two failed lanes can also start two
plans.

## Assigned proof

- Rung: R5, production-path integration.
- R4 backstops: fork quiescence/serial heads at `DerivedCompensationRuntimeTests.cs:154`, delayed start
  at `:1074`, two failed lanes at `:1118`, duplicate second-lane failure at `:1746`, stale join at
  `:2056`.
- Missing R5 artifact: concurrent real-host lane schedule with controlled delay/redelivery.
- Current disposition: Unproven.

## Refutation attempts for Stage 3

1. Start rollback after first lane failure regardless of remaining lane state.
2. Permit late completion to journal after active rollback.
3. Let two lane failures mint separate active plans.

## Open questions

- No real-host fork race fixture was found; R4 message permutations cannot be reported as R5.

## Investigation Log

### Does the behavioral host cover a fork schedule?

- Read: behavioral workflows/tests and Stage 1 inventory.
- Found: one linear host path; many in-memory fork permutations.
- Not found: real host fork/race test.
- Conclusion: obligation remains Unproven at its assigned rung.

## Stage 3 disposition

PF-7 records the generated permutation suite as existing and the controlled concurrent host fixture
as Missing. COV-04 separately owns the post-quiescence serial lowering rule.
