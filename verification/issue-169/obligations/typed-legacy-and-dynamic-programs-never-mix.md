# typed-legacy-and-dynamic-programs-never-mix

## Why this obligation exists

The no-identity `Compensate<T>()` overload remains a legacy runtime facade. Typed proof cannot safely
include it, a dynamic identity, or malformed imported metadata. `CompensationTopology.GetProgramKind`
at `src/Strategos.Generators/Models/CompensationTopology.cs:177` explicitly classifies mixed state;
derived runtime is not allowed to trust the closed subset.

## Failure scenario and boundary

A workflow proves two typed leaves but silently routes one legacy leaf through older compensation.
The scope is advertised as mechanically derived even though one executable leaf has no descriptor
proof or durable identity.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Artifacts: `WorkflowBindingProofAnalyzerTests.CollapsedTypedLegacyCompensation_WithoutBinding_FailsClosedInBothOrders`
  (`WorkflowBindingProofAnalyzerTests.cs:657`), dynamic/legacy cases at `:845` and `:886`, and
  `DerivedCompensationRuntimeTests.cs:327`, `:364`, `:2342`.
- Current disposition: Unproven pending protected both-order/imported cases.

## Refutation attempts for Stage 3

1. Classify `Typed + Legacy` as Typed in one source order.
2. Treat Dynamic as Legacy and emit the typed program subset.
3. Activate derived journal for a pure legacy workflow.

## Open questions

None.

## Investigation Log

No unresolved question. Legacy compatibility means preservation of the old path, not promotion into
the static proof program.
