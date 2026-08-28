# pad-contracts-changelog-contradicts-0-7-0

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 50 vs the 2.11.0 lede; inventory 60; package CHANGELOG vs T2 “regen the catalog”

| | |
|---|---|
| **Claim** | The 2.11.0 record of this wave states Contracts 0.6.0 → 0.7.0 and names AGWF037. |
| **Scope** | Product `CHANGELOG.md` 2.11.0 lede vs Residue; `src/Strategos.Contracts/CHANGELOG.md` Unreleased. |
| **Consequence** | A reader of the release lede, or of the package CHANGELOG that ships in the nupkg (`Strategos.Contracts.csproj` packs `CHANGELOG.md`), concludes the substrate is 0.6.0 and does not contain AGWF037. Exarchos upgrade-first guidance is missing for 0.7.0. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The three texts. No test asserts the product lede matches `ContractsVersion` or that the package CHANGELOG names AGWF037. |
| **Why not cheaper** | These are documents, not types. A generator could lock them to `ContractsVersion`; none does. |
| **Failure signal** | Nothing. Humans read the wrong version. |
| **Rollback** | Edit the two CHANGELOG files. The csproj pin stays 0.7.0 regardless. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — all three strings are in tree. |

**Open questions:**

- None on the contradiction. Whether the 2.11.0 lede is intentionally the *prior* wave’s sentence (Residue is the addendum) is a reading, not a resolution: a consumer who stops at the lede is still told 0.6.0.

## Discriminating detail

`CHANGELOG.md:17`: “`Strategos.Contracts` bumps **0.4.0 → 0.6.0** (additive minors — `AGWF035`, then `AGWF036`).”

`CHANGELOG.md:182`: “`Strategos.Contracts` bumps **0.6.0 → 0.7.0**.”

`src/Strategos.Contracts/CHANGELOG.md` Unreleased Added (`:18-26`) names AGWF036 and the 0.5.0 → 0.6.0 move. It does not name AGWF037 or 0.7.0. That file is `Pack="true"` (`Strategos.Contracts.csproj:53`).

Plan T2: “Regen the catalog (do not hand-edit `Generated/` or `docs/diagnostics/agwf.md`).” Catalog regen is a different artifact. The package CHANGELOG was not updated for the bump the plan required.

## Disposition

Inventory 50 as a *consistent published record*: **not supported.** The Residue sentence is true of the csproj; the lede and the packaged CHANGELOG contradict it.
