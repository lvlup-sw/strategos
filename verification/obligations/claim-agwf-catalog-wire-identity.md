# claim-agwf-catalog-wire-identity — AGWF catalog keeps 31 codes, gaps, and name-based wire identity

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 121, 122
Confidence: high as a catalog-contract claim touched by adding AGWF037

## Ledger

| | |
|---|---|
| **Claim** | Ground truth is exactly 31 defined AGWF codes with gaps preserved as gaps (INV-5: never renumber). Member names are the wire identity: the enum round-trips by name and never by ordinal, so a member may be added but never renamed or reordered without a major bump. |
| **Scope** | `AgwfCatalog.tsp`; generated `AgwfCode` / catalog / JSON Schema; `docs/diagnostics/agwf.md`; Exarchos extract. |
| **Consequence** | Renumbering or renaming breaks Exarchos converters (throw-on-unknown / name identity). Adding AGWF037 in the middle or reordering 035/036 would be a major-bump event presented as a minor. |
| **Proof rung** | Construction and generation |
| **Proof artifact** | TypeSpec is the single source; codegen-guard fails if `Generated/` / schemas / `docs/diagnostics` drift. Enum JSON round-trip by name (existing P18). Hand-authored `GroundTruthCodes` lists (P16/P17) are a duplicated authority — three identical test lists (survey L3). |
| **Why not cheaper** | This *is* the cheapest rung for "two representations of one catalog." A test that re-lists the 31 codes is a second authority, not a cheaper proof. |
| **Failure signal** | Codegen-guard on contracts paths. Path-filtered: a generator-only AGWF037 wiring change does not run it (P22). |
| **Rollback** | Revert the TypeSpec entry. A published 0.7.0 catalog does not reverse for upgraded consumers. |
| **Lenses** | 6 Claim Derivation (claims 121–122). |

**Open questions:**

- Three identical `GroundTruthCodes` test lists (survey L3) can drift from each other while still matching a stale catalog. That is a proof-system smell, not a second product claim.

## Evidence

`AgwfCatalog.tsp:14–17` (claims 121–122). This wave adds AGWF037 and therefore touches the 31-count and the "add but never rename/reorder" rule. Existing-proof P15–P21 are identity and parity, not diagnostic behavior. P19 markdown test is a substring; a footnote can satisfy.
