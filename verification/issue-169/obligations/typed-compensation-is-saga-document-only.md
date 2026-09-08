# typed-compensation-is-saga-document-only

## Why this obligation exists

Generated inverse completion folds returned `TState`. For EventSourced workflows, live and replay
state depend on consumer-defined `ApplyEvent`, which the generator cannot prove. The analyzer rejects
typed EventSourced at `WorkflowBindingProofAnalyzer.cs:147`; pure legacy event-sourced behavior stays
separate.

## Failure scenario and boundary

A no-op or incomplete `ApplyEvent` compiles, live rollback appears correct, and replay reconstructs
the pre-rollback state. A compile-only fold is therefore not evidence of semantic equivalence.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Artifacts: `WorkflowBindingProofAnalyzerTests.TypedCompensation_WithEventSourcedNoOpFold_ReportsAgwf045`
  (`WorkflowBindingProofAnalyzerTests.cs:449`) and
  `DerivedCompensationRuntimeTests.Emit_EventSourcedTypedProgram_ReportsAgwf045` (`:312`), plus pure
  legacy generated compilation (`:2371`).
- Current disposition: Unproven. `mutation-evidence.md` records a local historical precursor
  `42b4ed7` kill when the EventSourced predicate was disabled; exact-final mutation and protected
  typed-negative/legacy-positive execution are still unbound.

## Refutation attempts for Stage 3

1. Remove the EventSourced rejection; typed no-op fold must turn the suite red.
2. Apply the rejection to legacy mode; legacy compatibility positive must fail.
3. Move rejection after source emission and confirm generated text cannot be misread as acceptance.

## Open questions

None.

## Investigation Log

No unresolved question. This is an explicit v2.13 scope boundary, not a claim that event-sourced
rollback is impossible in a later design with a provable fold algebra.
