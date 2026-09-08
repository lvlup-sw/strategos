# nonproven-inverse-is-never-executable

## Why this obligation exists

`ActionInverseAnalysis` is a closed result algebra in
`src/Strategos.Ontology/Descriptors/ActionInverseAnalysis.cs`; rollback leaves/plans in
`ActionRollbackPlan.cs:128` carry compensability and executable action only for Proven non-identity
analysis. Stage 3 split the independent workflow-emission refusal into
`rejected-typed-inverse-cannot-reach-emitted-runtime`.

## Failure scenario and boundary

A public calculus caller receives executable rollback work from Missing, Refuted, Opaque, or Invalid
analysis. This file owns the core construction boundary, not generator integration.

## Assigned proof

- Rung: R1, construction.
- Primary authority: validated internal result construction plus plan derivation that preserves
  noncompensable leaves.
- Backstop: `ActionInverseTests.RefutedInverseIsNeverExposedAsExecutableRollbackCode`
  (`ActionInverseTests.cs:598`).
- Current disposition: Unproven pending protected constructor/leaf mutation.

## Refutation attempts for Stage 3

1. Allow executable type on Refuted analysis; the public plan fixture must turn red.
2. Return Proven with failures; result-construction tests must reject the impossible shape.
3. Pass a non-Proven analysis through each public rollback factory; no executable leaf may emerge.

## Open questions

None.

## Investigation Log

No unresolved question. PF-1 narrowed this file to the R1 core invariant; the new R3 evidence file
owns analyzer-to-emitter reachability.
