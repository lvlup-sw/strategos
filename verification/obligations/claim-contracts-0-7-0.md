# claim-contracts-0-7-0 — Strategos.Contracts 0.6.0 → 0.7.0 with AGWF037 in the catalog

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 24, 50, 69, 123
Confidence: high for the source/pack identity claim; medium for "the nupkg Exarchos extracts contains AGWF037"

## Ledger

| | |
|---|---|
| **Claim** | `Strategos.Contracts` versions at 0.7.0 and the generated catalog/schema set includes the duplicate-permitted-fork-trigger id (AGWF037) over 0.6.0's path-end type-collision id. Catalog artifacts are regenerated, not hand-edited. |
| **Scope** | Contracts csproj version; TypeSpec `AgwfCatalog.tsp`; generated `AgwfCode` / `agwf-catalog.json` / `AgwfEntryDuplicatePermittedForkTrigger.json`; packed nupkg consumed by Exarchos. |
| **Consequence** | Exarchos converters throw on unknown members if producer and consumer versions skew. A 0.7.0 nupkg that omits the AGWF037 schema/catalog is a published contract that does not contain the id this wave added. |
| **Proof rung** | Production-path integration tests |
| **Proof artifact** | `dotnet pack` of this revision whose nupkg name/nuspec are 0.7.0 *and* whose packed content includes `agwf-catalog.json` and `AgwfEntryDuplicatePermittedForkTrigger.json`. Codegen-guard that committed `Generated/` equals regen. |
| **Why not cheaper** | A csproj `Version` string (authored, not generated) can diverge from the packed nupkg. Catalog identity tests (P16–P19) read committed files and do not pack. Schema-diff CI skips and succeeds if no previous tag (P23). |
| **Failure signal** | Exarchos extract/convert failures after upgrade. In-repo, a pack test that does not require the AGWF037 files stays green (P24). |
| **Rollback** | Source revert. A published `contracts-v0.7.0` tag does not reverse for already-upgraded consumers. This branch does not create that tag (stage 0). |
| **Lenses** | 6 Claim Derivation (claims 50 / 24 / 123). Survey lens 5 P15–P25. |

**Open questions:**

- Is `contracts-v0.7.0` published? Stage 0: not created by this branch.
- Is `contracts-test` a required check? If not, P16–P19/P24 never gate merge.
- CHANGELOG 2.11.0 lede still says Contracts 0.4.0 → 0.6.0 while Residue and csproj say 0.7.0 (survey L7). Not an inventory-numbered claim; recorded because it contradicts claim 50's surrounding document.

## Evidence

Highest-stakes CHANGELOG (`CHANGELOG.md:182`) and plan T2 (claim 24): bump plus "Regen the catalog (do not hand-edit `Generated/` or `docs/diagnostics/agwf.md`)." Commit `12098da` (claim 69). Packaging test comments (claim 123).

Existing-proof P24 packs and asserts `LevelUp.Strategos.Contracts.0.7.0.nupkg` and named schemas that do **not** include `AgwfEntryDuplicatePermittedForkTrigger.json` or `agwf-catalog.json`. `Nupkg_Contains_SchemasUnderContentFiles` is satisfied by any one schema. P23 schema-diff skips and succeeds when `have_prev=false`.

Line anchors from survey: `PackagingTests.cs` (0.7.0 nupkg name); `AgwfCatalog.tsp:358–365`.
