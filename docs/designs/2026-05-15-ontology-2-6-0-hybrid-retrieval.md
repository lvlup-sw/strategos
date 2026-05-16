# Ontology 2.6.0 — Hybrid Retrieval Seams

**Status.** Design — approved for planning.
**Milestone.** Ontology 2.6.0 — Hybrid Retrieval Seams.
**Umbrella issue.** [#47](https://github.com/lvlup-sw/strategos/issues/47).
**Sub-issues.** [#56 (DR-1)](https://github.com/lvlup-sw/strategos/issues/56), [#57 (DR-2)](https://github.com/lvlup-sw/strategos/issues/57), [#58 (DR-3)](https://github.com/lvlup-sw/strategos/issues/58).
**Predecessor.** Ontology 2.5.0 — Dispatch Guarantees + Polyglot Ingestion + MCP 2025-11-25 + Starlight docs. Released; tag pushed; merge-queue clear.
**Successor.** Strategos 2.7.0 — Convergence (TypeSpec parity, IWorkflowBuilder PublicAPI baseline).

---

## 1. Problem statement

Strategos 2.5.0 shipped a fully-typed, MCP-conformant ontology query surface (`OntologyQueryTool.QueryAsync` with optional semantic-vector branch) backed by `IOntologySource` and version-stamped `ResponseMeta`. Basileus's [Ontology MCP Endpoint epic (basileus#167)](https://github.com/lvlup-sw/basileus/issues/167) needs that surface to optionally fan out a *parallel* keyword (sparse / BM25) retrieval path and fuse it with the existing dense (vector) path — without Strategos owning the BM25 backend choice, the reranker, or the cache.

The grounding research (`basileus:docs/research/2026-04-19-data-shape-query-performance-relevance.md`) and parent ADR (`basileus:docs/adrs/ontological-data-fabric.md` §4.3, §§9.8–9.9) establish that hybrid retrieval — dense + sparse + rank fusion — outperforms either path alone across the heterogeneous corpora Basileus indexes. The 2026 production consensus across Elasticsearch 8.16+, OpenSearch 2.19, Azure AI Search, Qdrant 1.11+, Vespa, Solr, and Pinecone is: RRF (Cormack 2009) generalized with per-source weights, with a score-aware sibling (DBSF, Qdrant 2024) for cases where score variance differs across paths.

Strategos must expose just enough surface to let Basileus assemble this composition over `OntologyQueryTool` without taking on any infrastructure choice.

## 2. Goals & non-goals

### Goals

1. **Backward-compatible extension.** 2.5.0 callers' compiled binaries and tests pass unmodified.
2. **Three minimal seams.** Sparse provider interface (zero default impl), pure fusion utilities, optional `HybridQueryOptions` parameter wired into the semantic branch of `OntologyQueryTool.QueryAsync`.
3. **Industry-default fusion semantics.** Weighted Reciprocal Rank Fusion as the default (matches Elasticsearch 8.16+ / Azure AI Search / Qdrant); Distribution-Based Score Fusion as a sibling option.
4. **DIM-7 fail-closed semantics.** No silent fallbacks. `_meta.degraded` surfaces all degradation paths (`no-keyword-provider`, `sparse-failed`). Dense failure remains a hard failure (dense is the baseline).
5. **Strategos owns no infrastructure choice.** No BM25 backend, no reranker, no cache. Basileus owns those (basileus#167).

### Non-goals

- **TypeSpec equivalents** in `Strategos.Contracts` for the new records — deferred to Strategos 2.7.0 (#3, Convergence).
- **Default Azure / pgvector keyword provider** — Basileus owns the implementation.
- **Reranker / cross-encoder layer** — Basileus owns this.
- **Adaptive fusion** (DAT, entropy-based, LTR, TRF) — application concerns; not exposed by Strategos library.
- **Score-aware fusion using `BmSaturationThreshold`** — the threshold is observational only in 2.6.0; future-iteration hook.
- **TM2C2 / convex combination** with α tuning — best on tuned benchmarks (Bruch 2024) but α is domain-specific; application-layer.

## 3. Design overview

Three additive surfaces under a new namespace `Strategos.Ontology.Retrieval`, plus a typed extension of `ResponseMeta`. Three PRs (A/B/C) mirroring the 2.5.0 slice cadence.

### 3.1 Three-PR slicing

| PR | Slice | Issue | Files touched | Depends on |
|---|---|---|---|---|
| **PR-A** | DR-1: `IKeywordSearchProvider` seam | #56 | `Strategos.Ontology/Retrieval/IKeywordSearchProvider.cs` + records + exception; in-memory test provider in `Strategos.Ontology.Tests/Retrieval/`. | — |
| **PR-B** | DR-2: `RankFusion` utilities | #57 | `Strategos.Ontology/Retrieval/RankFusion.cs` (+ partials), `RankedCandidate`, `ScoredCandidate`, `FusedResult` records, Qdrant DBSF parity oracle JSON, BenchmarkDotNet entries. | — |
| **PR-C** | DR-3: `HybridQueryOptions` wiring | #58 | `Strategos.Ontology/Retrieval/HybridQueryOptions.cs`, `FusionMethod` enum, `HybridMeta` typed sub-record on `ResponseMeta`, modifications to `Strategos.Ontology.MCP/OntologyQueryTool.cs` and `Strategos.Ontology.MCP/ResponseMeta.cs`. | PR-A, PR-B |

PR-A and PR-B touch disjoint files. They can be developed and merged in any order. PR-C is the wiring slice and depends on both.

### 3.2 Architectural sketch

```text
                ┌─────────────────────────────────────────────┐
                │            OntologyQueryTool                │
                │                                             │
                │  QueryAsync(...,                            │
                │      HybridQueryOptions? hybridOptions,     │ ← NEW (PR-C)
                │      CancellationToken ct)                  │
                └──────────────────┬──────────────────────────┘
                                   │
                       ┌───────────┴───────────┐
                       │  semanticQuery null?  │
                       └───────────┬───────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              │ yes                │ no                 │
              ▼                    ▼                    ▼
       structural path      ┌────────────────────────────────┐
       (2.5.0 unchanged)    │ hybridOptions enabled & valid? │
                            └────────┬───────────────────────┘
                                     │
                          ┌──────────┴──────────┐
                          │ no                  │ yes
                          ▼                     ▼
                  dense-only semantic    ┌────────────────────────────┐
                  (2.5.0 unchanged)      │ Task.WhenAll(               │
                                         │   dense (vector similarity),│ ← existing
                                         │   sparse (provider.Search)) │ ← NEW (PR-A)
                                         └──────────┬─────────────────┘
                                                    │
                                                    ▼
                                         ┌──────────────────────────┐
                                         │ RankFusion.Reciprocal    │ ← NEW (PR-B)
                                         │   | .DistributionBased   │
                                         └──────────┬───────────────┘
                                                    │
                                                    ▼
                                              SemanticQueryResult
                                              with HybridMeta
```

### 3.3 Public-surface summary

Net new public types in `Strategos.Ontology.Retrieval`:

- **Interface:** `IKeywordSearchProvider` (no default DI registration).
- **Records:** `KeywordSearchRequest`, `KeywordSearchResult`, `RankedCandidate`, `ScoredCandidate`, `FusedResult`, `HybridQueryOptions`.
- **Exception:** `KeywordSearchException`.
- **Enum:** `FusionMethod` (`Reciprocal = 0`, `DistributionBased = 1`).
- **Static class:** `RankFusion` (`Reciprocal`, `DistributionBased`).

Modified types:

- **`Strategos.Ontology.MCP.OntologyQueryTool`** — `QueryAsync` gains a `HybridQueryOptions? hybridOptions = null` parameter, positional before `CancellationToken`.
- **`Strategos.Ontology.MCP.ResponseMeta`** — gains a `HybridMeta? Hybrid` typed sub-record (sealed, immutable).
- **`Strategos.Ontology.MCP.HybridMeta`** — new sealed record co-located with `ResponseMeta`.

All additive. No type removed; no signature broken. 2.5.0 callers passing nothing additional see byte-for-byte 2.5.0 behavior.

---

## 4. Detailed design — PR-A (DR-1, #56)

`IKeywordSearchProvider` is the sparse-retrieval extension point. Strategos defines the contract; consumers register an implementation via DI.

### 4.1 Surface

```csharp
namespace Strategos.Ontology.Retrieval;

public interface IKeywordSearchProvider
{
    /// <summary>
    /// Executes a keyword (sparse / BM25) search against the underlying backend.
    /// </summary>
    /// <remarks>
    /// Rank semantics: 1-indexed (rank 1 = highest score). Matches BM25 / Lucene convention.
    /// Score semantics: non-negative, unbounded, provider-specific scale. Downstream RRF
    /// is rank-based so scale need not align across providers. DBSF normalizes scale
    /// via μ±3σ internally.
    /// </remarks>
    Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(
        KeywordSearchRequest request,
        CancellationToken ct = default);
}

public sealed record KeywordSearchRequest(
    string Query,
    string CollectionName,
    int TopK,
    IReadOnlyDictionary<string, string>? MetadataFilters = null);

public sealed record KeywordSearchResult(
    string DocumentId,
    double Score,
    int Rank);

public sealed class KeywordSearchException : Exception
{
    public KeywordSearchException(string message, Exception? inner = null)
        : base(message, inner) { }
}
```

### 4.2 Behavior table (provider contract)

| Concern | Specification |
|---|---|
| Rank semantics | 1-indexed; rank 1 = highest score. Documented in XML doc-comments. |
| Score semantics | Non-negative, unbounded, provider-specific scale. |
| Result ordering | Sorted by `Score` descending. Ties broken by `DocumentId` ordinal ascending for stability. |
| Empty results | Return empty `IReadOnlyList<KeywordSearchResult>`. Never `null`. |
| `TopK == 0` | Valid; return empty list without invoking the backend. |
| `TopK > collection size` | Return all matching documents ranked. |
| `MetadataFilters` | AND semantics across all key-value pairs. Provider may map to backend-native filters. |
| Collection not found | Throw `KeywordSearchException` naming the missing collection. |
| Cancellation | Must propagate `OperationCanceledException` honoring `ct`. |
| Provider faults | Wrap underlying transport exceptions as `KeywordSearchException(inner: ex)` — callers see one exception type. |

### 4.3 DI registration

Strategos provides **no** default registration. Consumers register their implementation in their own composition root:

```csharp
services.AddSingleton<IKeywordSearchProvider, MyAzureSearchKeywordProvider>();
```

The wiring slice (PR-C) consults the registered provider via constructor injection of `IKeywordSearchProvider?` into `OntologyQueryTool`. Absence is observable via `_meta.degraded = "no-keyword-provider"` when `HybridQueryOptions` is supplied.

### 4.4 In-memory test provider

Lives at `Strategos.Ontology.Tests/Retrieval/InMemoryKeywordSearchProvider.cs`. Deterministic; takes a `Dictionary<string, IReadOnlyList<(string DocId, double Score)>>` indexed by collection name. Used by PR-C tests too.

### 4.5 Acceptance criteria

- [ ] Interface, three records, and exception under `Strategos.Ontology.Retrieval`.
- [ ] XML doc-comments document rank-1-indexed and scale-agnostic conventions.
- [ ] No default registration in Strategos DI extensions.
- [ ] In-memory provider exercises the full behavior table.
- [ ] Public-API baseline (#51, if landed) updated; otherwise PR notes the addition.

---

## 5. Detailed design — PR-B (DR-2, #57)

Two pure static fusion methods under `RankFusion`. No DI, no global state, no I/O. Each method is deterministic, sub-1ms for 2-list × 100-candidate fusion.

### 5.1 Reciprocal (weighted RRF — Cormack 2009 generalized)

```csharp
namespace Strategos.Ontology.Retrieval;

public static partial class RankFusion
{
    /// <summary>
    /// Reciprocal Rank Fusion (Cormack et al., SIGIR 2009), generalized to per-list weights.
    /// </summary>
    /// <remarks>
    /// Score for each document = Σ_L  weight_L / (k + rank_in_L).
    /// Documents not present in a list contribute 0 from that list.
    /// When <paramref name="weights"/> is null or all 1.0, output is bit-identical to
    /// Cormack 2009 RRF.
    /// </remarks>
    public static IReadOnlyList<FusedResult> Reciprocal(
        IReadOnlyList<IReadOnlyList<RankedCandidate>> rankedLists,
        IReadOnlyList<double>? weights = null,
        int k = 60,
        int topK = 10);
}

public sealed record RankedCandidate(string DocumentId, int Rank);
public sealed record FusedResult(string DocumentId, double FusedScore, int Rank);
```

Formula:

```text
fused_score(d) = Σ_{L ∈ rankedLists}  weight_L / (k + rank_L(d))
                                     where rank_L(d) = position in L if present, else +∞
                                     (term ≡ 0 by convention when d ∉ L)
```

Output sorted by `fused_score` descending; ties broken by `DocumentId` ordinal ascending. `Rank` 1-indexed reflects post-fusion ordering. Sliced to `topK`. The weighted-RRF curve is `weight_L / (k + rank_L(d))` — matches **Elasticsearch 8.16+** convention (and Bruch 2024's parametric RRF analysis), not Qdrant's client-side `1 / ((pos+1)/weight + k - 1)` formula. This is a deliberate choice: ES form is the dominant production interpretation and the curve the literature analyzes.

### 5.2 DistributionBased (DBSF — Qdrant 2024)

```csharp
public static partial class RankFusion
{
    /// <summary>
    /// Distribution-Based Score Fusion (Qdrant, 2024). Per-list normalize via μ±3σ,
    /// clamp to [μ-3σ, μ+3σ], scale to [0,1], then weighted sum across lists.
    /// </summary>
    public static IReadOnlyList<FusedResult> DistributionBased(
        IReadOnlyList<IReadOnlyList<ScoredCandidate>> scoredLists,
        IReadOnlyList<double>? weights = null,
        int topK = 10);
}

public sealed record ScoredCandidate(string DocumentId, double Score);
```

Algorithm:

```text
1. For each list L: μ_L ← mean, σ_L ← stdev
2. low_L ← μ_L - 3σ_L,  high_L ← μ_L + 3σ_L
3. For each candidate (id, score) in L:
     normalized(id, L) = (clamp(score, low_L, high_L) - low_L) / (high_L - low_L)
4. fused_score(d) = Σ_L  weight_L · normalized(d, L)
                                    where normalized(d, L) = 0 if d ∉ L
```

### 5.3 Behavior table (Reciprocal)

| Concern | Specification |
|---|---|
| Default weights | `weights = null` → all 1.0 → bit-identical Cormack 2009 RRF. **Regression-tested.** |
| Argument validation | `k > 0`, `topK >= 0`, all `weight_L >= 0`, `weights.Count == rankedLists.Count` (when non-null). Negative or zero `k` throws `ArgumentOutOfRangeException`. Length mismatch throws `ArgumentException`. |
| Zero-weight list | A list with `weight_L = 0` contributes nothing to the fused score (effectively excluded). |
| Determinism | Identical inputs → identical outputs across runs and platforms. Tie-break by `DocumentId` ordinal. |
| Empty input | Zero lists or all-empty lists → empty output. |
| Single-list input | Equivalent to taking `topK` from that list with `weight_L / (k + rank)` scoring. |
| Duplicate within a list | Caller's contract: input lists must not contain duplicates. Enforced as `Debug.Assert` only. |
| Rank gaps | Caller's ranks need not be contiguous; RRF uses them as-is. |
| `topK == 0` | Valid; returns empty list. |
| Float stability | Sum left-to-right in input order. Tolerance `1e-12`. |

### 5.4 Behavior table (DistributionBased)

| Concern | Specification |
|---|---|
| Argument validation | `topK >= 0`, all `weight_L >= 0`, `weights.Count == scoredLists.Count` (when non-null). |
| Single-element list | Normalize that one element to `0.5` (Qdrant convention). |
| Zero-variance list (σ < 1e-9) | All elements normalize to `0.5` (Qdrant convention). |
| Empty list | Skip (contributes 0 to all documents). |
| Score scale | Any real-valued scores accepted (positive or negative). |
| Outlier handling | Clamp to `[μ-3σ, μ+3σ]` before normalizing (DBSF's stated advantage over min-max). |
| Determinism | Identical inputs → identical outputs. Tie-break by `DocumentId` ordinal. |
| Float stability | Mean and variance computed in single pass per list. |

### 5.5 Test strategy

**Reciprocal:**

- **Cormack regression.** A fixed reference set (Cormack 2009 §3.3 worked example) computed with `weights = null` matches the unweighted RRF output bit-identically. **Hard gate.**
- **Weighted-vs-unweighted parity.** All-`1.0` weights produce bit-identical output to `weights = null`.
- **Edge cases.** Empty input, single-list, n-list disjoint, n-list full overlap, `topK = 0`, `topK > size`, zero-weight list, length-mismatch (throws).
- **Property tests.** Output is a permutation of the union of input `DocumentId`s capped at `topK`; pairwise ordering follows fused score; increasing `k` is monotonic in flattening; uniform-weight scaling does not change ordering.
- **Benchmark.** 2-list × 100-candidate < 1 ms median on x64 (BenchmarkDotNet entry under `Strategos.Benchmarks/Subsystems/RankFusion/`).

**DistributionBased:**

- **Qdrant parity oracle.** A fixed test corpus computed via Qdrant's reference Python implementation (`qdrant_client/hybrid/fusion.py::distribution_based_score_fusion`) committed as JSON at `Strategos.Ontology.Tests/Retrieval/Fixtures/qdrant-dbsf-oracle.json`; C# reproduces ordering and per-document scores within `1e-9`. **Hard gate.** Regenerator script at `scripts/regenerate-dbsf-oracle.py` (Python, manual run; not in CI).
- **Edge cases.** Empty list, single-element list (→ 0.5), zero-variance list (→ 0.5), heavily-skewed scores, mixed positive/negative scores.
- **Outlier-robustness.** Queries with heavy outliers in one path; DBSF tracks expected ordering, min-max-baseline would not.
- **Property tests.** Translation invariance (adding constant offset to all scores in a list doesn't change output); scale invariance (multiplying by positive constant doesn't change output); weight monotonicity.
- **Benchmark.** Same 2×100 < 1 ms gate.

### 5.6 Caller guidance (in XML doc-comments)

> **Default to `RankFusion.Reciprocal` with all weights = 1.0.** Production default across Elasticsearch, OpenSearch, Azure AI Search, Qdrant, Vespa, Pinecone.
>
> **Add per-source weights** for known quality asymmetries. Production data shows per-source weighting moves NDCG more than `k` tuning.
>
> **Switch to `DistributionBased`** when score variance differs significantly across paths.
>
> **Look outside the library** for adaptive (DAT), learned (LTR), or tensor-rerank (ColBERT/TRF) fusion. Application concerns.

### 5.7 Acceptance criteria

- [ ] Static partial class + records under `Strategos.Ontology.Retrieval`.
- [ ] `Reciprocal` with `weights = null` is bit-identical to Cormack 2009 reference.
- [ ] Cormack 2009 §3.3 reference vectors green.
- [ ] DBSF Qdrant parity JSON oracle green within `1e-9`.
- [ ] Edge-case and property tests for both methods green.
- [ ] BenchmarkDotNet entries green at < 1 ms for 2×100 fusion.
- [ ] XML doc-comments cite Cormack 2009 and Qdrant DBSF.

---

## 6. Detailed design — PR-C (DR-3, #58)

The wiring slice. Adds `HybridQueryOptions` and a typed `HybridMeta` sub-record; threads them through the semantic branch of `OntologyQueryTool.QueryAsync`.

### 6.1 HybridQueryOptions

```csharp
namespace Strategos.Ontology.Retrieval;

public enum FusionMethod
{
    /// <summary>Reciprocal Rank Fusion (Cormack 2009). Generalizes to weighted RRF when SourceWeights is set.</summary>
    Reciprocal = 0,

    /// <summary>Distribution-Based Score Fusion (Qdrant). Score-aware via μ±3σ normalization.</summary>
    DistributionBased = 1,
}

public sealed record HybridQueryOptions
{
    /// <summary>Master switch. False forces dense-only even if a provider is registered.</summary>
    public bool EnableKeyword { get; init; } = true;

    /// <summary>Sparse-path TopK before fusion.</summary>
    public int SparseTopK { get; init; } = 50;

    /// <summary>Dense-path TopK before fusion.</summary>
    public int DenseTopK { get; init; } = 50;

    /// <summary>Fusion method. Default: Reciprocal.</summary>
    public FusionMethod FusionMethod { get; init; } = FusionMethod.Reciprocal;

    /// <summary>RRF smoothing constant. Used only when FusionMethod = Reciprocal.</summary>
    public int RrfK { get; init; } = 60;

    /// <summary>Optional per-source weights [denseWeight, sparseWeight]. Null = both 1.0. Each weight ≥ 0.</summary>
    public IReadOnlyList<double>? SourceWeights { get; init; } = null;

    /// <summary>BM25 score threshold; informational, surfaced in _meta.hybrid.bmSaturationThreshold. Does NOT affect fusion math in 2.6.0.</summary>
    public double BmSaturationThreshold { get; init; } = 18.0;
}
```

### 6.2 OntologyQueryTool signature change

```csharp
// 2.5.0 (existing)
public async Task<QueryResultUnion> QueryAsync(
    string objectType,
    string? domain = null,
    string? filter = null,
    string? traverseLink = null,
    string? interfaceName = null,
    string? include = null,
    string? semanticQuery = null,
    int topK = 5,
    double minRelevance = 0.7,
    string? distanceMetric = null,
    CancellationToken ct = default)

// 2.6.0 (additive — one new positional optional parameter before ct)
public async Task<QueryResultUnion> QueryAsync(
    string objectType,
    string? domain = null,
    string? filter = null,
    string? traverseLink = null,
    string? interfaceName = null,
    string? include = null,
    string? semanticQuery = null,
    int topK = 5,
    double minRelevance = 0.7,
    string? distanceMetric = null,
    HybridQueryOptions? hybridOptions = null,    // ← NEW
    CancellationToken ct = default)
```

`OntologyQueryTool`'s constructor gains an optional `IKeywordSearchProvider? keywordProvider = null` parameter resolved from DI.

### 6.3 Surface-attachment rationale (DIM analysis)

`HybridQueryOptions` attaches *semantically*: it has effect only on the `semanticQuery is not null` branch. The structural-query branch ignores `hybridOptions` entirely and returns 2.5.0 `QueryResult` byte-for-byte.

| Dimension | Semantic-only | Universal w/ throw | QueryRequest refactor |
|---|---|---|---|
| **DIM-1 cohesion** | ✓ hybrid IS semantic | argues unused param | over-engineered for the change |
| **DIM-2 coupling** | ✓ smallest change | adds runtime validation | touches every call site |
| **DIM-3 backward compat** | ✓ strictly additive | strictly additive | **breaks** existing callers |
| **DIM-4 surface clarity** | ✓ obvious in XML doc | ambiguous: "why allow it?" | clearer record shape but tax of refactor |
| **DIM-5 failure mode** | ✓ no confusion path | ArgumentException on misuse | clean but moot |
| **DIM-6 test surface** | ✓ semantic-only tests | + new failure tests | full re-test of structural paths |
| **DIM-7 fail-closed** | ✓ no degraded structural | requires explicit throw | tangential |
| **DIM-8 ops ergonomics** | ✓ minimal cognitive load | extra reading | breaks Basileus call sites today |

Semantic-only wins on every dimension. The issue text's `QueryRequest`-style signature was illustrative of intent, not a hard constraint — the milestone gate is *"2.5.0 test suite passes unmodified"*, which the QueryRequest refactor would violate. Semantic-only honors that gate exactly.

### 6.4 Behavior decision tree

```text
hybridOptions is null
  └─ existing 2.5.0 behavior (byte-for-byte). No HybridMeta on ResponseMeta.

hybridOptions non-null AND semanticQuery is null
  └─ structural path. hybridOptions silently ignored. No HybridMeta.

hybridOptions non-null AND semanticQuery non-null
  ├─ no IKeywordSearchProvider registered
  │   └─ dense-only semantic path; warn once per process.
  │       └─ HybridMeta { Hybrid = false, Degraded = "no-keyword-provider" }
  │
  ├─ hybridOptions.EnableKeyword == false
  │   └─ dense-only semantic path.
  │       └─ HybridMeta { Hybrid = false, Degraded = null }
  │
  └─ provider registered AND EnableKeyword
      ├─ Task.WhenAll(dense, sparse)
      │   ├─ dense throws → propagate (baseline)
      │   └─ sparse throws → catch, log with stack, fall back to dense-only.
      │       └─ HybridMeta { Hybrid = false, Degraded = "sparse-failed" }
      ├─ both complete → fuse:
      │   ├─ Reciprocal → RankFusion.Reciprocal(rankedLists, SourceWeights, RrfK, topK)
      │   └─ DistributionBased → RankFusion.DistributionBased(scoredLists, SourceWeights, topK)
      └─ HybridMeta {
            Hybrid = true,
            FusionMethod = "reciprocal" | "distribution_based",
            DenseTopScore = ..., SparseTopScore = ...,
            BmSaturationThreshold = options.BmSaturationThreshold,
            Degraded = null
        }
```

### 6.5 Extended ResponseMeta

```csharp
public sealed record ResponseMeta(
    [property: JsonPropertyName("ontologyVersion")] string OntologyVersion)
{
    /// <summary>Hybrid retrieval metadata. Null when hybridOptions was not supplied or
    /// when the semantic-query branch was not taken.</summary>
    [JsonPropertyName("hybrid")]
    public HybridMeta? Hybrid { get; init; }

    public static ResponseMeta ForGraph(OntologyGraph graph) { /* unchanged */ }
    internal static string WireFormat(string version) { /* unchanged */ }
}

public sealed record HybridMeta(
    [property: JsonPropertyName("hybrid")] bool Hybrid,
    [property: JsonPropertyName("fusionMethod")] string? FusionMethod = null,
    [property: JsonPropertyName("degraded")] string? Degraded = null,
    [property: JsonPropertyName("denseTopScore")] double? DenseTopScore = null,
    [property: JsonPropertyName("sparseTopScore")] double? SparseTopScore = null,
    [property: JsonPropertyName("bmSaturationThreshold")] double? BmSaturationThreshold = null);
```

Wire shape:

```jsonc
// dense-only (2.5.0 byte-for-byte; Hybrid sub-record absent)
{ "ontologyVersion": "sha256:..." }

// hybrid healthy
{
  "ontologyVersion": "sha256:...",
  "hybrid": {
    "hybrid": true,
    "fusionMethod": "reciprocal",
    "denseTopScore": 0.91,
    "sparseTopScore": 17.4,
    "bmSaturationThreshold": 18.0
  }
}

// hybrid degraded (sparse failed)
{
  "ontologyVersion": "sha256:...",
  "hybrid": { "hybrid": false, "degraded": "sparse-failed" }
}
```

`HybridMeta` is `null` (and serialized as absent) when `hybridOptions` was not supplied or when the structural branch was taken — DIM-3 backward-compat for 2.5.0 snapshots.

### 6.6 Behavior table

| Concern | Specification |
|---|---|
| Backward compat (DIM-3) | 2.5.0 `OntologyQueryTool` test suite passes unmodified. **Hard gate.** |
| Default fusion | `FusionMethod.Reciprocal` (production default). |
| `HybridMeta.Hybrid` | `true` only when sparse path actually contributed results. |
| `HybridMeta.FusionMethod` | Present when `Hybrid = true`. Values: `"reciprocal"` or `"distribution_based"`. |
| `HybridMeta.Degraded` | Present only on degraded paths (`"no-keyword-provider"`, `"sparse-failed"`). Null on healthy. |
| Cancellation | Single `ct` propagated to both legs via `Task.WhenAll`. |
| Parallelism | `Task.WhenAll(denseTask, sparseTask)` — both await before fusion. |
| Argument validation | `SparseTopK >= 0`, `DenseTopK >= 0`, `RrfK > 0`, `SourceWeights` (when non-null) length = 2, all weights ≥ 0. Otherwise `ArgumentOutOfRangeException` / `ArgumentException`. |
| `SourceWeights` order | `[denseWeight, sparseWeight]`. Documented in XML. |
| `BmSaturationThreshold` | Surfaces in `HybridMeta.BmSaturationThreshold`. **Does not affect fusion math in 2.6.0.** |
| Warning-once | Missing provider warning emitted exactly once per process via `Interlocked.CompareExchange` flag on the tool. |

### 6.7 Test strategy

- **Regression.** Full 2.5.0 `OntologyQueryTool` test suite green with `hybridOptions = null`. Zero diffs in shape, ranking, `_meta`.
- **Hybrid happy path — Reciprocal.** Provider registered, both legs return → `HybridMeta { Hybrid = true, FusionMethod = "reciprocal" }`, output matches `RankFusion.Reciprocal` over the inputs.
- **Hybrid happy path — DistributionBased.** Same as above with `FusionMethod.DistributionBased`.
- **Weighted Reciprocal.** `SourceWeights = [1.0, 0.5]` snapshot.
- **Weighted DistributionBased.** Snapshot.
- **Structural query w/ hybridOptions.** `semanticQuery = null` + non-null `hybridOptions` → returns 2.5.0 `QueryResult`; no `HybridMeta`; logs nothing.
- **Provider absent.** Options non-null, no `IKeywordSearchProvider` registered → dense-only, `Degraded = "no-keyword-provider"`, warning logged once (second call: no second warning).
- **Sparse failure.** Provider throws → dense-only fallback, `Degraded = "sparse-failed"`, exception+stack logged.
- **Dense failure.** Dense throws → entire call fails (no fallback to sparse-only).
- **Parallelism.** Instrumented timing assertion — sparse and dense overlap.
- **Cancellation.** `ct` cancellation propagates to both legs; `OperationCanceledException` surfaces.
- **`HybridMeta` snapshot.** `null` / healthy-Reciprocal / healthy-DistributionBased / no-provider / sparse-failed paths captured as snapshots.

### 6.8 Acceptance criteria

- [ ] `HybridQueryOptions` record + `FusionMethod` enum under `Strategos.Ontology.Retrieval`.
- [ ] `HybridMeta` typed sub-record on `ResponseMeta`.
- [ ] `OntologyQueryTool.QueryAsync` accepts `HybridQueryOptions? hybridOptions = null` as additive positional parameter before `ct`.
- [ ] `OntologyQueryTool` constructor accepts optional `IKeywordSearchProvider? keywordProvider = null`.
- [ ] Full 2.5.0 test suite green unmodified (backward-compat gate).
- [ ] Hybrid happy-path tests green for both fusion methods.
- [ ] Weighted-fusion tests green for both methods.
- [ ] Provider-absent test green with degraded `_meta` and warn-once.
- [ ] Sparse-failure test green.
- [ ] Dense-failure propagation test green.
- [ ] Parallelism overlap assertion green.
- [ ] Cancellation propagation test green.
- [ ] `HybridMeta` snapshot tests green on all five paths.
- [ ] Structural-query-with-hybridOptions test green (silent ignore, no `HybridMeta`).

---

## 7. Strategos invariant audit

Applied `/strategos-design-invariants` against the proposed design:

| Invariant | Compliance | Rationale |
|---|---|---|
| **Workflows lower to Wolverine+Marten via Roslyn SG** | N/A | No workflow surface touched. Retrieval is a leaf Ontology subsystem. |
| **Ontology is analyzer-validated and self-contained** | ✓ | New types live in `Strategos.Ontology.Retrieval` namespace, under `Strategos.Ontology` project. `Strategos.Ontology` already self-contains the ontology slice; no upward dependency added. `OntologyQueryTool` (`Strategos.Ontology.MCP`) consumes the new namespace. |
| **MCP tracks latest protocol spec (2025-11-25)** | ✓ | `ResponseMeta` extension is additive; sub-record `Hybrid` is omittable JSON when null. MCP `_meta` envelope (#40 / 2.5.0) remains the carrier; we're enriching the typed C# side without changing wire-protocol layer assumptions. |
| **Workflow DSL uses concrete domain nomenclature** | ✓ (vacuously) | Not a workflow surface. New names: `IKeywordSearchProvider`, `KeywordSearchResult`, `HybridQueryOptions`, `FusionMethod`, `RankFusion.Reciprocal`, `RankFusion.DistributionBased`. All concrete domain terms (no `IService`, no `Manager`, no `Handler`). |
| **State is immutable** | ✓ | Every new record is `sealed record` with `init`-only properties. No `set`. `FusedResult`, `RankedCandidate`, `ScoredCandidate`, `HybridQueryOptions`, `HybridMeta`, `KeywordSearchRequest`, `KeywordSearchResult` all sealed and immutable. |
| **Types are sealed-by-default** | ✓ | All new records, classes, and exceptions are `sealed`. `RankFusion` is `static partial`. `IKeywordSearchProvider` is the only interface — by definition extensibility. |
| **Descriptor identity is polyglot** | N/A | No descriptors involved. Documents identified by `DocumentId : string` (opaque to Strategos). |

**No invariant violations.** Two non-applicable lines noted above.

## 8. Axiom DIM-1..DIM-8 design audit

Applied `/axiom:design` across the design. Key takeaways carried into the design above:

- **DIM-1 (composition / SRP).** Each PR has a single responsibility. PR-A is a contract, PR-B is pure math, PR-C is wiring. The semantic-only attachment of `HybridQueryOptions` (§6.3) reinforces SRP — hybrid is a property of the semantic-retrieval operation, not of structural queries.
- **DIM-2 (coupling).** PR-A and PR-B touch disjoint files. PR-C depends on both but only adds one parameter and one provider constructor injection. No transitive coupling introduced.
- **DIM-3 (backward compat).** Hard gate. Hybrid sub-record is omittable; new parameter is optional and last positional; provider is optional in DI; structural queries see byte-for-byte 2.5.0 behavior. Verified via "2.5.0 test suite passes unmodified" acceptance criterion.
- **DIM-4 (surface clarity).** XML doc-comments call out: rank-1-indexed convention, score-scale-agnostic semantics, when to pick `Reciprocal` vs `DistributionBased`, `[denseWeight, sparseWeight]` ordering, that `BmSaturationThreshold` is informational only.
- **DIM-5 (failure mode visibility).** All degradation paths surface in `HybridMeta.Degraded`. Dense failure propagates (baseline). Sparse failure logs with stack. Provider absence warns once per process. No silent fallback to a "looks fine" wire envelope.
- **DIM-6 (test surface).** Three test layers: (a) PR-A in-memory provider exercises the full contract; (b) PR-B has Cormack 2009 + Qdrant DBSF parity oracles as hard gates plus property tests; (c) PR-C has snapshot tests on all five `HybridMeta` shapes. BenchmarkDotNet for PR-B.
- **DIM-7 (fail-closed).** No silent fall-through. `HybridMeta.Hybrid = true` if and only if the sparse path actually contributed. Provider absence is explicit; sparse failure is explicit; dense failure is hard. `EnableKeyword = false` is explicit caller opt-out.
- **DIM-8 (operational ergonomics).** Defaults match production consensus (`Reciprocal`, `k = 60`, weights = 1.0, `SparseTopK = DenseTopK = 50`). Library-level concerns (algorithm choice, weights) exposed; infrastructure-level concerns (backend, cache, reranker) left to consumers per parent ADR.

## 9. Release plan

### 9.1 Milestone close criteria

- All three PRs (A/B/C) merged.
- Full 2.5.0 `OntologyQueryTool` test suite green on `main` post-PR-C.
- BenchmarkDotNet entries green at < 1 ms for 2×100 fusion.
- Cormack 2009 and Qdrant DBSF parity oracles green.
- CHANGELOG.md updated under `## [2.6.0]`.
- Milestone "Ontology 2.6.0 — Hybrid Retrieval Seams" closed; issues #56, #57, #58, #47 closed.
- Downstream basileus#167 unblocked (verified by Basileus dev environment compose).

### 9.2 Deferrals tracked into 2.7.0 (Convergence)

- TypeSpec equivalents in `Strategos.Contracts 0.2.0` for the new records (`KeywordSearchRequest`, `KeywordSearchResult`, `HybridQueryOptions`, `HybridMeta`).
- Public-API baseline extension once #51 lands.

### 9.3 Out-of-scope (intentionally not in 2.6.0)

Score-aware fusion consuming `BmSaturationThreshold`; reranker/cross-encoder layer (Basileus's responsibility); per-call provider override; adaptive fusion (DAT / entropy / LTR / TRF); CombSUM / CombMNZ / Relative Score Fusion building blocks.

## 10. References

- **Parent ADR.** [basileus/docs/adrs/ontological-data-fabric.md §4.3, §§9.8–9.9](https://github.com/lvlup-sw/basileus).
- **Grounding research.** `basileus:docs/research/2026-04-19-data-shape-query-performance-relevance.md`.
- **Rank-fusion algorithm spike.** `exarchos/docs/research/2026-05-06-rank-fusion-algorithm-spike-strategos-57.md`.
- **Cormack, Clarke, Buettcher (2009).** *Reciprocal Rank Fusion outperforms Condorcet and individual Rank Learning Methods.* SIGIR.
- **Bruch, Gai, Ingber (2024).** *An Analysis of Fusion Functions for Hybrid Retrieval.* ACM TOIS (arXiv:2210.11934).
- **Qdrant DBSF.** `qdrant_client/hybrid/fusion.py::distribution_based_score_fusion`, Qdrant 1.11+.
- **Elasticsearch wRRF.** *Weighted Reciprocal Rank Fusion in Elasticsearch* (Search Labs, 2025-09-15).
- **Umbrella issue.** [strategos#47](https://github.com/lvlup-sw/strategos/issues/47).
- **Sub-issues.** [#56](https://github.com/lvlup-sw/strategos/issues/56), [#57](https://github.com/lvlup-sw/strategos/issues/57), [#58](https://github.com/lvlup-sw/strategos/issues/58).
- **Predecessor design.** `docs/designs/2026-05-08-ontology-2-5-0-dispatch-validation.md`, `docs/designs/2026-05-10-ontology-2-5-0-polyglot-ingestion.md`.
