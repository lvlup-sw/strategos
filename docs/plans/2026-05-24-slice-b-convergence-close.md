# Implementation Plan — Slice (B) Convergence Close

- **Design:** `docs/designs/2026-05-24-slice-b-convergence-close.md`
- **Date:** 2026-05-24
- **Closes:** #52 (AGWF catalog), #51 (API-stability gate) → #54 slice-B gate
- **Iron law:** no production code without a failing test first.

## Test-invocation note (for implementers)

TUnit in this repo: **`dotnet test` with `--filter` is unsupported** — use
`dotnet run --project <Test.csproj> -- --treenode-filter "/*/*/*/TestName"`.
`Strategos.Contracts.Tests` runs `npx tsp compile` (needs Node 22+); its CI job must be
Node-provisioned and skip-patterned out of the shared build-test reusable.

## DR-1 resolution (lead task — blocks all of Group 1)

The design left DR-1 (how `.tsp` carries catalog *data*, not just shape) as a spike. Resolve it
**before** writing Group-1 tests, because the chosen representation determines every test path.

Two viable representations, both keeping `.tsp` the single source:

- **(R-lit) literal-typed models** — each entry is a model whose fields are string literals
  (`id: "AGWF001"; severity: "error"; since: "0.2.0";`). TypeSpec emits these as JSON Schema
  `const`. The **existing C# `RecordEmitter` already reads JSON Schema** → one C#-only emitter
  pass reads the consts. No JS toolchain change. **Recommended.**
- **(R-dec) enum + metadata decorators** — requires reading the compiled program via
  `@typespec/compiler` (JS emitter). Heavier; only if R-lit can't carry the data faithfully.

---

### Task 1: SPIKE — confirm R-lit emits readable `const` metadata
**Phase:** SPIKE (decision, time-boxed)

1. Author a throwaway `AgwfProbe.tsp` with one literal-typed entry; run `scripts/contracts-codegen.sh`;
   inspect the emitted JSON Schema for `const` (or single-value `enum`) on each field.
2. **Decision gate:** if `const` values are present and machine-readable → adopt **R-lit**, record
   in the plan, proceed. If not → fall back to **R-dec** and re-plan T3/T5 against the JS emitter.
3. Delete the probe.

**Dependencies:** None · **Parallelizable:** No (gates Group 1)

---

## Group 1 — #52 AGWF single-source catalog (sequential; depends on T1)

### Task 2: `AgwfCatalog.tsp` — 10 codes, full metadata
**Phase:** RED → GREEN

1. [RED] `AgwfCatalogSchema_TenCodes_EmittedWithMetadata`
   - File: `src/Strategos.Contracts.Tests/Diagnostics/AgwfCatalogSchemaTests.cs`
   - Asserts: after codegen, the emitted JSON Schema enumerates exactly the 10 ground-truth IDs
     (`AGWF001,002,003,004,009,010,012,014,015,016`) each carrying `severity`/`summary`/
     `remediation`/`since`. Fails: no `AgwfCatalog.tsp` yet.
2. [GREEN] Author `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` (representation per T1),
   seeded from `WorkflowDiagnostics.cs` (id, title→summary, severity, message→remediation,
   `since: "0.2.0"`). Import it in `main.tsp`.

**Dependencies:** T1 · **Parallelizable:** No

### Task 3: Emit `agwf-catalog.json` (canonical data artifact)
**Phase:** RED → GREEN

1. [RED] `AgwfCatalogEmitter_TenEntries_EmitsManifestAndEntries`
   - File: `src/Strategos.Contracts.Tests/Diagnostics/AgwfCatalogEmitterTests.cs`
   - Asserts: emitter produces `agwf-catalog.json` with manifest (`catalog_version`) + 10 entries,
     ordered by ID, each with full metadata.
2. [GREEN] Add `AgwfCatalogEmitter` to `src/Strategos.Contracts.Codegen/` (reads the schema consts
   per R-lit); wire it into `RecordEmitter.RunAsync`/`Program.cs` output set.

**Dependencies:** T2 · **Parallelizable:** No

### Task 4: `AgwfCode` C# enum generation
**Phase:** RED → GREEN

1. [RED] `AgwfCodeEnum_TenMembers_RoundTripsWireValues`
   - File: `src/Strategos.Contracts.Tests/Diagnostics/AgwfCodeEnumTests.cs`
   - Asserts: generated `LevelUp.Strategos.Contracts.Diagnostics.AgwfCode` has 10 members; each
     serializes to its `AGWF0xx` wire string (existing `[JsonStringEnumMemberName]` path).
2. [GREEN] Ensure the enum emits (likely free via existing string-enum path; add member-name
   mapping if needed). Place under `Generated/`.

**Dependencies:** T2 · **Parallelizable:** Yes (with T3)

### Task 5: `docs/diagnostics/agwf.md` reference page
**Phase:** RED → GREEN

1. [RED] `AgwfMarkdown_TenRows_MatchesCatalog`
   - File: `src/Strategos.Contracts.Tests/Diagnostics/AgwfMarkdownTests.cs`
   - Asserts: generated `docs/diagnostics/agwf.md` table has 10 rows, columns
     id/severity/summary/remediation/since, sorted by ID.
2. [GREEN] Extend the emitter to render the Markdown table.

**Dependencies:** T3 · **Parallelizable:** No

### Task 6: Rewire `WorkflowDiagnostics.cs` to source IDs from `AgwfCode`
**Phase:** RED → GREEN → REFACTOR

1. [RED] `WorkflowDiagnostics_NoHandAuthoredAgwfLiterals_GrepGate`
   - File: `src/Strategos.Generators.Tests/Diagnostics/AgwfSingleSourceTests.cs`
   - Asserts: zero `AGWF0\d{2}` literals in `src/Strategos*/**/*.cs` excluding `Generated/`
     (mirrors the issue's grep AC). Fails: literals still hand-authored.
2. [GREEN] Replace each `id: "AGWF0xx"` with the generated `AgwfCode.<Member>` projection (Strategos.Generators
   takes a content/source dependency on the generated enum — netstandard2.0-compatible).
3. [REFACTOR] Keep severities/messages identical (INV-5 byte-for-byte); confirm existing
   `DiagnosticTests`/`EventSourcedEmitterIntegrationTests` stay green.

**Dependencies:** T4 · **Parallelizable:** No

### Task 7: Codegen-guard + package content
**Phase:** RED → GREEN

1. [RED] CI-level: extend `contracts-codegen-guard.yml` `paths` + diff set to include
   `src/Strategos.Contracts/Diagnostics/**`, the emitted `agwf-catalog.json`, and
   `docs/diagnostics/agwf.md`; add a test that a hand-edit to `agwf-catalog.json` dirties the tree.
2. [GREEN] Add `agwf-catalog.json` as `<Content>` to `Strategos.Contracts.csproj` (NuGet content,
   alongside existing JSON Schema content).

**Dependencies:** T5 · **Parallelizable:** No

### Task 8: Cross-product round-trip-by-name (exarchos T6)
**Phase:** RED → GREEN

1. [RED] `AgwfCatalog_RoundTripsByName_AgainstGeneratedEnum`
   - File: `src/Strategos.Contracts.Tests/Diagnostics/AgwfRoundTripTests.cs`
   - Asserts: every `agwf-catalog.json` entry's `id` maps to an `AgwfCode` member **by name**
     (not ordinal — INV-5 condition), and back. This is the contract exarchos's TS enum mirrors.
2. [GREEN] Adjust emitter naming so member name ⇄ catalog id is total and stable.

**Dependencies:** T6 · **Parallelizable:** No

---

## Group 2 — #51 builder API-stability gate (depends on Group 1 only at PR-merge; code-independent)

### Task 9: Wire `PublicApiAnalyzers` scoped to the 7 interfaces
**Phase:** RED → GREEN

1. [RED] `PublicApi_SevenBuilderInterfaces_Baselined`
   - File: `src/Strategos.Tests/PublicApi/BuilderApiBaselineTests.cs` (or analyzer-run assertion)
   - Asserts: building `src/Strategos` with an intentionally-removed baseline line raises
     `RS0017`/`RS0016`. Fails: analyzer not referenced.
2. [GREEN] Add `Microsoft.DotNet.PublicApiAnalyzers` to `Directory.Packages.props`; reference it in
   `src/Strategos/Strategos.csproj` with `<AdditionalFiles Include="PublicAPI.*.txt" />` **scoped so
   only the 7 builder interfaces are tracked** (INV-1 condition: no SG-internal types leak).

**Dependencies:** None · **Parallelizable:** Yes (independent of Group 1)

### Task 10: Populate `PublicAPI.Shipped.txt` baseline
**Phase:** RED → GREEN

1. [RED] Baseline absent → analyzer flags all 7 interfaces as undeclared (`RS0016`).
2. [GREEN] Generate `PublicAPI.Shipped.txt` for the 7 interfaces; `PublicAPI.Unshipped.txt` empty.
   Build is clean.

**Dependencies:** T9 · **Parallelizable:** No

### Task 11: CI fail-closed with named remediation
**Phase:** RED → GREEN

1. [RED] `ApiDrift_UnbaselinedChange_FailsWithNamedMessage`
   - File: CI smoke (`.github/workflows/ci.yml` step) — add a builder member, assert CI fails and
     the log contains "Update PublicAPI.Unshipped.txt and add a CHANGELOG entry under
     Cross-product breaking changes."
2. [GREEN] Add the CI step echoing the protocol on analyzer failure.

**Dependencies:** T10 · **Parallelizable:** No

### Task 12: CHANGELOG protocol + doc-comment
**Phase:** GREEN (docs; no failing-test gate — verified by T13 presence-check)

1. [GREEN] Add `## Cross-product breaking changes` section to `CHANGELOG.md` (empty-allowed);
   document the protocol in `CONTRIBUTING.md`; reference it in the `IWorkflowBuilder<TState>.cs`
   doc-comment.

**Dependencies:** None · **Parallelizable:** Yes

### Task 13: `public-api-drift.yml` — diff + fail-soft secret gate
**Phase:** RED → GREEN

1. [RED] `DriftWorkflow_ShippedDiverges_ProducesIssuePayload`
   - File: `src/Strategos.Tests/PublicApi/DriftPayloadTests.cs` (test the payload-builder script
     in isolation): given a baseline diff, builds a `gh issue create` invocation with
     `--repo lvlup-sw/exarchos --label cross-product:strategos` and a diff link.
2. [GREEN] Add `.github/workflows/public-api-drift.yml` (on push to `main`): diff
   `PublicAPI.Shipped.txt` vs previous tag; **if `EXARCHOS_ISSUES_PAT` present** → `gh issue create`;
   **else** → warn-and-skip (never fails `main`).

**Dependencies:** T10 · **Parallelizable:** No

### Task 14: Mocked-`gh` local dry-run job
**Phase:** RED → GREEN

1. [RED] `DriftDryRun_MockedGh_AssertsRepoLabelBody`
   - File: `src/Strategos.Tests/PublicApi/DriftDryRunTests.cs`
   - Asserts: with a synthetic divergence and a `gh` shim on PATH, the payload matches
     `--repo`/`--label`/body without a live token.
2. [GREEN] Add the dry-run job to the workflow using a mocked `gh`. The **live** cross-repo dry-run
   stays an out-of-band manual step recorded on #51 before close.

**Dependencies:** T13 · **Parallelizable:** No

---

## Parallelization summary

- **T1** gates everything in Group 1.
- **Group 1 (T2→T8)** is a mostly-sequential chain; T4 ∥ T3.
- **Group 2 (T9→T14)** is code-independent of Group 1 → a separate worktree/PR. T12 ∥ T9. T9→T10→{T11,T13}→T14.
- Suggested PRs: **PR-A = #52 (T1–T8)**, **PR-B = #51 (T9–T14)** on the same feature branch; #54 closes when both merge.

## Invariant conditions carried from design

1. Round-trip test asserts on enum **member names**, not ordinals (INV-5). → T8.
2. `<AdditionalFiles>` scopes the PublicAPI baseline to the 7 interfaces only (INV-1). → T9.
3. 10 existing IDs/severities/messages preserved byte-for-byte; gaps stay gaps (INV-5). → T2/T6.
