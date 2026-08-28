# recur-graph-representations-agree

Open class **R8**. Guard candidate **G-R8**. `PhaseGraph` extraction is type-share. The saga is unbound.

## What led here

Three filed disagreements of the same route graph: #175 (`TransitionsEmitter` flat linear chain; sibling exclusive paths chained; terminal → path step; emitted public API), #184 (table and Mermaid handled `BranchOnExit`; the saga did not — table declared an edge the saga cannot take), #189 (table `{ Normalize, [PayFull, PayPartial, Failed] }` vs saga that can emit one successor).

#187 built a construct-aware graph as a private nested type in `TransitionsEmitter`. #194 taught the saga loop-exit. This diff lifts the type (`46fb93a`) and points AGWF035 under-reach at that type. Production still builds twice. CHANGELOG “share one PhaseGraph so they cannot drift” is the claim; survey backbone §1 is the delivery.

## Surfaces at 324768f

- `TransitionsEmitter.cs:56` — `var graph = PhaseGraph.Build(model);` then emit `ValidTransitions`.
- `TerminalReachabilityGuard` — if `phaseGraph` is null, builds from model (`:127` in survey; optional param at `:67`).
- `WorkflowIncrementalGenerator.cs:1038` — does not pass a graph.
- `PhaseGraph.WithoutSuccessor` — test seam used by R1 under-reach positives. That is a diagnostic kill, not a saga/table equality kill.
- No test asserts saga edges ⊆ table edges ⊆ diagnostic edges (existing-proof P7).

The #184 class (missing `Start{Finally}` while IR is correct) stays silent under R1: under-reach compares IR rejoin dispatchers to `PhaseGraph`, not saga emission (survey backbone §3).

## Failure

`IsValidTransition` is generated public API. A consumer or a tool that trusts the table takes an edge the saga cannot, or the saga takes an edge the table forbids. Who observes it: a runtime hang (#184) or an unbounded exclusive-path chain (#175), after a green compile.

## Expensive to find again

- Type identity (`PhaseGraph` used in two files) reads as instance identity.
- R1’s injected-graph tests will be cited as the equality proof. They strip an edge the diagnostic sees; they do not read saga source.
- Mermaid may still be a fourth construction. Confirming that is cheaper now than after the next emitter edit.

## Open questions (with stakes)

- Does Mermaid still build a separate graph at this revision? If yes, G-R8’s policy list is incomplete and the next disagreement will be Mermaid vs table, the #184 shape again. Stakes: omitting Mermaid repeats the class with a new consumer name.

### Investigation Log

#### Does the generator pass one PhaseGraph instance to diagnostic and table?

- Read: `WorkflowIncrementalGenerator.cs:1038–1043`; `TransitionsEmitter.cs:54–56`.
- Found: two builds, no passed instance.
- Conclusion: G-R8’s mechanism is “build once, pass in,” not “extract the type.” Type extraction already landed and left the class open.
