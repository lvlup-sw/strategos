# Implementation Plan: Ontology 2.6.0 — Hybrid Retrieval Seams

**Date:** 2026-05-15
**Design:** `docs/designs/2026-05-15-ontology-2-6-0-hybrid-retrieval.md`
**Feature ID:** `ontology-2-6-0-hybrid-retrieval`
**Closes:** strategos#56, strategos#57, strategos#58; resolves milestone #47.
**Iron Law:** **NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST**

## Cadence

Three PRs, partial-parallel:

- **PR-A** (Tasks 1–9): `IKeywordSearchProvider` seam (#56, DR-1). No `OntologyQueryTool` change.
- **PR-B** (Tasks 10–22): `RankFusion` utilities — weighted RRF + DBSF (#57, DR-2). No `OntologyQueryTool` change.
- **PR-C** (Tasks 23–43): `HybridQueryOptions` wiring on `OntologyQueryTool.QueryAsync` (#58, DR-3). **Depends on PR-A merged AND PR-B merged.**

PR-A and PR-B touch disjoint files and may be developed and merged in any order. PR-C is the integration slice.

## Traceability matrix (design → tasks)

| DR | Section | Title | Tasks |
|---|---|---|---|
| DR-1 | §4 | `IKeywordSearchProvider` seam | 1, 2, 3, 4, 5, 6, 7, 8, 9 |
| DR-2 | §5 | `RankFusion` utilities | 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 |
| DR-3 | §6 | `HybridQueryOptions` wiring | 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43 |

## Conventions

- **Test naming:** `Method_Scenario_Outcome` (e.g., `Reciprocal_UnweightedAgainstCormack2009Reference_BitIdentical`).
- **Test framework:** TUnit (Strategos convention). Invocation:
  `dotnet test src/<TestProject>/<TestProject>.csproj -- --treenode-filter "/*/*/*/<TestName>"`.
- **TDD discipline:** Each task starts with a failing test ([RED]), then minimum implementation ([GREEN]), then optional [REFACTOR].
- **File paths:** absolute from repo root (e.g., `src/Strategos.Ontology/Retrieval/...`).
- **Project mapping:** New types live in `Strategos.Ontology` (project) under `Retrieval/` folder, namespace `Strategos.Ontology.Retrieval`. Tests in `Strategos.Ontology.Tests/Retrieval/`. Wiring tests in `Strategos.Ontology.MCP.Tests/`.
- **Public-API baseline:** Each public addition updates `PublicAPI.Unshipped.txt` if the file exists for the project; otherwise PR notes the addition.

---

## PR-A: `IKeywordSearchProvider` seam (DR-1, #56)

### Task 1: `KeywordSearchRequest` record

**Implements:** DR-1
**Phase:** RED → GREEN

1. [RED] Add test `Ctor_DefaultMetadataFilters_IsNull` to `src/Strategos.Ontology.Tests/Retrieval/KeywordSearchRequestTests.cs` asserting `new KeywordSearchRequest("q", "c", 5).MetadataFilters == null`. Expected failure: type does not exist.
2. [RED] Add test `WithExpression_NewTopK_PreservesImmutability` asserting `with` semantics.
3. [GREEN] Create `src/Strategos.Ontology/Retrieval/KeywordSearchRequest.cs`:
   ```csharp
   namespace Strategos.Ontology.Retrieval;

   public sealed record KeywordSearchRequest(
       string Query,
       string CollectionName,
       int TopK,
       IReadOnlyDictionary<string, string>? MetadataFilters = null);
   ```

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 2: `KeywordSearchResult` record

**Implements:** DR-1
**Phase:** RED → GREEN

1. [RED] Add test `Ctor_Constructs_AllFieldsRoundTrip` to `src/Strategos.Ontology.Tests/Retrieval/KeywordSearchResultTests.cs`. Expected failure: type does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/KeywordSearchResult.cs`:
   ```csharp
   public sealed record KeywordSearchResult(string DocumentId, double Score, int Rank);
   ```

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 3: `KeywordSearchException`

**Implements:** DR-1
**Phase:** RED → GREEN

1. [RED] Add test `Ctor_MessageAndInner_PreservesBothViaThrow` to `src/Strategos.Ontology.Tests/Retrieval/KeywordSearchExceptionTests.cs`. Expected failure: type does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/KeywordSearchException.cs`:
   ```csharp
   public sealed class KeywordSearchException : Exception
   {
       public KeywordSearchException(string message, Exception? inner = null) : base(message, inner) { }
   }
   ```

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 4: `IKeywordSearchProvider` interface

**Implements:** DR-1
**Phase:** GREEN (no test — interface; behavior tested via in-memory provider)

1. [GREEN] Create `src/Strategos.Ontology/Retrieval/IKeywordSearchProvider.cs` with the interface and XML doc-comments stating: rank-1-indexed; non-negative unbounded provider-scale scores; AND-semantics filters; collection-not-found → `KeywordSearchException`; cancellation propagates; transport faults wrap to `KeywordSearchException`.

**Dependencies:** Tasks 1, 2, 3
**Parallelizable:** No
**testingStrategy:** unit

### Task 5: In-memory test provider (`InMemoryKeywordSearchProvider`)

**Implements:** DR-1 (test infrastructure)
**Phase:** GREEN (no test — test support code; behavior tested via Task 6)

1. [GREEN] Create `src/Strategos.Ontology.Tests/Retrieval/InMemoryKeywordSearchProvider.cs` implementing `IKeywordSearchProvider`. Constructor takes `Dictionary<string, IReadOnlyList<(string DocId, double Score)>>` keyed by collection name. Implements: sort by Score desc, tie-break by DocumentId ordinal, 1-indexed Rank assignment, AND filter semantics (passes through to caller-supplied predicate dict, treated as exact-match on a synthetic metadata dict per doc supplied via secondary constructor parameter), throws `KeywordSearchException` for missing collection, honors `ct`.

**Dependencies:** Task 4
**Parallelizable:** No
**testingStrategy:** unit

### Task 6: Contract behavior table — `InMemoryKeywordSearchProvider`

**Implements:** DR-1 (provider contract conformance)
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Retrieval/KeywordSearchProviderContractTests.cs`:
   - `SearchAsync_TwoMatchingDocs_RankIs1Indexed_TopRankHighestScore`
   - `SearchAsync_EmptyMatchingDocs_ReturnsEmptyList_NeverNull`
   - `SearchAsync_TopKZero_ReturnsEmptyList_NoBackendInvoked` (assert via tracking flag on provider)
   - `SearchAsync_TopKExceedsCollectionSize_ReturnsAllMatchingRanked`
   - `SearchAsync_MetadataFiltersAllMatch_ReturnsFiltered`
   - `SearchAsync_MetadataFiltersOneMismatch_ExcludesDoc`
   - `SearchAsync_UnknownCollection_ThrowsKeywordSearchException_NamesCollection`
   - `SearchAsync_CancelledTokenAtCall_ThrowsOperationCanceledException`
   - `SearchAsync_TiedScores_TieBrokenByDocumentIdOrdinalAscending`
   - Expected failure: provider does not implement the table.
2. [GREEN] Implement the in-memory provider behaviors needed to pass.

**Dependencies:** Task 5
**Parallelizable:** No
**testingStrategy:** unit

### Task 7: Provider-fault wrap test

**Implements:** DR-1 (transport-fault wrap)
**Phase:** RED → GREEN

1. [RED] Add a sibling fault-throwing test provider `ThrowingKeywordSearchProvider` (inline private class in test file) wrapping an inner `IOException`. Add test `SearchAsync_TransportFault_WrapsAsKeywordSearchException_InnerPreserved`. Expected failure: must demonstrate the wrap pattern is documented/expected (no production code needed beyond what Task 6 covers; this verifies the inner-preservation contract from `KeywordSearchException`).
2. [GREEN] No production change; this is a contract conformance assertion. If the `KeywordSearchException` constructor regression is detected, fix the inner-preservation in Task 3's class.

**Dependencies:** Task 6
**Parallelizable:** No
**testingStrategy:** unit

### Task 8: XML doc-comment audit on public surface

**Implements:** DR-1 (surface documentation)
**Phase:** GREEN

1. [GREEN] Audit `IKeywordSearchProvider`, `KeywordSearchRequest`, `KeywordSearchResult`, `KeywordSearchException` XML doc-comments. Each public member documents rank-1-indexed convention, scale-agnostic semantics, filter AND-semantics, collection-not-found behavior, cancellation behavior, and transport-fault wrap behavior.

**Dependencies:** Tasks 1–4
**Parallelizable:** No
**testingStrategy:** unit

### Task 9: PublicAPI baseline + PR-A close-out

**Implements:** DR-1 (release plumbing)
**Phase:** GREEN

1. [GREEN] If `src/Strategos.Ontology/PublicAPI.Unshipped.txt` exists, add the new public types. Otherwise note the addition in PR description.
2. [GREEN] Sanity-build: `dotnet build src/Strategos.Ontology/Strategos.Ontology.csproj` + `dotnet test src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj -- --treenode-filter "/*/*/*/Retrieval*"`.

**Dependencies:** Tasks 1–8
**Parallelizable:** No
**testingStrategy:** unit

---

## PR-B: `RankFusion` utilities — wRRF + DBSF (DR-2, #57)

### Task 10: `RankedCandidate` record

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add test `Ctor_Constructs_FieldsRoundTrip` to `src/Strategos.Ontology.Tests/Retrieval/RankedCandidateTests.cs`. Expected failure: type does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/RankedCandidate.cs`:
   ```csharp
   public sealed record RankedCandidate(string DocumentId, int Rank);
   ```

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 11: `ScoredCandidate` record

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add test `Ctor_Constructs_FieldsRoundTrip` to `src/Strategos.Ontology.Tests/Retrieval/ScoredCandidateTests.cs`.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/ScoredCandidate.cs`:
   ```csharp
   public sealed record ScoredCandidate(string DocumentId, double Score);
   ```

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 12: `FusedResult` record

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add test `Ctor_Constructs_FieldsRoundTrip` to `src/Strategos.Ontology.Tests/Retrieval/FusedResultTests.cs`.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/FusedResult.cs`:
   ```csharp
   public sealed record FusedResult(string DocumentId, double FusedScore, int Rank);
   ```

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 13: `RankFusion.Reciprocal` — Cormack 2009 reference vectors

**Implements:** DR-2 (canonical RRF correctness)
**Phase:** RED → GREEN

1. [RED] Add test `Reciprocal_UnweightedAgainstCormack2009Reference_BitIdentical` to `src/Strategos.Ontology.Tests/Retrieval/RankFusionReciprocalTests.cs`. Build the Cormack 2009 §3.3 worked example (3 rankers × ~6 docs) as `IReadOnlyList<IReadOnlyList<RankedCandidate>>`. Assert the fused ordering and per-doc scores match the published table within `1e-12`. Expected failure: `RankFusion.Reciprocal` does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/RankFusion.cs` (`static partial class`) and `src/Strategos.Ontology/Retrieval/RankFusion.Reciprocal.cs` with the method.
   - Signature: `IReadOnlyList<FusedResult> Reciprocal(IReadOnlyList<IReadOnlyList<RankedCandidate>>, IReadOnlyList<double>?, int k = 60, int topK = 10)`.
   - Implementation: for each candidate union, sum `weight_L / (k + rank)` over input lists; sort desc; tie-break by `DocumentId` ordinal; assign 1-indexed `Rank`; slice to `topK`.

**Dependencies:** Tasks 10, 12
**Parallelizable:** No
**testingStrategy:** unit

### Task 14: `RankFusion.Reciprocal` — weighted-vs-unweighted parity

**Implements:** DR-2 (regression gate)
**Phase:** RED → GREEN

1. [RED] Add tests:
   - `Reciprocal_AllOnesWeights_BitIdenticalToNullWeights` — pass `weights = [1.0, 1.0, 1.0]` and expect output equal to `weights = null`.
   - `Reciprocal_WeightsLengthMismatch_ThrowsArgumentException`.
   - `Reciprocal_NegativeWeight_ThrowsArgumentException`.
   - `Reciprocal_ZeroWeightList_ContributesNothing` — assert doc ordering equals fusion over the *other* lists only.
   - Expected failure: weighted form not yet present.
2. [GREEN] Extend `Reciprocal` to honor `weights` per the design formula.

**Dependencies:** Task 13
**Parallelizable:** No
**testingStrategy:** unit

### Task 15: `RankFusion.Reciprocal` — edge cases

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add tests:
   - `Reciprocal_EmptyInput_ReturnsEmptyList`.
   - `Reciprocal_AllListsEmpty_ReturnsEmptyList`.
   - `Reciprocal_SingleListInput_EquivalentToTopKOverThatList`.
   - `Reciprocal_DisjointLists_AllUniqueDocsAppear`.
   - `Reciprocal_FullOverlap_DocOrderingMatchesUnweightedRrf`.
   - `Reciprocal_TopKZero_ReturnsEmptyList`.
   - `Reciprocal_TopKGreaterThanUniqueDocs_ReturnsAllRanked`.
   - `Reciprocal_KZero_ThrowsArgumentOutOfRangeException`.
   - `Reciprocal_KNegative_ThrowsArgumentOutOfRangeException`.
   - Expected failures pin all edge-case branches.
2. [GREEN] Add validation guards and edge-case fast-paths in `Reciprocal`.

**Dependencies:** Task 14
**Parallelizable:** No
**testingStrategy:** unit

### Task 16: `RankFusion.Reciprocal` — property tests

**Implements:** DR-2 (algebraic invariants)
**Phase:** RED → GREEN

1. [RED] Add tests in `src/Strategos.Ontology.Tests/Retrieval/RankFusionReciprocalPropertyTests.cs`:
   - `Reciprocal_OutputIsPermutationOfUnionInputs_CappedAtTopK` (hand-rolled cases over 20 randomized seeds).
   - `Reciprocal_PairwiseOrderingMatchesFusedScoreDescending`.
   - `Reciprocal_IncreasingKMonotonicallyFlattensCurve_ResultSetUnchanged` (set equality, allow rank perturbation under ties).
   - `Reciprocal_UniformWeightDoublingDoesNotChangeOrdering`.
2. [GREEN] No production change expected if Task 13 is correct; this verifies invariants.

**Dependencies:** Task 15
**Parallelizable:** No
**testingStrategy:** property

### Task 17: Qdrant DBSF parity oracle — fixture + regenerator

**Implements:** DR-2 (parity gate infrastructure)
**Phase:** GREEN

1. [GREEN] Create `scripts/regenerate-dbsf-oracle.py`: Python script using `qdrant_client.hybrid.fusion.distribution_based_score_fusion` to compute fused outputs over a fixed set of 6 hand-authored test queries (mix of: 2-list balanced, single-element list, zero-variance list, outlier-heavy list, mixed positive/negative scores, large-skew list). Emit `Strategos.Ontology.Tests/Retrieval/Fixtures/qdrant-dbsf-oracle.json` with `{query_id, lists, expected_fused: [{document_id, fused_score, rank}]}`.
2. [GREEN] Run the script once locally; commit the JSON oracle. Add README note that regenerator is manual.

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** parity-oracle

### Task 18: `RankFusion.DistributionBased` — Qdrant parity

**Implements:** DR-2 (DBSF correctness)
**Phase:** RED → GREEN

1. [RED] Add test `DistributionBased_AgainstQdrantOracle_AllQueriesMatch_Within1eMinus9` to `src/Strategos.Ontology.Tests/Retrieval/RankFusionDistributionBasedTests.cs`. Load `qdrant-dbsf-oracle.json`, iterate queries, call `RankFusion.DistributionBased`, assert per-doc score within `1e-9` and ordering identical. Expected failure: method does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/RankFusion.DistributionBased.cs` with the method. Implementation: per-list compute μ, σ; clamp to `[μ-3σ, μ+3σ]`; normalize to `[0,1]`; weighted sum; sort desc; tie-break by DocumentId; 1-indexed Rank; slice to topK.

**Dependencies:** Tasks 11, 12, 17
**Parallelizable:** No
**testingStrategy:** parity-oracle

### Task 19: `RankFusion.DistributionBased` — edge cases

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add tests:
   - `DistributionBased_EmptyInput_ReturnsEmptyList`.
   - `DistributionBased_SingleElementList_NormalizesToHalf`.
   - `DistributionBased_ZeroVarianceList_AllElementsNormalizeToHalf` (use `1e-9` epsilon).
   - `DistributionBased_MixedPositiveAndNegativeScores_NormalizesCorrectly`.
   - `DistributionBased_OutlierPath_TracksExpectedOrdering` — versus a min-max-baseline computed in-test to assert DBSF differs.
   - `DistributionBased_WeightsLengthMismatch_ThrowsArgumentException`.
   - `DistributionBased_NegativeWeight_ThrowsArgumentException`.
   - `DistributionBased_TopKZero_ReturnsEmptyList`.
2. [GREEN] Add edge-case branches and validation guards.

**Dependencies:** Task 18
**Parallelizable:** No
**testingStrategy:** unit

### Task 20: `RankFusion.DistributionBased` — property tests

**Implements:** DR-2
**Phase:** RED → GREEN

1. [RED] Add tests in `src/Strategos.Ontology.Tests/Retrieval/RankFusionDistributionBasedPropertyTests.cs`:
   - `DistributionBased_TranslationInvariance_AddingConstantOffsetPreservesOutput`.
   - `DistributionBased_ScaleInvariance_MultiplyingByPositiveConstantPreservesOutput`.
   - `DistributionBased_WeightMonotonicity_IncreasingWeightIncreasesContribution`.
2. [GREEN] No production change expected.

**Dependencies:** Task 19
**Parallelizable:** No
**testingStrategy:** property

### Task 21: BenchmarkDotNet entries

**Implements:** DR-2 (perf gate)
**Phase:** GREEN

1. [GREEN] Create `src/Strategos.Benchmarks/Subsystems/RankFusion/ReciprocalBenchmark.cs` and `DistributionBasedBenchmark.cs`. Configurations: 2 lists × 100 candidates with disjoint and overlapping inputs. Assert median < 1 ms via BenchmarkDotNet's stats.
2. [GREEN] Add a corresponding TUnit test in `src/Strategos.Benchmarks.Tests/RankFusionBenchmarkTests.cs` that exercises a coarse perf budget (assert call completes within 50 ms wall under 10 invocations — defensive non-CI-flake gate; pristine perf measurement is left to BenchmarkDotNet runs).

**Dependencies:** Tasks 13–20
**Parallelizable:** Yes (with Task 22)
**testingStrategy:** benchmark

### Task 22: PublicAPI baseline + PR-B close-out

**Implements:** DR-2 (release plumbing)
**Phase:** GREEN

1. [GREEN] If `Strategos.Ontology/PublicAPI.Unshipped.txt` exists, add `RankFusion`, all records, `FusionMethod` is **not** added here (PR-C). Otherwise note in PR description.
2. [GREEN] Sanity-build + run all `RankFusion*` tests + benchmark dry-run.

**Dependencies:** Tasks 10–21
**Parallelizable:** No
**testingStrategy:** unit

---

## PR-C: `HybridQueryOptions` wiring (DR-3, #58)

### Task 23: `FusionMethod` enum

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `FusionMethod_HasReciprocalAndDistributionBased_NumericValuesStable` to `src/Strategos.Ontology.Tests/Retrieval/FusionMethodTests.cs` asserting `(int)FusionMethod.Reciprocal == 0`, `(int)FusionMethod.DistributionBased == 1`. Expected failure: enum does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/FusionMethod.cs` with the enum and XML doc-comments per design §6.1.

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 24: `HybridQueryOptions` record + defaults

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.Tests/Retrieval/HybridQueryOptionsTests.cs`:
   - `Defaults_EnableKeywordTrue_FusionMethodReciprocal_K60_SparseAndDenseTopK50`.
   - `Defaults_SourceWeightsNull_BmSaturationThreshold18`.
   - `With_OverrideFusionMethod_PreservesOthers`.
   - Expected failure: type does not exist.
2. [GREEN] Create `src/Strategos.Ontology/Retrieval/HybridQueryOptions.cs` per design §6.1. Sealed record with `init` properties.

**Dependencies:** Task 23
**Parallelizable:** Yes (with 25, 26)
**testingStrategy:** unit

### Task 25: `HybridQueryOptions` argument validation

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add tests:
   - `Validate_SparseTopKNegative_ThrowsArgumentOutOfRangeException`.
   - `Validate_DenseTopKNegative_ThrowsArgumentOutOfRangeException`.
   - `Validate_RrfKZero_ThrowsArgumentOutOfRangeException`.
   - `Validate_RrfKNegative_ThrowsArgumentOutOfRangeException`.
   - `Validate_SourceWeightsLengthThree_ThrowsArgumentException`.
   - `Validate_SourceWeightsNegativeElement_ThrowsArgumentException`.
   - Expected failure: no validation present.
2. [GREEN] Add `internal void Validate()` (or static `EnsureValid`) on `HybridQueryOptions` invoked from `OntologyQueryTool` once at call-entry. Throws per design §6.6.

**Dependencies:** Task 24
**Parallelizable:** No
**testingStrategy:** unit

### Task 26: `HybridMeta` sub-record

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.MCP.Tests/HybridMetaTests.cs`:
   - `Defaults_HybridFalse_FusionMethodNull_AllOptionalsNull`.
   - `JsonShape_HealthyReciprocal_EmitsExpectedKeys` — serialize and assert keys: `hybrid`, `fusionMethod`, `denseTopScore`, `sparseTopScore`, `bmSaturationThreshold`.
   - `JsonShape_OmitsNullOptionals` — degraded shape: only `hybrid` and `degraded` keys when others null.
   - Expected failure: type does not exist.
2. [GREEN] Create `src/Strategos.Ontology.MCP/HybridMeta.cs` per design §6.5. Sealed record with `init` properties and `JsonPropertyName` attributes. Configure JSON to ignore-null for optional fields (or `JsonIgnoreCondition.WhenWritingNull`).

**Dependencies:** None
**Parallelizable:** Yes
**testingStrategy:** unit

### Task 27: `ResponseMeta.Hybrid` extension

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add tests to `src/Strategos.Ontology.MCP.Tests/ResponseMetaHybridTests.cs`:
   - `Default_HybridIsNull` — `ResponseMeta.ForGraph(graph).Hybrid == null`.
   - `JsonShape_HybridNull_KeyAbsent` — serialized JSON has no `"hybrid"` key when `Hybrid == null` (backward-compat with 2.5.0 snapshots).
   - `JsonShape_HybridSet_KeyPresent`.
   - `With_HybridSet_PreservesOntologyVersion`.
   - Expected failure: `Hybrid` property does not exist.
2. [GREEN] Modify `src/Strategos.Ontology.MCP/ResponseMeta.cs` to add `HybridMeta? Hybrid { get; init; }` with `[JsonPropertyName("hybrid")]` and `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`.

**Dependencies:** Task 26
**Parallelizable:** No
**testingStrategy:** unit

### Task 28: `OntologyQueryTool` constructor — optional `IKeywordSearchProvider?`

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `Ctor_WithoutKeywordProvider_Constructs` and `Ctor_WithKeywordProvider_StoresField` to `src/Strategos.Ontology.MCP.Tests/OntologyQueryToolConstructorTests.cs`. Expected failure: ctor signature does not accept provider.
2. [GREEN] Modify `OntologyQueryTool` to accept `IKeywordSearchProvider? keywordProvider = null` as the last constructor parameter; store in field.

**Dependencies:** Task 4 (interface exists — comes from PR-A merged)
**Parallelizable:** No
**testingStrategy:** unit

### Task 29: `OntologyQueryTool.QueryAsync` — additive `hybridOptions` parameter, null preserves 2.5.0

**Implements:** DR-3 (DIM-3 hard gate)
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridOptionsNull_StructuralBranch_ReturnsQueryResult_NoHybridMeta` and `QueryAsync_HybridOptionsNull_SemanticBranch_ReturnsSemanticQueryResult_NoHybridMeta` to `src/Strategos.Ontology.MCP.Tests/OntologyQueryToolHybridTests.cs`. Use existing 2.5.0 fixture; assert `_meta.Hybrid == null`. Expected failure: `hybridOptions` parameter does not exist.
2. [GREEN] Add `HybridQueryOptions? hybridOptions = null` to `QueryAsync` as the parameter immediately before `CancellationToken ct`. When `hybridOptions is null`, take the 2.5.0 path unchanged.

**Dependencies:** Tasks 25, 27, 28
**Parallelizable:** No
**testingStrategy:** unit

### Task 30: Structural query + non-null `hybridOptions` → silent ignore

**Implements:** DR-3 (DIM-3 / surface clarity)
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_StructuralQueryWithHybridOptions_IgnoresOptions_NoHybridMeta_NoWarnLog`. Use a `TestLogger` to assert no log entry was emitted; assert returned `QueryResult` shape unchanged.
2. [GREEN] In `QueryAsync`, gate hybrid-path branch on `semanticQuery is not null && hybridOptions is not null`. Structural branch ignores `hybridOptions`.

**Dependencies:** Task 29
**Parallelizable:** No
**testingStrategy:** unit

### Task 31: Semantic + `hybridOptions` + no provider → degraded `no-keyword-provider` + warn-once

**Implements:** DR-3 (fail-closed DIM-7)
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_SemanticWithHybridOptions_NoProviderRegistered_DenseOnly_DegradedNoKeywordProvider_WarnsOnce`. Assert `HybridMeta { Hybrid = false, Degraded = "no-keyword-provider" }`. Two consecutive calls produce exactly one warning log entry.
2. [GREEN] Implement provider-absent branch in `QueryAsync`. Use `Interlocked.CompareExchange` on a private `int _noProviderWarnedOnce` field to ensure single emission per `OntologyQueryTool` instance.

**Dependencies:** Task 30
**Parallelizable:** No
**testingStrategy:** unit

### Task 32: Semantic + `hybridOptions.EnableKeyword = false` → dense-only

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_SemanticWithHybridOptionsEnableKeywordFalse_DenseOnly_HybridMetaHybridFalse_NoDegraded`. Provider IS registered; option flag forces dense-only. Assert `HybridMeta { Hybrid = false, Degraded = null }`.
2. [GREEN] Branch in `QueryAsync` on `hybridOptions.EnableKeyword == false`.

**Dependencies:** Task 31
**Parallelizable:** No
**testingStrategy:** unit

### Task 33: Hybrid happy path — Reciprocal

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridReciprocal_BothLegsReturn_FusedOutputMatchesRankFusionReciprocal_HybridMetaHealthyReciprocal` to `OntologyQueryToolHybridTests.cs`. Register `InMemoryKeywordSearchProvider` with known input; the dense path returns a known fixture. Assert: returned `SemanticQueryResult` items ordering matches an explicit call to `RankFusion.Reciprocal(ranked, weights=null, k=60, topK=request.topK)` applied to the same inputs. Assert `HybridMeta { Hybrid = true, FusionMethod = "reciprocal" }` with non-null `DenseTopScore` and `SparseTopScore`.
2. [GREEN] Implement the dense+sparse parallel-fan-out + fusion path: convert dense similarity results to `RankedCandidate` list, convert sparse provider results, call `RankFusion.Reciprocal(...)`, project fused order back into the existing `SemanticQueryResult` items list.

**Dependencies:** Task 32
**Parallelizable:** No
**testingStrategy:** unit

### Task 34: Hybrid happy path — DistributionBased

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridDistributionBased_BothLegsReturn_FusedOutputMatchesRankFusionDistributionBased_HybridMetaHealthyDistributionBased`. Same setup as Task 33 but `FusionMethod = DistributionBased`; assert items ordering matches `RankFusion.DistributionBased(scored, weights=null, topK=request.topK)`.
2. [GREEN] Branch in fusion-selection to dispatch to `RankFusion.DistributionBased` when `hybridOptions.FusionMethod == DistributionBased`.

**Dependencies:** Task 33
**Parallelizable:** No
**testingStrategy:** unit

### Task 35: Weighted Reciprocal snapshot

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridReciprocalWeighted_DenseDominantWeights_SnapshotOrdering`. `SourceWeights = [1.0, 0.5]` over known inputs; assert ordering matches snapshot computed via `RankFusion.Reciprocal(..., weights: [1.0, 0.5], k: 60, topK: ...)`.
2. [GREEN] Ensure `SourceWeights` is propagated through the fusion call.

**Dependencies:** Task 34
**Parallelizable:** No
**testingStrategy:** unit-snapshot

### Task 36: Weighted DistributionBased snapshot

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridDistributionBasedWeighted_DenseDominantWeights_SnapshotOrdering`. Same as Task 35 with `FusionMethod = DistributionBased`.
2. [GREEN] Ensure `SourceWeights` is propagated through DBSF call.

**Dependencies:** Task 35
**Parallelizable:** No
**testingStrategy:** unit-snapshot

### Task 37: Sparse failure → fallback to dense-only with `Degraded = "sparse-failed"`

**Implements:** DR-3 (fail-closed DIM-7)
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridSparseProviderThrows_FallsBackToDenseOnly_DegradedSparseFailed_ExceptionAndStackLogged`. Use a `ThrowingKeywordSearchProvider` that raises an `IOException` mid-flight. Assert returned items match dense-only, `HybridMeta { Hybrid = false, Degraded = "sparse-failed" }`, log captures both exception type and stack frame.
2. [GREEN] Wrap sparse task in a try/catch within the parallel-fan-out coordination. On catch: log error with `ILogger.LogError(ex, ...)`; fall back to dense-only.

**Dependencies:** Task 36
**Parallelizable:** No
**testingStrategy:** unit

### Task 38: Dense failure → hard failure (no fallback to sparse-only)

**Implements:** DR-3 (baseline-failure semantics)
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridDenseThrows_PropagatesException_NoFallbackToSparseOnly`. Inject a dense-path failure; expect the exception to bubble; assert no `HybridMeta` returned because there's no return.
2. [GREEN] No catch on dense in the hybrid coordinator; dense remains baseline (existing 2.5.0 throw-through behavior preserved).

**Dependencies:** Task 37
**Parallelizable:** No
**testingStrategy:** unit

### Task 39: Cancellation propagation through both legs

**Implements:** DR-3
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridCancellationMidFlight_BothLegsCancelled_ThrowsOperationCanceledException`. Use a delaying in-memory provider that observes `ct`. Cancel `ct` after 10ms; assert `OperationCanceledException`.
2. [GREEN] Propagate `ct` to both `denseTask` and `sparseTask`; `Task.WhenAll` lets `OperationCanceledException` surface naturally.

**Dependencies:** Task 38
**Parallelizable:** No
**testingStrategy:** unit

### Task 40: Parallelism overlap timing assertion

**Implements:** DR-3 (DIM-8 ops ergonomics)
**Phase:** RED → GREEN

1. [RED] Add test `QueryAsync_HybridBothLegs_RunInParallel_TimingOverlapWithinTolerance`. Use a delaying provider for sparse (50ms artificial delay) and instrument dense to record start/end timestamps. Assert sparse-start < dense-end and sparse-end > dense-start (true overlap).
2. [GREEN] Ensure both leg `Task`s are *started* (`Task.Run` or `_ = task` pattern) before `Task.WhenAll`. Confirm no accidental sequential await.

**Dependencies:** Task 39
**Parallelizable:** No
**testingStrategy:** integration-timing

### Task 41: `HybridMeta` snapshot tests on all five paths

**Implements:** DR-3 (DIM-6 test surface coverage)
**Phase:** RED → GREEN

1. [RED] Add `src/Strategos.Ontology.MCP.Tests/OntologyQueryToolHybridMetaSnapshotTests.cs` with five snapshot tests for the JSON shape of `ResponseMeta` / `_meta`:
   - `Snapshot_HybridOptionsNull_NoHybridKey`.
   - `Snapshot_HybridHealthyReciprocal`.
   - `Snapshot_HybridHealthyDistributionBased`.
   - `Snapshot_HybridDegradedNoKeywordProvider`.
   - `Snapshot_HybridDegradedSparseFailed`.
   - Capture committed snapshots under `src/Strategos.Ontology.MCP.Tests/__snapshots__/`.
2. [GREEN] No production change expected; snapshots verify wire shape per design §6.5.

**Dependencies:** Tasks 31, 32, 33, 34, 37
**Parallelizable:** No
**testingStrategy:** snapshot

### Task 42: 2.5.0 regression suite green with `hybridOptions = null`

**Implements:** DR-3 (DIM-3 hard gate — milestone-close criterion)
**Phase:** GREEN

1. [GREEN] Run full `Strategos.Ontology.MCP.Tests` and `Strategos.Ontology.Tests` suites. All pre-existing tests must pass without modification. If any test fails because of changed wire shape, regress to Task 27 / Task 29 and tighten the omit-null shape.

**Dependencies:** Tasks 23–41
**Parallelizable:** No
**testingStrategy:** regression

### Task 43: CHANGELOG + milestone-close + PR-C close-out

**Implements:** DR-3 (release plumbing)
**Phase:** GREEN

1. [GREEN] Update `CHANGELOG.md` under `## [2.6.0] — 2026-MM-DD` heading. Sections: Added (IKeywordSearchProvider, RankFusion, HybridQueryOptions, HybridMeta), Changed (OntologyQueryTool additive parameter; ResponseMeta extended), Migration notes (no migration needed for 2.5.0 callers).
2. [GREEN] If `PublicAPI.Unshipped.txt` exists, finalize entries for PR-C.
3. [GREEN] Sanity build: `dotnet build` solution; full test suite green.

**Dependencies:** Task 42
**Parallelizable:** No
**testingStrategy:** release-plumbing

---

## Parallelization summary

### Within PR-A (no inter-task parallelism beyond Tasks 1–3)

- **Parallel group α** (no deps): Tasks 1, 2, 3 (independent records/exception).
- **Serial**: Task 4 → 5 → 6 → 7 → 8 → 9.

### Within PR-B (no inter-task parallelism beyond Tasks 10–12, 17)

- **Parallel group β** (no deps): Tasks 10, 11, 12, 17.
- **Serial**: 13 → 14 → 15 → 16; 18 (waits on 17) → 19 → 20; 21 (after 13–20); 22 (after 10–21).

### Within PR-C

- **Parallel group γ** (no deps): Tasks 23, 24, 26.
- **Serial**: 25 (after 24); 27 (after 26); 28 (after PR-A merged); 29 (after 25, 27, 28); 30 → 31 → 32 → 33 → 34 → 35 → 36 → 37 → 38 → 39 → 40; 41 (after 31, 32, 33, 34, 37); 42 (after 23–41); 43 (after 42).

### Cross-PR parallelism

- **PR-A and PR-B can run concurrently in separate worktrees.** They touch disjoint files.
- **PR-C must wait** for both PR-A and PR-B to be merged into `main`.

---

## Acceptance gate (milestone close)

- [ ] All 43 tasks completed.
- [ ] PR-A, PR-B, PR-C all merged to `main`.
- [ ] Full 2.5.0 `OntologyQueryTool` test suite green unmodified (Task 42).
- [ ] Cormack 2009 §3.3 parity passes within `1e-12` (Task 13).
- [ ] Qdrant DBSF parity oracle passes within `1e-9` (Task 18).
- [ ] BenchmarkDotNet entries green at < 1 ms median for 2×100 fusion (Task 21).
- [ ] CHANGELOG.md `## [2.6.0]` section published.
- [ ] Issues #56, #57, #58, #47 closed; milestone "Ontology 2.6.0 — Hybrid Retrieval Seams" closed.

---

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Qdrant Python regenerator output diverges from C# DBSF due to subtle float ops ordering | Pin Qdrant version in the regenerator script's requirements (single-row `requirements.txt` co-located); commit Python script + JSON oracle together; tolerance `1e-9` (not `1e-12`) absorbs minor reordering. |
| BenchmarkDotNet flake on shared CI runners | Coarse 50ms wall budget on the TUnit smoke test (Task 21 step 2); rely on local benchmark runs for true perf characterization. |
| `Task.WhenAll` exception-aggregation behavior masks logging context for sparse failure | Wrap each leg in its own try/catch before `Task.WhenAll`; rethrow only after logging (Task 37). |
| `IReadOnlyDictionary<string, string>?` value-equality semantics in records | Use `with` testing pattern in Task 1; record-equality across `null` and `{}` is distinct in C# — documented in XML doc-comments. |
| Existing `ResponseMeta` consumers depend on `default(ResponseMeta)` value-equality | New `Hybrid` property defaults to `null`; equality still well-defined; verified via Task 27 `Default_HybridIsNull`. |
| In-memory test provider drift from real BM25 backends in basileus | Provider contract test (Task 6) is the spec; basileus's real provider must conform. Cross-product compose validation (parent ADR) catches drift at release. |
