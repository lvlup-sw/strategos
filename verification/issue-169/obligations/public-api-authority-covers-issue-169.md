# public-api-authority-covers-issue-169

## Why this obligation exists

`Strategos.Ontology` adds the closed inverse/rollback public model and assembly-wide API baseline.
`Strategos` adds typed builder/configuration members and expands a historically selective API gate.
External interface implementers break even though ordinary legacy call sites remain source-compatible.

## Failure scenario and boundary

A public member disappears or changes nullability without a baseline failure, or the selective
Strategos allowlist and reflection test co-drift and stop covering the new overload.

## Assigned proof

- Rung: R3, deterministic API inventory/baseline analysis.
- Artifacts: ontology API analyzer plus `src/Strategos.Ontology/PublicAPI.Unshipped.txt`; Strategos
  `PublicApi.globalconfig`, shipped/unshipped ledgers, `BuilderApiBaselineTests`, and
  `scripts/check-builder-api-stability.sh`.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed the builder public
  API stability gate locally with 0 warnings/errors; final-revision/protected checks and a removal
  mutation in each assembly remain absent.

## Refutation attempts for Stage 3

1. Delete `Compensate<T>(WorkflowActionReference)` from an interface.
2. Change a tracked signature/nullability entry while leaving the baseline stale.
3. Remove one #169 type from selective Strategos tracking without changing product code.

## Open questions

- Can the selective Strategos policy and reflection inventory drift together without another
  independently derived expected surface?

## Investigation Log

### Is Strategos API coverage assembly-wide?

- Read: Stage 1 authority topology and API configuration.
- Found: ontology is assembly-wide; Strategos uses a configured cross-product allowlist plus tests.
- Conclusion: exact removal mutation is required before claiming the new surface is governed.

## Stage 3 disposition

PF-3 narrowed this file to mechanical drift and corrected the rung: R2 compiles a breaking API, while
R3 compares it to policy. Intrinsic closed/null/immutability/signature shapes moved to
`issue-169-public-api-shapes-are-compiler-enforced`; human compatibility intent is owned by
`issue-169-public-api-intent-is-reviewed` at R6.
