# claim-renovate-path-token — Second extends path names the file under tools/

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 25, 52, 78, 79
Confidence: high for the in-repo path-token claim; this is not "Renovate resolves"

## Ledger

| | |
|---|---|
| **Claim** | The second Renovate `extends` token names `tools/renovate-config/presets/dotnet.json` (the path where the preset file lives), not the repo-root path that 404'd. |
| **Scope** | `renovate.json` second `extends` entry; the preset file in `lvlup-claude` (survey: exists on that repo after the `exarchos` rename). |
| **Consequence** | A wrong path token is the #181 inert-if-404 class: the control looks present and applies nothing. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A check that the `extends` token's path suffix equals a resolvable preset path. In this repo that is at most "the token contains `tools/renovate-config/presets/dotnet.json`." Existence of the *remote* file is not this repository's tree. |
| **Why not cheaper** | `renovate.json` is not generated from the preset. The compiler does not parse Renovate extends. |
| **Failure signal** | Nothing in this repo. Renovate bot logs, if anyone reads them. |
| **Rollback** | Revert the one-line path. |
| **Lenses** | 6 Claim Derivation (claims 25 / 52 / 79). Survey lenses 3, 4, 6. |

**Open questions:**

- "Renovate resolves the organisation's dotnet preset" is a stronger claim (51). That is `claim-renovate-resolves-preset`.
- Does Renovate resolve the `lvlup-claude` slug after the `exarchos` rename? Survey run-wide question. Path-token correctness does not answer slug resolution.

## Evidence

Plan T3 (claim 25), CHANGELOG (`CHANGELOG.md:184–185`, claim 52), commit `334f64c` body (claim 79). Claim 78's subject ("point Renovate at the existing lvlup-claude dotnet preset") is this path-token claim, not the resolve claim.

Survey backbone §9: the path token points at a file that exists (`tools/renovate-config/presets/dotnet.json` on `lvlup-sw/lvlup-claude`). No binder that the remote preset resolves. Existing-proof P44: none found.
