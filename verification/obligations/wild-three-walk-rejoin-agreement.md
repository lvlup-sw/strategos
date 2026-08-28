# wild-three-walk-rejoin-agreement

The target depends on three walks of the same rejoin agreeing. It claims two.

## What led here

Residue CHANGELOG (`CHANGELOG.md:176-177`) and `PhaseGraph` remarks (`PhaseGraph.cs:16-17`) state one obligation: the termination guard and the emitted `ValidTransitions` table share `PhaseGraph`, so they cannot drift.

The motivating defect class is a third walk. Issue 185 named #184 as the instance: loop-exit rejoin cases never published `Start{Finally}` because they were absent from a dedicated handler loop, while the **model already knew** the rejoin. CHANGELOG 2.11.0 already records that nothing in the generated saga consults `ValidTransitions` at runtime (`CHANGELOG.md:127-128`).

The under-reach arm does not inspect saga emission. It fires when `EnumerateRejoinDispatchersOf` lists a last step whose `PhaseGraph.SuccessorsOf` does not contain the declared terminal (`TerminalReachabilityGuard.cs:150-164`). `PhaseGraph.AddLoopEdges` / `AddBranch` (`PhaseGraph.cs:219-227`, `:456-462`) add that rejoin edge from the same model fields (`RejoinStepName`, `ContinuationStepName`, `JoinStepName`) that the saga emitters read when they do emit `Start{Rejoin}` (`BranchHandlerEmitter.cs:187`, `:270`; `LoopCompletedHandlerEmitter.cs:262`). A handler that forgets a construct the IR still describes keeps the graph edge. Under-reach stays silent. `ValidTransitions` still advertises the missing dispatch.

The production call site omits `phaseGraph` (`WorkflowIncrementalGenerator.cs:1038-1043`), so the guard rebuilds `PhaseGraph.Build(model)` (`TerminalReachabilityGuard.cs:127`). `TransitionsEmitter` builds again (`TransitionsEmitter.cs:56`). Shared type and algorithm; two instances; neither is the saga walk.

Positive tests never compile a naturally dropped-edge workflow. They inject `PhaseGraph.WithoutSuccessor` (`TerminalReachabilityDiagnosticTests.cs:459`, `:484`). Production never constructs that graph.

## Failure scenario

A later emitter change drops `Start{Finally}` (or `Start{Rejoin}`) for a construct the model still describes — the #184 / #182 / #186 class. AGWF035 under-reach does not fire. The published `ValidTransitions` table still lists the edge. The saga hangs at runtime until a container-backed run, which is the failure the arm claims to make compile-time.

JSON import shares `EmitWorkflowSources` (`WorkflowIncrementalGenerator.cs:93-112`) and therefore the table, and never calls the guard at all. A JSON twin of the same shape has no AGWF035 even as a two-walk check.

## Code paths read (rev `324768f`)

- `src/Strategos.Generators/Models/PhaseGraph.cs:16-17`, `:67-71`, `:120-137`, `:219-227`, `:456-462`
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:20-28`, `:56-59`, `:119-128`, `:150-164`, `:174-231`
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:93-112`, `:1033-1045`
- `src/Strategos.Generators/Emitters/TransitionsEmitter.cs:19-24`, `:56`, `:76-80`
- `src/Strategos.Generators/Emitters/Saga/BranchHandlerEmitter.cs:187`, `:270`
- `src/Strategos.Generators/Emitters/Saga/LoopCompletedHandlerEmitter.cs:250-262`
- `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs:456-473`, `:481-498`
- `CHANGELOG.md:127-128`, `:172-177`

## Why not cheaper

- **Rung 1.** No single derivation emits saga `Start{X}` commands, `ValidTransitions`, and the under-reach check from one edge list. Two `PhaseGraph.Build` calls are type-share, not instance-share. Situational: a generator that lowered dispatch from `PhaseGraph` would move this claim to rung 1.
- **Rung 2.** Successor names and command identifiers are strings. The compiler cannot require that a `Start{CloseClaim}Command` publication exist for every `PhaseGraph` edge into `CloseClaim`.
- **Rung 3 is the cheapest sound rung.** A structural check that every rejoin edge in `PhaseGraph` has a corresponding `Start{target}` publication in the emitted saga (and that `ValidTransitions` lists the same edge) is graph closure. A component test that injects `WithoutSuccessor` does not close the saga walk.

## What is expensive to find again

The three walks look shared because they read the same model fields. The divergence is which **emitter** is allowed to forget a field the model still has. The test seam `WithoutSuccessor` makes the two-walk check look covered.

## Open questions

- Does under-reach cover a regression of #182 / #186 if those emitters drop a start command the IR still has? If no, this obligation is already violated for those historical shapes, and the Residue claim that AGWF035 "now decides route under-reach" is a two-walk claim only. The consequence does not change: the saga walk remains unlocked.
- Does any out-of-repo tool already treat `ValidTransitions` as the dispatch contract? If yes, a silent two-walk agreement becomes a published-API lie the moment the saga walk drifts, not only a hung saga.
- Does the JSON import path have a second AGWF035 call this lens missed? No `Report` call was found outside the C# transform. A hidden call would narrow Scope to C# vs import; it would not bind the saga walk.
