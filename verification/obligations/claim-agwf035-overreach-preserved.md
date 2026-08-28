# claim-agwf035-overreach-preserved — Over-reach half of AGWF035 still holds

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 42, 94, 95, 96, 97, 99
Confidence: high as a regression obligation; spec DR-3 is a lead about the *prior* arm, not a complete statement of this wave

## Ledger

| | |
|---|---|
| **Claim** | AGWF035 still fires when a declared `Finally<T>` is not last on the main flow, or when a main-flow step's computed successor is construct-owned. The diagnostic keeps a catalog entry and does not fire on the shipped generator-test corpus. |
| **Scope** | `TerminalReachabilityGuard` over-reach arm; spec DR-3 conditions; existing fixtures in `Strategos.Generators.Tests`. |
| **Consequence** | This wave's under-reach work regresses the already-shipped half. A terminal that is over-reachable compiles again. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Existing over-reach fixtures plus `Diagnostic_ExistingCorpus_NeverFires`. Counterfactual empty-classification tests (existing-proof P2) are the wrong subject for "the generator still classifies and reports." |
| **Why not cheaper** | Position/successor rules are not a type invariant. Structural analysis can lock the call (sibling `claim-agwf035-generator-wired`) and cannot decide the two over-reach conditions. |
| **Failure signal** | Compile-time AGWF035. Same catalog sentence as under-reach (see `claim-agwf035-catalog-honest`). |
| **Rollback** | Revert under-reach only; over-reach predates this wave. A revert of the whole guard is a change that does not isolate this claim. |
| **Lenses** | 6 Claim Derivation (claims 42 / 94–97). Survey: spec DR-3 describes only the over-reach half. |

**Open questions:**

- None about what DR-3 meant. Stage 0 / intent-and-claims already record that this wave must not treat DR-3 ACs as the complete under-reach spec.

## Evidence

Claim 42 is the CHANGELOG restatement of the already-shipped half (`CHANGELOG.md:172–173`). Claims 94–97 and 99 are spec DR-3 / INV-5 text about that same half (`docs/specs/2026-08-22-correctness-core.md:60`, `:105–110`). Claim 96 (reverting DR-1 with DR-2 in place produces the diagnostic) is a historical acceptance criterion, not a new fixture this wave adds.

Existing-proof P2/P4/P5 cover over-reach silence and the empty-classification counterfactual. They do not re-prove under-reach.

Claim 98 ("Nothing checks it") is historical and was not promoted. Claim 100's decidability thesis sits on `claim-agwf035-route-underreach`.
