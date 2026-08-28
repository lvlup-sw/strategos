# claim-agwf035-route-underreach — AGWF035 decides route under-reach

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 11, 17, 41, 43, 61, 62, 81, 82, 83, 100, 106, 107, 109, 112, 113
Confidence: high that the target *claims* this; medium that current proofs bind the shipped generator

## Ledger

| | |
|---|---|
| **Claim** | AGWF035 decides route under-reach: when a rejoin construct's last step does not dispatch the declared terminal, generation reports AGWF035. |
| **Scope** | `TerminalReachabilityGuard` under-reach arm; production call from `WorkflowIncrementalGenerator`; IR/`PhaseGraph` used as the route graph. |
| **Consequence** | A dropped rejoin→terminal edge ships. The saga never starts the declared terminal. Contributors only see it in a container-backed host run. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Generator-driven kill fixture: a `[Workflow]` whose IR marks a construct rejoin and whose last step's successors omit the declared terminal, run through `WorkflowIncrementalGenerator`, asserting diagnostic `Id == AGWF035`. Existing `WithoutSuccessor` injections are the wrong subject. |
| **Why not cheaper** | Construction cannot derive "this last step omitted the terminal." The type system cannot express route reachability. Structural analysis can lock the call site (see `claim-agwf035-generator-wired`) but cannot decide the fire rule. |
| **Failure signal** | Compile-time AGWF035. Nothing at runtime separates "terminal never started" from a successful exclusive-path complete. |
| **Rollback** | Revert the under-reach arm. Does not reverse already-emitted consumer saga source until those consumers rebuild. |
| **Lenses** | 6 Claim Derivation (claims 41 / 11 / 61 / 106). Survey lenses 1, 5, 7 supplied the subject-binding gap. |

**Open questions:**

- Does under-reach cover a regression of #182/#186 if those emitters drop a start command the IR still has? Survey W1 says likely no. The answer moves this obligation from a PhaseGraph/IR claim to an emitter-emission claim (see `claim-agwf035-emitter-dropped-edge`).
- Is AGWF035-without-gating (Error still emits saga) intentional? AGWF037 joins `hasErrors` and suppresses saga emission; AGWF035 does not. The inventory never claimed suppression.

## Evidence

Highest-stakes CHANGELOG sentence (`CHANGELOG.md:172`): "`AGWF035` now decides route under-reach." Restated as plan T1 purpose (claim 11), commit `5e94af4` (claims 61–62), issue 185 body (claims 81–82), and the guard's own remarks (`TerminalReachabilityGuard.cs:15–16`, `:22–23`, `:26–28`).

The competing explanation required by `validating-claims.md`: the description states route analysis, and the code decides something narrower — IR/PhaseGraph successors, not saga emission. Survey backbone §3 records that production rebuilds `PhaseGraph.Build(model)` and compares rejoin dispatchers to that graph. A handler that forgets a construct the model still describes keeps the graph edge.

Existing proofs (`verification/survey/existing-proof.md` P1): under-reach positives at `TerminalReachabilityDiagnosticTests.cs:456–498` call `PhaseGraph.Build(model).WithoutSuccessor(...)` then `Report` directly. They prove the guard function fires when the injected graph lacks the edge. They do not prove the emitter would omit that edge, or that the generator would pass that graph.

Claim 17 (rejoining loop-exit / branch case with Finally stripped → AGWF035) is the acceptance criterion for this obligation. It is the same subject-binding question: stripped in the test double, or stripped in the production composition.

Claims 43, 81, 82 state the prior blindness. They are leads that this wave claims to close. They are not independent product invariants.

Claims 83, 100, 109 are the thesis ("a defect the compiler can see should not need Postgres"). They attach here as the stated purpose of the arm, not as a second obligation.

Line anchors at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `TerminalReachabilityGuard.cs:15–28`, `:136–140`; `CHANGELOG.md:172–176`.
