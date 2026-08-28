# recur-termination-route-complete

Open class **R1**. Guard candidate **G-R1**. This diff extends AGWF035; it does not close the class.

## What led here

Recurrence seed R1: five filed termination-family instances. #187 shipped the over-reach arm only. #194 fixed emitters for #184/#182/#186 and left the route arm deferred. This branch adds the under-reach arm (`5e94af4`) and lifts `PhaseGraph` (`46fb93a`). Survey backbone §2–§4: under-reach compares IR rejoin dispatchers to `PhaseGraph`, not saga emission; AGWF035 is C#-only and does not join `hasErrors`; production `Report` does not receive a graph instance.

A fix (or a half-guard) with no complete protected path leaves the class open.

## Surfaces at 324768f

- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:1038–1043` — `TerminalReachabilityGuard.Report(...)` with `MainFlowClassification.For(model)` and **no** `phaseGraph` argument.
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:930–938` — `hasErrors` includes duplicate PermitTrigger (AGWF037) and does **not** include AGWF035.
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:61–67` — `phaseGraph` optional; null ⇒ build from model. Remarks at `:34–37` document the test seam (`WithoutSuccessor`).
- `src/Strategos.Generators/Emitters/TransitionsEmitter.cs:56` — second `PhaseGraph.Build(model)` for `ValidTransitions`.
- `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs:456–474, 481–498` — under-reach positives inject `WithoutSuccessor`.
- Same file `:629–676` — call-site scan requires `MainFlowClassification` as argument `[1]`; `phaseGraph` is invisible.

JSON import emit (`BridgeImportFile` → `EmitWorkflowSources`) never calls the guard (survey L1/L4).

## Failure

A consumer compiles. The saga either chains past `Finally` (over-reach: #155 stall, #175 cycle with zero `MarkCompleted`) or never publishes `Start{Finally}` (under-reach: #184 park). After this diff, a C# workflow whose **IR** lacks the Finally edge should fire AGWF035. A workflow whose IR is correct and whose **saga emit** drops the start command does not. A JSON-imported twin never sees the guard. An AGWF035 Error still emits `Saga.g.cs`.

Who observes it: the operator of a running saga, usually after a rejoin or approval, not the author at compile time.

## Expensive to find again

- The production call site omitting `phaseGraph` looks like “shared PhaseGraph” in CHANGELOG and is type-share only.
- Injected-graph tests stay green if the generator is unwired.
- #182/#186 sit in the same family and are outside the “construct marked rejoin” arm if the IR still has the edge.

## Open questions (with stakes)

- Does the under-reach arm fire on a regression of #182 / #186 if those emitters drop a start command the IR still has? If no, those shapes are R8, and claiming R1 closed them is a lie. Stakes: the plan scopes the arm to rejoin; treating approval-dispatch as covered would hide a hang behind a green AGWF035.

### Investigation Log

#### Does production Report share a PhaseGraph instance with ValidTransitions?

- Read: `WorkflowIncrementalGenerator.cs:1038–1043`; `TransitionsEmitter.cs:56`; `TerminalReachabilityGuard.cs:56–67`.
- Found: two `PhaseGraph.Build(model)` sites; generator does not pass a graph.
- Not found: a third shared field or emit-context graph.
- Conclusion: type-share, not instance-share. Tagged on the obligation as the reason G-R8 is the cheaper close for saga-true drops.

#### Does AGWF035 join hasErrors?

- Read: `WorkflowIncrementalGenerator.cs:930–945`.
- Found: `hasDuplicatePermittedForkTrigger` gates emission; AGWF035 is reported after that return.
- Conclusion: Error still emits. This is R3 applied to INV-5 on the same diagnostic that was already half-closed.
