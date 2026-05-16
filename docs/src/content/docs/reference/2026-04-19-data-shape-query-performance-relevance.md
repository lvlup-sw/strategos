---
title: "Research: Data Shape → Query Performance and Relevance in the Ontological Data Fabric"
---

# Research: Data Shape → Query Performance and Relevance in the Ontological Data Fabric

**Date:** 2026-04-19
**Workflow:** `data-shape-query-performance-relevance` (discovery)
**Parent ADR:** `docs/adrs/2026-04-18-exarchos-basileus-coordination.md` §§1.2, 2.2, 2.5, 2.14
**Sibling research:**
- `docs/research/2026-04-19-ontology-ingestion-cost-analysis.md`
- `docs/research/2026-04-19-basileus-saas-workspace-efficiency.md`
- `docs/research/2026-04-18-strategos-ontology-gap-analysis.md`
- `docs/designs/2026-04-19-ingest-ontology-from-source.md`
- `docs/designs/2026-04-05-data-fabric-ontology-context.md`
**External grounding:**
- cAST (EMNLP 2025) — AST-structural chunking for code RAG
- RANGER (ICLR 2026 submission) — repo-level code retrieval with knowledge graph + MCTS
- NVIDIA/BlackRock HybridRAG (arXiv 2408.04948) — graph ∪ vector fusion
- Voyage `voyage-code-3` benchmarks — MRL + quantization on code retrieval
- Matryoshka Representation Learning (Kusupati et al. 2022; arXiv 2205.13147)
- SCIP Code Intelligence Protocol (Sourcegraph)
- pgvector HNSW/IVFFlat/DiskANN comparisons
- Microsoft Learn — Roslyn `Solution.WithDocumentText`, `SymbolKey`, incremental workspace

---

## Executive summary

The ADR commits Basileus to three coexisting data shapes — **OntologyGraph** (small schema, in-memory), **SemanticDocument** (dense-vector chunks, pgvector-indexed), **Marten event streams** (`OntologicalRecord` + `OntologyDelta`) — each of which answers a different query class. Taken separately, each shape is defensible against the prior art. Taken *together*, the ADR does not yet specify the **retrieval composition** that turns those shapes into a coherent query surface for the `ontology_explore` / `ontology_query` / `fabric_resolve` / `ontology_validate` tools.

This research grounds the claim that **five data-shape choices carry most of the variance in query performance and relevance**, and each has a published answer:

1. **Chunk granularity and boundary alignment.** AST-structural chunks (cAST, EMNLP 2025) outperform fixed-size chunks by **1.8–4.3 points Recall@10 on RepoEval** and **+2.67 Pass@1 on SWE-bench**. The current ingestion design (`docs/designs/2026-04-19-ingest-ontology-from-source.md` §4.4) already names file / type / method / doc chunk levels — but does not commit to AST alignment. Committing to AST-aligned boundaries is a retrieval-quality win at zero additional ingestion cost (Roslyn is already parsing).
2. **Symbol-stable identity thread.** The workspace-efficiency research commits to caching by `SymbolKey` because `ISymbol` is not stable across `Compilation` instances. The current `SemanticDocument` shape has no symbol-stable identifier in `Metadata` — only `filePath` and `chunkLevel` strings. SCIP's moniker/descriptor scheme and Roslyn's `SymbolKey` converge on the same lesson: **retrieval identity must be a refactor-stable symbol, not a source string.** The shape fix is additive: three metadata keys (`symbolKey`, `symbolKind`, `symbolFqn`).
3. **Hybrid retrieval composition.** Pure vector retrieval is a 70% solution; hybrid BM25 + vector delivers **+15–30% recall** (industry benchmarks, confirmed on code-retrieval tasks); adding graph traversal on top (RANGER, NVIDIA HybridRAG) adds another quality tier for relationship queries. The ADR's `ontology_query` surface has the `OntologyGraph` (structure) and `SemanticDocument` (semantics) *in the same process* — the missing piece is a fusion layer (Reciprocal Rank Fusion + bounded graph expansion) feeding into a single result set.
4. **Embedding dimensionality and quantization.** `voyage-code-3` achieves **92.28% NDCG@10 at 1024 dims** (vs. OpenAI-v3-large at 77.64%) and retains 91.34% at 256 dims via MRL. The ADR's "1024-full, 512-truncated for index" choice (ingest design §4.4.1) is the right ships-well default, but the research surfaces a specific risk: **MRL recall degrades non-linearly as corpus size grows**. For the 500k-LOC reference workload the risk is negligible; at 10M-LOC monorepos it is real and the index dimensionality should be revisitable per-workspace.
5. **Vector index choice × insert load.** pgvector's HNSW vs. IVFFlat tradeoff is sharp for this workload profile (200 pushes/day × incremental chunk inserts). HNSW handles real-time updates cleanly; IVFFlat degrades with inserts until re-indexed. DiskANN (Azure PostgreSQL preview, Vamana graph) is the third option and solves HNSW's RAM constraint at the cost of Azure lock-in. The shipping default should be **HNSW with a documented migration path to DiskANN** at the scale tier where the 512-dim × 100k-chunk pgvector index exceeds ~1 GB resident.

Two corollary findings:

- **Two-stage retrieval (bi-encoder → cross-encoder reranker) should be specified but not be in the hot path.** Reranking 50–100 candidates adds ~500–1000 ms; the inversion thesis (ADR §1.2) prices agent-query latency at < 30 s steady-state, not < 1 s. A reranker is appropriate for `ontology_query(semanticQuery=...)` when the caller asks for precision, but the default path (fabric enrichment during `ThinkStep`) should be vector-only for latency.
- **Branch-delta graph composition (ADR §2.5 validation gate; ingest design §4.9) should be specified as a memoized `main ⊕ branchDelta` fold with an ontology-version-pinned cache key**, not rebuilt per query. This is already implied in §4.9 but should be lifted to a performance SLO because it is load-bearing for the p50 query latency on feature branches (which is most agent traffic).

**None of these findings invalidate the ADR or the ingestion design.** They are targeted additions to the *retrieval* half of the architecture — the half the ADR describes under the heading "tool surface" but underspecifies on the composition of the surfaces.

---

## Part 1 — The three shapes the ADR commits to

### 1.1 `OntologyGraph` — the authoritative schema

`strategos/src/Strategos.Ontology/OntologyGraph.cs` (read during Phase 1) is an **in-memory, immutable, dictionary-indexed graph** with these primary shapes:

```csharp
IReadOnlyList<DomainDescriptor>         Domains
IReadOnlyList<ObjectTypeDescriptor>     ObjectTypes
IReadOnlyList<InterfaceDescriptor>      Interfaces
IReadOnlyList<ResolvedCrossDomainLink>  CrossDomainLinks
IReadOnlyList<WorkflowChain>            WorkflowChains
IReadOnlyDictionary<Type, IReadOnlyList<string>> ObjectTypeNamesByType
```

With O(1) lookup indices:
- `_objectTypeLookup : (Domain, Name) → ObjectTypeDescriptor`
- `_implementorsLookup : InterfaceName → List<ObjectTypeDescriptor>`
- `_workflowChainLookup : WorkflowName → List<WorkflowChain>`

And a BFS traversal for links (`TraverseLinks(domain, typeName, maxDepth=2)`).

**Query class served:** *structural* — "what types implement this interface", "what links exit this type", "what are the subtypes of X", "what workflows target this step". These are the queries that `ontology_explore` and the non-semantic facet of `ontology_query` fulfill.

**Shape implications:**
- **Query latency:** O(1) hash lookup; O(links × depth) BFS. Sub-millisecond for the 500-LOC reference workload (dozens of object types).
- **Relevance:** deterministic — the graph is ground truth for the domain. No ranking needed.
- **Completeness constraint:** the graph must actually contain the types. This is the load-bearing problem the ingestion pipeline solves (ADR §1.3; ingest design §5).
- **Refactor-stability:** Types are keyed by `(DomainName, Name)`, not by source file. Renames propagate through `OntologyDelta.RenamePropertyDelta` events (ingest design §3.2). Moves do not affect the graph key.

### 1.2 `SemanticDocument` — the similarity surface

`shared/Basileus.Core/DataFabric/SemanticDocument.cs`:

```csharp
public sealed record SemanticDocument
{
    public required string Id { get; init; }
    public required string Content { get; init; }          // embedded text
    public required string ObjectType { get; init; }       // "{domain}.{typeName}"
    public required string SourceDomain { get; init; }
    public ImmutableDictionary<string, string> Metadata { get; init; }
    public ReadOnlyMemory<float> Embedding { get; init; }
    public DateTimeOffset IndexedAt { get; init; }
}
```

**Query class served:** *semantic similarity* — "find code that does X" via vector nearest-neighbor. Feeds `ontology_query(semanticQuery=..., topK=..., minRelevance=..., distanceMetric=...)`.

**Shape implications:**
- **Query latency:** O(log n) for HNSW / O(lists-probed × cluster-size) for IVFFlat. 10–50 ms at the 10k-chunk reference workload.
- **Relevance:** embedding-quality-dependent. `voyage-code-3` at 1024 dims achieves 92% NDCG@10 on code retrieval; MRL truncation to 512 dims retains ~92%; at 256 dims ~91% (Voyage benchmark, Dec 2024).
- **Refactor-stability:** brittle on content — a whitespace-only reformat changes the embedded text and (without normalization) hashes. The chunk cache (§4.3.1 of ingest design) normalizes via Roslyn `SyntaxTree`/`SyntaxTrivia`, which fixes content-level drift. But the `SemanticDocument.Id` itself — currently unspecified in the ADR but implicitly derived from the chunk boundary — is the weak point: a method rename changes the chunk boundary's *name*, and `ObjectType = "{domain}.{typeName}.{methodName}"` becomes stale.
- **Granularity:** the ingest design names four levels (file / type / method / doc comment). Research (cAST, EMNLP 2025) shows AST-aligned chunks deliver measurable retrieval quality gains over fixed-size chunks.

### 1.3 Marten event streams — the historical surface

Two distinct streams per workspace-scoped entity:
1. **`OntologicalRecord`** (ADR §2.5) — Marten event-sourced aggregate. Stream keyed by `recordId`; events: `IntentProposed`, `IntentValidated`, `IntentEnriched`, `IntentExecuting`, `IntentCompleted`, `IntentFailed`. Wolverine saga drives the state machine. A summary is indexed into a `SemanticDocument` for search.
2. **`OntologyDelta`** (ingest design §3.2) — per-workspace ontology-ingest stream, plus per-branch delta streams. Events: `AddObjectTypeDelta`, `UpdateObjectTypeDelta`, `RemoveObjectTypeDelta`, `AddPropertyDelta`, `RenamePropertyDelta`, `RemovePropertyDelta`, `AddLinkDelta`, `RemoveLinkDelta`.

**Query class served:** *temporal* — "what did the ontology look like at commit SHA X", "how did this feature's record transition", "who touched this node and when". Marten's `AggregateStreamAsync<T>(id, version)` gives deterministic time-travel (context7 results on Marten projections confirmed this pattern).

**Shape implications:**
- **Query latency:** live aggregation is O(events_in_stream); snapshot projections are O(1) read + daemon lag. The confirmed Marten pattern for query-time freshness is `await store.WaitForNonStaleProjectionDataAsync(timeout)` before loading the projection.
- **Relevance:** by construction — the stream *is* the truth. The ontology-graph-at-a-version is reconstructible from the stream prefix.
- **Refactor-stability:** events are immutable. The stream carries the full history through renames and moves.
- **Branch scoping:** main baseline + per-branch delta streams (ingest design §4.9). Query-time composition is `main ⊕ branchDelta`, memoized per `(workspace, branch, mainVersion, branchVersion)` tuple.

### 1.4 The shape triangle

```
                OntologyGraph
                (structural)
                      ▲
                      │   schema lookups
                      │
        ┌─────────────┼─────────────┐
        │             │             │
        │             │             │
 SemanticDocument ◄───┼───► Marten event streams
 (similarity)         │      (temporal)
                      │
              ontology_query (hybrid)
              fabric_resolve (action dispatch)
              ontology_explore (schema walk)
              ontology_validate (gate)
```

The MCP tools the ADR exposes at `/mcp/ontology` each pull from one or more of these shapes:

| Tool | OntologyGraph | SemanticDocument | Marten streams | Composition |
|---|:-:|:-:|:-:|---|
| `ontology_explore` | ✓ | — | — | Pure structural |
| `ontology_query` (filter+links) | ✓ | — | (for time-travel) | Structural |
| `ontology_query` (semanticQuery) | ✓ (to scope) | ✓ | — | **Hybrid** — needs composition |
| `ontology_validate` | ✓ | — | ✓ (for blast radius) | Structural + temporal |
| `fabric_resolve` | ✓ (to resolve action) | — | ✓ (to read live state) | Structural + live dispatch |

The underspecified cell is **`ontology_query` with `semanticQuery` set**. Today the ADR says `semanticQuery, topK, minRelevance, distanceMetric` are parameters; it does not say how vector results interact with the `objectType` filter, link-traversal narrowing, or interface narrowing that the same tool accepts. §4 of this research proposes the composition.

---

## Part 2 — How data shape informs query performance

Performance is what the user pays at query time. The ADR's latency budget (ADR §2.14: p50 10 s / p95 30 s for propagation; §1.2 implies query latency well inside that) caps the tolerable query cost. Each shape choice has a published performance profile.

### 2.1 Chunk granularity × retrieval latency

The ingest design (§4.4) chunks at four levels — file / type / method / doc comment — writing **one `SemanticDocument` per chunk, per level**. At the reference workload (500k LOC, ~50 projects), this produces ~10,000 chunks total (cost research §1.1).

**Performance implication:** 10k chunks is trivial for HNSW or IVFFlat — 10–50 ms query latency (pgvector benchmarks: Mastra 2025, Zylos 2026). **But chunk count grows linearly with code volume, and a 10M-LOC monorepo would produce ~200k chunks.** At that scale, index type starts to matter:

| Index | 10k chunks | 200k chunks | Insert latency (per chunk) |
|---|---|---|---|
| `pgvector` flat | 50–200 ms | 1–4 s | ~0.1 ms |
| `pgvector` IVFFlat (lists=200) | 20–40 ms | 80–200 ms | ~1 ms; degrades on inserts |
| `pgvector` HNSW (m=16, ef=64) | 10–30 ms | 30–80 ms | ~5 ms; stable on inserts |
| Azure DiskANN (preview) | 10–30 ms | 30–80 ms | ~5 ms; stable; disk-resident |

HNSW is the default choice for this workload — the reference pushes rate (200/day × ~10 novel chunks post-cache) is ~2000 inserts/day, which HNSW handles without re-indexing. IVFFlat would need periodic `REINDEX CONCURRENTLY` and drift in recall between reindexes. (LinkedIn post by Sahgal, March 2026; PIXION 2024 pgvector writeup; DEV Community 2026 index comparison all converge on this conclusion.)

**Recommendation:** specify `USING hnsw` as the default index type in the Marten schema registration for the chunk-vector collection. Make it a workspace manifest override for customers with known-stable datasets.

### 2.2 AST alignment × retrieval quality

The cAST paper (EMNLP 2025) is the clearest published result:

> *"All models show gains of 1.2–3.3 points in Precision and 1.8–4.3 in Recall on code-to-code retrieval (RepoEval), and 0.5–1.4 in Precision and 0.7–1.1 in Recall on NL-to-code retrieval (SWE-Bench)."*

The delta comes from two structural properties AST-chunks preserve that fixed-size chunks violate:

1. **Syntactic integrity** — a method body is not split across chunks, so embeddings are computed on self-contained semantic units. Fixed-size chunks frequently cut mid-function, losing the context a retriever needs to match on intent.
2. **Metadata retention** — AST-aligned chunks naturally carry file / class / function metadata as first-class fields, not as string substrings. This is exactly the `ObjectType = "{domain}.{typeName}.{methodName}"` scheme the ingest design uses — but only if the chunk *boundary* respects the AST, the scheme works. Fixed-size chunks produce "ObjectType half-method, half-next-method" which fails both structurally and semantically.

**Performance implication:** AST alignment at chunk time costs *nothing extra* in this codebase — Roslyn is already producing a `SyntaxTree` for every Document in the workspace (ingest design §4.1). Walking declaration nodes (`TypeDeclarationSyntax`, `MethodDeclarationSyntax`, `PropertyDeclarationSyntax`, XML doc trivia) is O(nodes) on an already-materialized tree. The cost is the ingester's marginal traversal time, which was already paid.

**Recommendation:** add a §4.4 clause to the ingest design committing chunk boundaries to AST-declaration granularity (`TypeDeclarationSyntax`, `MethodDeclarationSyntax`, `XmlElementSyntax` for doc comments). File-level chunks are the only level allowed to exceed a single declaration, and only when the file is under a configurable `MaxChunkTokens` threshold.

### 2.3 Symbol-stable identity × refactor survival

The workspace-efficiency research commits to `SymbolKey` caching because:

> *"`ISymbol` instances are not stable across `Compilation` instances. Any cache keyed on `ISymbol` will silently produce wrong results after a workspace mutation."*

The same constraint applies to *retrieval identity* at query time. If a `SemanticDocument` is keyed only by source-string `ObjectType = "{domain}.TradeOrder.Execute"` and the method is renamed to `Submit`, the following happens:

1. The old `SemanticDocument` is orphaned — nothing will ever hit it because no query will ask for `TradeOrder.Execute` anymore.
2. A new `SemanticDocument` is inserted at `TradeOrder.Submit` — paid for with a fresh embedding.
3. The content-hash chunk cache (§4.3.1 of ingest design) may hit (if the method body is unchanged), saving the embedding cost. But the *document* is new.
4. Historical queries ("what was TradeOrder.Execute") have no way to find the old doc.

SCIP's solution — the same one Roslyn uses internally — is to key documents by **symbol moniker/descriptor**, not by name. The SCIP protocol's `Descriptor` field is:

```proto
message Descriptor {
  // e.g., "mypackage/MyClass#myMethod().", a refactor-stable moniker
  string name = 1;
  Suffix suffix = 2;  // Namespace, Type, Method, Parameter, ...
}
```

Roslyn's `SymbolKey.ToString()` produces a serialized form suitable for the same role within a single solution. For cross-solution stability (Basileus may analyze multiple repos per workspace), SCIP-style descriptors are the prior art.

**Shape fix (additive):** extend `SemanticDocument.Metadata` (which is already `ImmutableDictionary<string, string>`) with:

| Key | Value | Role |
|---|---|---|
| `symbolKey` | Roslyn `SymbolKey.ToString()` output | In-process identity |
| `symbolKind` | `"NamedType" \| "Method" \| "Property" \| ...` | Filter scope at query time |
| `symbolFqn` | `"namespace.Type.Member"` | Human-readable / display |
| `contentHash` | existing (§4.3.1) | Embedding dedup |
| `filePath` | existing | Navigation |
| `chunkLevel` | existing | Scope |

This is additive — no breaking change to `SemanticDocument`. Queries against `symbolKey` become refactor-stable. Historical streams can join old and new `SemanticDocument` rows on `symbolKey` through a rename event.

**Performance implication:** `SymbolKey.ToString()` is stable-hash-serializable and suitable as a Marten btree index key. The join cost is O(log n). No new infrastructure.

### 2.4 Hybrid retrieval composition × relevance

The industry numbers converge on a tight range:

| Approach | Recall improvement vs. next-cheapest | Source |
|---|---|---|
| Dense vector only | 85% baseline quality | Brenndoerfer 2026; Kumar 2026 |
| BM25 + dense with RRF | **+15–30%** | Kumar 2026; Nawaz 2025 |
| BM25 + dense + graph traversal | **+15–30% above hybrid** | NVIDIA HybridRAG (arXiv 2408.04948); Shereshevsky 2026 |
| RANGER-style MCTS on KG | outperforms Qwen3-8B dense embeddings on CodeSearchNet, RepoQA | RANGER (ICLR 2026 submission, OpenReview EPTVoeaz7Y) |

The **GraphRAG / HybridRAG finding** — that a knowledge-graph layer above vector retrieval delivers compounding quality on relationship questions — is directly applicable to Basileus because the `OntologyGraph` *is* the knowledge graph, and the `SemanticDocument` corpus *is* the vector layer. The architectural pieces exist; what's missing is the **fusion spec**.

**Proposed fusion for `ontology_query(semanticQuery=..., objectType=..., branch=...)`:**

```
1. Structural pre-filter (OntologyGraph):
   - Resolve objectType to ObjectTypeDescriptor (and subtypes via GetSubtypes)
   - Expand to Link-related types if follow-links=true
   - Produce allowed_object_types: Set<string>

2. Parallel retrieval:
   2a. Dense:  pgvector search with filter (objectType in allowed_object_types)
              → top-K_d candidates (K_d = 50 default)
   2b. Sparse: BM25/Postgres full-text search with same filter
              → top-K_s candidates (K_s = 50 default)

3. RRF fusion (Reciprocal Rank Fusion, k=60):
   - combined_score(doc) = sum over retrievers of 1/(k + rank_i)
   - Return top-K fused (K = topK param default 10)

4. Optional graph expansion (follow-links=true):
   - For each top result with an ObjectType:
       enumerate N_1 links via OntologyGraph.TraverseLinks(maxDepth=1)
       fetch representative chunks for each linked type
       append as context-only (not re-ranked)

5. Optional rerank (precision=true):
   - If caller asks for precision, cross-encode top-K fused with bge-reranker-v2-m3
   - Budget: K=20 × ~15ms = ~300ms added latency
```

This is a **compositional shape** — it reuses the three shapes Basileus already has, adds one BM25 index (Postgres `tsvector` in the same Marten schema), and one RRF merge step (~100 lines). No new infrastructure.

**Performance implication:**
- Steps 1, 2a, 3 are the hot path: ~30 ms p50 at the reference workload.
- Step 2b adds ~10–30 ms depending on corpus size (Postgres full-text is well-characterized).
- Step 4 adds ~5–20 ms of graph traversal (BFS depth 1, in-memory).
- Step 5 is opt-in and +300–1000 ms — appropriate for `ontology_query(precision=true)` but not the default fabric-enrichment path during `ThinkStep`.

### 2.5 Dimensionality × storage × recall

`voyage-code-3` published numbers (December 2024 blog post; confirmed via MTEB leaderboard PR embeddings-benchmark/results#61):

| Dimensions | `voyage-code-3` NDCG@10 | OpenAI v3-large | CodeSage-large |
|---|---|---|---|
| 2048 | 92.12% | — | 91.95% |
| 1024 | 92.28% | 77.64% | 90.71% |
| 512 | **92.00%** | — | — |
| 256 | 91.34% | 73.68% | 83.29% |

The ADR's default — **1024-full stored in cache, 512-truncated for the pgvector index** — targets the 92.00% retention point at 2× storage savings. This is the right shipping default.

**But a subtlety surfaced by the Reddit discussion of MRL limits (arXiv 2510.19340, March 2026):**

> *"MRL truncated vectors struggle as corpus size increases. It depends on how aggressively vector size is reduced."*

At the reference workload (10k chunks), there is no corpus-size concern. At 10M LOC / ~200k chunks, the 512-dim truncation may lose recall on hard negatives. The fix is **per-workspace dimensionality** — customers with large monorepos can opt into the full 1024-dim index at 2× storage cost. The workspace manifest already supports this shape (ingest design §4.4.1).

**Recommendation:** surface `ingestion.embedding.indexDimensions` in `workspace.yml` with `512` default and `1024` documented escape valve. Do not make it a runtime query parameter — dimensionality must match between embed and query for cosine similarity to be meaningful.

### 2.6 Reranking × latency budget

Two-stage retrieval (bi-encoder then cross-encoder) is the industry default for precision-critical RAG. The published cost model (Ho 2026 in Towards Data Science; Hakim 2026):

| Stage | Latency per query | Typical budget |
|---|---|---|
| Vector search (top-50) | 50–150 ms | Always paid |
| BM25 (top-50) | 20–80 ms | Only if hybrid |
| RRF fusion | ~1 ms | Always cheap |
| Cross-encoder rerank of 50 candidates | 500–1000 ms | Opt-in |
| LLM reranker (e.g., bge-reranker-v2) | 300–800 ms | Opt-in |

**Applied to this codebase:** the agent's steady-state fabric-enrichment query (fabric `ontology_query` during `ThinkStep`) budgets for sub-second latency per §2.1's implicit reading of ADR §1.2. A 1-second reranker tax on every enrichment query compounds: five enrichments per task × ten tasks per feature × ten features per day = 500 reranks per day per workspace. At ~800 ms each, that is 6–7 minutes of cumulative latency per workspace per day. Small in absolute terms; noticeable per-interaction.

**Recommendation:** specify reranking as a **caller-controlled `precision: bool`** parameter on `ontology_query`, default `false` for fabric-enrichment calls, default `true` for explicit user-facing queries (e.g., `/discover semantic-search`). The cross-encoder model choice is left to the `IReranker` abstraction already present in `Basileus.AgentHost.Abstractions/DataFabric/IReranker.cs`.

---

## Part 3 — How data shape informs relevance

Relevance is the quality the user gets *back* from a query. Three shape choices move the needle.

### 3.1 Granularity: query type determines optimal chunk level

The four chunk levels (ingest design §4.4) are not interchangeable. A query's natural relevance tier depends on the query shape:

| Query intent | Natural chunk level | Why |
|---|---|---|
| "what methods compute risk exposure" | **method** | Returns callable units, not file scaffolding |
| "where is Position defined" | **type** | One chunk per type; minimal noise |
| "what is the trading domain's public API" | **file** (if file-per-type) or **type** (multi-type files) | Coarser unit for overview |
| "how does the trading strategy work" (NL exploratory) | **file + doc comment** | Prose explanation beats code body |

**Shape implication:** returning an unbiased union of all four chunk levels with RRF is *wrong* — file-level chunks will dominate short-text queries because they have more tokens to match on, and method-level chunks will dominate specific queries. The published fix (cAST §4.3) is **per-level relevance thresholding** or **query-type classification before retrieval**.

**Pragmatic alternative:** expose `chunkLevel` as a filter parameter on `ontology_query` — default `any`, narrows to `method`/`type`/`file`/`doc` on caller request. This is a cheap shape fix (one more SQL `WHERE` clause) that gives callers control without the complexity of a query classifier.

### 3.2 Branch scoping: relevance is always branch-conditional

The ingest design §4.9 and ADR §2.5 both commit to **main + per-branch delta** as the storage shape. The retrieval implication is that relevance is *always* evaluated against the caller's branch-scoped graph, not against main.

The composition cost model:

```
On query with branch=feature/foo:
  composed_graph = main_graph ⊕ branch_delta_graph   (cached per (workspace, branch, mainVersion, branchVersion))
  composed_embeddings = main_embeddings ∪ branch_delta_embeddings  (no cache; query both tables with UNION)
```

**Graph composition is memoized** (ADR §2.12's `ontologyVersion` is the cache key component). At the reference workload (15 open PRs), the cache holds the full set of composed graphs in ~15 entries of <10 MB each; LRU eviction is irrelevant.

**Embeddings are not memoized.** pgvector queries return rows; UNION-ing two pgvector tables is a cheap Postgres operation. The only relevance cost is that a chunk may appear in both main and branch delta (the branch modified a file main still has) — the query must deduplicate by `symbolKey`, preferring the branch version.

**Shape implication:** `SemanticDocument` needs a `branchStream` identifier in Metadata (or live in distinct streams as the ingest design specifies) so queries can determine branch ownership and deduplicate.

### 3.3 Provenance: hand-authored vs. ingested as a relevance signal

The ingest design §3.4 adds `DescriptorSource { HandAuthored, Ingested }` to every descriptor. This is a **first-class relevance signal** — a hand-authored action's `Description()` text is curated intent; an ingested type's description is a mechanically-extracted XML doc comment.

For semantic search, this matters because:
- Hand-authored content typically describes *why* something exists (intent).
- Ingested content typically describes *what* it is (mechanical).
- User queries are split between the two — "why do we validate orders at submission" (intent) vs. "find the validate-order method" (mechanical).

**Shape implication:** boost or demote results by provenance in the RRF fusion stage. The cheap form: add a `provenance` field to the fusion scoring (hand-authored: ×1.2, ingested: ×1.0, on a normalized scale). The expensive form: classify the query intent and route to the appropriate corpus subset.

**Recommendation for v1:** pass `provenance` through in the `SemanticDocument.Metadata` but don't score on it yet. Capture it in the `FabricQueryData` audit event (ADR §4.2) so we have the evaluation data to inform the v2 scoring rule.

---

## Part 4 — Proposed tool composition spec

The ADR §2.2 table enumerates the tool surfaces; this research proposes the internal composition for each.

### 4.1 `ontology_explore` — pure structural

No changes. Uses `OntologyGraph` directly. Already covered by the existing `OntologyExploreTool` in Strategos.

### 4.2 `ontology_query` — hybrid

```
Inputs: objectType?, filter?, semanticQuery?, topK=10, minRelevance?,
        distanceMetric="cosine", branch?, followLinks=false,
        chunkLevel=any, precision=false, provenance=any

1. If objectType given: resolve via OntologyGraph (O(1))
2. If no semanticQuery: return filter results from ObjectSet<T> (existing code path)
3. Structural pre-filter: allowed_object_types = resolved type ∪ subtypes [∪ linked types if followLinks]
4. Parallel:
   4a. pgvector search (HNSW) within allowed_object_types filter → top-50 (dense)
   4b. Postgres tsvector full-text within same filter → top-50 (sparse)
5. RRF merge (k=60) → top-K ranked list
6. If chunkLevel != any: filter by Metadata.chunkLevel
7. If precision=true: cross-encode top-K via IReranker
8. Emit FabricQueryData event (ADR §4.2) with provenance signals captured
9. Return results with _meta.ontologyVersion, _meta.branchVersion (for cache-key threading)
```

### 4.3 `ontology_validate` — structural + historical

No changes to the Strategos `OntologyValidateTool` shape. Its internal `EstimateBlastRadius` and `DetectPatternViolations` already traverse the `OntologyGraph`. Branch-scoped evaluation (ADR §2.5) requires the composed `main ⊕ branchDelta` graph — the composition cache in §2.2 of this research covers it.

### 4.4 `fabric_resolve` — structural + live dispatch

No data-shape changes. `IActionDispatcher.DispatchReadOnlyAsync` (ADR §2.10.1) handles the shape; this research touches only the relevance of what's dispatched. Recommendation: for multi-action dispatches, parallelize per ADR §2.6 routing, and return aggregated `ActionDispatchResult[]` as the ingest design §4.3 spec already does.

### 4.5 `intent_register` — write-only

No changes. Writes to Marten event stream per ADR §2.5.

---

## Part 5 — Open questions for `/ideate` follow-up

These are design-level questions the research surfaces but does not resolve. They should feed a `/ideate` cycle if the ADR is revised (v3) or if a new design doc is spun up for the retrieval layer.

### 5.1 Should Basileus ship a BM25/tsvector index alongside pgvector?

**Tradeoff:** +15–30% recall for hybrid vs. +1 Marten collection + Postgres full-text index config. The Postgres side is near-free (tsvector is native); the Marten side is a new collection definition. Estimate: ~2 days of engineering. Shippable alongside the initial Ontology MCP Endpoint epic (ADR §6.2 new).

### 5.2 Should `SemanticDocument` gain a richer identity shape?

The current design has `Id: string` with unspecified derivation. Proposed: `Id = sha256(workspaceId, branch, symbolKey, chunkLevel, contentHash)`. This makes the ID a function of the symbol-identity + content — refactor-stable through renames (via symbolKey), change-aware through contentHash. Cost: schema migration (one-time) + DI wiring. Blocks nothing; delivers refactor-stable retrieval.

### 5.3 Is the `OntologicalRecord`'s `SemanticDocument` summary indexed at the right granularity?

ADR §2.5 says the record's *summary* is indexed for search while the structured layers live in the event stream. This is the right shape, but the summary's content (what text gets embedded?) is unspecified. Proposed: concatenate `feature title + design description + affected ontology nodes + top 3 validation findings` as the embedding source. Cost: one helper in `IOntologicalRecordService`. Pay-off: `ontology_query(semanticQuery="past records about routing changes")` becomes useful.

### 5.4 How does the `ontologyVersion` propagate through retrieval caches?

ADR §2.12 pins the graph version into `_meta.ontologyVersion` on every response. For the retrieval caches proposed here (composed graph; RRF result cache; reranker result cache), the cache key must include `ontologyVersion` — otherwise a graph mutation leaves stale results reachable. This is a small but load-bearing piece of plumbing. Proposed: a `IOntologyVersionedCache<TKey, TValue>` abstraction in `Basileus.AgentHost.Abstractions` that always takes `ontologyVersion` as part of the key.

### 5.5 Cross-workspace semantic search: deferred but surfaces real demand

The ingest design §4.3.1 scopes the chunk-embedding cache per-workspace to avoid customer-A/customer-B leakage. The same scoping applies to retrieval: `ontology_query` cannot currently span workspaces. For multi-repo customers (a common enterprise shape), this becomes a request. Prior art: Sourcegraph's `repogroup:` search, Cursor's multi-repo index. Requires explicit sharing policy and tenant-isolation audit. **Out of scope for this research; flagged as a v2 product question.**

---

## Part 6 — Source provenance

### 6.1 Research papers (peer-reviewed or preprint)
- Zhang et al., *cAST: Enhancing Code Retrieval-Augmented Generation with Structural Chunking via Abstract Syntax Tree*. EMNLP 2025 Findings. `https://aclanthology.org/2025.findings-emnlp.430/`, `https://arxiv.org/html/2506.15655v2`
- RANGER, *Repository-Level Agent for Graph Enhanced Retrieval*. ICLR 2026 submission. `https://openreview.net/forum?id=EPTVoeaz7Y`
- Gandhi et al., *Repository-level Code Search with Neural Retrieval Methods*. CMU 2025. `https://arxiv.org/pdf/2502.07067`
- NVIDIA/BlackRock, *HybridRAG: Integrating Knowledge Graphs and Vector Retrieval*. `https://arxiv.org/abs/2408.04948`
- Kusupati et al., *Matryoshka Representation Learning*. NeurIPS 2022. `https://arxiv.org/abs/2205.13147`
- MRL limits under scale. `https://arxiv.org/pdf/2510.19340`
- Yamaguchi et al., *Modeling and Discovering Vulnerabilities with Code Property Graphs*. IEEE S&P 2014. (Joern/CPG foundation.)

### 6.2 Vendor documentation and benchmarks
- Voyage AI, *voyage-code-3*. Dec 2024. `https://blog.voyageai.com/2024/12/04/voyage-code-3/`
- Voyage AI, *Text Embeddings overview*. `https://docs.voyageai.com/docs/embeddings`
- OpenAI, *New embedding models and API updates*. Jan 2024.
- Sourcegraph, *SCIP Code Intelligence Protocol*. `https://github.com/sourcegraph/scip`
- Weaviate, *Matryoshka Embeddings in Weaviate*. `https://weaviate.io/papers/paper21`
- Microsoft Learn, *Roslyn Solution.WithDocumentText*. `https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.solution.withdocumenttext`
- Microsoft Learn, *Work with a workspace (Roslyn)*. `https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/work-with-workspace`

### 6.3 Industry writeups and benchmarks
- Brenndoerfer, *Hybrid Search: BM25 + Dense Retrieval Combined*. Jan 2026. `https://mbrenndoerfer.com/writing/hybrid-search-bm25-dense-retrieval-fusion`
- Kumar, *Building Hybrid Search That Actually Works*. Jan 2026. `https://ranjankumar.in/building-a-full-stack-hybrid-search-system-bm25-vectors-cross-encoders-with-docker`
- Mastra, *Benchmarking pgvector RAG performance*. Feb 2025. `https://mastra.ai/blog/pgvector-perf`
- Zylos Research, *pgvector Performance & Optimization 2025*. Jan 2026. `https://zylos.ai/research/pgvector-optimization-2025`
- Sahgal, *HNSW vs IVFFlat for Production AI*. March 2026. LinkedIn post referencing Azure DiskANN preview.
- McClarence, *pgvector Index Selection*. April 2026. `https://medium.com/@philmcc/pgvector-index-selection-ivfflat-vs-hnsw-for-postgresql-vector-search-6eff26aaa90c`
- Ho, *Advanced RAG Retrieval: Cross-Encoders & Reranking*. April 2026. Towards Data Science.
- Shereshevsky, *Hybrid Vector-Graph Retrieval Patterns*. Jan 2026. `https://medium.com/graph-praxis/hybrid-vector-graph-retrieval-patterns-11fdbd800e3e`
- NetApp, *Hybrid RAG in the Real World: Graphs, BM25, and the End of Black-Box Retrieval*. Jan 2026.

### 6.4 Repo-local context (Phase 1 gathering)
- `docs/adrs/2026-04-18-exarchos-basileus-coordination.md` §§1.2, 2.2, 2.5, 2.11, 2.12, 2.14
- `docs/designs/2026-04-19-ingest-ontology-from-source.md` §§3.1–3.4, 4.1, 4.3–4.4, 4.9
- `docs/research/2026-04-19-ontology-ingestion-cost-analysis.md` §§1.1–1.7, 2.1–2.5, 4
- `docs/designs/2026-04-05-data-fabric-ontology-context.md` §§2, 3 (three-phase assembly)
- `shared/Basileus.Core/DataFabric/SemanticDocument.cs` (current shape)
- `shared/Basileus.AgentHost.Abstractions/DataFabric/{IActionDispatcher,IReranker,IToolInvoker,SemanticSearchResult,ContextQualityTier}.cs`
- `strategos/src/Strategos.Ontology/OntologyGraph.cs` (current lookup shape)

### 6.5 Issue references
- basileus #114 (IActionDispatcher), #70 (semantic RAG), #145 (data fabric ingestion), #147 (Phronesis Code Review), #152 (Strategos.Contracts), #112 (Cross-Tier Event Bridge)
- strategos #16 (Ontology), #23, #37 (IOntologySource)
- exarchos #1109, #1125

---

## Part 7 — Recommended next actions

This discovery surfaces five targeted additions to the retrieval half of the architecture. Each maps cleanly to an `/exarchos:ideate` or a targeted revision.

1. **Extend `SemanticDocument.Metadata` with `symbolKey`/`symbolKind`/`symbolFqn`.** Ingest design §4.4 + `SemanticDocument` shape change. Refactor-stable retrieval identity. ~2 days engineering; no new infrastructure.
2. **Commit chunk boundaries to AST declarations in ingest design §4.4.** Recall-quality win at zero added cost. Update ingest design §4.4 (not a code change; it's the spec the implementation already wants to follow).
3. **Specify `ontology_query` internal composition (hybrid + RRF).** Proposed spec in §4.2 of this research. Requires one new Marten collection (`tsvector` index) and a ~100-line RRF merge. ~1 week engineering including tests.
4. **Pin `pgvector USING hnsw` as the default index in Marten chunk-vector schema.** One line of schema configuration. Document DiskANN migration path for scale-tier customers.
5. **Specify the `IOntologyVersionedCache<TKey, TValue>` abstraction and apply to composed-graph and RRF result caches.** Plumbing that enforces the ADR §2.12 version-invalidation contract at every retrieval cache. ~3 days.

These additions do **not** change the ADR's thesis or boundary decisions. They specify the retrieval layer the ADR underspecifies. Each is independently shippable; together they move the `/mcp/ontology` tool surface from "vector-first best effort" to "hybrid-retrieval production default."

If the user agrees these are valuable, the next workflow is either:
- `/exarchos:ideate retrieval-composition-for-ontology-mcp` — a design doc covering actions 2, 3, 5 (the compositional work).
- Or a revision pass on `docs/designs/2026-04-19-ingest-ontology-from-source.md` folding actions 1, 2, 4 directly into the ingestion spec.

Action 3 is the largest delta and most deserves its own design cycle.
