# Promise obligation 02 — workflow behavioral refinement

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over `45c86a63a9437abd920240b4dc95b235c0f72d37`; final binding was pending.
- **Promise sources:** issue #167 acceptance criterion 2; `IC-167-003`–`006`, `IC-MIGRATION-003`, `IC-CALCULUS-001`, `IC-CALCULUS-004`.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

For a closed, compilation-local bound workflow, prove all of the following before the consumer can
build: the bound requirement implies every possible entry requirement; each successful exit's
effective guarantee implies the bound action's declared guarantee; all leaf subjects equal the
bound subject; the leaf frame union is contained in the bound frame; and the pointwise authority
join is no stronger than the bound action's authority. A definite counterexample must be AGWF041;
invalid, opaque, contradictory, or otherwise unclassifiable input must be AGWF042.

The issue's pre-#168 phrase “the saga's postcondition” is discharged by explicit leaf `Ensures`
plus sound derived `CreatesLink` facts. `ModifiesProperty` remains frame-only; the analyzer does not
claim to verify arbitrary CLR step bodies.

The cheapest sufficient proof is **R4, component contract testing**, because this combines parser,
formula projection, finite solver, topology, frame, and authority semantics. Merely observing the
diagnostic declarations (R3) cannot prove the implication directions.

## Delivery evidence

- `WorkflowBindingProofAnalyzer.cs:183-371` rejects unclosed topology/compensation, proves the bound
  and leaf contracts, resolves every occurrence, rejects subject mismatch, then invokes entry,
  seams, ingress, frame, authority, and fork checks.
- Contract proof at `:417-542` validates resource domains, rejects Custom predicates and
  contradictions, checks frame realizability, semantically forgets written resources, and forms
  the effective guarantee.
- Entry contravariance is at `:374-415`; frame and authority bounds are at `:894-980`; successful
  exit covariance is at `:1241-1293`.
- `CheckImplication` at `:1106-1139` maps satisfiable counterexamples to AGWF041 and non-closed
  decisions to AGWF042.
- `WorkflowBindingProofAnalyzerTests.cs:301-359` exercises illegal seams, a failed bound guarantee,
  and opaque input. `ActionRefinementProofVectors.All` has nine shared classifications spanning
  requirement/guarantee variance, subject, frame, authority, opaque, and invalid contracts.
- `ActionRefinementTests.cs:115-175` independently pins that may-write is not a guarantee while
  `CreatesLink` is a sound guarantee.

## Result and limitation

The actual contract-level delivery matches the revised #168 model and the documented closed proof
boundary. It does **not** prove that an arbitrary `IWorkflowStep.ExecuteAsync` implementation honors
the ontology action named by `.Performs`; no public promise in the current docs asserts such body
verification.
