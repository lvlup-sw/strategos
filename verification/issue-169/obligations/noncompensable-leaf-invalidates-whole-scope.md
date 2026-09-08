# noncompensable-leaf-invalidates-whole-scope

## Why this obligation exists

Issue #169 requires upward propagation: one noncompensable leaf invalidates every containing
composite. The public plan carries recursive `IsCompensable` and ordered noncompensable leaves, while
`WorkflowBindingProofAnalyzer` closes every rollback-claimed scope and reports AGWF045 rather than
emitting a partial program.

## Failure scenario and boundary

A workflow containing one written leaf with no inverse still compiles because another leaf has typed
compensation. Runtime rolls back only the easy part and presents the workflow as safe.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Artifacts: `ActionInverseTests.NonCompensableLeafPropagatesThroughEveryCompositeKind`
  (`ActionInverseTests.cs:572`),
  `WorkflowBindingProofAnalyzerTests.TypedCompensation_WithUncompensatedWrittenSibling_ReportsAgwf045`
  (`WorkflowBindingProofAnalyzerTests.cs:567`), and rollback-safe binding case at line 930.
- Current disposition: Unproven pending protected analyzer/calculus execution.

## Refutation attempts for Stage 3

1. Aggregate compensability with OR instead of AND.
2. Stop scope traversal after the first proved inverse.
3. Drop identity or nested-handler leaves from topology and require closure fixtures to detect it.

## Open questions

None.

## Investigation Log

No unresolved question. The obligation is about declared/topological compensability, not arbitrary
runtime success of the inverse implementation.
