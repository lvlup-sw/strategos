# pad-phasegraph-type-not-instance

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 13, 45, 64, 65, 102

| | |
|---|---|
| **Claim** | The guard and the emitted `ValidTransitions` table share one `PhaseGraph` so they cannot drift. |
| **Scope** | `PhaseGraph` construction at `TransitionsEmitter` and `TerminalReachabilityGuard`; the generator `Report` call. |
| **Consequence** | Reviewers treat instance-identity as the lock. Two `Build` calls can later diverge (different model snapshots, a test seam left wired, a future third caller) while CHANGELOG still says they cannot drift. The published `ValidTransitions` table can then disagree with AGWF035. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | None that locks equality of the two graphs. `Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline` is source-text and does not assert a `phaseGraph` argument. |
| **Why not cheaper** | Generation (rung 1) would hold if one instance were passed. Today two independent `Build` calls exist, so a derived-once artifact is the cheap lock that is missing. Types (rung 2) cannot express “these two graphs are the same object / equal.” |
| **Failure signal** | Nothing. A drifted table is published API. AGWF035 may stay silent or fire on a different pair. |
| **Rollback** | Revert the lift of `PhaseGraph`. Already-emitted consumer tables do not reverse until rebuild. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — two call sites, no pass-through. |

**Open questions:**

- None about the instance/type distinction. `Build` is a pure function of the model today; two calls on the same model agree. That agreement is an accident of purity, not of sharing one graph.

## What the target claims

CHANGELOG Residue (`CHANGELOG.md:176-177`): “The guard and the emitted `ValidTransitions` table now share one `PhaseGraph` so they cannot drift.”

Commit `46fb93a` body: “The termination-reachability guard and the emitted ValidTransitions table must resolve successors from one graph so they cannot drift.”

`PhaseGraph.cs:16-17`: “Shared by the transition table emitter and the termination-reachability guard so the diagnostic and the emitted `ValidTransitions` table cannot drift.”

## Competing explanation

The lift shares a type and an algorithm. Callers each build their own instance.

## Discriminating detail

```56:56:src/Strategos.Generators/Emitters/TransitionsEmitter.cs
        var graph = PhaseGraph.Build(model);
```

```127:127:src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs
                phaseGraph ?? PhaseGraph.Build(model));
```

```1038:1043:src/Strategos.Generators/WorkflowIncrementalGenerator.cs
        TerminalReachabilityGuard.Report(
            model,
            MainFlowClassification.For(model).OffMainFlowStepNames,
            FluentDslParser.ExtractDeclaredTerminalStepName(context.TargetNode, context.SemanticModel, ct),
            GetAttributeLocation(context),
            diagnostics);
```

The generator does not pass a graph. `WithoutSuccessor` is a test seam (`PhaseGraph.cs:116-118`). Under-reach also walks `EnumerateRejoinDispatchersOf` independently of `PhaseGraph` for *who* should dispatch. Two authorities decide the same route question.

The call-site test (`TerminalReachabilityDiagnosticTests.cs:629-646`) asserts the owner type and that argument 1 mentions `MainFlowClassification`. It does not read argument 6 (`phaseGraph`). Unwiring a shared instance still passes.

## Disposition

- Inventory 13 / 45 / 65 / 102: **implemented narrower than claimed** — type-share and algorithm-share, not one instance, and no equality lock.
- Inventory 64: “Lift TransitionsEmitter.PhaseGraph to a shared internal type” — **supported** (the type exists). The “so they cannot drift” purpose is the overclaim.
