### agwf035-call-site-scan-ignores-graph — Guard call-site scan must observe the under-reach graph

| | |
|---|---|
| **Claim** | The structural call-site gate that this wave relies on to prove `TerminalReachabilityGuard.Report` is still reached must fail if the generator stops using `PhaseGraph.Build` of the same model for the under-reach arm — including passing a private graph, a graph built from another model, or a wrapper that keeps the invocation text. |
| **Scope** | S1. `Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline` and `GuardCallSitesAsync` in `TerminalReachabilityDiagnosticTests.cs:629-681`. Production call at `WorkflowIncrementalGenerator.cs:1038-1044`. |
| **Consequence** | The only arm the class says fails when the guard is unwired stays green while the under-reach arm is handed a tautological or empty graph. Reviewers read a pass as "the pipeline still shares the emitter's route graph." The new route-analysis positives stay green because they inject their own graph. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A call-graph or syntax check that the generator invocation's `phaseGraph` argument is either omitted (the production default that means `Build(model)`) or is exactly `PhaseGraph.Build(<the same model>)`, and that rejects a private construction, a different model's graph, or a commented-out call. |
| **Why not cheaper** | The compiler does not track which `PhaseGraph` instance `Report` received. There is no generator that emits this call. A component test that calls `Report` directly is the shape this wave already has; it does not observe the generator's argument list. |
| **Failure signal** | Nothing in production. The scan is a test. A miss is a green suite plus a silent under-reach arm. |
| **Rollback** | Revert the scan or the generator call. The scan is not a shipped control. |
| **Lenses** | False-Green Shapes |

**Open questions:**

- Is omitting `phaseGraph` (current production) the intended shared-graph contract, with `Build(model)` inside `Report` as the single construction site? If yes, the scan should lock "argument absent or default," not "argument present." If the CHANGELOG "share one PhaseGraph" claim means one instance passed from the generator into both the emitter and the guard, the scan is looking at the wrong argument and the generator does not do that share.
- Would an alias (`var g = TerminalReachabilityGuard; g.Report(...)`) or a local wrapper evade `member.Expression.ToString() != nameof(TerminalReachabilityGuard)`? If yes, the scan's expression-text match is the same substring class as the classification `Contains`.

## What led here

Survey P6 said the call-site scan checks only `arguments[1]` for the substring `MainFlowClassification` and does not look at `phaseGraph`. This wave's new under-reach arm is the first production behavior that consults a `PhaseGraph`. The scan existed on `4d060f4` for the over-reach classification seam. This wave did not extend it. Competing explanation: production omits `phaseGraph` on purpose, so there is nothing to scan. Discriminating detail: a generator that *does* pass a graph — `phaseGraph: PhaseGraph.Build(other)` or `new` empty — keeps `arguments[1]` as `MainFlowClassification.For(model).OffMainFlowStepNames`. The scan stays green. The under-reach arm then consults the wrong graph.

## Code read (revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`)

- `TerminalReachabilityDiagnosticTests.cs:639-645` — asserts `pipelineCall.ClassificationArgument` `Contains` `nameof(MainFlowClassification)`.
- `TerminalReachabilityDiagnosticTests.cs:664-676` — matches `TerminalReachabilityGuard.Report` by identifier text; records `arguments[1].ToString()` only; no read of later arguments.
- `TerminalReachabilityDiagnosticTests.cs:620-624` — comments claim the second-argument check stops a private skip list. They do not mention the graph.
- `WorkflowIncrementalGenerator.cs:1038-1044` — five-argument call; `phaseGraph` omitted.
- `TerminalReachabilityGuard.cs:61-67` — optional sixth parameter.

Walk failure throws (`703-704`): fail-closed if the tree is missing. A commented-out call is trivia and does not match: that half of the scan is sound.

## Kill probe

- Replace the generator call with `TerminalReachabilityGuard.Report(model, MainFlowClassification.For(model).OffMainFlowStepNames, ..., phaseGraph: PhaseGraph.Build(emptyModel))`. Scan stays green (`arguments[1]` still contains `MainFlowClassification`).
- Unwire `phaseGraph` (already omitted) or pass a private graph: scan stays green.
- Remove the `Report` call entirely: scan goes red. That is the only kill the scan currently has.

## Failure scenario

Someone "shares" a graph by constructing one in the generator from a partial model, or passes `phaseGraph: default` after changing the default to an empty graph. The scan still sees `MainFlowClassification` in argument 1. CI is green. Under-reach is blind.

## Open questions (full stakes)

### Is omitting `phaseGraph` the intended shared-graph contract?

Stage 1 backbone 1 already recorded that "share one PhaseGraph" is type-share, not instance-share: the generator does not pass a graph; `TransitionsEmitter` and `Report` each call `Build`. If that is the contract, this obligation's proof is "the sixth argument is absent" plus "inside `Report`, null means `PhaseGraph.Build(model)` and that call is still there." A scan that demanded a passed instance would be a false obligation. If the contract is one instance, both the generator and the scan are short, and promise-vs-delivery owns the claim. This lens only needs the answer to know which argument the structural gate must lock.

### Would an alias or wrapper evade the expression-text match?

`member.Expression.ToString() != nameof(TerminalReachabilityGuard)` is a substring/identity check on syntax text. `using static` or a local function `Report(...)` that forwards would drop the site from the list. `callSites` would then lack `WorkflowIncrementalGenerator` and the test would fail — unless another file still contains a matching invocation (the scan walks every `.cs` under `src/Strategos.Generators`). A decoy invocation in a leftover file would satisfy `.Contains(nameof(WorkflowIncrementalGenerator))`. That changes the obligation from "extend the argument check" to "bind the exact invocation in `WorkflowIncrementalGenerator.cs`."
