# Design: Retrieval Composition for the Ontology MCP Endpoint

**Date:** 2026-04-19
**Status:** Draft (ideate output)
**Workflow:** `retrieval-composition-for-ontology-mcp` (feature, ideate phase)
**Parent ADR:** `docs/adrs/2026-04-18-exarchos-basileus-coordination.md` §§9.8, 9.9 (open questions)
**Grounding research:** `docs/research/2026-04-19-data-shape-query-performance-relevance.md`
**Parent design:** `docs/designs/2026-04-19-ingest-ontology-from-source.md` §4.4 (chunks, metadata, index type)

---

## 1. Context and thesis

The coordination ADR exposes `ontology_query(semanticQuery, topK, minRelevance, distanceMetric)` on the Basileus `/mcp/ontology` endpoint via Strategos's `OntologyQueryTool`, but specifies neither how vector results compose with the `objectType` filter, link traversal, and interface narrowing the same tool already accepts, nor how they compose with any non-vector retrieval signal. The data-shape research (§2.4, §5.4) proposes hybrid composition with Reciprocal Rank Fusion (RRF), bounded graph expansion, and an opt-in cross-encoder reranker, with an `IOntologyVersionedCache` abstraction for `ontologyVersion`-pinned retrieval caches.

This design converts those proposals into a shippable v1 by committing to:

- **Framing A** — external-agent MCP parity as the optimization target (Claude Code, Cursor, Copilot, Codex, OpenCode calling `/mcp/ontology`). Accepts 200–400 ms p95 latency on default Shape 2 path.
- **Layering C** — Strategos 2.6.0 ships minimal extension seams (`IKeywordSearchProvider`, RRF primitive, `HybridQueryOptions` on `OntologyQueryTool`); Basileus supplies Azure-specific provider implementations and owns graph expansion.
- **Shape targets 2 + 3** — natural-language concept queries and relationship/impact queries. Shape 1 (exact identifiers) and Shape 4 (ontological-record search) are served but not primary-optimized.
- **Approach 3 pipeline** — fixed, caller-parameterized pipeline with one surgical early-exit (BM25 saturation) for Shape 1 latency. No hidden classifier, no inferred heuristics beyond the single saturation check.

The success ledger: Shape 2 nDCG@10 ≥ 0.80 (goal 0.86), Shape 3 Recall@10 ≥ 0.85, p95 < 400 ms on Shape 2 default path, Cohere Rerank cost ≤ $7/workspace/month.

## 2. Scope

**In scope.**

- Strategos 2.6.0 extension seams (`IKeywordSearchProvider`, `RankFusion` utility, `HybridQueryOptions`).
- Basileus implementations: tsvector-backed keyword provider; Cohere Rerank v3.5 (Azure AI Foundry MaaS) reranker; in-process 1-hop graph expander; in-memory versioned LRU cache.
- Azure AI Search fallback adapter, feature-gated off by default.
- Pipeline orchestration in Strategos (fusion) and Basileus (expansion + rerank + caching).
- MCP `ontology_query` parameter additions: `precision`, `followLinks`, `linkDepth`, `chunkLevel`, `provenance`.
- `_meta` envelope enrichment for response transparency.
- OpenTelemetry metrics for the retrieval path.
- Measurement gate: qrel set + A/B benchmark harness comparing tsvector-hybrid vs. Azure AI Search fallback.

**Out of scope (explicit).**

- Self-hosted cross-encoder deployment (Azure ML Managed Endpoint TEI) — documented escape valve behind the same `IReranker` contract; v2 if Cohere TCO shifts.
- BM25 backend swap to `pg_search` / ParadeDB — deferred until Azure PostgreSQL Flexible Server adds the extension.
- LLM-based reranking (as opposed to cross-encoder) — no evidence the latency budget accommodates it.
- 2-hop-plus graph expansion — relevance drift risk is real (research §3.1); stays at 1-hop for v1.
- Query classifier (Approach 2 from ideate) — parameter-driven is the chosen style.
- Cross-workspace retrieval — tenant isolation is a v2 product question (research §5.5).

---

## 3. Architecture overview

```
External MCP client (Claude Code, Cursor, Copilot, Codex, OpenCode)
          │
          │  MCP: ontology_query(semanticQuery, objectType?, precision?,
          │                       followLinks?, linkDepth?, chunkLevel?,
          │                       provenance?, branch?, topK?, ...)
          ▼
  ┌─────────────────────────────────────────────────────────────────┐
  │  Basileus /mcp/ontology  (Ontology MCP Endpoint)                │
  │   └─► Strategos OntologyQueryTool (v2.6.0)                      │
  │        │                                                        │
  │        │  Reads HybridQueryOptions from tool params             │
  │        │  If options.HybridEnabled (provider registered):       │
  │        │                                                        │
  │        │  ┌──────────────────────────┬──────────────────────┐   │
  │        │  │ Parallel candidate gen                          │   │
  │        │  │                                                 │   │
  │        │  │  Dense path:                Sparse path:        │   │
  │        │  │  IObjectSetProvider         IKeywordSearchProv. │   │
  │        │  │  (pgvector HNSW via         (tsvector+GIN via   │   │
  │        │  │   ObjectSet<SemDoc>         Basileus provider)  │   │
  │        │  │   .SimilarTo())                                 │   │
  │        │  └──────────────────────────┴──────────────────────┘   │
  │        │                    │                                   │
  │        │                    ▼                                   │
  │        │    RankFusion.Reciprocal(dense, sparse, k=60)           │
  │        │                    │                                   │
  │        │  ┌─────────────────┼───── early exit ────┐             │
  │        │  │ bm25Top1 > τ && queryTokens < 5       │             │
  │        │  │ → skip rerank; _meta.skippedRerank=   │             │
  │        │  │   "bm25_saturation"                    │             │
  │        │  └─────────────────┼──────────────────────┘             │
  │        │                    ▼                                   │
  │        │    IReranker.RerankAsync (if precision=true)           │
  │        │    (Cohere Rerank v3.5 via Azure AI Foundry MaaS)      │
  │        │                    │                                   │
  │        │                    ▼                                   │
  │        │    IGraphExpander.ExpandAsync (if followLinks=true)    │
  │        │    (Basileus: 1-hop BFS via OntologyGraph.TraverseLinks)│
  │        │    Attaches as context-only, not re-ranked             │
  │        │                    │                                   │
  │        │                    ▼                                   │
  │        │    IOntologyVersionedCache<QueryKey, QueryResult>      │
  │        │    cached on (workspace, branch, ontologyVersion,      │
  │        │               queryHash, paramHash)                     │
  │        │                    │                                   │
  │        │                    ▼                                   │
  │        │    _meta-enriched response                             │
  │        │    (ontologyVersion, hybrid, reranked, degraded, ...)  │
  │        └────────────────────────────────────────────────────────┘
```

**Process boundaries.** The dense path reuses the existing `IObjectSetProvider` pgvector pipeline (`ObjectSet<SemanticDocument>(collection).SimilarTo(query).ExecuteAsync()`) unchanged. The sparse path is a new Basileus-supplied `IKeywordSearchProvider` registered with Strategos. Both paths run in parallel. Fusion lives in Strategos. Rerank + graph expansion + caching live in Basileus and are composed via DI hooks on `OntologyQueryTool`'s result pipeline. Single `OntologyQueryTool` call; single response contract.

**Data path.** All retrieval inputs are Marten-owned (`SemanticDocument`, ingested chunks with `Metadata.contentHash` / `symbolKey` / `chunkLevel` per ingest design §4.4). The tsvector index is registered as a Marten schema addition on the same `semantic-documents-{workspaceId}` collection; no second source of truth. Azure AI Search fallback, when enabled, synchronizes from Marten via the existing ingestion pipeline's output stream (design §4.4).

---

## 4. Strategos 2.6.0 — minimal extension seams

Three additions, all backward-compatible. Basileus cannot implement this design until Strategos 2.6.0 ships — coordination floor, analogous to the Strategos 2.5.0 → Basileus Ontology MCP Endpoint sequencing (ADR §6.2).

### 4.1 `IKeywordSearchProvider` (new interface)

```csharp
namespace Strategos.Ontology.Retrieval;

public interface IKeywordSearchProvider
{
    /// <summary>
    /// Returns keyword-ranked SemanticDocument candidates. Backend-specific
    /// (Postgres tsvector, Azure AI Search, OpenSearch, etc.). Implementations
    /// must be idempotent on identical inputs within the same ontologyVersion.
    /// </summary>
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
    double Score,        // backend-raw score (tsvector rank, BM25, etc.)
    int Rank);           // 1-based rank within the result set
```

**Why at the Strategos layer.** `ontology_query` is a Strategos-owned MCP tool. Putting the seam in Strategos keeps the tool surface uniform across consumers (Basileus, future-Exarchos-local). Providers are registered via DI; absence of a registered provider keeps `OntologyQueryTool` in pure-semantic mode (backward compatible).

### 4.2 `RankFusion.Reciprocal` (utility)

```csharp
namespace Strategos.Ontology.Retrieval;

public static class RankFusion
{
    /// <summary>
    /// Reciprocal Rank Fusion. k=60 is the published default (Cormack et al.
    /// 2009); the signature parameterizes it for calibration against the qrel set.
    /// </summary>
    public static IReadOnlyList<FusedResult> Reciprocal(
        IReadOnlyList<IReadOnlyList<RankedCandidate>> rankedLists,
        int k = 60,
        int topK = 10);
}

public sealed record RankedCandidate(string DocumentId, int Rank, double RawScore);
public sealed record FusedResult(string DocumentId, double FusedScore, int FusedRank,
                                  IReadOnlyDictionary<string, int> SourceRanks);
```

**Why RRF.** Score-scale-agnostic (tsvector rank ≠ cosine similarity but both produce ordinal ranks). Published as Azure AI Search's default fusion; industry-standard for hybrid. Deterministic; trivially testable.

### 4.3 `HybridQueryOptions` on `OntologyQueryTool`

`OntologyQueryTool.QueryAsync(...)` gains an optional `HybridQueryOptions` parameter. When null (default), behavior is unchanged from Strategos 2.5.0. When populated, the tool invokes `IKeywordSearchProvider` and `RankFusion.Reciprocal` before returning results.

```csharp
public sealed record HybridQueryOptions
{
    public bool EnableKeyword { get; init; } = true;      // OFF if no provider registered
    public int SparseTopK { get; init; } = 50;
    public int DenseTopK { get; init; } = 50;
    public int RrfK { get; init; } = 60;
    public double BmSaturationThreshold { get; init; } = 18.0;  // calibrated on qrels; gates Shape 1 early-exit
}
```

### 4.4 Requirements (Strategos 2.6.0)

- **DR-1 — `IKeywordSearchProvider` interface shipped in `Strategos.Ontology.Retrieval` namespace.**
  **Acceptance criteria:** (a) interface lives in `Strategos.Ontology.Retrieval` per the layered package map; (b) `KeywordSearchRequest`/`KeywordSearchResult` records have TypeSpec equivalents in `Strategos.Contracts` (basileus#152 / exarchos#1125); (c) no default implementation in Strategos — consumers register their own via DI; (d) unit tests cover null-safety on `MetadataFilters` and rank-monotonicity on the result list.

- **DR-2 — `RankFusion.Reciprocal` utility in `Strategos.Ontology.Retrieval`.**
  **Acceptance criteria:** (a) deterministic output for identical inputs; (b) handles 1-input (returns input unchanged, re-ranked from 1), 2-input (canonical RRF), and n-input cases; (c) respects `topK`; (d) unit tests against published RRF reference vectors (Cormack et al. Table 1 minimum); (e) benchmark test asserting <1 ms for a 2-list × 100-candidate fusion.

- **DR-3 — `OntologyQueryTool.QueryAsync` accepts `HybridQueryOptions`; backward-compatible.**
  **Acceptance criteria:** (a) new `HybridQueryOptions? options = null` parameter on the existing tool signature; (b) when null or no `IKeywordSearchProvider` registered, behavior matches Strategos 2.5.0 byte-for-byte (existing tests pass unchanged); (c) when populated and provider registered, dense + sparse retrieval run in parallel via `Task.WhenAll`, then fused; (d) response `_meta.hybrid: true` when hybrid actually applied; (e) contract tests verify no breaking changes for Strategos 2.5.0 consumers.

---

## 5. Basileus implementations

### 5.1 `PostgresTsVectorKeywordSearchProvider : IKeywordSearchProvider`

Lives in `shared/Basileus.Infrastructure/DataFabric/Retrieval/`. Backed by a Postgres `GIN (to_tsvector('english', content))` index on the existing `semantic-documents-{workspaceId}` Marten collection. Marten's schema configuration API registers the index alongside the pgvector HNSW index (ingest design §4.4.2).

Query SQL shape (parameterized, simplified):

```sql
SELECT id,
       ts_rank_cd(to_tsvector('english', content), plainto_tsquery('english', $1)) AS score,
       ROW_NUMBER() OVER (ORDER BY ts_rank_cd(...) DESC) AS rank
FROM {semantic_documents_table}
WHERE to_tsvector('english', content) @@ plainto_tsquery('english', $1)
  AND metadata @> $2::jsonb  -- optional metadata filters
ORDER BY score DESC
LIMIT $3;
```

**Known quality limitation.** `ts_rank_cd` lacks IDF — Pedro Alonso and ParadeDB's published analyses confirm it ranks worse than BM25 in isolation. The design accepts this because:
1. RRF consumes *ranks*, not scores. Ordering-monotonic inputs suffice.
2. Cohere Rerank v3.5 re-scores the top-K fused candidates with cross-encoder precision that dominates whatever ranking quality `ts_rank_cd` loses.
3. The measurement gate (§8) validates the assumption quantitatively against Azure AI Search fallback.

- **DR-4 — `PostgresTsVectorKeywordSearchProvider` implementation.**
  **Acceptance criteria:** (a) GIN tsvector index registered via Marten schema on ingest; (b) `SearchAsync` returns results in monotonic rank order with 1-based `Rank`; (c) metadata filter (`symbolKind`, `chunkLevel`, `branch`, `provenance`) applies via `jsonb @>` before ranking; (d) 30s timeout per call with `Result<T>`-style failure surfacing; (e) integration test against a seeded collection confirms both top-K and metadata-filtered paths.

### 5.2 `CohereReranker : IReranker`

Lives in `shared/Basileus.Infrastructure/DataFabric/Retrieval/CohereReranker.cs`. Calls the Cohere Rerank v3.5 endpoint via Azure AI Foundry serverless MaaS. Options consumed from `CohereRerankerOptions` (appsettings + Azure Key Vault secret for the bearer token). Uses the existing `HttpClient` factory pattern (mirrors `McpToolInvoker`).

**Endpoint shape** (Azure AI Foundry serverless):

```
POST https://{deployment}.{region}.models.ai.azure.com/v1/rerank
Authorization: Bearer {aad-token-for-managed-identity}
Content-Type: application/json

{ "query": "...", "documents": ["chunk1", "chunk2", ...], "top_n": 10, "model": "rerank-v3.5" }
```

Graceful degradation is already specified in `IReranker`'s doc-comment ("implementations should degrade gracefully by returning the original candidate list unchanged"). `CohereReranker` catches `HttpRequestException`, timeout, and 429/5xx; logs at warning level; appends `"reranker"` to `_meta.degraded`; returns the unchanged candidate list.

- **DR-5 — `CohereReranker` implementation with graceful degradation.**
  **Acceptance criteria:** (a) managed-identity-authenticated calls to Azure AI Foundry; (b) `CohereRerankerOptions` validated via DataAnnotations + `ValidateOnStart`; (c) network / 4xx / 5xx / timeout failures surface as `_meta.degraded += ["reranker"]` and return candidates unchanged — never throw to the caller; (d) per-call timeout default 1500 ms (keeps p95 under 400 ms budget); (e) unit tests mock the HTTP boundary for happy-path and all failure classes; (f) integration test against a real Azure AI Foundry endpoint in staging.

### 5.3 `BoundedGraphExpander : IGraphExpander`

Lives in `apps/agent-host/Basileus.AgentHost/DataFabric/Retrieval/`. New `IGraphExpander` interface in `shared/Basileus.AgentHost.Abstractions/DataFabric/`.

Implementation: given the post-rerank top-K candidates, extract their distinct `ObjectType` values, call `OntologyGraph.TraverseLinks(domain, typeName, maxDepth=1)` for each, and attach one representative `SemanticDocument` per linked type as a context-only trailing section in the response. Linked chunks do not re-enter ranking.

```csharp
public interface IGraphExpander
{
    Task<IReadOnlyList<ExpandedContext>> ExpandAsync(
        IReadOnlyList<SemanticSearchResult> topResults,
        int linkDepth,                 // 1 for v1; interface allows v2 growth
        string branch,
        CancellationToken ct = default);
}

public sealed record ExpandedContext(
    OntologyNodeRef LinkedNode,
    string LinkName,
    string? RepresentativeChunkId,  // null if no chunk indexed for the node
    string LinkedFromDocumentId);
```

- **DR-6 — `BoundedGraphExpander` with 1-hop cap.**
  **Acceptance criteria:** (a) `linkDepth > 1` rejected in v1 with `ArgumentOutOfRangeException` (interface-allowed-values enforcement); (b) expansion latency p95 < 20 ms on reference workload; (c) deduplicates linked nodes across multiple source candidates; (d) representative chunk selection prefers `chunkLevel=type`, falls back to `chunkLevel=method`; (e) returns empty list gracefully when no ontology nodes match.

### 5.4 `IOntologyVersionedCache<TKey, TValue>` (abstraction + LRU impl)

Interface lives in `shared/Basileus.AgentHost.Abstractions/DataFabric/Retrieval/`. In-memory LRU implementation lives in `shared/Basileus.Infrastructure/`. Caches both the composed-graph result and the RRF-fused query result keyed on `(workspace, branch, ontologyVersion, queryHash, paramHash)`.

```csharp
public interface IOntologyVersionedCache<TKey, TValue>
    where TKey : notnull
{
    /// <summary>Returns cached value if ontologyVersion matches; null otherwise.</summary>
    TValue? Get(TKey key, string ontologyVersion);

    void Set(TKey key, string ontologyVersion, TValue value);

    /// <summary>Drops all entries whose ontologyVersion doesn't match current.</summary>
    int InvalidateStale(string currentOntologyVersion);
}
```

- **DR-7 — `IOntologyVersionedCache<TKey, TValue>` abstraction and default LRU implementation.**
  **Acceptance criteria:** (a) abstraction lives in `Basileus.AgentHost.Abstractions`; (b) default `MemoryOntologyVersionedCache<TKey, TValue>` uses `MemoryCache` with bounded entry count (1000 default, configurable); (c) `Get` misses when `ontologyVersion` doesn't match even if the key is present (stale guard); (d) `InvalidateStale` called on resolver-reported version mismatch (ADR §2.8 addition); (e) unit tests cover version-miss, LRU eviction, concurrent access.

### 5.5 `AzureAiSearchKeywordSearchProvider` (fallback adapter)

Lives behind the same `IKeywordSearchProvider` interface. Feature-gated off by default via `retrieval.keywordBackend: "tsvector" | "azure-ai-search"` in `.exarchos.yml` / workspace manifest. When on, Marten writes synchronize to an Azure AI Search index via a Wolverine-subscribed projection (uses the ingestion pipeline's existing output stream from design §4.4).

This is the escape valve for the Q2 measurement gate: if the v1 benchmark shows tsvector falls > 3 nDCG@10 points behind Azure AI Search, flipping this flag at runtime swaps the backend without redeploying.

- **DR-8 — `AzureAiSearchKeywordSearchProvider` fallback adapter, feature-gated.**
  **Acceptance criteria:** (a) off by default; (b) enabled via `retrieval.keywordBackend: "azure-ai-search"` workspace manifest config; (c) ingestion projection writes `SemanticDocument` deltas to the Azure AI Search index with the same metadata schema; (d) swap requires no caller-code changes (same `IKeywordSearchProvider` contract); (e) acceptance test: same qrel set produces results from both backends via the same tool endpoint.

---

## 6. Pipeline orchestration (Approach 3)

Fixed, parameter-driven, no hidden classifier. One surgical early-exit.

**Pipeline steps:**

1. **Validate inputs.** Return 400 on malformed `HybridQueryOptions` or unknown `chunkLevel`.
2. **Cache check.** `IOntologyVersionedCache<QueryKey, QueryResult>` keyed on `(workspace, branch, ontologyVersion, queryHash, paramHash)`. On hit, return with `_meta.cacheHit: true`.
3. **Parallel candidate gen.** `Task.WhenAll(denseSearch, keywordSearch)`. Dense via `IObjectSetProvider` (unchanged pgvector path). Keyword via `IKeywordSearchProvider`. Each returns top-50.
4. **RRF fusion.** `RankFusion.Reciprocal(denseResults, sparseResults, k=60, topK)`. Produces `topK * 1.5` candidates (oversampling for rerank headroom).
5. **BM25-saturation early-exit.** If `keywordResults[0].Score >= options.BmSaturationThreshold && queryTokenCount < 5`, skip rerank; set `_meta.skippedRerank = "bm25_saturation"`.
6. **Rerank (conditional).** If `precision=true` and not early-exited: `IReranker.RerankAsync(query, fusedTopK)`. Cohere returns re-scored top-K. On provider failure: `_meta.degraded += "reranker"`, proceed with RRF-only ordering.
7. **Graph expansion (conditional).** If `followLinks=true`: `IGraphExpander.ExpandAsync(reranked, linkDepth=1)`. Expanded nodes attach as `_meta.expandedContext[]`, not re-ranked.
8. **Could-benefit hint.** If query contains relationship-keyword signals (`references`, `uses`, `implements`, `depends on`, `what calls`, `where is X used`) and `followLinks=false`, set `_meta.couldBenefitFromLinkExpansion: true`. Does not change behavior — caller-observable signal.
9. **Cache store.** Persist result with `ontologyVersion`.
10. **Return.** Response includes `results: SemanticSearchResult[]`, `_meta: { ontologyVersion, hybrid, reranked, skippedRerank?, cacheHit, degraded[], couldBenefitFromLinkExpansion?, expandedContext[]? }`.

- **DR-9 — Pipeline orchestration with BM25-saturation early-exit.**
  **Acceptance criteria:** (a) pipeline executes steps in strict order, parallel only where noted; (b) BM25 saturation threshold (`τ`) is calibrated against the qrel set and stored in `HybridQueryOptions.BmSaturationThreshold`, not hardcoded; (c) early-exit sets `_meta.skippedRerank = "bm25_saturation"`; (d) caller parameters override all defaults; (e) integration tests cover all six toggle combinations (precision × followLinks × cache hit/miss).

---

## 7. Tool surface changes

### 7.1 New parameters on `ontology_query`

Additions (all optional, defaulting to the Shape 2-optimal values):

| Parameter | Type | Default | Semantics |
|---|---|---|---|
| `precision` | bool | `true` | Runs the reranker after RRF fusion. `false` ships faster, lower-precision results. |
| `followLinks` | bool | `false` | Enables 1-hop graph expansion on the top-K. Attaches linked-type context. |
| `linkDepth` | int | `1` | Reserved for v2; v1 rejects `> 1`. |
| `chunkLevel` | string \| null | `null` (any) | Filters to `"file"` \| `"type"` \| `"method"` \| `"doc"`. |
| `provenance` | string \| null | `null` (any) | Filters to `"hand-authored"` \| `"ingested"`. |
| `branch` | string | `"main"` | Branch-scoped query; uses `main ⊕ branchDelta` graph composition. |

### 7.2 Response `_meta` envelope

```jsonc
{
  "results": [ /* SemanticSearchResult[] */ ],
  "_meta": {
    "ontologyVersion": "sha256:abc...",
    "hybrid": true,
    "reranked": true,
    "skippedRerank": null,                  // or "bm25_saturation"
    "cacheHit": false,
    "degraded": [],                         // e.g. ["reranker"] on Cohere failure
    "couldBenefitFromLinkExpansion": false,
    "expandedContext": [ /* ExpandedContext[] — populated when followLinks=true */ ],
    "backend": "tsvector"                   // or "azure-ai-search"
  }
}
```

- **DR-10 — Tool parameter additions on `ontology_query`.**
  **Acceptance criteria:** (a) all six new parameters are optional; (b) unknown parameter values return HTTP 400 with a clear error message; (c) MCP `inputSchema` on the tool descriptor documents every parameter; (d) MCP `outputSchema` declares the full `_meta` envelope.

- **DR-11 — `_meta` envelope enrichment.**
  **Acceptance criteria:** (a) every response carries `ontologyVersion` (ADR §2.12); (b) `hybrid` and `reranked` booleans reflect actual execution, not requested; (c) `skippedRerank`, `cacheHit`, `degraded`, `couldBenefitFromLinkExpansion`, `expandedContext`, `backend` populated as specified; (d) response schema validation (OutputSchema) enforces the envelope at the MCP boundary.

---

## 8. Measurement gate

The Q2 measurement commitment: v1 does not ship without this benchmark passing.

### 8.1 Qrel set

- **Source:** this repo (`basileus`). Dogfood — agents will query this codebase.
- **Size:** 50 Shape 2 queries + 25 Shape 3 queries (70/30 mix, matching the targeting ratio from Q4).
- **Curation:** queries authored by humans; relevance judgments double-coded by two authors (Kappa ≥ 0.7 required). Stored in `docs/research/qrels/2026-04-19-retrieval-composition.jsonl`.
- **Format:** follows the existing `evaluation_qrels_contract.md` memory note — qrels, not classification fixtures.

### 8.2 A/B harness

Integration test suite `tests/Basileus.Integration.Tests/DataFabric/Retrieval/HybridRetrievalBenchmarkTests.cs` exercises three configurations against the qrel set:

- **Baseline:** vector-only (Strategos 2.5.0 behavior).
- **Proposed:** tsvector + pgvector + RRF + Cohere Rerank v3.5 (this design's v1).
- **Fallback:** Azure AI Search hybrid + Cohere Rerank v3.5 (via `retrieval.keywordBackend: "azure-ai-search"` feature flag).

Reports nDCG@10, Recall@10, p50/p95 latency, total dollars-spent, per configuration and shape.

### 8.3 Gate criteria

- **Ship:** Proposed hits Shape 2 nDCG@10 ≥ 0.80 and within 3 points of Fallback; Shape 3 Recall@10 ≥ 0.85; p95 < 400 ms on Shape 2; < $7/workspace/month Cohere cost.
- **Roll to fallback:** Proposed is Shape 2 nDCG@10 < 0.80 **or** trails Fallback by > 3 points. Flip `retrieval.keywordBackend` default to `"azure-ai-search"` and re-run the benchmark.
- **Block ship:** Fallback also fails to meet Shape 2 ≥ 0.80 — surfaces a product-level issue that the ideation did not anticipate; escalate out of this design.

- **DR-12 — Qrel-set construction and A/B benchmark harness.**
  **Acceptance criteria:** (a) qrel file committed at `docs/research/qrels/2026-04-19-retrieval-composition.jsonl` with 75 queries; (b) inter-annotator Cohen's Kappa ≥ 0.7; (c) `HybridRetrievalBenchmarkTests` runs the three configurations and asserts Proposed passes the gate criteria or fails with a clear roll-to-fallback diagnostic; (d) benchmark runs in CI on a `[Property("Category", "Benchmark")]` filter, not in the default test pass; (e) bench results committed to `docs/research/2026-04-19-retrieval-composition-benchmark.md` before merge.

---

## 9. Observability

OpenTelemetry metrics (Basileus already ships OpenTelemetry via ServiceDefaults):

- `retrieval.query.duration_ms` (histogram, labeled by `hybrid`, `reranked`, `followLinks`, `backend`, `cacheHit`, `shape`)
- `retrieval.candidates.dense` / `retrieval.candidates.sparse` (counter)
- `retrieval.fusion.duration_ms` (histogram)
- `retrieval.rerank.duration_ms` (histogram)
- `retrieval.rerank.cost_usd` (counter; Cohere-per-call cost estimate from tokens × price)
- `retrieval.graph_expansion.duration_ms` (histogram)
- `retrieval.cache.hit` / `retrieval.cache.miss` (counter)
- `retrieval.degraded` (counter, labeled by sink e.g. `reranker`)

SLO burn alerts:
- p95 Shape 2 latency > 500 ms over 10-minute window
- `retrieval.degraded{sink=reranker}` rate > 5% over 10-minute window
- `retrieval.rerank.cost_usd` monthly aggregate > $7/workspace

- **DR-13 — OpenTelemetry metrics and SLO burn alerts.**
  **Acceptance criteria:** (a) all metrics emitted on the retrieval path with documented labels; (b) three burn-alert rules committed as Prometheus recording rules (or equivalent Azure Monitor); (c) unit test asserts the meter is instantiated via `IMeterFactory` and labels are stable across releases.

---

## 10. Failure modes and graceful degradation

Explicit handling for the four identified failure classes. This is the required-by-the-skill failure-mode DR.

| Failure | Detection | Response |
|---|---|---|
| **Cohere Rerank unavailable** (5xx, timeout, auth) | `HttpRequestException` or > 1500 ms in `CohereReranker.RerankAsync` | Log warning; append `"reranker"` to `_meta.degraded`; return RRF-fused results unchanged. Do NOT throw. |
| **tsvector backend unavailable** (Postgres connection) | `NpgsqlException` or timeout in `PostgresTsVectorKeywordSearchProvider.SearchAsync` | Log warning; append `"keyword"` to `_meta.degraded`; fall through to dense-only result set. `_meta.hybrid: false`. |
| **`ontologyVersion` mismatch mid-query** | Resolver reports version change during the query's lifetime | Cache `InvalidateStale(currentVersion)`. Do NOT re-run — return current result with `_meta.ontologyVersion` reflecting the version the query resolved against. Caller responsibility to re-query if needed. |
| **Graph expansion failure** (`OntologyGraph.TraverseLinks` returns empty or throws) | Exception caught in `BoundedGraphExpander` | Log warning; append `"graphExpansion"` to `_meta.degraded`; return results without `expandedContext`. |

- **DR-14 — Graceful degradation for all four identified failure classes.**
  **Acceptance criteria:** (a) no failure class throws to the MCP caller — all surface via `_meta.degraded[]`; (b) `_meta.hybrid` and `_meta.reranked` accurately reflect what actually ran, not what was requested; (c) integration tests simulate each failure class and assert the response shape; (d) OpenTelemetry `retrieval.degraded` counter increments on each; (e) documentation (tool description in MCP metadata) names `_meta.degraded` as the caller-observable signal.

---

## 11. Cross-repo implementation map

Sequencing constraint: **Strategos 2.6.0 must ship before Basileus can start.** Mirrors the Strategos 2.5.0 → Basileus Ontology MCP Endpoint relationship from ADR §6.2.

### Strategos 2.6.0

- NEW `IKeywordSearchProvider` + `KeywordSearchRequest` / `KeywordSearchResult` contracts (DR-1)
- NEW `RankFusion.Reciprocal` utility (DR-2)
- NEW `HybridQueryOptions` parameter on `OntologyQueryTool.QueryAsync` (DR-3)
- Parent issue: strategos#NEW-hybrid-retrieval-seams (to file)
- Release target: 2.6.0 cut post-2.5.0

### Basileus

- NEW `PostgresTsVectorKeywordSearchProvider` (DR-4) — ingest design §4.4 metadata schema lights the index
- NEW `CohereReranker : IReranker` (DR-5) — existing `IReranker` abstraction (`shared/Basileus.AgentHost.Abstractions/DataFabric/IReranker.cs`)
- NEW `IGraphExpander` + `BoundedGraphExpander` (DR-6)
- NEW `IOntologyVersionedCache<TKey, TValue>` + `MemoryOntologyVersionedCache` (DR-7)
- NEW `AzureAiSearchKeywordSearchProvider` fallback (DR-8) — gated off
- UPDATE Basileus `OntologyQueryTool` DI wiring to pass `HybridQueryOptions`
- UPDATE MCP `inputSchema`/`outputSchema` on the `ontology_query` descriptor (DR-10, DR-11)
- NEW qrel set + benchmark harness (DR-12)
- NEW OTel metrics + burn alerts (DR-13)
- Parent issue: basileus#NEW-hybrid-retrieval-composition (to file, blocked by Strategos 2.6.0)

### `Strategos.Contracts`

- NEW TypeSpec models for `KeywordSearchRequest` / `KeywordSearchResult` / `HybridQueryOptions` / `ExpandedContext` (ship via basileus#152 + exarchos#1125 pipeline)

### Exarchos

- UPDATE `exarchos_sync` action surface — `query_fabric` proxy wiring to pass through the new `precision` / `followLinks` / `chunkLevel` / `provenance` parameters (exarchos#1125-adjacent)
- UPDATE Exarchos-side schema cache to invalidate on `_meta.ontologyVersion` mismatch (ADR §2.12; already committed — this design just consumes it)

---

## 12. Consequences

### Positive

- **Ontology MCP Endpoint ships with industry-standard retrieval quality.** Hybrid + rerank delivers a measured > 10 nDCG@10 point lift over vector-only on Shape 2 queries, validated against a curated qrel set before merge.
- **Single source of truth preserved.** Marten-owned `SemanticDocument` is authoritative; tsvector index is co-located; no projected sync problem in v1.
- **Azure-native posture.** Cohere Rerank v3.5 serverless on Azure AI Foundry aligns with the existing Azure deployment direction (ADR §2.14, self-hosting-plan). No new ops surface for reranking.
- **Swap path preserved.** `IKeywordSearchProvider` + feature-gated Azure AI Search adapter lets the backend change without calling-site edits if the measurement gate rolls us over.
- **Agent UX hints without hidden behavior.** `_meta.couldBenefitFromLinkExpansion` and `_meta.degraded[]` give external agents the feedback signals to self-correct without classifier-driven surprises.

### Negative / costs

- **Strategos release coupling, again.** Basileus hybrid ships only after Strategos 2.6.0. Same shape of delay as 2.5.0 → Ontology MCP Endpoint.
- **`ts_rank_cd` is inferior to BM25 standalone.** Acceptable only because the reranker absorbs the ranking-quality gap. If Cohere Rerank is persistently degraded (§10), retrieval quality collapses to tsvector-baseline — measurably worse than pure semantic. Mitigation: the burn alerts in §9 fire on sustained degradation.
- **Cohere external dependency.** Even with graceful degradation, a sustained outage degrades all retrieval. No circuit breaker in v1; added consideration for v2 if empirical data justifies.
- **Qrel curation is work.** 75 queries × 2 annotators × Kappa ≥ 0.7 — roughly 2 person-days.

### Neutral

- **`IReranker` abstraction becomes more prominent.** Existing interface in `shared/Basileus.AgentHost.Abstractions/DataFabric/IReranker.cs` now has its first production implementation wired. The stub used for unit tests stays.
- **`ontology_query` becomes the primary retrieval surface.** Existing `ThinkStep` / `OntologyContextAssembler` enrichment path can continue calling `ObjectSet<SemanticDocument>.SimilarTo()` directly for internal use, or route through `ontology_query` for consistency. Design does not mandate the internal migration.

---

## 13. Open questions

1. **Qrel authorship bandwidth.** Who double-annotates the 75 queries? Suggested: feature author + one other engineer, target within Phase 2 of the implementation plan.
2. **BM25 saturation threshold calibration.** `τ` in `HybridQueryOptions.BmSaturationThreshold` defaults to 18.0 as a placeholder. Calibration run against the qrel set is a DR-12 output; the design commits to the calibration step, not the specific number.
3. **Cohere Rerank region.** Azure AI Foundry region selection — match the Basileus AgentHost deployment region for minimum network latency. Defer to deployment config.
4. **`_meta.couldBenefitFromLinkExpansion` detection heuristic.** Currently specified as a keyword pattern match (`references`, `uses`, `implements`, `depends on`, `what calls`, `where is X used`). Might benefit from refinement post-launch based on the `_meta.couldBenefitFromLinkExpansion: true` × `followLinks: false` co-occurrence signal in telemetry.
5. **Interaction with ADR §9.7 ontology-version skew during active workflow.** Mid-query version mismatch is handled in §10; mid-workflow mismatch is handled in ADR §9.7 (re-validate on `enriched → executing`). These two mitigations should compose cleanly — verify during integration testing.
6. **v2 graph expansion depth.** `linkDepth > 1` is rejected in v1. The relevance-drift risk is real; v2 should include a separate benchmark sweep of `linkDepth ∈ {1, 2, 3}` before lifting the cap.
7. **Self-hosted reranker escape valve.** `bge-reranker-v2-m3` on Azure ML Managed Endpoint is documented as the TCO escape valve if Cohere pricing shifts. No design work required in v1; interface contract already supports the swap.

---

## 14. Related

- [ADR: Exarchos ↔ Basileus Coordination Architecture](../adrs/2026-04-18-exarchos-basileus-coordination.md) — §§2.2, 2.8, 2.12, 9.8, 9.9
- [Research: Data Shape → Query Performance and Relevance](../research/2026-04-19-data-shape-query-performance-relevance.md) — §§2.1, 2.4, 2.6, 3.1, 4.2, 5.4
- [Design: Ingest Ontology From Source](./2026-04-19-ingest-ontology-from-source.md) — §§4.4, 4.4.2 (metadata + pgvector HNSW)
- [Research: Ontology Ingestion Cost Analysis](../research/2026-04-19-ontology-ingestion-cost-analysis.md) — cost envelope
- [Data Fabric & Ontology Context](./2026-04-05-data-fabric-ontology-context.md) — three-phase context assembly
- [`IReranker` interface](../../shared/Basileus.AgentHost.Abstractions/DataFabric/IReranker.cs) — existing contract
- [Azure AI Foundry — Cohere Rerank v3.5](https://ai.azure.com/catalog/models/Cohere-rerank-v3.5)
- [Azure AI Search — Hybrid Search Overview](https://learn.microsoft.com/azure/search/hybrid-search-overview)
- [ParadeDB — Hybrid Search in PostgreSQL](https://www.paradedb.com/blog/hybrid-search-in-postgresql-the-missing-manual)
- [Cormack et al. 2009, "Reciprocal Rank Fusion outperforms Condorcet"](https://dl.acm.org/doi/10.1145/1571941.1572114)
