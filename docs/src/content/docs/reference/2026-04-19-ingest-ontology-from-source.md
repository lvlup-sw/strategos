---
title: "Design: Ingest Ontology From Source"
---

# Design: Ingest Ontology From Source

**Date:** 2026-04-19
**Status:** Draft (ideate output)
**Workflow:** `ingest-ontology-from-source` (feature, ideate phase)
**Parent ADR:** `docs/adrs/2026-04-18-exarchos-basileus-coordination.md` §1.3, §6.2
**Research inputs:** `docs/research/2026-04-18-strategos-ontology-gap-analysis.md`

---

## 1. Context and thesis

The coordination ADR's top-down inversion (§1.2) depends on ontology completeness (§1.3). Agents stop grep-based codebase archaeology and start ontology-query-based lookup — but only if the ontology covers the domain. Hand-authored coverage does not scale: new types ship without registration, refactors silently diverge, and value is left on the table as codebases grow.

This design specifies a **continuous source-repository ingestion pipeline** that keeps both the ontology schema layer and the semantic fact layer current with source evolution. No generated files in the build. No CLI scaffolder developers have to remember. No drift window between source truth and ontology truth.

The load-bearing insight from the `/ideate` cycle is that ontological knowledge has **three distinct streams with different cadences and sources**. Conflating them produces the dual-authoring architectures that violate DIM-1 single-source-of-truth. Separating them cleanly is what makes this design work.

### 1.1 Three streams, three cadences

| Stream | Source | Cadence | Example content |
|---|---|---|---|
| **Hand-authored intent** | Developer-written `DomainOntology.Define()` in source | Slow-changing, high-value | `obj.Action("Execute").Requires(p => p.Status == Active)`; Lifecycle state machines; Interface mappings; IS-A declarations |
| **Mechanically-discovered schema** | Roslyn analysis of source repositories | Changes on every commit | Type list, property list, C#-inheritance-as-IS-A, key property via `[OntologyKey]`, navigation properties as Links |
| **Semantic facts** | Source file chunking + embedding | Changes on every commit | `SemanticDocument` entries for files, types, methods, XML doc comments; the search surface for `ontology_query(semanticQuery=...)` |

Stream 1 lives in source. Streams 2 and 3 live in Basileus's Marten event store — produced by a Basileus-hosted ingester, consumed by the Ontology MCP endpoint (ADR §2.2).

### 1.2 Scope

**In scope:**
- `IOntologySource` extension point in Strategos (the one structural change)
- Runtime `OntologyBuilder` API for non-DSL descriptor contributions
- Provenance + merge semantics on descriptors
- `Basileus.Ontology.Ingestion` project: Roslyn analyzer, Marten source, semantic indexer, trigger adapters
- Trigger model: install-once CLI, git post-commit hook, dev-loop file watcher, CI action
- Coverage reporting through `ValidationVerdict` (ADR §2.10.3)
- Cross-repo productization: workspace manifest, tenant isolation

**Out of scope (explicit):**
- Action discovery from method signatures — methods stay hand-authored (§10.1)
- Lifecycle inference from state-enum usage patterns — too speculative for v1 (§10.2)
- Text-ingestion pipeline generalization — orthogonal pipeline; no merge (§11.3)
- Cross-language support (Python, TypeScript) — .NET-only for this round
- Customer-visible productization UI — design here covers data plane only

---

## 2. Architecture overview

**Basileus is a web application, not a CLI.** The ingestion pipeline — Roslyn analyzer, semantic indexer, embedding, Marten writes — lives inside Basileus because that's where the .NET runtime, Marten connection, and embedding credentials live. **Exarchos is the CLI and is the trigger surface** — it runs on the developer machine, CI runner, or inside git hooks, and it calls Basileus's HTTP API to request ingestion.

```
  [source of truth: git HEAD]
            │
            ├───────────────────────────────────────────────────┐
            │                                                   │
  [Stream 1: hand-authored]                    [Streams 2+3: triggered ingestion]
  DomainOntology.Define() in source                          │
            │                                                   │
            │            ╔════════════════════════════╗         │
            │            ║  Exarchos CLI (local, TS)  ║◄────────┤  git post-commit hook
            │            ║  - exarchos ingest         ║         │  CI pipeline step
            │            ║  - exarchos install-hooks  ║         │  --watch (dev loop)
            │            ║  - exarchos workspace init ║         │
            │            ╚════════════╤═══════════════╝         │
            │                         │ HTTP POST               │
            │                         ▼                         │
            │   ╔══════════════════════════════════════════════════╗    │
            │   ║  Basileus web app (.NET/Aspire)                  ║    │
            │   ║                                                  ║    │
            │   ║  POST /api/ontology/ingest  ← write-only, 202    ║    │
            │   ║     │                                            ║    │
            │   ║     ▼                                            ║    │
            │   ║  Debouncer (Wolverine + Marten)                  ║    │
            │   ║  · Keyed on {install}:{repo}:{branch}            ║    │
            │   ║  · Leading-schedule / trailing-fire              ║    │
            │   ║  · 5s quiet window, 3min max-wait                ║    │
            │   ║     │  (fires once after burst settles)          ║    │
            │   ║     ▼                                            ║    │
            │   ║  OntologyIngestionService                        ║    │
            │   ║     │                                            ║    │
            │   ║     ├─► RoslynSourceAnalyzer                     ║    │
            │   ║     │   (long-lived workspace; diff-apply only)  ║    │
            │   ║     │                                            ║    │
            │   ║     ├─► ChunkContentHashCache                    ║    │
            │   ║     │   (skip embed on hash hit)                 ║    │
            │   ║     │                                            ║    │
            │   ║     └─► SourceSemanticIndexer                    ║    │
            │   ║         + IEmbeddingProvider                     ║    │
            │   ║              │                                   ║    │
            │   ║              ▼                                   ║    │
            │   ║       [Marten]                                   ║    │
            │   ║        ontology-ingest-{workspaceId}             ║    │
            │   ║        semantic-documents-{workspaceId}          ║    │
            │   ║        chunk-embedding-index-{workspaceId}       ║    │
            │   ║        debounce-records-{workspaceId}            ║    │
            │   ║              │                                   ║    │
            │   ║              ▼                                   ║    │
            │   ║       MartenOntologySource                       ║    │
            │   ║       (IOntologySource)                          ║    │
            │   ╚══════════╤═══════════════════════════════════════╝    │
            │              │                                    │
            ▼              ▼                                    │
  ┌───────────────────────────────────────────────┐             │
  │  OntologyGraph (in Basileus.AgentHost)        │             │
  │    · DomainOntology subclasses (hand)         │             │
  │    · IOntologySource contributions (ingested) │             │
  │    · Field-level provenance                   │             │
  │    · Deterministic merge                      │             │
  └───────────────────────┬───────────────────────┘             │
                          │                                     │
                          ▼                                     │
                   Ontology MCP endpoint (ADR §2.2)  ◄──────────┘
                   ontology_query / ontology_explore / fabric_resolve
```

Key properties:

- **Push model, not pull.** Source changes → Exarchos fires HTTP trigger → Basileus analyzes & appends → `OntologyGraph` updates. No scheduled sweep. No drift window.
- **No generated files in the build.** Streams 2 and 3 persist in Marten. Stream 1 source remains the only `.cs` authoring surface.
- **Single source of truth per field.** Field-level provenance means intent fields (lifecycle, predicates) have one authoritative source (hand); mechanical fields have one (ingested). Conflicts are analyzer diagnostics, not silent overrides.
- **Cross-repo ready.** Workspace-agnostic: Basileus reads a `workspace.yml` manifest to know which repos/assemblies to analyze; Exarchos uses the same manifest to know which Basileus endpoint to call.
- **Roslyn runs where the source is readable.** For local-dev Basileus is co-located with the repo (same host). For remote Basileus, the repo must be mounted/cloned on the Basileus host — see §7.2 deployment modes.

---

## 3. Strategos additions

Three additions to the ADR §2.10 refinements set. All three are backward-compatible.

### 3.1 `IOntologySource` extension point

```csharp
namespace Strategos.Ontology;

public interface IOntologySource
{
    /// <summary>Stable identifier; used for provenance tagging and conflict diagnostics.</summary>
    string SourceId { get; }

    /// <summary>Emit all known deltas at startup. Idempotent — re-calling replays the same stream.</summary>
    IAsyncEnumerable<OntologyDelta> LoadAsync(CancellationToken ct);

    /// <summary>Emit deltas as they occur at runtime (optional; sources that don't support live updates return an empty stream).</summary>
    IAsyncEnumerable<OntologyDelta> SubscribeAsync(CancellationToken ct);
}
```

Registered via DI alongside static `DomainOntology` subclasses:

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();              // stream 1
    options.AddSource<MartenOntologySource>();         // stream 2 (runtime)
});
```

At app startup, Strategos:
1. Builds the initial graph from `DomainOntology` subclasses (current behavior)
2. Drains `LoadAsync` from each `IOntologySource` and applies deltas
3. Begins listening on `SubscribeAsync` for live updates

Merge behavior is specified in §5.

### 3.2 `OntologyDelta` event vocabulary

```csharp
public abstract record OntologyDelta(string DomainName, DateTimeOffset Timestamp, string SourceId);

public sealed record AddObjectTypeDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    ObjectTypeDescriptor Descriptor) : OntologyDelta(DomainName, Timestamp, SourceId);

public sealed record UpdateObjectTypeDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    string TypeName, ObjectTypeDescriptor Descriptor) : OntologyDelta(DomainName, Timestamp, SourceId);

public sealed record RemoveObjectTypeDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    string TypeName) : OntologyDelta(DomainName, Timestamp, SourceId);

public sealed record AddPropertyDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    string TypeName, PropertyDescriptor Property) : OntologyDelta(DomainName, Timestamp, SourceId);

public sealed record RenamePropertyDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    string TypeName, string OldName, string NewName) : OntologyDelta(DomainName, Timestamp, SourceId);

public sealed record RemovePropertyDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    string TypeName, string PropertyName) : OntologyDelta(DomainName, Timestamp, SourceId);

public sealed record AddLinkDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    string TypeName, LinkDescriptor Link) : OntologyDelta(DomainName, Timestamp, SourceId);

public sealed record RemoveLinkDelta(string DomainName, DateTimeOffset Timestamp, string SourceId,
    string TypeName, string LinkName) : OntologyDelta(DomainName, Timestamp, SourceId);
```

Delta granularity is property- and link-level so a property rename is a single `RenamePropertyDelta`, not an add+remove pair (preserves event-stream semantics; enables targeted cache invalidation).

### 3.3 Runtime `OntologyBuilder` API

The expression-tree DSL (`builder.Object<T>(obj => obj.Property(p => p.X))`) requires a known CLR type. Ingested types may only be known by symbol name — the CLR type may not be loaded in the ingester process. Add descriptor-level APIs that bypass the expression trees:

```csharp
public interface IOntologyBuilder
{
    // existing DSL methods unchanged

    void ObjectTypeFromDescriptor(ObjectTypeDescriptor descriptor);
    void ApplyDelta(OntologyDelta delta);
}
```

The DSL path continues to work unchanged for hand-authored `DomainOntology` subclasses. The new descriptor path is the mechanism `IOntologySource` contributions reach the graph.

### 3.4 Provenance on descriptors

Every descriptor carries provenance:

```csharp
public enum DescriptorSource { HandAuthored, Ingested }

public sealed record ObjectTypeDescriptor(...)
{
    public DescriptorSource Source { get; init; } = DescriptorSource.HandAuthored;
    public string? SourceId { get; init; }         // e.g., "ingested:acme-trading"
    public DateTimeOffset? IngestedAt { get; init; }
}
```

Same for `PropertyDescriptor`, `LinkDescriptor`. Merge logic (§5) reads `Source` to resolve conflicts. `ontology_explore` responses include provenance so agents can distinguish "this property is intent-declared" from "this property was mechanically discovered."

---

## 4. Ingestion pipeline architecture

Two halves: a Basileus-hosted ingestion service (does the work) and an Exarchos CLI (provides the trigger surface). The Basileus half is a new project `Basileus.Ontology.Ingestion` under `shared/` plus an HTTP API surface exposed by AgentHost. The Exarchos half is a new CLI command and hosted-service worker in the existing Node/TypeScript Exarchos codebase.

### 4.1 `RoslynSourceAnalyzer` (Basileus)

Loads a .NET solution via `MSBuildWorkspace` and extracts ontology deltas from it. The architectural constraints below are **load-bearing** for the cost/latency SLOs in §4.12 — a naive analyzer that re-opens the workspace per ingest blows the 30-second p95 budget by 5–10×.

**Workspace lifetime.** `MSBuildWorkspace` is opened **once per service process**, kept alive indefinitely, one instance per registered workspace. Cold `OpenSolutionAsync` costs 2–5 minutes and 8–16 GB RAM on a medium solution — unacceptable per-ingest. The service pays this cost once at startup (§4.1.1) and never again.

**Per-ingest execution:**
1. Snapshot the live workspace's `CurrentSolution`.
2. Fork into an `AdhocWorkspace` to isolate the ingest from concurrent work.
3. Apply `Solution.WithDocumentText(docId, newText)` for each changed file in the trigger's diff. Roslyn's immutable solution model means unchanged files inherit their prior `SyntaxTree`, `SemanticModel`, and binding caches.
4. Run the two-pass analyzer (below) against the diffed solution.
5. Emit deltas; discard the adhoc workspace.

**Never re-open `MSBuildWorkspace` per ingest.** Keep it alive for the life of the service.

#### 4.1.1 Pre-warm at startup

`Basileus.AgentHost` boots → `OntologyWorkspacePreWarmHostedService` opens every registered workspace concurrently before AgentHost starts accepting ingest traffic or serving `/mcp/ontology` queries. This is what moves cold-workspace p99 from 2–5 minutes to 60 seconds (the budget in §4.12). Readiness probe reports `unhealthy` until all workspaces are pre-warmed; Kubernetes / Aspire waits accordingly. Partial failure (one workspace fails to open) is reported per-workspace; the service accepts traffic for the workspaces that succeeded.

#### 4.1.2 Two-pass syntax → semantic analysis

Never interleave syntax walks with semantic queries. Pattern:

```csharp
// Pass 1: syntax walk, cheap, collects candidate nodes
var candidates = new List<(Document doc, SyntaxNode node, CandidateKind kind)>();
foreach (var doc in diffedDocuments)
{
    var root = await doc.GetSyntaxRootAsync(ct);
    foreach (var node in root.DescendantNodes().Where(IsCandidate))
        candidates.Add((doc, node, ClassifyCandidate(node)));
}

// Pass 2: batched semantic analysis, one SemanticModel per Document
foreach (var group in candidates.GroupBy(c => c.doc))
{
    var model = await group.Key.GetSemanticModelAsync(ct);
    foreach (var (_, node, kind) in group)
        yield return ExtractDelta(model.GetDeclaredSymbol(node), kind);
}
```

This avoids the pathological N×M `SemanticModel.GetSymbolInfo` call pattern that turns a 10-second analysis into a 10-minute one on cross-project references.

#### 4.1.3 Caching rule: `SymbolKey`, never `ISymbol`

`ISymbol` instances are **not stable across `Compilation` instances** (confirmed by Roslyn maintainers: "You cannot cache ISymbols. The symbols associated with one compilation are not equatable with ones associated with the next."). Any cache keyed on `ISymbol` will silently produce wrong results after a workspace mutation.

Cache by `SymbolKey` (Roslyn's stable, serializable symbol identifier) or by fully-qualified metadata name. `SymbolFinder.FindReferencesAsync` accepts these and is the correct tool for reverse-dependency lookup across the diffed solution.

#### 4.1.4 Memory budget

Workspaces keep their `Compilation`, `SyntaxTree` collections, and binding caches resident. Plan for **~16 GB RAM per active workspace** on a medium solution (300 projects / 500k LOC). This is a per-workspace, not per-ingest, cost — 10 workspaces ≠ 160 GB per ingest, but 10 workspaces resident ≠ 160 GB total (minus OS caching). Budget accordingly in the Basileus deployment manifest; if the host cannot fit all registered workspaces, reject registration rather than ship a service that OOMs.

#### 4.1.5 What is extracted

- **Type-level:** name, namespace, base type (→ IS-A), declared interfaces, `ObjectKind` heuristic (record types → `Entity`; classes with past-tense names like `TradeExecution` → candidate `Process`, flagged for human confirmation), `[OntologyKey]` attribute if present
- **Property-level:** name, CLR type string, `PropertyKind` inference (registered domain type → `Reference`; primitive/enum → `Scalar`; collection of registered type → creates a `HasMany` Link instead; `Vector<float>` → `Vector`)
- **Link-level:** navigation properties (`public TradeOrder Parent { get; }` → `HasOne`; `public IReadOnlyList<TradeOrder> Children { get; }` → `HasMany`), flagged `InferredFromNavigation = true`
- **Filters:** only public types by default; exclude types matching the workspace manifest `exclude` patterns; skip compiler-generated types (`<>`, `$`, `k__BackingField`)

Not extracted (stays hand-authored): actions, preconditions, postconditions, lifecycles, event emissions, interface property mappings, extension points.

Emits an `IAsyncEnumerable<OntologyDelta>` — one pass produces a complete delta stream representing the current source state.

### 4.2 `MartenOntologySource : IOntologySource` (Basileus)

Implements Strategos's `IOntologySource` against a Marten event stream (`ontology-ingest-{workspaceId}`):

- `LoadAsync` — reads all events from the stream and replays as deltas. Cached in-memory after first load.
- `SubscribeAsync` — uses Marten `ISubscription` (same pattern Basileus uses for workflow events) to emit new deltas as they're appended.

One `MartenOntologySource` per registered workspace. The ingester writes deltas; the source reads them. The boundary is event-sourced and replayable.

### 4.3 `OntologyIngestionService` (Basileus)

The orchestrator, invoked by the **debouncer** (§4.8) — not directly by the HTTP controller. Given an `IngestRequest` (workspace id, branch, mode: full | incremental, since: ref, latestHeadSha):

1. Resolve workspace manifest; validate tenant auth.
2. Fork `MSBuildWorkspace.CurrentSolution` → `AdhocWorkspace` (§4.1); apply `Solution.WithDocumentText` for the diff files.
3. Run `RoslynSourceAnalyzer` against the diffed solution → produces delta stream.
4. Compare against the Marten stream's tip state → produces a **diff** (what changed).
5. Append diff events to the Marten stream (`EventAppendMode.Quick`; §4.12).
6. For each file chunk produced by `SourceSemanticIndexer` (§4.4), check `ChunkContentHashCache` (§4.3.1) before embedding.
7. Return `IngestResponse { acceptedDeltaCount, ontologyVersion, coverage, cacheHitRate }`.

Diff semantics are crucial: re-ingesting an unchanged repo produces zero new events and zero embedding calls (idempotent). This is what makes git-hook triggers and debouncer-replayed triggers safe.

#### 4.3.1 `ChunkContentHashCache`

**Load-bearing** for the embedding cost SLO (§4.12). A naive pipeline embeds every chunk on every ingest — ~$1,625/workspace/year at 100 pushes/day on OpenAI-3-large. With this cache, typical PR ingests produce 2–5 novel embeddings out of 50 chunks (the rest are hash hits); annual cost drops to single-digit dollars.

**Not a merkle tree.** Cursor's published indexing architecture uses a merkle tree of file hashes to identify which files changed between client and server without transmitting all file contents. That solves a problem Basileus does not have: in all three deployment modes (co-located filesystem, mounted volume, GitHub App clone), the ingestion service has direct access to git. **Git is already a merkle tree** — `git diff --name-only $BEFORE..$AFTER` gives the changed-file list in O(changed) time, backed by git's own blob-hash tree. Building a second merkle tree in Marten would duplicate infrastructure we inherit for free. The cache is a **flat (content_hash → vector_id) table** indexed by btree.

**Zero new dependencies.** The cache composes three things Basileus already ships: `System.Security.Cryptography.SHA256` (BCL), Marten (`.Store` / `.LoadAsync` with a btree index on `ContentHash`), and Roslyn (`SyntaxTree` + `SyntaxTrivia` for code-semantic normalization — see below). Faster hash functions exist (Blake3 ~10×, xxHash ~100×), but at 10k chunks hashed once per ingest, SHA-256 is sub-10ms for a full-repo pass — not a bottleneck. Choosing a cryptographic hash is the low-cognitive-load default; switching is a follow-up optimization if profiling ever justifies it. Total implementation: ~250 lines for the cache service + ~150 lines for normalization + Marten schema registration.

**Shape:**

```csharp
public sealed record ChunkEmbeddingIndex
{
    public required string Id { get; init; }               // = ContentHash
    public required string ContentHash { get; init; }      // sha256(normalized_content, model_id, model_version)
    public required string ModelId { get; init; }          // e.g., "voyage-code-3"
    public required int ModelVersion { get; init; }        // bumps when we swap model or normalization rules
    public required Guid VectorId { get; init; }           // reference into pgvector collection
    public required DateTimeOffset FirstSeenAt { get; init; }
    public required DateTimeOffset LastUsedAt { get; init; }
    public required int RefCount { get; init; }            // # of SemanticDocument rows pointing at this vector
}
```

**Storage:** Marten collection `chunk-embedding-index-{workspaceId}`. Btree index on `ContentHash`. One document per unique (content, model, version) triple.

**Normalization** (what goes into the hash):
- CRLF → LF (line-ending normalization)
- Trim trailing whitespace on every line
- Strip volatile tokens: `__DATE__`, `__TIME__`, `<auto-generated>` header blocks, generated-code markers (`// <auto-generated/>`), stamp comments
- Preserve semantic content: variable names, operators, doc comments, all tokens that affect meaning

Normalization rules are versioned. A rule change bumps `ModelVersion`, invalidating the cache — this is intentional. Content normalization is a contract between ingests; changes must be coordinated.

**Scope: per-workspace** in v1. Cross-workspace dedup (same code snippet across multiple customer repos) is a v2 feature — tenant isolation beats it in v1 because customer A's proprietary constant-string should not influence what customer B's agent sees. Revisit when multi-tenant hosted Basileus ships.

**Invalidation:** Never by hash. Only by `ModelVersion` bump — at which point the whole cache becomes stale and the next ingest re-embeds. Combine with **stale-but-useful** (§4.4): serve existing vectors during the re-embed window; async worker upgrades vectors keyed by new `ModelVersion`.

**Semantics per chunk, per ingest:**

```
compute_hash = sha256(normalize(content), model_id, model_version)
record = ChunkEmbeddingIndex.FindAsync(compute_hash)
if record is not null:
    record.LastUsedAt = now; record.RefCount++
    reuse record.VectorId
else:
    vector_id = embed(content) → store in pgvector
    ChunkEmbeddingIndex.InsertAsync(compute_hash, vector_id, ...)
```

**Normalization quality is the whole ballgame.** The cache's hit rate depends entirely on normalization being **stable across benign edits** while **meaningfully diverging on semantic changes**. Two failure modes to guard against as operational concerns, not one-time correctness:

- **Under-normalization** — cache misses edits that should hit. Example: CRLF vs. LF line endings drift between developers on Windows vs. Mac, producing different hashes for identical code. Fix: always normalize line endings before hashing; version the normalization rules (§below).
- **Over-normalization** — cache hits when it shouldn't. Example: stripping all whitespace collapses `int x=1;int y=2;` and `int x=1;int y=3;` into the same hash if digits are accidentally stripped. Fix: preserve all semantically-meaningful tokens; strip only demonstrably-volatile markers.

Neither failure causes immediate correctness bugs (wrong-model embeddings are possible only if `ModelVersion` tracking lets the wrong one through), but both erode the cost/latency SLOs. The `cache_hit_rate` metric from §4.12.3 is the canary: steady-state below 0.85 means the normalizer is leaking volatile tokens, and the normalization rules need revisiting. Investment in normalization quality pays compounding dividends — every 1-percentage-point improvement in hit rate is proportional savings on embedding cost for the life of the workspace.

**Normalization uses Roslyn, not regex.** Auto-generated regions are identified via `[GeneratedCodeAttribute]` on symbols and `<auto-generated>` comment pragmas in `SyntaxTrivia` — the same primitives Roslyn itself uses to skip auto-generated files in analyzers. Regex-based stripping is tempting but fragile: a regex for `<auto-generated>` pragmas can't tell it apart from a comment about auto-generation. The cost is modest (we already have a live Roslyn workspace per §4.1) and the precision is load-bearing.

**Observability:** emit `ingestion.chunk.cache_hit` / `_miss` metrics per ingest; surface `cache_hit_rate` in the `IngestResponse` so operators can monitor normalization quality.

**GC:** chunks with `RefCount == 0` and `LastUsedAt > retention` (default 30 days) are evicted by a background Wolverine scheduled job. Cheap operation — a blob delete and a row delete.

### 4.4 `SourceSemanticIndexer` (Basileus)

Produces `SemanticDocument` entries (ADR §2.5 already uses this type). Chunking strategy:

| Chunk level | AST boundary | Content | `SemanticDocument.ObjectType` |
|---|---|---|---|
| **File** | `CompilationUnitSyntax` | Full source file content (or chunked if >N tokens; SentenceBoundaryChunker from Strategos.Ontology.Chunking) | `{domain}.{primaryTypeName}` if file declares one dominant type; else `{domain}.File` |
| **Type** | `TypeDeclarationSyntax` (class/record/struct/interface) | Type declaration + XML docs, inner implementations summarized | `{domain}.{typeName}` |
| **Method** | `MethodDeclarationSyntax` / `ConstructorDeclarationSyntax` / `PropertyDeclarationSyntax` (get/set bodies) | Method signature + body + XML docs | `{domain}.{typeName}.{methodName}` (namespaced to parent type) |
| **Doc comment** | `XmlElementSyntax` on the parent declaration's leading trivia | XML doc content (summary, remarks, examples) | `{domain}.{typeName}` — supplementary embedding |

**AST alignment is load-bearing.** Chunk boundaries MUST align to Roslyn declaration syntax — never split a method, a type, or an XML doc comment across chunks. Fixed-size (line-based or token-based) chunking is prohibited because it breaks the syntactic units the embedder is scoring against. Published evidence: cAST (EMNLP 2025) shows +1.8–4.3 Recall@10 on RepoEval and +2.67 Pass@1 on SWE-bench from AST-aligned boundaries over fixed-size, at zero added ingestion cost because Roslyn is already producing the `SyntaxTree` (see `docs/research/2026-04-19-data-shape-query-performance-relevance.md` §2.2). The only degraded case is a file-level chunk exceeding `MaxChunkTokens` — in that case the chunker falls back to splitting along top-level declarations (each type becomes its own file-level sub-chunk) before the token ceiling forces further subdivision.

All chunks get:
- `Content` — the text fed to the embedder (post-normalization, §4.3.1)
- `Embedding` — reference resolved via `ChunkContentHashCache` (§4.3.1); embedder called only on cache miss
- `ObjectType` — as above; enables `ontology_query(objectType="Position", semanticQuery="margin call")` to scope search to type-relevant chunks
- `Metadata` — see the key table below
- `IndexedAt` — for staleness tracking

**`SemanticDocument.Metadata` keys (canonical set):**

| Key | Value | Role |
|---|---|---|
| `contentHash` | `sha256(normalize(content), modelId, modelVersion)` | Embedding dedup (§4.3.1) |
| `filePath` | Repo-relative path | Navigation / display |
| `chunkLevel` | `"file"` / `"type"` / `"method"` / `"doc"` | Scope filter at query time |
| `modelId` | e.g., `"voyage-code-3"` | Model provenance |
| `modelVersion` | integer | Model provenance |
| **`symbolKey`** | Roslyn `SymbolKey.ToString()` serialization | **Refactor-stable identity** — survives renames via rename-event join |
| **`symbolKind`** | `"NamedType"` / `"Method"` / `"Property"` / `"Field"` / `"Event"` / `"Namespace"` | Query-time filter on symbol category |
| **`symbolFqn`** | `"Namespace.Type.Member"` | Human-readable display; fallback when `symbolKey` is absent |
| `provenance` | `"hand-authored"` / `"ingested"` | Inherited from the owning `ObjectTypeDescriptor` (§3.4); relevance signal captured in `FabricQueryData` audit |

The three **bold** keys (`symbolKey`, `symbolKind`, `symbolFqn`) are additive on top of the existing `SemanticDocument.Metadata: ImmutableDictionary<string, string>` shape — no record-shape change is required. They are written by the ingester from the already-materialized Roslyn `ISymbol` at chunk time (`SymbolKey.Create(symbol).ToString()`; `symbol.Kind.ToString()`; `symbol.ToDisplayString()`). They are refactor-stable because `SymbolKey` is designed to survive `Compilation` churn by serializing the symbol's metadata-name shape rather than its reference identity (`ISymbol` instances are explicitly non-equatable across compilations per the Roslyn team; see workspace-efficiency research §4.1.3). Retrieval consumers use `symbolKey` as the join key across rename events, so a method rename produces a single logical retrieval row that carries forward through history, not an orphaned old row + a new row.

#### 4.4.1 Embedding model defaults

Locked by the cost analysis research (§Part 4). These are the shipping defaults; workspace manifest overrides are permitted for customers with existing investments in other providers.

- **Default model:** **`voyage-code-3` at 1024 dimensions.** Code-specific embedding model; 92% NDCG@10 on code-retrieval benchmarks vs. 78% for OpenAI `text-embedding-3-large`. Voyage's free tier includes 200M tokens — covers ~400 full re-embeds of the reference workload, effectively zero cost in practice.
- **Dimension strategy (storage):** **MRL truncation to 512 dims** for the pgvector index. 6× storage reduction at <1 point quality loss. The cache stores the full 1024-dim vector; the query index uses the 512-dim truncation. Re-truncating for queries is trivial; re-generating full vectors is not, so we keep the source of truth at 1024.
- **Initial onboarding backfill:** **OpenAI Batch API** (if the workspace defaults to OpenAI on first ingest). 50% discount vs. real-time. 24-hour SLA is acceptable for one-time load. Not used for continuous ingestion.
- **Stale-but-useful refresh:** when `ModelVersion` bumps (§4.3.1), the cache becomes stale. Pattern:
  1. Old vectors remain in place; queries continue to serve them.
  2. A Wolverine-scheduled background worker iterates `ChunkEmbeddingIndex` entries with old `ModelVersion`, re-embeds via the new model, atomically swaps the `VectorId` in the cache record.
  3. Individual `SemanticDocument` rows reference the cache by `ContentHash`, so the swap is invisible to readers.
  4. No downtime; no query regression during the refresh window (vectors match the model that produced them because the cache record pins them).
- **Embedding provider abstraction:** `IEmbeddingProvider` in `Strategos.Ontology.Embeddings` stays unchanged; add `VoyageEmbeddingProvider` alongside the existing `OpenAiCompatibleEmbeddingProvider`. Workspace manifest's `ingestion.embedding.provider` field switches between them.

#### 4.4.2 pgvector index type

**Default: `USING hnsw (vector_cosine_ops)` with `m = 16`, `ef_construction = 64`.** Pin this in the Marten schema registration for the `semantic-documents-{workspaceId}` collection's vector column. Rationale and full comparison in the data-shape research (`docs/research/2026-04-19-data-shape-query-performance-relevance.md` §2.1):

- HNSW handles the workload's insert profile (~200 pushes/day × ~10 novel chunks post-cache ≈ 2000 inserts/day) without periodic reindexing. IVFFlat degrades on inserts until a `REINDEX CONCURRENTLY` runs, leaking recall between maintenance windows.
- Query latency at the reference workload (~10k chunks, 512-dim): 10–30 ms p50. Sits inside the budget implied by the `ontology_query` latency regime (ADR §1.2, §2.14).
- RAM cost: HNSW's graph is resident. At 10k chunks × 512 dims × float32 + graph overhead, ≈ 30–40 MB per workspace. Well inside the `~16 GB peak RAM per active workspace` envelope (§4.12.1).

**Scale-tier escape valve — DiskANN (Azure Database for PostgreSQL Flexible Server, preview).** When a workspace's chunk count grows past ~1M (large monorepos; 10M+ LOC), HNSW's resident-graph cost becomes material relative to the 16 GB envelope. DiskANN keeps quantized vectors in memory and full vectors on disk via the Vamana graph; it handles inserts on an empty table gracefully and scales past what HNSW can hold in RAM. Migration is a schema-level change, not a code change — the `IEmbeddingProvider` and query surface are unaffected. Out of scope for v1; flagged as a deferred option on basileus#145 / §11 open questions.

**IVFFlat is not a supported option.** Its recall-drift under insert load is incompatible with the "agent queries ontology freely" latency regime — even a 10% recall drop between reindexes would route agents back to grep fallback. If a customer's data profile is truly insert-stable (rare for active codebases), revisit per-workspace in v2.

### 4.5 HTTP API surface (Basileus)

New routes mounted in AgentHost alongside the Ontology MCP endpoint:

| Method + Path | Request | Response | Purpose |
|---|---|---|---|
| `POST /api/ontology/ingest` | `IngestRequest { workspaceId, mode: "full" \| "incremental", since?: string }` | `IngestResponse { acceptedDeltaCount, ontologyVersion, coverage: CoverageReport, durationMs }` | Trigger an ingestion run for a configured workspace |
| `GET /api/ontology/coverage?workspaceId=X` | — | `CoverageReport` | Read coverage without triggering ingestion |
| `GET /api/ontology/workspaces` | — | `WorkspaceSummary[]` | List workspaces configured on this Basileus instance |

Auth: MCP bearer token for Basileus-to-Basileus calls (same token as `/mcp/ontology`); bearer or mTLS for Exarchos-to-Basileus calls. Tenant-scoped by workspace ID.

### 4.6 Exarchos CLI surface (trigger)

The CLI lives in the Exarchos codebase (TypeScript/Node). It does **not** do source analysis — it posts HTTP requests to Basileus. Four commands, one hosted-service worker:

| Command | Action | Calls |
|---|---|---|
| `exarchos workspace init` | Scaffolds `workspace.yml` in the current repo; registers the workspace with the configured Basileus instance via `POST /api/ontology/workspaces` | Basileus register |
| `exarchos ingest` | Full ingest of current workspace | `POST /api/ontology/ingest { mode: "full" }` |
| `exarchos ingest --since <ref>` | Incremental ingest since git ref (default in CI) | `POST /api/ontology/ingest { mode: "incremental", since: "<ref>" }` |
| `exarchos install-hooks` | Writes `.git/hooks/post-commit` shim that calls `exarchos ingest --since HEAD@{1}` | None (local filesystem) |
| `exarchos ingest --watch` (hosted worker) | File-system watcher; on change, debounce 500ms, then call `POST /api/ontology/ingest { mode: "incremental", since: "HEAD" }` | Basileus ingest |

Exarchos reads the same `workspace.yml` to resolve the Basileus endpoint URL and workspace ID. If Basileus is unreachable or misconfigured, Exarchos commands degrade gracefully: `exarchos ingest` prints a diagnostic and exits non-zero (CI fails); `--watch` retries with backoff; `install-hooks` still installs but hook invocation warns silently and does not block the commit.

### 4.7 GitHub App webhook surface (Basileus)

Exarchos-driven triggers cover dev-loop, CI, and the client-side git hook. None of those cover "a teammate pushed to origin" — and without that coverage, the ontology is a per-developer view, not a team view. Basileus adds a GitHub App installation that closes this gap.

**GitHub App registration.** One app per Basileus deployment (org-level install for customers with many repos; per-repo install for single-repo customers). App permissions: `Metadata: Read`, `Contents: Read`, `Pull requests: Read`, `Webhooks: Read`. Events subscribed: `push`, `pull_request`, `create`/`delete` (branches), `repository` (renames/transfers).

**Webhook endpoint** — `POST /webhooks/vcs/github` on Basileus:

```
1. Verify X-Hub-Signature-256 (HMAC-SHA256 with per-install secret from Azure Key Vault).
2. Resolve installation ID → workspaceId via admin mapping.
3. Normalize event → IngestTrigger { workspaceId, kind, ref, sha, prNumber?, baseSha? }.
4. Enqueue job on Basileus's work queue (Wolverine, existing infra).
5. Return 202 Accepted.

Job handler:
6. Mint installation access token (JWT → /app/installations/{id}/access_tokens).
7. Clone or fetch the specified ref (mode 3 from §7.2: git-clone-on-host).
8. Run OntologyIngestionService with the cloned workspace path + branch + sha.
9. For PR events, correlate with OntologicalRecord (§4.10).
```

**Event → action matrix:**

| GitHub event | Action |
|---|---|
| `push` to default branch (main) | Incremental ingest on main-branch graph |
| `push` to other branch with open PR | Incremental ingest on the branch's delta view (§4.9) |
| `push` to branch without PR | **Ignored** (WIP branches not tracked remotely; §4.9) |
| `pull_request.opened` | Create branch-delta view; initial full-branch ingest; create `OntologicalRecord` correlation if one doesn't exist |
| `pull_request.synchronize` | Incremental ingest on branch |
| `pull_request.closed` (merged=true) | Fold branch delta into main via merge-commit ingest; evict branch view |
| `pull_request.closed` (merged=false) | Evict branch view; archive `OntologicalRecord` (status → failed or cancelled) |
| `create` (branch) | No-op (wait for PR or push to default) |
| `delete` (branch) | Evict branch view if present |
| `repository` (renamed/transferred) | Re-resolve installation → workspace; admin alert |

**Abstraction.** `IVcsWebhookHandler` with a `GitHubWebhookHandler` implementation. `IVcsProvider` interface supplies clone/fetch + token exchange. v1 ships GitHub only per product decision; the interface is clean enough that GitLab / Azure DevOps are additive without changes to `OntologyIngestionService`.

**Auth + secrets.** GitHub App private key in Azure Key Vault under `basileus/vcs/github/{installationId}/private-key`. Installation tokens are short-lived (~1 hour) and minted per job — never cached long. Webhook secret rotates on App-level settings change.

### 4.8 Debouncer (Wolverine + Marten)

**Load-bearing** for the burst-coalescing SLO (§4.12). Without a debouncer, 10 pushes to a branch in 60 seconds produces 10 concurrent ingest jobs — or, worse, serializes them and spends 10× the baseline. With it: 10 pushes → 1 effective job, trailing-fire after the burst settles.

All trigger paths (HTTP API, GitHub webhook, Exarchos CLI, git hooks) are **write-only** — they upsert a `DebounceRecord` and return immediately. A Wolverine-scheduled `CoalesceTick` message fires the actual `OntologyIngestionService.IngestAsync` call after the quiet window settles or the max-wait ceiling hits.

#### 4.8.1 Keying

Key: `{installationId}:{repoFullName}:{branch}`. One debouncer state per (install, repo, branch) triple. This matches the natural coalescing boundary — a developer pushing multiple commits to the same branch collapses; a cross-branch storm (mass-merge) spawns one coalescer per branch, each independently collapsing its burst.

#### 4.8.2 `DebounceRecord` shape

Marten document, collection `debounce-records-{workspaceId}`:

```csharp
public sealed record DebounceRecord
{
    public required string Id { get; init; }                  // = Key
    public required string Key { get; init; }                 // {install}:{repo}:{branch}
    public required DateTimeOffset FirstEventAt { get; init; }
    public required DateTimeOffset LastEventAt { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }          // null until first tick scheduled
    public required string LatestHeadSha { get; init; }        // authoritative commit to ingest
    public string? BaseSha { get; init; }                      // for incremental mode
    public HashSet<string> DeliveryIds { get; init; } = new(); // GitHub X-GitHub-Delivery dedup set
    public bool Fired { get; init; }                           // terminal: this record's window has fired
    public int Epoch { get; init; } = 0;                       // rotates on fire to allow a fresh window
}
```

#### 4.8.3 Upsert handler (trigger path)

```csharp
// Called from webhook, HTTP API, and Exarchos CLI paths — all uniform.
public async Task EnqueueIngestAsync(IngestTrigger trigger, IMessageContext bus, IDocumentSession db)
{
    var key = $"{trigger.InstallationId}:{trigger.Repo}:{trigger.Branch}";
    var now = DateTimeOffset.UtcNow;

    var rec = await db.LoadAsync<DebounceRecord>(key) ?? new DebounceRecord
    {
        Id = key, Key = key, FirstEventAt = now, LastEventAt = now,
        LatestHeadSha = trigger.HeadSha,
    };

    if (trigger.DeliveryId is not null && rec.DeliveryIds.Contains(trigger.DeliveryId))
        return;                                               // idempotent on GitHub retry

    rec = rec with
    {
        LastEventAt = now,
        LatestHeadSha = trigger.HeadSha,                      // newest SHA wins regardless of arrival order
        DeliveryIds = new HashSet<string>(rec.DeliveryIds) { trigger.DeliveryId ?? Guid.NewGuid().ToString() },
    };

    if (rec.ScheduledAt is null)                              // first arrival in this window schedules
    {
        var dueAt = Min(now + QuietWindow, rec.FirstEventAt + MaxWait);
        rec = rec with { ScheduledAt = dueAt };
        db.Store(rec);
        await db.SaveChangesAsync();
        await bus.ScheduleAsync(new CoalesceTick(key, rec.Epoch, dueAt), dueAt);
    }
    else
    {
        db.Store(rec);                                        // later arrivals just update state
        await db.SaveChangesAsync();
    }
}
```

Constant-time, idempotent on GitHub retry. Handler returns 202 from HTTP; webhook acks within the 10s GitHub SLA.

#### 4.8.4 Tick handler (firing path)

```csharp
public async Task Handle(CoalesceTick tick, IMessageContext bus, IDocumentSession db)
{
    var rec = await db.LoadAsync<DebounceRecord>(tick.Key);
    if (rec is null || rec.Fired || rec.Epoch != tick.Epoch) return;

    var now = DateTimeOffset.UtcNow;
    var settledFor = now - rec.LastEventAt;
    var hitMaxWait = (now - rec.FirstEventAt) >= MaxWait;

    if (settledFor < QuietWindow && !hitMaxWait)
    {
        // Still noisy — reschedule, don't fire yet.
        var dueAt = Min(rec.LastEventAt + QuietWindow, rec.FirstEventAt + MaxWait);
        db.Store(rec with { ScheduledAt = dueAt });
        await db.SaveChangesAsync();
        await bus.ScheduleAsync(new CoalesceTick(rec.Key, rec.Epoch, dueAt), dueAt);
        return;
    }

    // Fire: mark the current epoch terminal; a new window starts from epoch+1 on next event.
    db.Store(rec with { Fired = true });
    await db.SaveChangesAsync();
    await bus.PublishAsync(new RunIngestion(
        rec.Key, rec.LatestHeadSha, rec.BaseSha, rec.DeliveryIds.ToArray()));
}
```

`RunIngestion` is dispatched to a separate Wolverine queue handled by `OntologyIngestionService` (§4.3). Fire-and-forget from the tick handler.

#### 4.8.5 Timing constants

- **Quiet window:** 5 seconds. Balances dev-loop responsiveness (sub-10s for a single push) against burst-collapse (ten pushes in 60s coalesce into one).
- **Max-wait:** 3 minutes. Prevents starvation under pathological continuous pushing (developer pushes every 25s indefinitely).

Both are per-workspace configurable in `workspace.yml` for customers with unusual cadences.

#### 4.8.6 Correctness properties

- **At-least-once trigger → at-most-one ingest per window:** GitHub retries send the same `X-GitHub-Delivery`; the `DeliveryIds` set dedups.
- **Out-of-order trigger arrivals:** earlier SHAs are subsumed by `LatestHeadSha := trigger.HeadSha` (we always act on the newest observed commit, not the newest arrived). Reordering is harmless.
- **Restart-safe:** `DebounceRecord` and Wolverine scheduled messages both persist in Postgres. A crash between webhook arrival and tick fires loses nothing — the tick replays from Wolverine's durable inbox.
- **Missed trailing event after fire:** mitigated by `Epoch` rotation — the firing tick writes `Fired = true` on the current record; the next event creates a new record (or writes with `Epoch = old + 1`) and a fresh window.
- **Schedule-once race (two concurrent handlers both see `ScheduledAt = null`):** Marten's optimistic concurrency on `SaveChangesAsync` makes the second writer retry as an update, observing the first writer's `ScheduledAt`. No double-scheduled ticks.

#### 4.8.7 Observability

Emit per-key metrics:
- `debounce.events_received` (counter)
- `debounce.jobs_fired` (counter)
- `debounce.coalesce_ratio = events_received / jobs_fired` (gauge)
- `debounce.window_duration_ms` (histogram, from `FirstEventAt` to fire)
- `debounce.max_wait_fired` (counter — how often we hit the ceiling)

A high max-wait-fired rate signals a developer push-loop that should be investigated; a coalesce ratio of 1:1 signals the debouncer isn't earning its keep and the design can be simplified for that workspace.

### 4.9 Branch handling

**Strategy: main baseline + per-branch deltas, PR-lifecycle scoped.**

Main is the baseline `OntologyGraph`. Each open-PR branch has a `BranchOntologyDelta` stream in Marten — only the differences from main. At query time, the graph is composed as `main ⊕ branchDelta` for the requested branch. Branches without an open PR are not tracked remotely (they're visible only via Exarchos local ingest).

**Storage:**

```
Marten streams (per workspace):
  ontology-ingest-{workspaceId}              ← main baseline, appended continuously
  ontology-ingest-{workspaceId}-branch-{branchName}-{prNumber}
                                             ← branch delta, appended on push to branch
  semantic-documents-{workspaceId}           ← main baseline chunks
  semantic-documents-{workspaceId}-branch-{branchName}-{prNumber}
                                             ← branch delta chunks
```

Branch streams carry events of shape `OntologyDelta` (same vocabulary as main; §3.2) but are scoped to the branch. Eviction on PR close removes the branch streams.

**Query composition:**

```csharp
// Simplified
OntologyGraph ResolveGraph(string workspaceId, string? branch)
{
    var main = martenMainSource.LoadGraph(workspaceId);
    if (branch is null or "main") return main;

    var delta = martenBranchSource.LoadDelta(workspaceId, branch);
    if (delta is null) return main;           // branch not tracked; fall back to main

    return main.Apply(delta);                  // returns new composed graph
}
```

Composed graphs are memoized per `(workspace, branch, mainVersion, branchVersion)` tuple with bounded LRU — ADR §2.12's `ontologyVersion` gives us the cache-key component for both main and branch.

**Branch parameter on APIs:**

- `POST /api/ontology/ingest { workspaceId, branch, mode, since? }` — defaults to `main`.
- `GET /api/ontology/coverage?workspaceId=X&branch=Y` — coverage per branch.
- MCP `ontology_query({ ..., branch? })`, `ontology_explore({ ..., branch? })`, `ontology_validate({ ..., branch? })`, `fabric_resolve({ ..., branch? })` — all accept an optional `branch` parameter; default `main`.

**Branch name normalization.** Branch names are URI-safe-normalized; `feature/trading-routing` → `feature-trading-routing` in stream keys. Raw name preserved in stream metadata for display.

**What isn't tracked remotely:** any branch with zero open PRs. Rationale:
- Storage is bounded by active PR count, not branch count (the latter grows unboundedly on large teams).
- A branch without a PR is WIP — the developer can see it via Exarchos local ingest.
- When a PR opens, GitHub webhook triggers an initial branch ingest that catches everything.

### 4.10 PR ↔ OntologicalRecord correlation

PR lifecycle maps onto `OntologicalRecord` status (ADR §2.5) with minor additions. Extend the record:

```csharp
public sealed record OntologicalRecord(
    string Id, string FeatureId,
    OntologicalRecordStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string OntologyVersion,
    ProcessLayer ProcessLayer,
    DomainLayer? DomainLayer,
    string? Branch,       // NEW — branch the record is scoped to
    int? PrNumber);       // NEW — GitHub PR number when correlated
```

**Correlation paths:**

1. **Exarchos creates record first** (e.g., `/ideate` on a feature branch before pushing). Record has `Branch = "feature/trading-routing"`, `PrNumber = null`. When the developer opens a PR, the webhook handler looks up records with matching `(workspaceId, branch)` and backfills `PrNumber`.
2. **PR opened first** (e.g., quick fix PR without `/ideate`). Webhook handler creates a minimal placeholder record with `PrNumber` set and `ProcessLayer = {...empty}`. If an `/ideate` runs later on the same branch, it upgrades the record.

**Status transitions from webhook events:**

| Webhook event | Record transition |
|---|---|
| `pull_request.opened` | If no record exists → create placeholder with `Status = proposed`, `Branch`, `PrNumber` set |
| `pull_request.review_requested` | No change (review lifecycle is Basileus-internal for Phronesis reviews; GitHub reviews stay on GitHub) |
| `pull_request.synchronize` | Trigger ingest; if record is `proposed` and a full `ProcessLayer` exists, re-run validation → may transition to `validated` |
| `pull_request.closed` (merged=true) | `Status → completed`, fold branch delta into main |
| `pull_request.closed` (merged=false) | `Status → failed`, eviction |
| `pull_request.reopened` | If record exists → un-archive; re-ingest branch |

### 4.11 Trigger matrix

Five trigger paths, all producing the same `OntologyIngestionService.IngestAsync(...)` call internally.

| Trigger environment | Mechanism | Scope |
|---|---|---|
| **Local dev loop** | `exarchos ingest --watch` running as a background worker | Current repo, current branch (HEAD); HTTP POST per change, debounced |
| **Git post-commit** | Shim installed by `exarchos install-hooks` | Current branch; HTTP POST on every commit |
| **CI** | GitHub Action template shipped with Exarchos | PR head; HTTP POST with `--since $BASE_SHA` |
| **Manual** | Direct invocation | Arbitrary `exarchos ingest --full` / `--since` |
| **GitHub App webhook** (teammate push, PR lifecycle) | Basileus webhook endpoint | Whatever the event references: main push, branch push (if PR open), PR open/sync/close |

All five paths produce the same Basileus ingestion job. Ingestion is continuous after initial setup; developers never invoke it manually in steady state. The webhook path is the coverage floor for team visibility; Exarchos paths are the low-latency dev-loop accelerant.

### 4.12 Service level objectives

All numbers for the reference workload defined in the cost analysis research (10 devs, 500k LOC .NET repo, ~200 push events/day, 50 PRs/week, main + ~15 active branches). Deviations scale roughly linearly with those parameters.

#### 4.12.1 Cost budget per workspace per day

| Metric | Budget | Notes |
|---|---|---|
| **Embedding API $** | < $0.05/day (~$18/year) | Steady-state after `ChunkContentHashCache` (§4.3.1) is populated. Pre-cache cost is a one-time $0.09 for initial onboarding |
| **Compute CPU-seconds** | < 300 CPU-s/day | ~200 debounced ingests × ~1.5s each |
| **Marten storage growth** | ~100 MB/workspace/month | `mt_events` jsonb at ~300 bytes/event; 100k events/month |
| **pgvector storage** | ~50 MB per 10k chunks @ 1024-dim MRL-truncated to 512 | Grows with unique code volume, not churn (thanks to chunk cache) |
| **Peak RAM** | ~16 GB per active workspace | Roslyn workspace resident; **per-workspace, not per-ingest** |

10 workspaces → ~$0.50/day + ~160 GB RAM aggregate + ~1 GB Marten/month. Comfortably within a reasonable Basileus deployment envelope.

#### 4.12.2 Propagation latency

| Percentile | Budget | Mechanism |
|---|---|---|
| p50 (typical push) | 10 s | 5s quiet window + ~1s Roslyn diff-apply + Marten append + projection apply |
| p95 (typical push) | 30 s | Same plus async projection tail |
| p99 under burst | 3 min | Debouncer max-wait ceiling (§4.8.5) |
| Cold-workspace first query | 60 s | Pre-warm at service startup (§4.1.1) pays the 2–5 min `MSBuildWorkspace.OpenSolutionAsync` cost once per service lifetime |

The 30-second p95 budget sits inside the "agent queries ontology freely" regime of the inversion thesis (ADR §1.2). Above ~1 minute, agents start hedging with grep fallback; above ~5 minutes, the thesis fails. All four budgets here stay in the "freely" regime except p99 under burst, which occupies "hedges" — the correct tradeoff for rare pathological pushes.

#### 4.12.3 Observability requirements

Every ingest run emits OpenTelemetry metrics:

- `ingestion.duration_ms` (histogram, labeled by phase: debounce / roslyn / marten / projection / embedding)
- `ingestion.chunks_analyzed` (counter)
- `ingestion.chunks_embedded` (counter) — should be ≪ chunks_analyzed in steady state (cache hit proof)
- `ingestion.cache_hit_rate` (gauge)
- `ingestion.delta_count` (counter)
- `ingestion.embedding_cost_usd` (counter, computed from tokens × model price)

SLO burn alerts fire when p95 propagation latency exceeds 60s over a 10-minute window, or when `cache_hit_rate` drops below 0.85 (signals normalization leak). Basileus already has OpenTelemetry wiring via ServiceDefaults — this is a label/histogram addition, not new infrastructure.

#### 4.12.4 What these budgets rule out

A service implementation that misses these budgets is not shipping. Specifically:

- An ingestion that re-opens `MSBuildWorkspace` per run (blows p95 by 10×)
- An ingestion that embeds every chunk on every push (blows cost budget by 4 orders of magnitude)
- A webhook handler that doesn't coalesce (blows p99 by 10× under burst)
- A workspace that doesn't pre-warm (blows cold-query budget by 3×)

These are flagged as acceptance criteria on the corresponding implementation issues.

---

## 5. Merge semantics

The hardest part. Three sources can contribute to the same `ObjectTypeDescriptor`: hand-authored `Define()`, ingested main-branch baseline, and (optionally) an ingested branch delta. Merge rules must be deterministic, explicit, and diagnosable.

### 5.1 Field-level provenance, not record-level

Provenance is per-field, not per-record. A single `ObjectTypeDescriptor` can have hand-authored `Lifecycle`, ingested main `Properties`, and branch-delta `Properties` that add to or override main. Strategos's merge is a three-input fold:

```csharp
// Pseudocode
ObjectTypeDescriptor Merge(
    ObjectTypeDescriptor hand,
    ObjectTypeDescriptor ingestedMain,
    ObjectTypeDescriptor? ingestedBranch)
{
    // Step 1: compose main baseline from ingested main + hand (per §5.2 rules below)
    var mainComposed = MergeTwo(hand, ingestedMain);

    // Step 2: overlay branch delta if present
    if (ingestedBranch is null) return mainComposed;

    return mainComposed with
    {
        Properties = MergeProperties(mainComposed.Properties, ingestedBranch.Properties),
        Links      = MergeLinks(mainComposed.Links, ingestedBranch.Links),
        // Intent fields (Actions, Events, Lifecycle, etc.) never come from branch delta:
        // intent is hand-authored; branch deltas are mechanical additions only.
    };
}

ObjectTypeDescriptor MergeTwo(ObjectTypeDescriptor hand, ObjectTypeDescriptor ingested)
{
    return new ObjectTypeDescriptor(
        Name: hand.Name,                               // must match; mismatch = AONT006
        ClrType: hand.ClrType ?? ingested.ClrType,
        DomainName: hand.DomainName,
        KeyProperty: hand.KeyProperty ?? ingested.KeyProperty,
        Properties: MergeProperties(hand.Properties, ingested.Properties),
        Links: MergeLinks(hand.Links, ingested.Links),
        Actions: hand.Actions,                         // INTENT — hand only
        Events: hand.Events,                           // INTENT — hand only
        ImplementedInterfaces: hand.ImplementedInterfaces,
        Lifecycle: hand.Lifecycle,                     // INTENT — hand only
        InterfaceActionMappings: hand.InterfaceActionMappings,
        ExternalLinkExtensionPoints: hand.ExternalLinkExtensionPoints,
        Kind: hand.Kind ?? ingested.Kind,
        ParentType: hand.ParentType ?? ingested.ParentType,
        ParentTypeName: hand.ParentTypeName ?? ingested.ParentTypeName);
}
```

**Branch-delta rule of thumb.** Branches never contribute intent — lifecycle/action edits are always hand-authored, either in `main` before branching or in a PR that updates `DomainOntology.Define()`. A branch delta holds only mechanical changes (property added/renamed/removed on a type). If a branch hand-edits `Define()`, those edits flow through the hand-authored stream via the ingested main graph once merged; until then, the branch's hand edits are visible only to developers who local-ingest their unpushed working tree.

### 5.2 Field-level rules

| Field | Rule |
|---|---|
| `Name` | Must match between sources; mismatch is AONT006 (duplicate type) |
| `Properties` | Union with hand-override: hand-declared properties take precedence (same name ⇒ hand wins); ingested adds the remainder |
| `Links` | Same as Properties |
| `KeyProperty` | First non-null (hand > ingested) |
| `Kind`, `ParentType` | First non-null (hand > ingested) |
| `Actions`, `Events`, `Lifecycle`, `InterfaceActionMappings`, `ExternalLinkExtensionPoints` | **Hand-authored only.** Ingester never produces these. Conflict is impossible. |

### 5.3 Conflict diagnostics (AONT200 series)

| Diagnostic | Trigger | Severity |
|---|---|---|
| **AONT201** Hand-declared property does not exist on ingested type | `Define()` has `Property(p => p.Quantity)` but ingester shows no `Quantity` — likely rename drift | Error |
| **AONT202** Hand-declared property type mismatches ingested CLR type | `Define()` declares `Quantity` as Scalar but ingester shows it's a `Reference` to another registered type | Warning |
| **AONT203** Ingested-only property missing from hand `Define()` where hand opts in to strict coverage | Only when the type is marked `[DomainEntity(Strict = true)]` | Warning |
| **AONT204** Ingested type not reachable by any hand-authored `Define()` | The ingester discovered a `[DomainEntity]` type not referenced by any `DomainOntology` subclass; graph still includes it but flagged for review | Info |
| **AONT205** Two `IOntologySource` contributions conflict on a mechanical field | Two ingesters (e.g., multi-repo workspace) both declare properties for the same type | Error |

AONT201 is the critical one — it's how rename drift surfaces as a loud build error instead of silent data loss.

### 5.4 Graph version hash (ADR §2.12 integration)

Every merge produces an `OntologyGraph.Version` hash over `(Domains ⊕ ObjectTypes ⊕ Links ⊕ Actions ⊕ Lifecycles)`. Ingester commits bump the hash; Exarchos caches invalidate via `_meta.ontologyVersion` response field per ADR §2.8. No extra work needed here — §2.12 just needs to be aware the hash is now mutable at runtime, not only at build time.

---

## 6. Coverage reporting

The feedback loop for the inversion thesis. Agents must know when the map has holes.

### 6.1 `DomainCoverage` record

Extend `Strategos.Contracts`:

```typespec
model DomainCoverage {
  domain: string;
  totalDiscovered: int32;      // from ingester
  handAuthored: int32;         // covered by a Define() reference
  ingestedOnly: int32;         // discovered but not in any Define()
  completeness: float64;       // (handAuthored + ingestedOnly) / max(totalDiscovered, 1)
  intentDensity: float64;      // handAuthored / max(totalDiscovered, 1)
}

model CoverageReport {
  ontologyVersion: string;
  byDomain: DomainCoverage[];
  overallCompleteness: float64;
  overallIntentDensity: float64;
}
```

`completeness` measures "is there any ontology record for this type?" `intentDensity` measures "is there hand-authored intent?" Both matter: a domain with 100% completeness but 10% intentDensity has mechanical coverage only (no lifecycles, no preconditions) — `ontology_validate` can still compute blast radius but cannot evaluate predicate constraints.

### 6.2 Integration with `ValidationVerdict` (ADR §2.10.3)

Extend `ValidationVerdict`:

```csharp
public sealed record ValidationVerdict(
    bool Passed,
    IReadOnlyList<ConstraintEvaluation> HardViolations,
    IReadOnlyList<ConstraintEvaluation> SoftWarnings,
    BlastRadius BlastRadius,
    IReadOnlyList<PatternViolation> PatternViolations,
    CoverageReport Coverage);        // NEW
```

`/ideate` phase reads `Coverage.byDomain` for the affected domains. If `intentDensity < 0.5` on any affected domain, the ValidationVerdict surfaces a warning: "This design touches `trading` (intent density 34%) — ontology lifecycle/action coverage is partial. Agent planning may rely on mechanical structure only." Agents are informed; they don't get silent blind spots.

### 6.3 CI gate contract

`exarchos ingest --coverage-gate --min-completeness 0.95 --min-intent-density 0.3` — the build-gate shape from ADR §6.2. Exarchos fetches `GET /api/ontology/coverage` from Basileus after triggering ingest and exits non-zero on threshold breach. Thresholds are per-workspace in `workspace.yml`. Default thresholds are lenient (completeness 0.8, intent density 0.1) because stream 2 handles completeness automatically and stream 1 intent grows over time.

The gate fails the CI build on threshold breach. Escape hatch: per-domain override in `workspace.yml` for domains explicitly marked as "partial coverage acceptable."

---

## 7. Cross-repo productization

The ingester is designed from day one to work on any .NET repo, not just Basileus itself.

### 7.1 Workspace manifest

```yaml
# workspace.yml
workspace:
  id: acme-trading                          # stable; used for tenant scoping
  displayName: "Acme Trading Platform"

repos:
  - path: ./src/Acme.Trading
    domain: acme.trading
    include: ["Acme.Trading.Core.*", "Acme.Trading.Domain.*"]
    exclude: ["*.Dto", "*.Transport.*", "*.Tests.*"]

  - path: ./src/Acme.Knowledge
    domain: acme.knowledge
    include: ["Acme.Knowledge.*"]
    exclude: ["*.Migrations.*"]

ingestion:
  embedding:
    provider: openai-compatible
    endpoint: https://embed.basileus.local
    model: text-embedding-3-small
  chunking:
    fileThresholdTokens: 4000
    methodThresholdTokens: 2000
  triggers:
    postCommit: true
    fileWatch: true
    ci: true

coverage:
  thresholds:
    completeness: 0.9
    intentDensity: 0.2
  overrides:
    - domain: acme.knowledge
      completeness: 0.5     # legacy area; lower bar
```

### 7.2 Deployment modes — how Basileus sees the repo

Roslyn runs inside Basileus. For the analyzer to work, the repo's source tree must be readable from the Basileus process. Three supported modes:

| Mode | Basileus location | Source access | Triggered by | Use case |
|---|---|---|---|---|
| **Co-located** | Same host as the repo (dev loop, or CI with Aspire spin-up) | Direct filesystem path in `workspace.yml` | Exarchos CLI | Developer dev loop; CI runs where Aspire can spin up Basileus in-pipeline |
| **Mounted volume** | Remote Basileus with a shared volume or bind-mount | `workspace.yml` path points to the mount | Exarchos CLI from CI | Customer's Basileus on a server; repo mounted from NFS, S3FS, or Azure Files; CI uploads the repo to the shared volume before triggering |
| **GitHub App install** (productizable) | Remote Basileus, no repo storage at rest | Basileus clones/fetches using installation access tokens; ephemeral working directory per job | GitHub webhook (teammate push, PR lifecycle) + Exarchos CLI for dev-loop accelerant | Hosted Basileus tenants; customers install the GitHub App on their org; no repo sync to a shared volume required |

Exarchos treats all three uniformly from its side: it reads `workspace.yml`, resolves `basileus.endpoint`, and POSTs. The mode is a Basileus-side configuration concern, recorded per-workspace in admin state.

**Mode 3 is the productizable form** for hosted Basileus (§7.5). The GitHub App private key + installation ID live in Azure Key Vault under `basileus/vcs/github/{installationId}/*`. Webhook events trigger ingestion automatically; Exarchos adds low-latency dev-loop coverage without requiring repo-to-Basileus sync. **v1 ships mode 1 (co-located) and mode 3 (GitHub App).** Mode 2 (mounted volume) is a fallback for air-gapped deployments or customers on VCS providers Basileus doesn't yet support — designed but not scheduled for v1.

**What the GitHub App can and can't do.** It covers every pushed commit on main and every push to a branch with an open PR (§4.9). It cannot cover unpushed WIP — that gap is filled by Exarchos local-ingest on the developer's machine. This is a feature: developers get fast feedback on local changes without those changes leaking to the remote Basileus before the developer is ready to push.

### 7.3 Tenant isolation

Each `workspace.id` maps to:
- An isolated Marten database (or schema) for `ontology-ingest-{id}` and `semantic-documents-{id}`
- A dedicated `MartenOntologySource` instance
- Workspace-scoped embedding provider credentials (from Azure Key Vault, per tenant)

Cross-workspace queries require explicit opt-in (the Exarchos client sets an X-Cross-Workspace header on the MCP call; Basileus validates per-tenant permission). Default is isolation; this matters because ingested ontology contains type names and XML docs that may be proprietary.

### 7.4 Multi-repo coordination

A workspace with multiple repos (e.g., `Acme.Trading` + `Acme.Knowledge`) produces one merged `OntologyGraph`. Cross-repo links work via Strategos's existing `CrossDomainLink` — ingester infers them when a property in `acme.trading` references a type registered in `acme.knowledge`, producing a `CrossDomainLinkDelta`. No new Strategos primitive needed.

### 7.5 Productization surface

Phase 1 (this design): data plane only. Ingestion runs in any Basileus deployment; consumers install Exarchos as the trigger client and `workspace.yml` as the configuration contract.

Phase 2 (follow-up, not in this design): hosted Basileus tenants with mode 3 (git-clone-on-host), a multi-tenant control plane, per-tenant billing and quotas. Out of scope here, but the workspace manifest format and the HTTP API shape are designed to support it without breaking changes.

---

## 8. Migration from the current state

Today: `apps/agent-host/Basileus.AgentHost/Extensions/OntologyRegistration.cs` reflection-discovers `DomainOntology` subclasses from loaded Basileus assemblies. That's the entire ontology-build path.

Migration is additive. **Critical ordering constraint: `ChunkContentHashCache` (§4.3.1), the debouncer (§4.8), and workspace pre-warm (§4.1.1) must all land before the first dogfood ingest runs.** Landing them late means the dogfood ingests pay the naive cost ($0.09 per run × many re-ingests during development), and worse, the SLOs in §4.12 are unverified until production traffic exposes the bugs.

1. **Install Strategos 2.5.0** — new `IOntologySource` interface, runtime builder API, provenance. Existing `DomainOntology` classes continue to work unchanged. No breaking changes.
2. **Add `Basileus.Ontology.Ingestion` project** — analyzer, indexer, ingestion service. Not called by anything yet.
3. **Land the chunk cache, debouncer, and workspace pre-warm** — the three cost-control components from §4.3.1 / §4.8 / §4.1.1. These are prerequisites, not optimizations applied after launch.
4. **Add AgentHost HTTP routes** — `/api/ontology/ingest`, `/webhooks/vcs/github`, etc. Routes invoke the debouncer only; debouncer invokes the ingestion service.
5. **Scaffold workspace** — `exarchos workspace init` in the target repo writes `workspace.yml` pointing at the configured Basileus endpoint; `exarchos install-hooks` installs the git post-commit hook.
6. **Run initial ingest** — `exarchos ingest --full`. Basileus pre-warms the Roslyn workspace, runs the chunk-cache-gated indexer, appends deltas to Marten. First run pays ~$0.09 for initial embeddings; subsequent runs pay pennies per year.
7. **Register `MartenOntologySource` in AgentHost DI** — merges ingested state into the graph. Hand-authored `DomainOntology.Define()` remains the intent source; ingester fills the mechanical gaps.
8. **Run analyzer in CI** — AONT201 surfaces any rename drift that's been lurking. Fix any hits.
9. **Optional: prune mechanical declarations from hand-authored `Define()`** — once ingester coverage is confirmed, developers can delete `Property(p => p.X)` lines that the ingester now supplies. Purely aesthetic; no functional change. Analyzer AONT206 ("this property is also ingested; consider removing") is opt-in noise.

No big-bang. Each existing domain (Trading, StyleEngine, Knowledge) migrates independently. Zero-disruption path: stop at step 7, and the system works with hand-authored intent + ingested mechanical coverage. Steps 8–9 are optional cleanup.

---

## 9. What this design does not do

- **Does not infer intent.** Lifecycles, predicate preconditions, postconditions, interface action mappings, extension points — all stay hand-authored. The ingester only supplies what Roslyn can reliably extract.
- **Does not replace hand-authored `Define()`.** Stream 1 is still authoritative for intent. This design makes stream 1 *smaller* by relieving it of mechanical declarations, not obsolete.
- **Does not ingest text documents.** `IIngestionPipeline<T>` (text → vector → domain object) is a separate pipeline. Source-repository ingestion produces different outputs (deltas + source-chunk semantic documents) via a different path. No shared machinery; no attempt to unify.
- **Does not watch non-source artifacts.** Config files, YAML, JSON schemas are not ingested. If those need ontology representation, hand-authored.
- **Does not support non-.NET languages.** MSBuildWorkspace is .NET-specific. TypeScript / Python would need parallel analyzers; out of scope here.

---

## 10. Open questions

### 10.1 Action discovery heuristics

Roslyn can identify public methods, but which are "ontology actions" vs. "internal helpers" is not machine-decidable. Options:

- **A:** Ignore methods entirely. Actions stay hand-authored. (Current recommendation.)
- **B:** Heuristic discovery: methods returning `Task<Result<T>>` with `[OntologyAction]` attribute. Half-automated; requires attribute discipline.
- **C:** Aggressive discovery: any public async method on a `[DomainEntity]` type. Over-discovers; needs strong exclusion patterns.

Recommendation: **A for v1.** Revisit once basic schema ingestion is working and we have real usage data on what developers want automated.

### 10.2 Lifecycle inference from state enums

If a domain type has a property of type `enum PositionStatus { Pending, Active, Closed }`, and usage patterns show `Status = Active` transitions gated by method calls, we could infer a lifecycle. Too speculative; would produce low-quality inferred state machines that developers have to delete as often as accept.

Recommendation: **out of scope for v1.** Possibly a separate "ontology scaffolder" tool later that produces draft lifecycle YAML for human review.

### 10.3 Link cardinality inference

`public IReadOnlyList<TradeOrder> Orders { get; }` → clearly `HasMany`. `public TradeOrder Parent { get; }` → `HasOne`. `public int OrderCount { get; }` → not a link. Heuristic works for navigation-like properties but misses ManyToMany (needs edge type) and `HasOne` through shared keys (no navigation property). v1 supports `HasOne`/`HasMany` from navigation; ManyToMany stays hand-authored.

### 10.4 How does ingestion interact with `intent.validated` (ADR §2.5)?

The validation gate at `intent.proposed → intent.validated` reads `CoverageReport.byDomain`. If coverage is low on affected domains, ValidationVerdict flags it. But what does Exarchos do with the flag? Options:

- **Strict:** ValidationVerdict.passed = false if intentDensity < threshold on any affected domain. Forces developers to raise coverage before `/delegate`.
- **Warn-only:** ValidationVerdict.passed = true; the `lowCoverage` field is surfaced to the agent as a reliability hint; agent decides whether to proceed.

Recommendation: **warn-only** initially. Strict gating will be frustrating while coverage is building up; revisit after 2–3 months of runtime data.

### 10.5 Re-ingesting at what cost?

Quantified in §4.12 and in the cost analysis research. Summary: <$0.05/workspace/day in steady state after the chunk cache warms; ~$0.09 one-time for initial onboarding on a medium repo. Large-repo initial ingest (~500k LOC) is $1–5 one-time. Incremental ingests approach free due to the chunk cache.

### 10.6 Strategos 2.5.0 release scoping

This design adds three Strategos items (§3.1, §3.3, §3.4). The ADR §2.10 refinements are already planned for Strategos 2.5.0. These can ship in the same release, or 2.6.0 if 2.5 is size-constrained. The Basileus ingester waits on whichever release ships them.

### 10.7 Demand-triggered re-embed (promoted from implicit to explicit)

**Question.** Should we embed every pushed chunk, or wait until an agent queries the affected branch?

**Context.** The current design (§4.4) is push-triggered: every `RunIngestion` job that the debouncer fires invokes `SourceSemanticIndexer`, which embeds every new/changed chunk. With the chunk cache in place this is cheap (pennies), but for dead branches — branches with open PRs where no agent ever runs `/ideate` — we still pay the indexer cost.

**Alternative.** Separate schema ingestion (always push-triggered — cheap, schema updates must propagate) from embedding (demand-triggered — only run on first query of the affected branch). Copilot's architecture validates this: they don't index a repo until someone starts a conversation about it.

**Tradeoff.** Push-triggered gives predictable hot-query latency (chunks are already embedded when the first query arrives). Demand-triggered gives lower steady-state cost on rarely-queried branches at the expense of first-query latency (+5–10s for typical PR-scope embedding).

**v1 decision.** Ship with push-triggered embedding. The chunk cache already pushes steady-state cost to pennies; demand-triggered is a follow-up optimization contingent on measuring dead-branch cost in production. Tracked as a follow-up issue.

### 10.8 Chunk cache warming for forked workspaces

When a workspace forks (e.g., company fork of an open-source repo), can we prepopulate the `ChunkEmbeddingIndex` from the parent workspace's cache? Cursor does this via simhash-based similarity across tenants. v2 feature; out of scope here but the key shape `sha256(content, model, version)` is tenant-independent, so a future migration path exists.

---

## 11. Proposed implementation sequence

(For the subsequent `/plan` workflow to elaborate into TDD tasks.)

### Phase 1: Strategos 2.5.0 additions (§3)

1. `IOntologySource` interface + `AddSource` DI extension
2. `OntologyDelta` event hierarchy in `Strategos.Contracts`
3. Runtime `OntologyBuilder` APIs (`ObjectTypeFromDescriptor`, `ApplyDelta`)
4. `DescriptorSource` provenance on `ObjectTypeDescriptor`, `PropertyDescriptor`, `LinkDescriptor`
5. Merge logic in `OntologyGraphBuilder` — §5 rules
6. AONT200-series analyzer diagnostics — §5.3

### Phase 2: Basileus ingestion core (§4.1–4.4)

Basileus repo. New project `Basileus.Ontology.Ingestion` under `shared/`.

1. Project scaffold + DI wiring
2. `RoslynSourceAnalyzer` — MSBuildWorkspace loading, type/property/link extraction, two-pass syntax→semantic analysis, `SymbolKey`-based caching
3. `MartenOntologySource` — event stream replay as `IOntologySource`
4. `OntologyIngestionService` — diff computation, delta append, indexer kick (invoked by the debouncer, not directly)
5. `SourceSemanticIndexer` — chunking (file/type/method/doc), `SemanticDocument.ObjectType` binding

### Phase 2.5: `ChunkContentHashCache` (§4.3.1) — **PREREQUISITE, must land before dogfood ingest**

Basileus repo. Cost-control component — without it, every ingest run embeds every chunk and the SLO in §4.12 is unreachable.

1. Marten collection `chunk-embedding-index-{workspaceId}` with btree on `ContentHash`
2. Normalization function (CRLF→LF, trim, strip volatile tokens)
3. Cache-lookup path in `SourceSemanticIndexer` — embed only on miss
4. Observability: `cache_hit_rate` metric, per-ingest summary
5. GC job: evict `RefCount == 0` records past retention (Wolverine scheduled)

### Phase 2.6: Debouncer (§4.8) — **PREREQUISITE, must land before webhook wiring**

Basileus repo. Correctness-under-burst component.

1. `DebounceRecord` Marten document + collection scoping
2. Upsert handler: webhook / HTTP / CLI paths all route through it; constant-time, idempotent on retry
3. Wolverine `CoalesceTick` scheduled message + handler
4. Epoch-rotation on fire (handle missed trailing event)
5. Metrics: `coalesce_ratio`, `window_duration_ms`, `max_wait_fired`
6. Tests for race: two concurrent upserts, both see `ScheduledAt = null`; optimistic concurrency forces one retry; no double-scheduled tick

### Phase 2.7: Workspace pre-warm + lifetime management (§4.1.1)

Basileus repo. Latency-floor component.

1. `OntologyWorkspacePreWarmHostedService` — opens all registered workspaces concurrently at `AgentHost` startup
2. Readiness probe reports `unhealthy` until pre-warm completes
3. `MSBuildWorkspace` kept alive for process lifetime; `AdhocWorkspace` forked per ingest via `CurrentSolution` snapshot
4. Memory budget validation at registration: reject workspaces that would push total RAM over configured ceiling
5. Partial-failure semantics: one workspace failing doesn't block the others

### Phase 3: Basileus HTTP API + branch composition (§4.5, §4.9)

Basileus repo. Routes in `Basileus.AgentHost`.

1. `POST /api/ontology/ingest` controller + request/response contracts (with `branch` parameter)
2. `GET /api/ontology/coverage` controller (with `branch` parameter)
3. `GET /api/ontology/workspaces` controller + workspace-registration path
4. Auth middleware: bearer token + workspace tenant scoping
5. `workspace.yml` parser + per-workspace DI-scoped services
6. **Branch-delta Marten stream schema** (`ontology-ingest-{workspaceId}-branch-{name}-{pr}`)
7. **Branch-aware graph composition** (`main ⊕ branchDelta` with LRU cache keyed on `(workspace, branch, mainVersion, branchVersion)`)
8. **`branch` parameter on MCP tools** (`ontology_query`, `ontology_explore`, `ontology_validate`, `fabric_resolve`)

### Phase 4: GitHub App + webhook surface (§4.7, §4.10)

Basileus repo. New subsystem.

1. GitHub App registration + webhook endpoint `POST /webhooks/vcs/github`
2. `IVcsWebhookHandler` abstraction + `GitHubWebhookHandler` implementation
3. `IVcsProvider` clone/fetch surface + installation-token minting
4. Wolverine job queue for ingest jobs off the webhook hot path
5. Event → `IngestTrigger` normalization (push, pull_request, create/delete, repository)
6. PR ↔ `OntologicalRecord` correlation (backfill `PrNumber` / `Branch`; status transitions on `opened` / `synchronize` / `closed`)
7. Branch-view eviction on PR close

### Phase 5: Exarchos CLI surface (§4.6)

Exarchos repo. TypeScript/Node.

1. `exarchos workspace init` — scaffolds `workspace.yml`, registers with Basileus
2. `exarchos ingest` (full + incremental modes) — HTTP client for Basileus ingest API (with branch parameter)
3. `exarchos install-hooks` — git post-commit shim
4. `exarchos ingest --watch` — file-system watcher hosted service with debounce
5. `exarchos ingest --coverage-gate` — CI-mode wrapper over `/api/ontology/coverage`
6. GitHub Action template shipped with Exarchos

### Phase 6: Coverage + cross-repo (§6, §7)

Split across repos.

1. `CoverageReport` contracts in `Strategos.Contracts` (strategos repo)
2. `ValidationVerdict.Coverage` extension (strategos repo)
3. `workspace.yml` schema authoritative definition (strategos repo — shipped as a JSON schema consumed by both Basileus parser and Exarchos client)
4. Tenant-isolated Marten collections + credential resolution (basileus repo)
5. `exarchos ingest --coverage-gate` CI helper + GitHub Action template (exarchos repo)

### Phase 7: Migration (§8)

1. Register `MartenOntologySource` in `Basileus.AgentHost` DI
2. Run initial ingest on Basileus repo itself (dogfood)
3. Fix any AONT201 diagnostics in existing hand-authored `DomainOntology` classes
4. Document migration for consumer codebases

---

## 12. Related

- Exarchos ↔ Basileus Coordination ADR (rev) §1.3 (completeness constraint), §2.10 (Strategos refinements), §6.2 (coverage CI gate)
- Strategos Ontology Gap Analysis — the source of the three-stream insight
- Strategos Ontology Theoretical Grounding §4.3 (N&R Fact Database gap — partially addressed by stream 3 semantic indexing)
- Strategos Ontology-to-Tools Grounding §3.1 (compilation pipeline — source-to-ontology ingestion is the inverse direction)
- Data Fabric & Ontology Context
- Backend Quality Dimensions — DIM-1 (single source of truth per field), DIM-5 (no dead code / dual rot surfaces)
