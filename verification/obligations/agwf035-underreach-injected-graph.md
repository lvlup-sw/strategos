### agwf035-underreach-injected-graph — AGWF035 under-reach positives must bind the production graph

| | |
|---|---|
| **Claim** | A green AGWF035 under-reach positive means `WorkflowIncrementalGenerator` would report AGWF035 for that dropped rejoin edge when it calls `TerminalReachabilityGuard.Report` with `phaseGraph` omitted (the production default, which builds `PhaseGraph.Build(model)`). |
| **Scope** | S1. The under-reach positives added on `324768f` in `TerminalReachabilityDiagnosticTests`, the `WithoutSuccessor` test seam on `PhaseGraph`, and the generator call at `WorkflowIncrementalGenerator.cs:1038-1044`. |
| **Consequence** | The wave's kill fixtures stay green after the generator is unwired, after `Report` is handed a private graph, or after `PhaseGraph.Build` keeps an edge the test already deleted. Reviewers and CI treat the new route-analysis arm as locked. A dropped Finally in a future lowering block is the class the comments say the arm exists for, and it is the class these positives never drive. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | A generator-driven under-reach fixture: a `WorkflowModel` whose own constructs omit the rejoin edge, run through `GeneratorTestHelper.RunGenerator` / `WorkflowIncrementalGenerator`, asserting `Id == "AGWF035"`. Until such a fixture exists, the injected-graph positives are not this proof. |
| **Why not cheaper** | Construction cannot derive a "dropped Finally" from a model that still has the edge. The type system cannot tell a test-injected `PhaseGraph` from `PhaseGraph.Build(model)`. A structural scan of the call site can lock that the generator still calls `Report`; it cannot lock that a missing rejoin edge in the model produces the diagnostic. |
| **Failure signal** | Nothing. A silent AGWF035 under-reach is a compile that succeeds. The saga still emits (AGWF035 does not join `hasErrors`). |
| **Rollback** | Revert the under-reach arm and the `WithoutSuccessor` seam. Already-generated consumer sagas do not reverse until rebuild. |
| **Lenses** | False-Green Shapes |

**Open questions:**

- Does any in-repo workflow source omit a rejoin/Finally edge while the IR still describes the construct, so a generator-driven kill fixture could exist without `WithoutSuccessor`? If yes, the injected-graph positives are redundant and this obligation collapses to "use that fixture." If no, the comments at `TerminalReachabilityDiagnosticTests.cs:604-611` stay true and the positives cannot bind production.
- Would deleting the `phaseGraph` parameter (forcing `Build(model)` inside `Report`) turn the current positives red? If they would fail, the seam is load-bearing for the only under-reach greens. If they would still pass, a different injection is in play.

## What led here

Survey lens 5 P1 (`verification/survey/existing-proof.md`) and the Stage 1 backbone item 3 said the new under-reach positives inject `WithoutSuccessor` and call `Report` directly. This lens validated that against the code at `324768f`. Competing explanation: the injection is an honest counterfactual, like the empty-classification over-reach arm, and the call-site scan binds the generator. Discriminating detail: production never passes a graph (`WorkflowIncrementalGenerator.cs:1038-1044`), and the new positives pass a surgically edited one. The scan does not look at that argument. Unwiring the generator, or changing `Build` so the Finally edge stays, leaves the new positives green.

The class comments state the hole (`TerminalReachabilityDiagnosticTests.cs:598-600`): every arm that calls `Report` directly stays green with the guard unwired.

## Code read (revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`)

- `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs:456-498` — `Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires` and `Diagnostic_RejoiningBranch_FinallyEdgeStripped_Fires` build `PhaseGraph.Build(model).WithoutSuccessor(...)` then `Report(..., phaseGraph: graph)`.
- `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs:792-807` — test `Report` helper forwards the optional graph.
- `src/Strategos.Generators/Models/PhaseGraph.cs:116-137` — `WithoutSuccessor` is documented as a test seam that copies the dictionary and `RemoveAll`s one target.
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:56-59`, `119-127` — `phaseGraph` null means `PhaseGraph.Build(model)`.
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:1038-1044` — production call omits `phaseGraph`.
- `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs:508-523` — the all-Complete negative is the only new arm that also runs `GeneratorTestHelper.RunGenerator`. It is a silence check.

`git diff 4d060f4...324768f` on this test file adds the `WithoutSuccessor` positives. They were not on the merge-base.

## Kill probe

Edit that must turn the new positives red if they bound production: stop passing `phaseGraph` in those two tests (use `Build(model)` as the generator does), or unwire `TerminalReachabilityGuard.Report` from `WorkflowIncrementalGenerator`. The first stays green because the test still injects. The second stays green because the tests do not run the generator.

Edit that *does* turn them red: make `ReportUnderReach` a no-op, or make `WithoutSuccessor` a no-op. That proves the guard function and the seam, not the shipped composition.

## Failure scenario

A later lowering block appends a rejoin name and forgets the Finally edge. `PhaseGraph.Build` still publishes the edge because the IR still describes the construct (survey backbone 3). Production `Report` sees the edge and stays silent. The injected-graph tests still pass. CI is green. The author of the lowering block has a catalog sentence that says AGWF035 now decides route under-reach.

## Open questions (full stakes)

### Does any in-repo workflow source omit a rejoin/Finally edge while the IR still describes the construct?

The comments at `TerminalReachabilityDiagnosticTests.cs:604-611` say no consumer-visible under-reach workflow exists and that today's derivations enumerate the same constructs. If that is still true, a generator-driven positive cannot be written from current fixtures and the injected-graph tests are the only "positives" this wave added. The obligation then stands: those positives are not a production-path proof. If a fixture already omits the edge, this obligation is the wrong shape and the missing work is "run that fixture through the generator."

### Would deleting the `phaseGraph` parameter turn the current positives red?

If yes, the seam is the only reason the new greens exist, and removing the test-only parameter is a kill fixture for this obligation. If the tests were rewritten to mutate the model instead, the injection shape would be gone. The answer changes whether the fix is "add a generator fixture" or "delete the seam and accept no positive until a real under-reach model exists."
