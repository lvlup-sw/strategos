# claim-agwf035-emitter-dropped-edge — Under-reach is a compile-time lock on the next dropped emitter edge

Lens: 6 Claim Derivation
Disposition: unsupported-claim finding
Inventory claims: 12, 84
Confidence: high that the #184-class lock is not exhibited

## Finding

Claim 12 (plan T1): "#184 was the motivating instance and is already fixed in the emitter; this arm is the compile-time lock so the next dropped edge does not need Postgres."

Claim 84 (issue 185): "Adding that arm makes #184 compile-time decidable."

The competing explanation: the arm decides a missing edge *in the IR/PhaseGraph*, not a missing start command *in saga emission*. Survey backbone §3: under-reach compares IR rejoin dispatchers to `PhaseGraph.Build(model)`, not saga emission. A handler that forgets a construct the model still describes keeps the graph edge. The #184 class — missing `Start{Finally}` while IR is correct — stays silent. Positive tests inject `WithoutSuccessor`; production rebuilds the graph from the model.

Nothing in the survey exhibits a generator-driven fixture whose IR is complete and whose emitted saga omitted the terminal start. Existing-proof P1 states the same subject split. Survey run-wide question: "Does under-reach cover a regression of #182/#186 if those emitters drop a start command the IR still has? (Likely no — W1.)"

The narrower claim "AGWF035 fires when PhaseGraph last-step successors omit the terminal" is `claim-agwf035-route-underreach`. That claim has a mechanism. This finding is that claims 12 and 84, as written about the #184 / dropped-emitter class, have no supporting exhibit.

## Ledger (for the claim that failed promotion)

| | |
|---|---|
| **Claim** | The under-reach arm makes the next emitter-dropped Finally / #184-class edge compile-time decidable without Postgres. |
| **Scope** | Saga emitters vs `PhaseGraph` built from IR; `WorkflowIncrementalGenerator` composition. |
| **Consequence** | The motivating class can regress in an emitter while AGWF035 stays green. The CHANGELOG still says the class is locked. |
| **Proof rung** | (none — unsupported) |
| **Proof artifact** | Would need a production-path test that mutates emission, not IR. None found in the existing-proof inventory. |
| **Failure signal** | Container-backed saga run — the channel the thesis said this arm replaces. |
| **Rollback** | Not applicable; the claimed lock is not present. |
| **Lenses** | 6 Claim Derivation. Survey lenses 1, 5, 7. |

**Open questions:**

- Is AGWF035-without-comparing-emission intentional (IR-only lock) and claims 12/84 overstated? If yes, this finding stands and the under-reach obligation should be rewritten to drop the #184-class wording. `(needs human input)` only after an investigator confirms no emission-side compare exists.

Line anchors cited from survey at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `TerminalReachabilityGuard.cs:127`; `WorkflowIncrementalGenerator.cs:1038`.
