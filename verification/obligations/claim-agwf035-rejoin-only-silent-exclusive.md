# claim-agwf035-rejoin-only-silent-exclusive — Fire only on rejoin; all-terminal exclusive stays silent

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 14, 15, 18, 19, 44, 63, 85, 86, 108, 110, 114
Confidence: high

## Ledger

| | |
|---|---|
| **Claim** | AGWF035 under-reach fires only when a construct is marked rejoin (fork join, rejoining branch/loop-exit case, approval resume, linear predecessor) and that last step's successors omit the declared terminal. All-terminal exclusive paths (every case `Complete()` plus `Finally<T>`) stay silent. The shipped fork/branch corpus and `Diagnostic_ExistingCorpus_NeverFires` stay silent. |
| **Scope** | `TerminalReachabilityGuard` under-reach fire rule; generator-driven negatives on exclusive-complete and the existing fixture corpus. |
| **Consequence** | A false AGWF035 on a legitimate all-`Complete()` branch + `Finally` blocks legal workflows. The inverse — firing on a non-rejoin construct, or staying silent on a marked-rejoin dropped edge — is `claim-agwf035-route-underreach`. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Generator-driven negatives: all-`Complete()` + `Finally` reports no AGWF035; existing corpus (`SourceTexts`, ≥30) reports no AGWF035; rejoin-positive remains the sibling obligation. |
| **Why not cheaper** | The language cannot encode "rejoin vs exclusive-complete" as an unrepresentable state. Structural analysis can see a `rejoin` flag and cannot decide silence vs fire without interpreting the graph. |
| **Failure signal** | A false-positive is a compile error the author sees. A missed exclusive-path shape that should stay silent has no production signal until someone files it. |
| **Rollback** | Revert the under-reach arm. Over-reach is independent (`claim-agwf035-overreach-preserved`). |
| **Lenses** | 6 Claim Derivation (claims 15 / 44 / 110). Survey lens 5: P3 binds the generator for the all-Complete fixture; P5 binds the corpus silence only. |

**Open questions:**

- Claim 86's second clause ("a construct dropped the edge") is the emitter-IR gap in `claim-agwf035-emitter-dropped-edge`. This obligation covers only the "every route legitimately terminated" half.
- P3/P5 let through any exclusive-path shape that is not the one all-Complete fixture. Whether more silent shapes exist is unsettled.

## Evidence

Highest-stakes plan T1 fire rule (claim 15), restated in CHANGELOG (`CHANGELOG.md:174–176`, claim 44), commit `5e94af4` (claim 63), issue 185 (claim 85), and `TerminalReachabilityGuard.cs:25`, `:136–139` (claims 108, 110). Acceptance criteria: claims 18, 19. Test remarks: claims 114, and the "two conditions, one code" comment (claim 112 lives on the sibling under-reach obligation).

Naive "something must list the terminal as next step" is the named false-positive trap (claims 14, 85). The obligation exists because that naive rule is representable and wrong.

Existing-proof P3 (`TerminalReachabilityDiagnosticTests.cs:508–523`) runs both `Report` and `GeneratorTestHelper.RunGenerator(AllCompleteBranchSource)`. That is the one under-reach-adjacent proof that binds this revision's generator. P5 corpus silence is the "no false positives on shipped fixtures" half (claim 19 / spec claim 97).

Line anchors at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `TerminalReachabilityGuard.cs:25`, `:136–139`; `CHANGELOG.md:174–176`.
