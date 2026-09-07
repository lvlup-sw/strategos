# ci-proof-policy-version-pinned — CI proof policy version is pinned

| | |
|---|---|
| **Claim** | A green #167 build/test result identifies the immutable reusable-workflow revision that selected and ran the test projects; a comment recording an old SHA while executing a movable tag is not evidence binding. |
| **Scope** | `.github/workflows/ci.yml` `build-test`, `coverage-gate`, and `update-baseline` reusable workflow references and every #167 component test delegated to them. |
| **Consequence** | The same Strategos commit can receive different coverage/test selection as the remote `v1` tag moves, or pass after the policy stops executing an expected suite, with no change in this repository. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | SHA-pin each reusable workflow invocation (with a human-readable version comment) and structurally reject movable refs on required proof jobs; retain the selected project/filter parameters in-repo. |
| **Why not cheaper** | Rung 1 does not generate the remote policy reference. Rung 2 cannot constrain GitHub Actions refs or prove job selection. |
| **Failure signal** | Usually nothing: the check remains green under the new remote policy. A later missing-test regression is the first signal, and it cannot identify which historical policy produced the prior verdict. |
| **Rollback** | Restore the last reviewed reusable-workflow SHA or vendor the required commands. Editing only the explanatory SHA comment does not change the executed policy. |
| **Lenses** | False-green shapes: stale evidence, wrong policy subject, skipped suite. |

**Confidence:** High.

**Current proof posture:** Indeterminate. All three reusable jobs execute immutable revision `ffd6fb4979fba8a4317cca6fd7ad9ec4090acff9`, and the local workflow lint in `final-evidence.md` passed at exact-current `98fabb4`. The required protected run has not established that this pinned policy selected and executed its proofs.

**Open questions:**

- None. Record the protected check URL and final revision after execution.

### Investigation Log

#### Is the required test policy bound to an immutable revision?

- Read: `.github/workflows/ci.yml` build-test, coverage-gate, and update-baseline declarations, their inputs and comments, plus the evidence-binding skill reference.
- Historical finding: the reusable jobs used movable `@v1` refs while adjacent comments named a SHA; the comments did not constrain execution.
- Current finding: all three `uses` fields are pinned to `ffd6fb4979fba8a4317cca6fd7ad9ec4090acff9`; local structural lint passed at exact-current `98fabb4`.
- Not found: protected execution bound to final subject `98fabb4`.
- Conclusion: policy selection is structurally closed in the candidate, while the guard's execution evidence remains `Indeterminate`.
