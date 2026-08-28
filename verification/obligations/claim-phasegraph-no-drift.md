# claim-phasegraph-no-drift — Guard and ValidTransitions share one PhaseGraph

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 13, 45, 64, 65, 102
Confidence: high that the target claims no-drift; high that type-share alone does not establish it

## Ledger

| | |
|---|---|
| **Claim** | The termination-reachability guard and the emitted `ValidTransitions` table resolve successors from one `PhaseGraph` so they cannot drift. |
| **Scope** | Shared internal `PhaseGraph`; consumers `TerminalReachabilityGuard` and `TransitionsEmitter`; production registration in `WorkflowIncrementalGenerator`. |
| **Consequence** | AGWF035 and the published transition table disagree. A consumer reads `IsValidTransition` / `ValidTransitions` and gets a different graph than the diagnostic that certified the workflow. Stage 0 names that drift a published-API lie. |
| **Proof rung** | Construction and generation |
| **Proof artifact** | One `PhaseGraph` instance built once and passed to both consumers. Absent instance-share: a deterministic equality lock that the diagnostic graph's edges equal the edges `TransitionsEmitter` writes into `ValidTransitions`. |
| **Why not cheaper** | This *is* the cheapest rung. Type-share (a shared class, two `Build` calls) is not this rung. It is a cheaper-looking shape that lets the two consumers see different edges. |
| **Failure signal** | Nothing. Drift is a silent published-API disagreement until a consumer hits a transition the diagnostic did not see, or the reverse. |
| **Rollback** | Revert the lift. Does not reverse already-emitted consumer tables until rebuild. |
| **Lenses** | 6 Claim Derivation (claims 45 / 13 / 65 / 102). Survey lenses 1, 3, 7: type-share, not instance-share. |

**Open questions:**

- Does any production path pass the same instance today, or do `TransitionsEmitter` and `TerminalReachabilityGuard` each call `PhaseGraph.Build`? Survey backbone §1 says production builds twice (`TransitionsEmitter.cs:56`, `TerminalReachabilityGuard.cs:127`) and the generator `Report` at `WorkflowIncrementalGenerator.cs:1038` does not pass a graph. If that holds, the claim is false as written and the proof is missing, not merely misplaced.

## Evidence

Highest-stakes CHANGELOG guarantee (`CHANGELOG.md:176–177`): "The guard and the emitted `ValidTransitions` table now share one `PhaseGraph` so they cannot drift." Plan T1 (claim 13) and commit `46fb93a` (claims 64–65) use the same "cannot drift" / "one graph" wording. `PhaseGraph.cs:16–17` (claim 102) repeats it as a type-level comment.

The competing explanation: the lift shared a *type*, not an *instance*. Survey backbone §1 settled this from code: `WithoutSuccessor` is a test seam; CHANGELOG "share one PhaseGraph" is type-share. Existing-proof P7: no equality lock between the diagnostic graph and `ValidTransitions`. P6's call-site scan checks `MainFlowClassification` text and does not require a `phaseGraph` argument.

This obligation does not assert that instance-share holds. It asserts that the no-drift claim is what the target must keep true, and that the cheapest sound proof is one derivation (rung 1) or an edge-equality check (rung 3, situational if instance-share is refused).

Line anchors at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `PhaseGraph.cs:16–17`; `CHANGELOG.md:176–177`.
