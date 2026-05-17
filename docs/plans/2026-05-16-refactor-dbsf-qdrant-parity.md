# Implementation Plan: refactor-dbsf-qdrant-parity

**Workflow type:** refactor (overhaul track)
**Brief:** workflow state (`exarchos_workflow get featureId: refactor-dbsf-qdrant-parity`)
**Source issue:** #79
**Target release:** Ontology 2.6.0 (reconciliation lands before v2.6.0 tag; current main HEAD c4e69d9 is unreleased)
**Date:** 2026-05-16

---

## Problem (one-paragraph recap)

PR #77 shipped `RankFusion.DistributionBased` with a stated "Qdrant DBSF parity ≤ 1e-9" gate. Release-gate verification on 2026-05-16 proved the claim is false: `scripts/regenerate-dbsf-oracle.py` reimplements the algorithm in-process using population stdev (`/N`) + output clamping + 0.5-conventions; real `qdrant-client==1.12.1` uses sample stdev (`/(N-1)`), no clamping, and raises `ZeroDivisionError` on single-element / zero-variance lists. C# matches the script → tests pass → parity claim is wrong by 4e-2 to 7e-2 on 4 of 6 fixture queries (and 2 of 6 crash real qdrant). This plan reconciles to **real-qdrant parity for non-degenerate inputs + documented robustness extensions for degenerate inputs**, plus a mechanical CI guard to prevent recurrence.

## Iron Law

**NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST.** For this refactor, the "failing test" at each step takes one of three forms:

1. **Python parity check** — the new `--check` mode of `regenerate-dbsf-oracle.py`, run against the committed oracle, must fail before we regenerate.
2. **C# oracle test** — `DistributionBased_AgainstQdrantOracle_AllQueriesMatch_Within1eMinus9` must fail against the regenerated oracle before C# behavior is fixed.
3. **CI parity guard** — a synthetic drift (oracle byte-twiddle) must cause the new CI job to fail before we commit.

## Design Constraints

| ID | Constraint | Source |
|---|---|---|
| C1 | 2.5.0 OntologyQueryTool dense-only path (hybridOptions=null) must be byte-identical pre/post refactor | DIM-3 hard backward-compat gate |
| C2 | DBSF score values WILL change for hybrid callers — acceptable because main HEAD c4e69d9 is unreleased | Brief.constraints |
| C3 | Tie-break by `DocumentId` ordinal preserved — do NOT switch to qdrant's insertion-order tie-break | Brief.constraints |
| C4 | 1e-9 tolerance is the gate for non-degenerate inputs | G1 |
| C5 | Mean/variance computation single-pass and order-independent within a list (cross-platform float stability) | Brief.constraints |
| C6 | Single PR; sequential tasks; no parallel worktrees (small surface, tight coupling) | Workflow type sizing |

---

## Tasks

### Task 1: Rewrite `regenerate-dbsf-oracle.py` to call real qdrant + add `--check` mode
**Phase:** RED → GREEN
**Branch:** `refactor/dbsf-qdrant-parity` (single branch for this refactor)

1. **[RED]** Replace `_normalize_list` + `dbsf` with a thin wrapper that:
   - `from qdrant_client.hybrid.fusion import distribution_based_score_fusion`
   - `from qdrant_client.http.models import ScoredPoint`
   - For each query: construct `list[list[ScoredPoint]]` (one ScoredPoint per `(doc_id, score)` — set required fields: `id`, `score`, `version=0`, `payload={}`); call the real fn; catch `ZeroDivisionError` per-list and emit the 0.5-convention fallback with inline comment citing the Strategos extension.
   - Add `--check` flag: when present, regenerate in-memory and `diff` against the committed `qdrant-dbsf-oracle.json` byte-for-byte; exit non-zero on mismatch.
   - Remove the incorrect "we mirror np.std ddof=0" docstring; replace with "calls qdrant_client.hybrid.fusion.distribution_based_score_fusion directly; degenerate inputs get the documented Strategos 0.5-extension".

   Run `python3 -m pip install -r scripts/requirements.txt && python3 scripts/regenerate-dbsf-oracle.py --check`.
   **Expected failure:** "oracle mismatch: q1-2list-balanced, q4-outlier-heavy, q5-mixed-pos-neg, q6-large-skew differ from real-qdrant output" (proves the gap).

2. **[GREEN]** Run `python3 scripts/regenerate-dbsf-oracle.py` (no flag) to write the corrected oracle. Re-run `--check` → passes.

**Files:**
- `scripts/regenerate-dbsf-oracle.py` (rewrite)
- `src/Strategos.Ontology.Tests/Retrieval/Fixtures/qdrant-dbsf-oracle.json` (regenerated)

**Dependencies:** None.
**Parallelizable:** No (unblocks all downstream).

---

### Task 2: Update `RankFusion.DistributionBased` to real-qdrant algorithm
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Run `dotnet test src/Strategos.Ontology.Tests -- --treenode-filter "/*/*/RankFusionDistributionBasedTests/DistributionBased_AgainstQdrantOracle_AllQueriesMatch_Within1eMinus9"`.
   **Expected failure:** ~4e-2 to 7e-2 divergence on the 4 non-degenerate queries (C# still uses `/N` + clamping; oracle now reflects qdrant's `/(N-1)` + no-clamp).

2. **[GREEN]** In `src/Strategos.Ontology/Retrieval/RankFusion.DistributionBased.cs`:
   - Change `double sigma = Math.Sqrt(varSum / list.Count);` → `double sigma = Math.Sqrt(varSum / (list.Count - 1));` (Bessel correction; safe because `list.Count >= 2` at this point — the single-element branch returned earlier).
   - Remove the clamping: `double clamped = Math.Min(high, Math.Max(low, c.Score));` → `double normalized = (c.Score - low) / span;` (drop `clamped` local).
   - **Keep** single-element 0.5-convention (line 99-104) — documented Strategos robustness extension.
   - **Keep** zero-variance 0.5-convention (line 124-141) — documented Strategos robustness extension.
   - **Keep** `DocumentId` ordinal tie-break (line 171).
   - **Keep** `ZeroVarianceEpsilon = 1e-9` threshold and topK / weights validation unchanged (no behavior change for those).

   Re-run the parity test → passes within 1e-9.

3. **[REFACTOR]** Update XML doc-comments on the public `DistributionBased` method to reflect new behavior:
   - `<summary>`: "Per-list compute μ, σ (sample stdev, Bessel-corrected); normalize via `(score − (μ−3σ)) / 6σ`; multiply by per-list weight; sum across lists. Matches `qdrant_client.hybrid.fusion.distribution_based_score_fusion` (1.12.1) within 1e-9 on non-degenerate inputs."
   - `<remarks>` list-items: rewrite the σ definition bullet to cite Bessel; **drop** the "outlier handling" / clamping bullet; **add** an "Output range" bullet noting normalized scores are not bounded to [0,1] (no clamping); **mark** the single-element and zero-variance bullets as "Strategos robustness extensions — qdrant raises `ZeroDivisionError` here".

**Files:**
- `src/Strategos.Ontology/Retrieval/RankFusion.DistributionBased.cs` (behavior + XML docs)

**Dependencies:** Task 1.
**Parallelizable:** No.

---

### Task 3: Reconcile C# edge-case tests with new behavior
**Phase:** RED → GREEN

1. **[RED]** Run the full `RankFusionDistributionBasedTests` suite. Expected outcomes:
   - `DistributionBased_AgainstQdrantOracle_AllQueriesMatch_Within1eMinus9` → passes (Task 2 fixed).
   - `DistributionBased_SingleElementList_NormalizesToHalf` → passes (extension preserved).
   - `DistributionBased_ZeroVarianceList_AllElementsNormalizeToHalf` → passes (extension preserved).
   - `DistributionBased_MixedPositiveAndNegativeScores_NormalizesCorrectly` → expected to **still pass** (ordering claim — A > B > C — is independent of clamping; Bessel only scales σ).
   - `DistributionBased_OutlierPath_TracksExpectedOrdering` → **expected to fail**. The test asserts `dbsfGapAB < mmGapAB`, which was true only because DBSF clamped the outlier. Without clamping, DBSF stops blunting the outlier and the gap may equal or exceed min-max's.
   - `DistributionBased_EmptyInput_ReturnsEmptyList`, `WeightsLengthMismatch_ThrowsArgumentException`, `NegativeWeight_ThrowsArgumentException`, `TopKZero_ReturnsEmptyList` → all pass (unchanged behavior).

2. **[GREEN]** For `DistributionBased_OutlierPath_TracksExpectedOrdering`: the test as written codifies the deleted clamping behavior. Rewrite to assert what the new algorithm actually does:
   - **Option A (preferred):** Rename to `DistributionBased_OutlierPath_MatchesOracleFixture` and replace the body with a small numeric assertion driven by the q4 oracle entry (or simply delete the test, since q4-outlier-heavy already covers it via the oracle).
   - **Option B:** Keep the test but flip the assertion to capture the *no-clamping* relationship — e.g., assert that the top doc's score is well-defined and ordering matches the oracle's q4 expected_fused.
   - Either way: any prose comment about "DBSF's stated advantage over min-max" / "DBSF clamps the outlier" must be removed (no longer true).

**Files:**
- `src/Strategos.Ontology.Tests/Retrieval/RankFusionDistributionBasedTests.cs`

**Dependencies:** Task 2.
**Parallelizable:** No.

---

### Task 4: Verify property tests still hold
**Phase:** RED (may pass without changes)

1. **[RED]** Run `dotnet test src/Strategos.Ontology.Tests -- --treenode-filter "/*/*/RankFusionDistributionBasedPropertyTests/*"`.
   - **TranslationInvariance** — algebraically preserved (μ shifts by same constant; σ unchanged; normalized score identical). Expected pass.
   - **ScaleInvariance** — algebraically preserved (σ scales linearly with input; span = 6σ scales linearly; normalized identity holds). Expected pass.
   - **WeightMonotonicity** — preserved (weight is a post-normalization scalar). Expected pass.

2. **[GREEN]** If any seed produces failure (suspect: float ordering near tie-break with `/N-1` rescaling), debug the offending seed and either:
   - Tighten / loosen the tolerance with justification, OR
   - Adjust `RandomLists` seed range to exclude pathological pre-existing seeds (last resort; document why).

**Files:**
- `src/Strategos.Ontology.Tests/Retrieval/RankFusionDistributionBasedPropertyTests.cs` (only if a property test breaks)

**Dependencies:** Task 2.
**Parallelizable:** Yes (with Task 3 — different test files, both read the new C# impl).

---

### Task 5: Refresh HybridMeta wire-shape snapshots
**Phase:** RED → GREEN

1. **[RED]** Run `dotnet test src/Strategos.Ontology.MCP.Tests -- --treenode-filter "/*/*/OntologyQueryToolHybridMetaSnapshotTests/*"`. DBSF score values appearing in `HybridMeta` wire output will differ from the verified snapshots. Tests expected to fail with Verify diffs.

2. **[GREEN]** Audit each `*.received.txt` produced under `src/Strategos.Ontology.MCP.Tests/snapshots/` (or equivalent path). For each:
   - Confirm the diff is **only** in DBSF-derived numeric fields (no shape change, no key reordering, no other behavior leakage).
   - Promote `*.received.txt` → `*.verified.txt`.
   - If any non-DBSF field changed unexpectedly, STOP and investigate (would indicate accidental contract break — violates C1).

   Re-run the MCP test suite → green.

3. **[Negative check / C1 guard]** Run `dotnet test src/Strategos.Ontology.MCP.Tests --filter <2.5.0 OntologyQueryTool dense-only tests>` to confirm no diffs on the dense-only path. Per brief: zero diffs expected.

**Files:**
- `src/Strategos.Ontology.MCP.Tests/**/*.verified.txt` (snapshot refresh)
- (no source changes expected)

**Dependencies:** Task 2.
**Parallelizable:** Yes (with Tasks 3, 4 — different test project).

---

### Task 6: Add CI parity guard job
**Phase:** RED → GREEN

1. **[RED]** Locally simulate drift: introduce a 1-bit twiddle in `qdrant-dbsf-oracle.json` (e.g., change one `fused_score` digit), run `python3 scripts/regenerate-dbsf-oracle.py --check` → exits non-zero with a clear diff message. Revert the twiddle.

2. **[GREEN]** Add to `.github/workflows/ci.yml` a new job `dbsf-parity-guard`:
   - Runs on `pull_request` + `push` to `main`.
   - Triggered only when `scripts/regenerate-dbsf-oracle.py`, `scripts/requirements.txt`, `src/Strategos.Ontology.Tests/Retrieval/Fixtures/qdrant-dbsf-oracle.json`, or `src/Strategos.Ontology/Retrieval/RankFusion.DistributionBased.cs` change (path filter — keeps unrelated PRs fast). If path filters aren't worth the complexity here, run unconditionally.
   - Steps: `actions/checkout@v4`; `actions/setup-python@v5` with Python 3.11; `python3 -m pip install -r scripts/requirements.txt`; `python3 scripts/regenerate-dbsf-oracle.py --check`.
   - Runner: same as `pack-verify` (`self-hosted`) OR `ubuntu-latest` if `self-hosted` doesn't have Python available — pick whichever already runs Python in this repo's CI. Default to `ubuntu-latest` unless self-hosted is required.

3. **[Negative-path validation in CI]** After the PR is open, push a temporary commit that introduces drift to confirm the job actually fails in CI; revert before merge. (Captured as a PR-time checklist item, not a code task.)

**Files:**
- `.github/workflows/ci.yml` (new job)

**Dependencies:** Task 1 (`--check` must exist).
**Parallelizable:** Yes (with Tasks 2–5 — workflow file is independent of C#/test files).

---

### Task 7: Reconcile design doc, CHANGELOG, fixtures README
**Phase:** GREEN (pure docs; no test to fail first because there's no executable assertion — but the work is mechanically scope-gated by `grep`)

1. **[GREEN]** Update `src/Strategos.Ontology.Tests/Retrieval/Fixtures/README.md`:
   - Replace the "Manual run; not in CI" bullet with: "CI parity-guarded — see `.github/workflows/ci.yml#dbsf-parity-guard`. Regeneration is still manual (re-run after pin bump or fixture change); CI will reject any PR whose committed oracle diverges from `--check`."
   - Replace the "absorbs the minor float-ordering differences" sentence — the new oracle is qdrant's *literal output*; the 1e-9 tolerance now absorbs only C#↔Python floating-point reordering on identical algorithm.

2. **[GREEN]** Update `docs/designs/2026-05-15-ontology-2-6-0-hybrid-retrieval.md`:
   - Search for "parity", "DBSF", "Qdrant" in §6 and the success-criteria sections. Replace any unqualified "parity" claim with "parity ≤ 1e-9 on non-degenerate inputs; documented Strategos robustness extension (0.5-convention) for single-element and zero-variance lists where qdrant raises `ZeroDivisionError`".
   - Add a brief "2026-05-16 reconciliation" note pointing at issue #79 and this plan.

3. **[GREEN]** Update `CHANGELOG.md`:
   - In the Unreleased / 2.6.0 section, rewrite the existing DBSF bullet (or add a clarifying follow-up bullet) to the same "parity on non-degenerate + documented extension on degenerate" framing. Reference issue #79.

4. **[GREEN — verification]** Run `rg -i 'parity' src/ docs/ scripts/ CHANGELOG.md .github/` and inspect each hit. Every remaining mention must be either honest, qualified, or in a comment block that's clearly historical.

**Files:**
- `src/Strategos.Ontology.Tests/Retrieval/Fixtures/README.md`
- `docs/designs/2026-05-15-ontology-2-6-0-hybrid-retrieval.md`
- `CHANGELOG.md`

**Dependencies:** Tasks 1, 2 (so prose accurately describes shipped behavior).
**Parallelizable:** Yes (with Tasks 3, 4, 5, 6 — pure docs).

---

## Execution Order

```text
Task 1 (regen script + --check)  ─┐
        │                          │
        ▼                          │
Task 2 (C# behavior)               │── Task 6 (CI guard, parallel after T1)
        │                          │
        ├──────────┬───────────┐   │
        ▼          ▼           ▼   │
   Task 3       Task 4      Task 5 │── Task 7 (docs, parallel after T2)
   (edge        (property   (snapshot
    tests)       tests)      refresh)
        │          │           │
        └──────────┴───────────┴───────────► all green → PR
```

Single branch, single PR. Tasks 3–7 can be done in any interleaved order after Task 2; Task 6 only needs Task 1.

## Success Gates (mapped from brief)

- **SC1** — Real-qdrant validator (or `--check` mode): all 6 queries pass after Task 1.
- **SC2** — `rg "from qdrant_client.hybrid.fusion import distribution_based_score_fusion" scripts/regenerate-dbsf-oracle.py` returns 1 match after Task 1.
- **SC3** — `.github/workflows/ci.yml` has a `dbsf-parity-guard` job; it's green on the PR after Task 6.
- **SC4** — `dotnet test src/Strategos.Ontology.Tests` passes after Tasks 2–4.
- **SC5** — `dotnet test src/Strategos.Ontology.MCP.Tests` passes after Task 5.
- **SC6** — 2.5.0 OntologyQueryTool tests have zero diffs (no source touched in that path; verified during Task 5 negative-check).
- **SC7** — `rg -i parity` returns only honest hits after Task 7.
- **SC8** — Issue #79 closed on PR merge (synthesize phase).
- **SC9** — Milestone "Ontology 2.6.0 — Hybrid Retrieval Seams" ready for v2.6.0 tag.

## Out of Scope

- Adding new fixture queries (current 6 are sufficient for parity demonstration).
- Performance tuning of the new C# `/(n-1)` path (algorithmic complexity unchanged).
- Changing tie-break behavior to match qdrant's insertion-order (explicitly excluded — C3).
- Refactoring `ScoredCandidate` / `FusedResult` records (no shape change).

## Risks & Mitigations

| Risk | Mitigation |
|---|---|
| qdrant 1.12.1's `distribution_based_score_fusion` may not exist at the path documented in the brief, or its signature may differ | Task 1 [RED] step will surface this immediately. If so: fall back to importing whatever the qdrant-client 1.12.1 module actually exposes; document the import path; re-pin requirements.txt if needed. |
| Property tests may break in non-obvious ways with Bessel correction (e.g., a seed that previously had tied scores now has distinct scores → ordering change) | Task 4 includes a debug-and-document step. Tolerance is 1e-9 — generous; failures would indicate real bugs, not numerical noise. |
| Snapshot refresh (Task 5) may surface non-DBSF field changes (would indicate C1 violation) | Task 5 step 2 includes an explicit audit; STOP if anything outside DBSF numeric fields diffs. |
| CI parity guard runner choice (self-hosted vs ubuntu-latest) | Task 6 GREEN step explicitly notes the decision; defaults to `ubuntu-latest` unless self-hosted is required. |

## Out-of-band followups (none expected; record here if discovered)

_(none yet — populate during delegation/review)_
