# Dependency Refresh — Implementation Plan

**Feature:** `refactor-dep-refresh`
**Type:** refactor (overhaul)
**Date:** 2026-04-18
**Brief source:** workflow state `refactor-dep-refresh.brief`

## Overview

Bump all centrally-managed dependencies in `src/Directory.Packages.props` to latest stable (2026-04-18). Behavior-preserving. One commit per bump, sequential validation.

Characterization fence (regression proof):

- `dotnet build /p:TreatWarningsAsErrors=true` must remain green.
- Full TUnit suite must pass via `-- --treenode-filter "/*/*/*/Name"`.
- Source generator Verify snapshots must remain unchanged (both `Strategos.Generators.Tests` and `Strategos.Ontology.Generators.Tests`).

All tasks are **sequential** — each bump depends on the prior being validated. Parallelization is intentionally off: a later bump failing after a mid-stream regression would need git archaeology to isolate.

## Version targets

| Package | From | To |
|---|---|---|
| Microsoft.Extensions.Caching.Memory | 10.0.0 | 10.0.6 |
| Microsoft.Extensions.DependencyInjection(.Abstractions) | 10.0.0 | 10.0.6 |
| Microsoft.Extensions.Http | 10.0.0 | 10.0.6 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.0 | 10.0.6 |
| Microsoft.Extensions.Options | 10.0.0 | 10.0.6 |
| Microsoft.Extensions.TimeProvider.Testing | 10.0.0 | 10.0.6 |
| MemoryPack | 1.21.3 | 1.21.4 |
| Pgvector | 0.3.0 | 0.3.2 |
| BitFaster.Caching | 2.5.2 | 2.5.4 |
| CommunityToolkit.HighPerformance | 8.4.0 | 8.4.2 |
| Microsoft.Extensions.AI(.Abstractions) | 10.0.1 | 10.5.0 |
| Npgsql | 9.0.3 | 10.0.2 |
| TUnit | 1.2.11 | 1.37.0 |
| Microsoft.CodeAnalysis.CSharp | 4.14.0 | 5.3.0 |
| Microsoft.CodeAnalysis.Analyzers | 4.14.0 | 5.3.0 |

Held at current: NSubstitute 5.3.0 (6.0 RC skipped), MinVer 6.0.0, Lvlup.Build 1.4.0, Verify.SourceGenerators 2.5.0, BenchmarkDotNet 0.15.8.

## Tasks

### Task 1: Baseline characterization

**Phase:** RED (capture baseline only)

1. [RED] Establish current-state evidence:
   - Run `dotnet build src/strategos.sln /p:TreatWarningsAsErrors=true` — record result.
   - Run `dotnet test src/strategos.sln -- --treenode-filter "/*/*/*/Name"` — record pass count.
   - Confirm Verify snapshots exist and are current in `Strategos.Generators.Tests` and `Strategos.Ontology.Generators.Tests`. Run those two test projects specifically — any mismatched snapshots must be resolved before proceeding.
2. [GREEN] n/a (no code change).
3. [REFACTOR] n/a.

**File touched:** none (reports only).
**Dependencies:** None.
**Parallelizable:** No (blocks everything).
**Exit criterion:** All three gates green; pass counts recorded in the task output for later regression comparison.

---

### Task 2: Patch bumps (bundle)

**Phase:** RED → GREEN → REFACTOR

1. [RED] No new tests — characterization from Task 1 is the fence.
2. [GREEN] Edit `src/Directory.Packages.props`:
   - `Microsoft.Extensions.*` (6 entries): 10.0.0 → 10.0.6
   - `MemoryPack`: 1.21.3 → 1.21.4
   - `Pgvector`: 0.3.0 → 0.3.2
   - `BitFaster.Caching`: 2.5.2 → 2.5.4
   - `CommunityToolkit.HighPerformance`: 8.4.0 → 8.4.2
3. [REFACTOR] Run gates (build + full test suite). Commit as `build(deps): patch bumps — Microsoft.Extensions.*, MemoryPack, Pgvector, BitFaster, CommunityToolkit`.

**File touched:** `src/Directory.Packages.props`.
**Dependencies:** Task 1.
**Parallelizable:** No.
**Exit criterion:** Build + full test suite green; no Verify snapshot drift.

---

### Task 3: Microsoft.Extensions.AI 10.0.1 → 10.5.0

**Phase:** RED → GREEN → REFACTOR

1. [RED] No new tests.
2. [GREEN] Edit `src/Directory.Packages.props`:
   - `Microsoft.Extensions.AI`: 10.0.1 → 10.5.0
   - `Microsoft.Extensions.AI.Abstractions`: 10.0.1 → 10.5.0 (must stay version-locked)
3. [REFACTOR] Gates. Commit as `build(deps): Microsoft.Extensions.AI 10.0.1 → 10.5.0`.

**File touched:** `src/Directory.Packages.props`.
**Dependencies:** Task 2.
**Parallelizable:** No.
**Exit criterion:** Build + test suite green; `Strategos.Agents.Tests` passes without code changes (consumer references `IChatClient`-shaped types only).

---

### Task 4: Npgsql 9.0.3 → 10.0.2

**Phase:** RED → GREEN → REFACTOR

1. [RED] Inspect `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs` and `PgVectorServiceCollectionExtensions.cs`. Confirm no use of:
   - `date` / `time` columns (would trigger DateOnly/TimeOnly default mapping change).
   - `cidr` type (NpgsqlCidr removed in favour of `IPNetwork`).
   - `BeginTextImportAsync` / `BeginTextExportAsync` COPY APIs.
   - `NpgsqlParameter` where both `DataTypeName` and `NpgsqlDbType` are set together.
   Run `Strategos.Ontology.Npgsql.Tests` with current version to capture baseline.
2. [GREEN] Edit `src/Directory.Packages.props`: `Npgsql` 9.0.3 → 10.0.2. Resolve any compile errors surfaced.
3. [REFACTOR] Gates. Commit as `build(deps): Npgsql 9.0.3 → 10.0.2`.

**File touched:** `src/Directory.Packages.props` + (contingent) `src/Strategos.Ontology.Npgsql/*.cs`.
**Dependencies:** Task 3.
**Parallelizable:** No.
**Exit criterion:** Build + all tests green; `Strategos.Ontology.Npgsql.Tests` passes; no ontology integration behavior change.

---

### Task 5: TUnit 1.2.11 → 1.37.0

**Phase:** RED → GREEN → REFACTOR

1. [RED] Confirm current invocation syntax works (`-- --treenode-filter "/*/*/*/Name"`) — already the repo convention per `memory/feedback_tunit_test_invocation.md`.
2. [GREEN] Edit `src/Directory.Packages.props`: `TUnit` 1.2.11 → 1.37.0. If any analyzer diagnostics surface on `[Test]` / `Assert.That` / `[Arguments]`, apply the mechanical fixes (rename, hook-lifecycle adjustments); do NOT refactor test structure beyond what the analyzer requires.
3. [REFACTOR] Gates. Commit as `build(deps): TUnit 1.2.11 → 1.37.0`.

**File touched:** `src/Directory.Packages.props` + (contingent) test files surfaced by analyzers.
**Dependencies:** Task 4.
**Parallelizable:** No.
**Exit criterion:** All 12 test projects pass with the recorded invocation syntax. Pass count matches or exceeds Task 1 baseline.

---

### Task 6: Microsoft.CodeAnalysis.CSharp 4.14.0 → 5.3.0

**Phase:** RED → GREEN → REFACTOR

1. [RED] Verify baseline: run `Strategos.Generators.Tests` and `Strategos.Ontology.Generators.Tests` — confirm all Verify snapshots current.
2. [GREEN] Edit `src/Directory.Packages.props`:
   - `Microsoft.CodeAnalysis.CSharp`: 4.14.0 → 5.3.0
   - `Microsoft.CodeAnalysis.Analyzers`: 4.14.0 → 5.3.0
   Resolve any compile warnings in `Strategos.Generators` / `Strategos.Ontology.Generators` (netstandard2.0 targets, `IsRoslynComponent=true`).
3. [REFACTOR] Run both generator test projects. Verify snapshots MUST match exactly (behavioral invariance). Commit as `build(deps): Microsoft.CodeAnalysis.CSharp 4.14.0 → 5.3.0 (Roslyn 5)`.

**File touched:** `src/Directory.Packages.props` + (contingent) `src/Strategos.Generators/**/*.cs`, `src/Strategos.Ontology.Generators/**/*.cs`.
**Dependencies:** Task 5.
**Parallelizable:** No.
**Exit criterion:** Both generator test projects pass with zero snapshot drift. Full-solution build + test green.

---

### Task 7: CHANGELOG + AGENTS.md update

**Phase:** GREEN (docs only)

1. [GREEN]
   - Append to `CHANGELOG.md`: a `## Unreleased` subsection (or next version header if one exists) with a `Changed` entry summarizing the dep bumps.
   - If Task 6 introduced a consumer SDK floor (e.g., Roslyn 5 requires .NET 10 SDK in consumer builds), add a note to `AGENTS.md` under the build requirements section. If no floor change, document this decision in the task output and skip the AGENTS.md edit.
2. [REFACTOR] Commit as `docs: record dependency refresh in CHANGELOG`.

**File touched:** `CHANGELOG.md`, optionally `AGENTS.md`.
**Dependencies:** Task 6.
**Parallelizable:** No.
**Exit criterion:** Docs reflect the bumps; no further gates required.

## Parallelization summary

All tasks sequential. Total: 7 tasks. Each bump is a single commit behind a full validation pass, so the history tells a clear bisect story if anything regresses downstream.

## Risk matrix

| Task | Risk | Mitigation |
|---|---|---|
| 1 | Baseline reveals pre-existing failure | Abort workflow; fix baseline first |
| 2 | Patch bump breaks downstream | Extremely unlikely; one commit per patch lineage |
| 3 | MEAI 10.5 changes `IChatClient` shape | Read IL diff; shim if needed; escape hatch = revert to 10.0.6 held at that version floor |
| 4 | Npgsql 10 type-mapping default flips behavior | RED step scans source for triggers; fallback = `LegacyPostgresTypeResolverFactory` |
| 5 | TUnit analyzers fail ~35 minor jump | Apply mechanical fixes only; if API break detected, stage via 1.10/1.20/1.30 intermediate bumps |
| 6 | Roslyn 5 changes generator output | Verify snapshots catch drift; resolve case-by-case |
| 7 | Missed CHANGELOG entry | Trivial to amend |

## Out of scope (per brief)

- ModelContextProtocol SDK adoption → separate `/exarchos:ideate`.
- MEAI 10.5 feature adoption in `Strategos.Agents` → future refactor.
- NSubstitute 6.0 RC → wait for stable.
- Npgsql 10 `DateOnly` migration in ontology types → N/A (no date/time columns).
