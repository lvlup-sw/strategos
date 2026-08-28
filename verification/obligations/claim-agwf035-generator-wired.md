# claim-agwf035-generator-wired — Guard still reached from the generator with MainFlowClassification

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 20
Confidence: high as a wiring claim; high that the present source-scan is the wrong proof

## Ledger

| | |
|---|---|
| **Claim** | `TerminalReachabilityGuard.Report` is reached from `WorkflowIncrementalGenerator` with `MainFlowClassification`, and the under-reach arm receives the same `PhaseGraph` the transition table uses. |
| **Scope** | Production registration in `WorkflowIncrementalGenerator`; not the test `Report` seam. |
| **Consequence** | Tests stay green while generation never calls the guard, or calls it without the shared graph. Under-reach is then dark in every `[Workflow]` compilation. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A call-graph or syntax check that the generator invokes `Report` with both `MainFlowClassification` *and* the `PhaseGraph` instance (or the same `Build` result) used by `TransitionsEmitter`. Existing `GuardCallSitesAsync` matches only argument[1] text `Contains("MainFlowClassification")`. |
| **Why not cheaper** | The call is not generated from a single spec. The compiler does not require a static method to be invoked. This is the registration/closure class rung 3 owns. |
| **Failure signal** | Nothing. An unwired guard is a silent pass at generation time. |
| **Rollback** | Revert generator argument changes. Does not reverse consumer sagas already emitted under a wired or unwired guard. |
| **Lenses** | 6 Claim Derivation (claim 20). Survey lens 5 P6. |

**Open questions:**

- Survey backbone §1: generator `Report` at `WorkflowIncrementalGenerator.cs:1038` does not pass a graph. If that remains true, the second clause of this claim (shared graph argument) is already false and joins `claim-phasegraph-no-drift`.

## Evidence

Plan T1 AC (claim 20): "Guard still reached from WorkflowIncrementalGenerator.cs with MainFlowClassification." Existing-proof P6 (`TerminalReachabilityDiagnosticTests.cs:629–681`) is a source-text walk. It can pass while `phaseGraph:` is omitted or replaced with a private graph. Class comments at 598–600 note that unwiring the guard leaves the `Report` tests green.

Line anchors from survey at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `WorkflowIncrementalGenerator.cs:1038`; `TerminalReachabilityDiagnosticTests.cs:629–681`.
