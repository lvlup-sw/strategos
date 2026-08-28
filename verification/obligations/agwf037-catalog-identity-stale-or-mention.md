### agwf037-catalog-identity-stale-or-mention — Catalog tests this wave extended must not pass on a stale file or a mention

| | |
|---|---|
| **Claim** | The catalog tests this wave extended to AGWF037 must fail if `AGWF037` is missing from a freshly compiled TypeSpec catalog, or if `docs/diagnostics/agwf.md` contains the id only as a mention and not as a table data row. Appending `"AGWF037"` to a hand-authored `GroundTruthCodes` list and reading a committed file must not be sufficient. |
| **Scope** | S2. This wave's one-line list appends in `AgwfCatalogEmitterTests.cs`, `AgwfCatalogSchemaTests.cs`, `AgwfCodeEnumTests.cs`, `AgwfMarkdownTests.cs`. Subject files: committed `Generated/agwf-catalog.json`, `docs/diagnostics/agwf.md`. |
| **Consequence** | Reviewers treat "catalog tests updated for AGWF037" as the 0.7.0 identity lock. A committed catalog that still lists 31 codes passes the emitter test even if `.tsp` has moved on, unless a different job regenerates. A footnote that contains `AGWF037` counts as a markdown "data row." The diagnostic can be unwired in the generator and these four tests stay green. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Emitter: regenerate then compare (the shape `AgwfCatalog_HandEdit_FailsGuard` already has; the emitter test this wave edited does not invoke it). Markdown: parse the table and require a row whose id cell equals each ground-truth code. Schema compile (`TspToolchain.CompileAsync`) and enum reflection are identity checks that *can* fail on a missing generated member; they are not enough for the markdown/emitter holes. |
| **Why not cheaper** | The lists are hand-authored; they are not generated from `AgwfCatalog.tsp`. The compiler does not know what `agwf.md` contains. |
| **Failure signal** | Nothing. These tests are the signal, and they report pass. |
| **Rollback** | Revert the `GroundTruthCodes` / `Expected` appends. The committed catalog and markdown revert with the contracts commit. |
| **Lenses** | False-Green Shapes |

**Open questions:**

- Does `AgwfCatalog_HandEdit_FailsGuard` (`AgwfCodegenGuardTests.cs:51-86`) plus `contracts-codegen-guard.yml` already fail when the committed catalog diverges from codegen? If both run on this change, the emitter test's stale read is redundant, not a current miss. If `contracts-test` or the codegen-guard job is not required, the emitter test is the identity gate and it does not regenerate.
- Does the generated `agwf.md` ever mention a code outside the table? If the emitter only writes table rows, a mention-only pass requires a hand-edit that codegen-guard would reject *when that job runs*.

## What led here

This wave's catalog-test diff is "exactly 30 → 31" and `+        "AGWF037"` (or the enum pair). Competing explanation: identity is what these tests are for, and behavior is `DuplicatePermittedForkTriggerTests`. Discriminating detail for *this* lens: the emitter test comment says "After the full codegen pipeline runs" (`AgwfCatalogEmitterTests.cs:12-13`) and then reads the committed file with no `TspToolchain` / codegen invoke (`42-48`). The markdown test builds `dataRows` as lines where `GroundTruthCodes.Any(c => l.Contains(c))` (`AgwfMarkdownTests.cs:58-60`). A prose line that contains `AGWF037` is a row.

Schema test *does* compile TypeSpec (`AgwfCatalogSchemaTests.cs:45`) then reads `AgwfEntry*.json` on disk. Enum test reflects `AgwfCode` in the compiled assembly. Those two can fail if the generated member is missing. They still cannot fail if the generator never reports AGWF037. Behavior is a different obligation (promise / integration). This file is the stale-file and substring holes in the tests this wave actually edited.

`AgwfCatalogParityTests` was not in the diff. It walks the committed catalog and compares descriptor strings. It is identity of metadata, not a mention-hole.

## Code read (revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`)

- `AgwfCatalogEmitterTests.cs:23-32, 40-74` — `GroundTruthCodes` includes `AGWF037`; reads `Generated/agwf-catalog.json`; positional id join.
- `AgwfMarkdownTests.cs:18-27, 34-70` — same list; `Contains` per line; method still named `AgwfMarkdown_TenRows_MatchesCatalog`.
- `AgwfCatalogSchemaTests.cs:26-35, 43-73` — compile then file-set identity.
- `AgwfCodeEnumTests.cs:54, 62-94` — `DuplicatePermittedForkTrigger` → `"AGWF037"`; enum member count 31; JSON round-trip.
- `AgwfCodegenGuardTests.cs:51-86` — *does* regenerate and require `regenerated == committed`. Not edited in this diff.
- `contracts-codegen-guard.yml:48-60` — regen then `git diff --exit-code` on `Generated/`, `schemas/`, `docs/diagnostics`. Path-filtered; not edited in this diff.

## Kill probe

- Change `AgwfCatalog.tsp` so AGWF037's id is gone; leave committed `agwf-catalog.json` and the `GroundTruthCodes` lists. Emitter test: pass. Markdown test: pass if `agwf.md` still mentions the id. Schema test: fail if compile drops the entry file. Enum test: fail if the committed `.g.cs` is also stale *and* the test process loads that assembly; if someone updates the enum but not the catalog, emitter still passes.
- Keep the table, add a footnote line containing `AGWF037`. Markdown test: `dataRows.Count` becomes 32 and fails (extra mention). The hole is the other direction: remove the table row, leave a footnote. Count stays 1. Pass.
- Delete the AGWF037 `ReportDiagnostic` site in the extractor. All four catalog tests stay green.

## Failure scenario

A catalog string edit lands in `.tsp` and is regenerated in CI on a machine that does not run `contracts-codegen-guard` (path filter miss, or job not required). The emitter test reads the old committed JSON, still sees `AGWF037` in the list, passes. Markdown still has a leftover mention. 0.7.0 identity is "green."

## Open questions (full stakes)

### Do the regenerate-and-diff jobs run on this wave?

`contracts-codegen-guard.yml` paths include `src/Strategos.Contracts/**`, which this wave touches. If that job is required, committed-catalog staleness fails there and the emitter test's skip-of-codegen is a weaker duplicate. If the job is not required, or a future AGWF038 is added only to `GroundTruthCodes` and the committed files, the emitter test is the gate and it is a stale-file pass. Survey already left `contracts-test` required-check as open. Same stake here.

### Does generated markdown mention codes outside the table?

If the emitter writes only a table, mention-only is a hand-edit. Codegen-guard diffs `docs/diagnostics`. Combined with a required codegen-guard, the markdown substring hole is latent. Combined with a missing guard, it is the only check on `agwf.md` and it accepts a mention.
