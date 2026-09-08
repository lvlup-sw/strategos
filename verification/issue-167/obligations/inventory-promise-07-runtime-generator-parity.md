# Promise obligation 07 — refinement result invariants and runtime/generator parity

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; final binding was pending.
- **Promise sources:** `IC-CHANGELOG-003`, `IC-CALCULUS-002`, `IC-COMMENT-007`–`009`, claim seed 7.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

The public runtime refinement API must classify a substitute as Proven, Refuted, Opaque, or
Invalid without permitting contradictory result states. Runtime and build-time workflow proof must
agree on the shared behavioral-subtyping rules: requirement/guarantee variance, subject equality,
frame containment, authority ordering, opaque input, and invalid contracts.

The cheapest sufficient proof is **R4, one neutral vector corpus interpreted by both production
paths**. Sharing only the finite solver/parser source prevents low-level drift but cannot establish
parity of orchestration, precedence, authority, or frame decisions.

## Delivery evidence

- `ActionRefinementAnalysis.cs:5-138` defines the closed statuses/obligations, snapshots failure
  arrays, rejects undefined enum values and null entries, forbids failures on Proven, requires a
  failure on non-Proven, and exposes `IsRefinement` only for the consistent Proven state.
- `ActionCalculus.cs:10-84` analyzes either an action or composite and maps closed/opaque/refuted
  implementation contracts into the shared refinement routine.
- `Strategos.Generators.csproj:37-53` source-links the same `LogicFormula`, `FiniteDomainSolver`, and
  Roslyn predicate parser used by the ontology analyzer.
- `ActionRefinementProofVectors.cs:10-126` is a neutral nine-vector matrix covering positive
  variance plus each stated failure family. `ActionRefinementProofVectorTests.cs:10-39` runs it
  through public `ActionCalculus`; `ActionRefinementProofVectorGeneratorTests.cs:19-69` independently
  lowers it into source and runs the real workflow generator, requiring exact diagnostic class and
  stable message fragment.
- `ActionRefinementTests.cs:207-341` separately checks result invariants and refuted/opaque/invalid
  precedence.

“Same rules” has local coverage for the enumerated overlapping rules, not identity/topology diagnostics
that have no counterpart in the two-action runtime API.
