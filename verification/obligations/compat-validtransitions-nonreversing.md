# compat-validtransitions-nonreversing — Emitted ValidTransitions is a published API that source revert does not roll back

| | |
|---|---|
| **Claim** | The generated `ValidTransitions` / `IsValidTransition` pair stays the public contract of each `[Workflow]`. This revision must not change that pair’s signatures, and a revert of the generator must not be treated as a revert of already-emitted consumer tables. |
| **Scope** | Emitted public API from `TransitionsEmitter` (`{PascalName}Transitions.g.cs`) consumed by consumer apps and any tool that reads the dictionary. |
| **Consequence** | If the lifted `PhaseGraph` yields different successors than the old nested type, consumers who rebuild publish a different table under the same type names. If they do not rebuild, they keep the old table after a generator revert. Either way the in-repo source history is not the consumer’s contract. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `TransitionGraphLoweringTests` plus `TransitionsEmitterUnitTests` (signature and successor-set fixtures). An equality lock between the diagnostic graph and the emitted table does not exist (survey L5). |
| **Why not cheaper** | The emitter is generated (rung 1) and that proves one file is derived from `PhaseGraph.Build`. It does not prove the lift preserved successor sets versus `4d060f4`, and it does not prove the diagnostic `Build` and the emitter `Build` agree. The compiler cannot compare two generated dictionaries. |
| **Failure signal** | Nothing in production. A wrong table is a silent published lie until a human reads `IsValidTransition` or a runtime check uses it. |
| **Rollback** | Revert `46fb93a` / `5e94af4`. Does not reverse already-emitted consumer source until those consumers rebuild. No stated rollback for that residue. |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high for non-reversal of emitted source. high that signatures are unchanged in this diff. medium that successor sets are identical to the pre-lift nested type (algorithm was moved, not re-specified; no equality fixture against `4d060f4`). |

**Compatibility class:** generated public API; shape-compatible; content bound to consumer rebuild; does not reverse.

**Impact of this diff on the contract**

- Signatures unchanged: `IReadOnlyDictionary<{Phase}, {Phase}[]>` and `bool IsValidTransition({Phase} from, {Phase} to)` (`TransitionsEmitter.cs:68-109`).
- Nested `PhaseGraph` lifted to `src/Strategos.Generators/Models/PhaseGraph.cs`. Emitter now calls `PhaseGraph.Build(model)` at `:56`. Terminal names moved to `PhaseGraph.CompletedPhase` / `FailedPhase` (`:84-85`, `PhaseGraph.cs:41-46`).
- No removed or renamed generated members. No default change. No serialization-format change of the table (it is C# source, not a wire document).
- CHANGELOG 2.11.0 lede’s “data-migration risk for a generated enum’s member order” is the earlier main-flow work, not this lift. This residue does not touch `PhaseEnumEmitter`.

**Reverse dependency closure:**

1. `TransitionsEmitter.Emit` — C# and JSON import both reach it via `EmitWorkflowSources` (survey 1c).
2. Every consumer `[Workflow]` compilation that packs `LevelUp.Strategos.Generators`.
3. Any runtime or test that calls `IsValidTransition` or enumerates `ValidTransitions`.
4. `TerminalReachabilityGuard` — same type, second `Build` (`TerminalReachabilityGuard.cs:127`). Drift between the two calls is a published-API lie (diagnostic vs table), not a consumer source-compat break.

**Persisted data:** none. Older saga documents do not store this table. Older *generated source* in consumer repos is the residue that does not reverse.

**Reverses?** In-repo generator: yes. Consumer generated files: no, until rebuild. That non-reversal is the obligation.

**Open questions:**

- Do any first-party or documented consumers check `ValidTransitions` at runtime against Marten-stored phase values? If yes, a successor-set drift is a runtime break, not only a source-table change.
- Is there an out-of-repo copy of the old nested algorithm that must stay aligned? Unlikely; the type is `internal`.

**What is expensive to find again**

`WorkflowIncrementalGenerator.cs:1038` does not pass a graph into `Report`. Production therefore builds twice. The CHANGELOG sentence “share one PhaseGraph so they cannot drift” (`CHANGELOG.md:176-177`) is type-share. A later reader who treats that as instance-share will miss the non-reversing emitted table.
