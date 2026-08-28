# compat-agwf035-underreach-breaking-diagnostic — AGWF035 gains failing shapes under an existing error id

| | |
|---|---|
| **Claim** | The under-reach arm of `AGWF035` is a breaking diagnostic for `[Workflow]` compilations that previously succeeded: the same error id now fails a rejoin last-step that does not dispatch the declared terminal. |
| **Scope** | Compile-time contract between `LevelUp.Strategos.Generators` and every consumer `[Workflow]`. C# extract path only (`WorkflowIncrementalGenerator` → `TerminalReachabilityGuard.Report`). JSON import does not call the guard. |
| **Consequence** | A consumer who upgrades the generator without changing their workflow can go from a clean build to AGWF035 error. The catalog sentence still describes over-reach (`chains to` / `runs past termination`), so the new failure reads as the old one. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `TerminalReachabilityDiagnosticTests` fire/silent fixtures. Those tests inject `WithoutSuccessor` rather than the production `PhaseGraph.Build(model)` (survey L5). The obligation needs a fixture whose production graph, not the test seam, is missing the rejoin edge. |
| **Why not cheaper** | Generation cannot express “this IR shape must fail.” The compiler does not encode diagnostic firing conditions. A call-site scan (rung 3) can prove the guard is invoked; it cannot prove which shapes newly fail. |
| **Failure signal** | Consumer compile error `AGWF035`. The channel is the compiler. It does not fire on JSON-imported workflows (unreached). |
| **Rollback** | Revert `5e94af4`. Already-emitted consumer saga source stays until those consumers rebuild. A revert after they rebuilt restores the old silence only on the next rebuild. |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high that the arm is a new failing shape on an existing error id. high that JSON import is unreached. |

**Compatibility class:** breaking change presented as additive (new arm of an existing error code; catalog id and message template reused).

**Impact:** source-breaking for previously-compiling C# workflows that match the rejoin-under-reach shape. Binary-compatible for already-generated saga DLLs until rebuild. Not a serialization or persistence change.

**Reverse dependency closure:**

1. `TerminalReachabilityGuard` under-reach arm (survey: `ReportUnderReach` at `:119-128`).
2. Production registration: `WorkflowIncrementalGenerator.cs:1038-1043` — after `hasErrors`; does not suppress `EmitWorkflowSources`.
3. Shared algorithm: `PhaseGraph.Build` (`PhaseGraph.cs:67`).
4. Consumers: every `[Workflow]` compilation that takes `LevelUp.Strategos.Generators`; generated `ValidTransitions` (same graph type, separate `Build` at `TransitionsEmitter.cs:56`).
5. Transitive: consumer CI, any tool that treats AGWF035 as the over-reach sentence only.
6. Catalog / Contracts: id and remediation string unchanged (`AgwfCatalog.tsp:340-346`; `WorkflowDiagnostics.cs:561-564`). T1 plan said “Only widen catalog remediation if that sentence becomes a lie.”

**What this revision does not do**

- Does not rename or remove AGWF035.
- Does not change the emitted `ValidTransitions` / `IsValidTransition` signatures.
- Does not persist a new field. The break is compile-time.

**Reverses?** Generator source: yes. Consumer trees: only after rebuild. JSON-imported workflows: the new arm never applied, so revert is a no-op there.

**Open questions:**

- Is shipping AGWF035-without-gating (error still emits the saga) intentional? If yes, a consumer can have a red diagnostic and a generated table that still claims transitions. That is a published-API lie adjacent to this break.
- Do any in-repo or documented sample workflows now fail under-reach on the production `Build`? If the only red fixtures use `WithoutSuccessor`, the breaking surface may be narrower than the CHANGELOG reads.

**What is expensive to find again**

The message template at `WorkflowDiagnostics.cs:564` is shared by both arms. Under-reach passes `{0}` = terminal and `{2}` = last step; the sentence still says `{0}` chains to `{2}`. That inversion is the “additive” presentation: one catalog row, two opposite graphs.
