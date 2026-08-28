# claim-phasegraph-edge-kinds — PhaseGraph routed vs additional vs membership rules

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 103, 104, 105
Confidence: medium — implementation comments that the no-drift table depends on

## Ledger

| | |
|---|---|
| **Claim** | A routed edge replaces linear chaining: a step that has one never also falls through to the next list entry. An additional edge coexists with linear chaining (confidence-gated low-confidence handler alongside the main-flow successor). Every target is a step-name-list entry or one of the two standard terminals, so a target is always a member of the emitted phase enum. |
| **Scope** | `PhaseGraph` construction; consumers `TransitionsEmitter` (`ValidTransitions`) and `TerminalReachabilityGuard`. |
| **Consequence** | Wrong edge kind makes AGWF035 and `ValidTransitions` lie in different directions: a routed step that also falls through, or an additional edge that replaced the main-flow successor. Membership drift is an emitted enum that cannot name a graph target. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Component tests on `PhaseGraph.Build` for a routed construct, a confidence-gated additional edge, and a target outside the step list. No dedicated `PhaseGraph` test file was inventoried (existing-proof P7). |
| **Why not cheaper** | The two edge kinds are not distinct types. Comments are not generation. Structural analysis can see `Build` exists and cannot decide replace-vs-alongside. |
| **Failure signal** | Nothing until a consumer hits a transition or a diagnostic that used the wrong successor. |
| **Rollback** | Revert `PhaseGraph` construction rules. Emits new consumer tables only on rebuild. |
| **Lenses** | 6 Claim Derivation (claims 103–105). |

**Open questions:**

- These comments support `claim-phasegraph-no-drift`. If instance-share is proven, edge-kind tests still belong here; the two consumers would then share the same mistake.

## Evidence

`PhaseGraph.cs:21–33` (claims 103–105). Promoted because the published transition table and AGWF035 both inherit these rules. Low-stakes relative to no-drift; not batched into no-drift because a shared instance can still encode the wrong edge kind.
