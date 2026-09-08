# parallel-rollback-is-noninterfering

## Why this obligation exists

`ActionCalculus.DeriveParallelRollbackPlan` begins at `ActionCalculus.cs:690`. A valid parallel plan
must reject write/write overlap and both write/read directions, including reads nested in an inverse
contract, while shared reads are harmless. The selected conflict is deterministic.

## Failure scenario and boundary

Branch A's inverse writes `Status` while branch B's inverse requires the old `Status`. Parallel
structure becomes schedule-dependent even with disjoint forward frames. External effects may still
interfere despite declared disjoint resources; that separate consumer boundary is not proved here.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Artifacts: ActionInverse tests at lines 421 (write/write), 462 (both write/read directions), 487
  (nested reads/snapshot), and 511 (shared reads), plus workflow fork AGWF cases.
- Current disposition: Unproven until both-direction kill probes and protected vectors are bound.

## Refutation attempts for Stage 3

1. Remove left-write/right-read detection.
2. Remove right-write/left-read detection.
3. Compute read footprints only from direct leaves, omitting nested sequences/scopes.
4. Reject shared read/read and confirm the legal vector catches over-conservatism.

## Open questions

None within declared resources.

## Investigation Log

The survey distinguished structural parallel algebra from generated serial state folding. That is an
intentional runtime refinement, not a refutation of this plan-level obligation.
