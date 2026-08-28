# pad-agwf035-underreach-is-ir-not-emission

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 12, 41, 61, 62, 83, 84, 106, 107, 109

| | |
|---|---|
| **Claim** | AGWF035 decides route under-reach: a rejoin last step that does not dispatch the declared terminal fails at generation. |
| **Scope** | `TerminalReachabilityGuard.ReportUnderReach` and its sole production call from `WorkflowIncrementalGenerator`. |
| **Consequence** | A dropped saga start command of the #184 class (IR still lists the Finally edge; the emitter forgets `Start{Finally}`) compiles and ships. The author sees no AGWF035. The only remaining catch is a container-backed run. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires` / `Diagnostic_RejoiningBranch_FinallyEdgeStripped_Fires` — both inject `PhaseGraph.WithoutSuccessor`. |
| **Why not cheaper** | The language cannot express “this IR edge is present in the saga.” A shared type (rung 1) does not compare emission to IR. A compiler check (rung 2) does not walk generated handlers. |
| **Failure signal** | Nothing at compile time for the #184 class. Runtime: saga parks and never starts the terminal. |
| **Rollback** | Revert the under-reach arm. Does not restore a compile-time lock that this arm never had over saga emission. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — discriminating detail is the comparison subject. |

**Open questions:**

- Is AGWF035-without-gating (saga still emitted) intentional? A later investigator must read whether Error + emit is the house rule for AGWF035 specifically. Stakes: if intentional, this obligation narrows to “the arm’s subject is IR, not emission”; if not, emission-gating is a second violation. See `pad-agwf035-error-still-emits`.

## What the target claims

CHANGELOG Residue (`CHANGELOG.md:172`): “`AGWF035` now decides route under-reach.”

Plan T1: “[#184] was the motivating instance and is already fixed in the emitter; this arm is the compile-time lock so the next dropped edge does not need Postgres.”

Issue 185: “Adding that arm makes #184 compile-time decidable.”

`TerminalReachabilityGuard.cs:15-16`: “Decides, at emission time, whether a workflow's main flow actually ends at the termination its author declared.”

`TerminalReachabilityGuard.cs:26-28`: “a defect the compiler can see should not need Postgres to surface.”

## Competing explanation (named before the read)

The description states a route lock over what the saga will dispatch. The code implements a comparison of two IR derivations over the same `WorkflowModel`.

## Discriminating detail

`ReportUnderReach` (`TerminalReachabilityGuard.cs:150-164`) enumerates last steps from `EnumerateRejoinDispatchersOf(model, terminal)` and asks `graph.SuccessorsOf(lastStep)` whether the terminal is listed. Production builds that graph as `phaseGraph ?? PhaseGraph.Build(model)` (`:127`). The generator call (`WorkflowIncrementalGenerator.cs:1038-1043`) does not pass a graph.

`PhaseGraph.Build` walks the same constructs. A well-formed IR therefore always has the Finally edge the enumerator expects. The positive tests do not use production `Build`:

```459:459:src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs
        var graph = PhaseGraph.Build(model).WithoutSuccessor("PayClaim", "CloseClaim");
```

```484:484:src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs
        var graph = PhaseGraph.Build(model).WithoutSuccessor("ProcessApprovedOrder", "ShipApprovedOrder");
```

The #184 mechanism was a missing `Start{Finally}` in the saga emitter while `LoopModel.BranchOnExit` still described the rejoin. That pair — IR present, emission absent — cannot trip this arm. The arm treats the symptom the ticket named (a missing edge in an abstract graph) instead of the mechanism (a handler that forgets a construct the model still describes).

## What the code does support

A C# `[Workflow]` whose IR itself omits a rejoin→terminal edge reports AGWF035. That is a real, narrower lock. It is not the CHANGELOG sentence and not “#184 compile-time decidable.”

## Failure scenario

A future lowering block emits path-end handlers without `Start{Finally}` while `PhaseGraph` still records the edge from `AddBranch`. The guard stays silent. `ValidTransitions` still lists the edge. The saga never starts the terminal. Postgres is required again.

## Disposition of related claims

- Inventory 41 / 61 / 107: **narrower than claimed.**
- Inventory 12 / 84 / 109: **nothing supports “#184 compile-time decidable” / “does not need Postgres” for the emitter-miss class.**
- Inventory 62: “Under-reach is now decidable from the shared PhaseGraph” — true of the IR comparison; false as a saga-emission lock.
