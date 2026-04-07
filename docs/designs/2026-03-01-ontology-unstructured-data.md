# Design: First-Class Unstructured Data Support for Strategos.Ontology

## Problem Statement

Strategos.Ontology provides a solid vector search **abstraction** (`IObjectSetProvider.ExecuteSimilarityAsync`, `SimilarityExpression`, `ScoredObjectSetResult<T>`) but ships zero production implementations. The only `IObjectSetProvider` is `InMemoryObjectSetProvider` — a keyword-matching mock for testing. Three system-level gaps prevent production use:

1. **Embedding generation** — No `IEmbeddingProvider` abstraction. `SimilarityExpression` carries raw query text with a comment: "provider is responsible for embedding." But there's no contract defining what an embedding provider looks like.
2. **Document ingestion** — No pipeline from raw text to embedded, searchable domain objects. Users must manually chunk text, generate embeddings, and seed objects.
3. **Production vector store** — No real vector backend. The `DistanceMetric` enum maps to pgvector operators (`<=>`, `<->`, `<#>`) but no implementation exists.

The platform architecture (§12.2-12.3) defers these as "consumer responsibilities." This design promotes them to first-class, batteries-included capabilities following the library's progressive disclosure principle: **simple by default, complexity opt-in.**

## Chosen Approach

**Layered Packages** — Abstractions in the existing `Strategos.Ontology` package; implementations in two new packages that users add progressively.

```text
Strategos.Ontology              ← IEmbeddingProvider, ITextChunker, IObjectSetWriter,
                                   IngestionPipeline<T>, built-in chunkers
Strategos.Ontology.Embeddings   ← OpenAiCompatibleEmbeddingProvider (new package)
Strategos.Ontology.Npgsql       ← PgVectorObjectSetProvider (new package)
```

**Rationale:** Matches the existing packaging pattern (`Strategos.Ontology` / `.Generators` / `.MCP`). Users compose what they need. pgvector is the opinionated default backend (consistent with the Marten/PostgreSQL infrastructure stack). OpenAI-compatible HTTP client is the opinionated default embedder (works with OpenAI, Azure OpenAI, Ollama, vLLM, LiteLLM — any server exposing `/v1/embeddings`).

---

## Requirements

### DR-1: IEmbeddingProvider Abstraction

Add an embedding provider contract to `Strategos.Ontology` enabling pluggable text-to-vector conversion.

**Acceptance criteria:**
- `IEmbeddingProvider` interface in `Strategos.Ontology.Embeddings` namespace
- Exposes `Dimensions` property (int) for compile-time validation against `.Vector(dimensions)` declarations
- Supports single-text and batch embedding methods (both async, cancellation-aware)
- Registered via `OntologyOptions.UseEmbeddingProvider<T>()` following the existing provider registration pattern
- `InMemoryObjectSetProvider` continues to work without an `IEmbeddingProvider` (keyword-based scoring)

### DR-2: OpenAI-Compatible Embedding Provider

Ship a default `IEmbeddingProvider` implementation in `Strategos.Ontology.Embeddings` that calls any OpenAI-compatible `/v1/embeddings` endpoint.

**Acceptance criteria:**
- Works out of the box with: OpenAI, Azure OpenAI, Ollama (OpenAI-compatible mode), vLLM, LiteLLM
- Configurable via `OpenAiEmbeddingOptions` (endpoint, API key, model name, dimensions, batch size)
- Uses `IHttpClientFactory` for connection management (standard .NET pattern)
- Implements batching with configurable batch size (default: 100)
- Handles HTTP errors with clear exception messages (not swallowed)
- Default model: `text-embedding-3-small` (1536 dimensions)
- No vendor-specific SDK dependency — raw HTTP with `System.Net.Http.Json`

### DR-3: ITextChunker Abstraction and Built-in Chunkers

Add text chunking contracts and implementations to `Strategos.Ontology` for splitting text into embeddable segments.

**Acceptance criteria:**
- `ITextChunker` interface in `Strategos.Ontology.Chunking` namespace
- `TextChunk` record containing: `Content`, `Index` (position in sequence), `StartOffset`, `EndOffset` (character offsets in source text)
- `ChunkOptions` record with: `MaxTokens` (default 512), `OverlapTokens` (default 64)
- Three built-in implementations:
  - `FixedSizeChunker` — splits by approximate token count with overlap
  - `SentenceBoundaryChunker` — splits at sentence boundaries respecting max token limit
  - `ParagraphChunker` — splits at paragraph boundaries (double newline), falls back to sentence splitting for large paragraphs
- Token counting uses a simple word-count heuristic (1 token ≈ 0.75 words) — not a tokenizer dependency
- All chunkers are stateless and thread-safe

### DR-4: IObjectSetWriter Abstraction

Add a write contract to complement the read-only `IObjectSetProvider`, enabling ingestion pipelines.

**Acceptance criteria:**
- `IObjectSetWriter` interface in `Strategos.Ontology.ObjectSets` namespace
- Methods: `StoreAsync<T>(T item, ct)` and `StoreBatchAsync<T>(IReadOnlyList<T> items, ct)`
- `InMemoryObjectSetProvider` implements `IObjectSetWriter` (delegates to existing `Seed<T>()` logic)
- `PgVectorObjectSetProvider` implements both `IObjectSetProvider` and `IObjectSetWriter`
- Registered via `OntologyOptions.UseObjectSetWriter<T>()` or auto-registered when the provider implements both interfaces
- Not required — providers that are read-only simply don't implement `IObjectSetWriter`

### DR-5: Ingestion Pipeline DSL

Add a fluent ingestion pipeline that composes chunking, embedding, and storage into a single declarative operation.

**Acceptance criteria:**
- `IngestionPipeline<T>` builder in `Strategos.Ontology.Ingestion` namespace
- Fluent API: `.Chunk(chunker)` → `.Map((chunk, embedding) => T)` → `.WriteTo(writer)` → `.ExecuteAsync(texts, ct)`
- The pipeline calls `IEmbeddingProvider.EmbedBatchAsync` for efficiency (batches chunks)
- Mapper function receives `TextChunk` + `float[]` embedding, returns domain object `T`
- Progress reporting via `IProgress<IngestionProgress>` (optional)
- Works with any combination of `ITextChunker`, `IEmbeddingProvider`, and `IObjectSetWriter` implementations
- Can also be resolved from DI: `IIngestionPipeline<T>` with pre-wired dependencies

### DR-6: PgVector Object Set Provider

Ship a production `IObjectSetProvider` + `IObjectSetWriter` backed by PostgreSQL with pgvector in `Strategos.Ontology.Npgsql`.

**Acceptance criteria:**
- Implements `IObjectSetProvider` (all three query methods) and `IObjectSetWriter`
- `ExecuteSimilarityAsync` generates pgvector similarity SQL using the correct operator per `DistanceMetric`:
  - `Cosine` → `<=>`, `L2` → `<->`, `InnerProduct` → `<#>`
- If `SimilarityExpression.QueryVector` is null, calls `IEmbeddingProvider.EmbedAsync` to vectorize `QueryText`
- If `SimilarityExpression.QueryVector` is provided, uses it directly (bypass embedding)
- `StoreBatchAsync` uses `COPY` for bulk inserts (Npgsql's binary COPY for performance)
- `StoreAsync` uses parameterized `INSERT`
- `ExecuteAsync` translates `FilterExpression` predicates to SQL `WHERE` clauses
- `StreamAsync` uses `NpgsqlDataReader` with sequential access for memory efficiency
- Requires `Npgsql` and `pgvector` NuGet dependencies
- Table schema is convention-based: `{TypeName.ToSnakeCase()}` table with `id`, `data` (jsonb), `embedding` (vector) columns
- Accepts `NpgsqlDataSource` via constructor (standard Npgsql pattern)
- Schema auto-creation via `EnsureSchemaAsync()` method (opt-in, not automatic)

### DR-7: DI Registration Extensions

Add ergonomic DI registration methods consistent with the existing `OntologyOptions` fluent pattern.

**Acceptance criteria:**
- `OntologyOptions.UseEmbeddingProvider<T>()` — registers `IEmbeddingProvider` as singleton (mirrors `UseObjectSetProvider<T>()`)
- `OntologyOptions.UseObjectSetWriter<T>()` — registers `IObjectSetWriter` as singleton
- Auto-detection: when `UseObjectSetProvider<T>()` is called and `T` also implements `IObjectSetWriter`, automatically register both interfaces
- `Strategos.Ontology.Embeddings` ships `AddOpenAiEmbeddings(Action<OpenAiEmbeddingOptions>)` extension on `IServiceCollection`
- `Strategos.Ontology.Npgsql` ships `AddPgVectorObjectSets(Action<PgVectorOptions>)` extension on `IServiceCollection`
- Shorthand registration in Npgsql package:
  ```csharp
  services.AddOntology(opts => opts
      .AddDomain<MyDomain>()
      .UsePgVector(connectionString)
  );
  ```

### DR-8: Platform Architecture Spec Update

Update `docs/reference/platform-architecture.md` §12.2-12.3 to reflect the new first-class support, replacing stale `IVectorSearchAdapter` references.

**Acceptance criteria:**
- §12.2 (Context Assembly) updated: replace `IVectorSearchAdapter` references with `IObjectSetProvider` + `IngestionPipeline<T>`
- §12.3 (RAG Integration) updated: replace manual adapter registration with the new package-based approach
- Add new subsection to §4.14 covering the embedding, chunking, and pgvector packages
- All code examples compile against the new API
- Deferred status removed for context assembly and RAG — now first-class

### DR-9: InMemoryObjectSetProvider Writer + Embedding Support

Extend the existing in-memory provider to support the writer interface and optional embedding, enabling end-to-end testing of ingestion pipelines without pgvector.

**Acceptance criteria:**
- `InMemoryObjectSetProvider` implements `IObjectSetWriter`
- `StoreAsync<T>` delegates to existing `Seed<T>` logic (extracts searchable content via `ToString()` or `ISearchable.Embedding`)
- `StoreBatchAsync<T>` iterates and calls `StoreAsync` per item
- When an `IEmbeddingProvider` is provided (optional constructor parameter), `ExecuteSimilarityAsync` uses real embeddings instead of keyword matching
- Without `IEmbeddingProvider`, behavior is unchanged (keyword-based mock scoring)
- All existing tests continue to pass without modification

---

## Technical Design

### Package Dependency Graph

```text
Strategos.Ontology (existing)
├── IEmbeddingProvider, ITextChunker, IObjectSetWriter    (new abstractions)
├── IngestionPipeline<T>                                   (new DSL)
├── FixedSizeChunker, SentenceBoundaryChunker, ParagraphChunker
└── InMemoryObjectSetProvider (updated: +IObjectSetWriter)

Strategos.Ontology.Embeddings (new)
├── depends on: Strategos.Ontology
├── depends on: Microsoft.Extensions.Http
└── OpenAiCompatibleEmbeddingProvider

Strategos.Ontology.Npgsql (new)
├── depends on: Strategos.Ontology
├── depends on: Npgsql (>= 9.0)
├── depends on: Pgvector (for vector type support)
└── PgVectorObjectSetProvider
```

### API Surface

#### Core Abstractions (Strategos.Ontology)

```csharp
namespace Strategos.Ontology.Embeddings;

public interface IEmbeddingProvider
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default);
}
```

```csharp
namespace Strategos.Ontology.Chunking;

public readonly record struct TextChunk(
    string Content, int Index, int StartOffset, int EndOffset);

public sealed record ChunkOptions
{
    public int MaxTokens { get; init; } = 512;
    public int OverlapTokens { get; init; } = 64;
}

public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null);
}
```

```csharp
namespace Strategos.Ontology.ObjectSets;

public interface IObjectSetWriter
{
    Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class;
    Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class;
}
```

#### Ingestion Pipeline (Strategos.Ontology)

```csharp
namespace Strategos.Ontology.Ingestion;

public sealed class IngestionPipeline<T> where T : class
{
    // Built via fluent API or DI
    public static IngestionPipelineBuilder<T> Create() => new();

    public Task<IngestionResult> ExecuteAsync(
        IEnumerable<string> texts,
        CancellationToken ct = default);

    public Task<IngestionResult> ExecuteAsync(
        IAsyncEnumerable<string> texts,
        CancellationToken ct = default);
}

public sealed class IngestionPipelineBuilder<T> where T : class
{
    public IngestionPipelineBuilder<T> Chunk(ITextChunker chunker);
    public IngestionPipelineBuilder<T> Chunk(ChunkOptions options);  // uses SentenceBoundaryChunker
    public IngestionPipelineBuilder<T> Embed(IEmbeddingProvider provider);
    public IngestionPipelineBuilder<T> Map(Func<TextChunk, float[], T> mapper);
    public IngestionPipelineBuilder<T> WriteTo(IObjectSetWriter writer);
    public IngestionPipelineBuilder<T> OnProgress(IProgress<IngestionProgress> progress);
    public IngestionPipeline<T> Build();
}

public sealed record IngestionResult(int ChunksProcessed, int ItemsStored, TimeSpan Duration);
public sealed record IngestionProgress(int ChunksProcessed, int TotalChunks, string Phase);
```

**Usage:**
```csharp
var result = await IngestionPipeline<DocumentChunk>.Create()
    .Chunk(new SentenceBoundaryChunker())
    .Embed(embeddingProvider)
    .Map((chunk, embedding) => new DocumentChunk
    {
        Content = chunk.Content,
        Embedding = embedding,
        SourceOffset = chunk.StartOffset,
    })
    .WriteTo(objectSetWriter)
    .Build()
    .ExecuteAsync(rawTexts, ct);
```

#### OpenAI-Compatible Provider (Strategos.Ontology.Embeddings)

```csharp
namespace Strategos.Ontology.Embeddings;

public sealed record OpenAiEmbeddingOptions
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public string Model { get; init; } = "text-embedding-3-small";
    public int Dimensions { get; init; } = 1536;
    public int BatchSize { get; init; } = 100;
}

public sealed class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    public OpenAiCompatibleEmbeddingProvider(
        HttpClient httpClient, IOptions<OpenAiEmbeddingOptions> options);

    public int Dimensions { get; }
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default);
}

// DI extension:
public static class EmbeddingServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiEmbeddings(
        this IServiceCollection services,
        Action<OpenAiEmbeddingOptions> configure);
}
```

#### PgVector Provider (Strategos.Ontology.Npgsql)

```csharp
namespace Strategos.Ontology.Npgsql;

public sealed record PgVectorOptions
{
    public required string ConnectionString { get; init; }
    public string Schema { get; init; } = "public";
    public bool AutoCreateSchema { get; init; } = false;
}

public sealed class PgVectorObjectSetProvider : IObjectSetProvider, IObjectSetWriter
{
    public PgVectorObjectSetProvider(
        NpgsqlDataSource dataSource,
        IEmbeddingProvider embeddingProvider,
        IOptions<PgVectorOptions> options);

    // IObjectSetProvider
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(...);
    public IAsyncEnumerable<T> StreamAsync<T>(...);
    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(...);

    // IObjectSetWriter
    public Task StoreAsync<T>(T item, CancellationToken ct = default);
    public Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default);

    // Schema management (opt-in)
    public Task EnsureSchemaAsync<T>(CancellationToken ct = default);
}

// DI extensions:
public static class PgVectorServiceCollectionExtensions
{
    public static IServiceCollection AddPgVectorObjectSets(
        this IServiceCollection services,
        Action<PgVectorOptions> configure);

    // Shorthand on OntologyOptions:
    public static OntologyOptions UsePgVector(
        this OntologyOptions options,
        string connectionString);
}
```

### Table Schema Convention

For a domain object `DocumentChunk`, the provider creates/expects:

```sql
CREATE TABLE IF NOT EXISTS document_chunk (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    data        jsonb NOT NULL,
    embedding   vector(1536),   -- dimension from IEmbeddingProvider.Dimensions
    created_at  timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_document_chunk_embedding
    ON document_chunk USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);
```

- Table name: `PascalCase` type name → `snake_case` (e.g., `DocumentChunk` → `document_chunk`)
- Domain object serialized to `data` jsonb column
- Embedding stored in `vector(N)` column
- IVFFlat index created by `EnsureSchemaAsync` (HNSW available as configuration option)

### End-to-End Usage

```csharp
// Startup:
services.AddOntology(opts => opts
    .AddDomain<KnowledgeDomain>()
    .UsePgVector("Host=localhost;Database=myapp")
);
services.AddOpenAiEmbeddings(opts =>
{
    opts.Endpoint = "https://api.openai.com/v1";
    opts.ApiKey = config["OpenAI:ApiKey"];
    opts.Model = "text-embedding-3-small";
});

// Ingestion:
var result = await IngestionPipeline<KnowledgeChunk>.Create()
    .Chunk(new SentenceBoundaryChunker())
    .Embed(embeddingProvider)
    .Map((chunk, embedding) => new KnowledgeChunk
    {
        Content = chunk.Content,
        Embedding = embedding,
    })
    .WriteTo(objectSetWriter)
    .Build()
    .ExecuteAsync(documents, ct);

// Querying (fluent ObjectSet API — 2.4.0+):
var results = await objectSet.Of<KnowledgeChunk>()
    .SimilarTo("how does authentication work?")
    .Take(5)
    .ExecuteAsync(ct);
```

### Migration from Strategos.Rag

| Before (Obsolete) | After |
|---|---|
| `services.AddRagCollection<T, TAdapter>()` | `services.AddOntology(o => o.UsePgVector(...))` |
| `IVectorSearchAdapter.SearchAsync(query, topK, min)` | `ObjectSet<T>.SimilarTo(query).Take(topK).WithMinRelevance(min).ExecuteAsync()` |
| `VectorSearchResult` (string content + score) | `ScoredObjectSetResult<T>` (typed items + scores) |
| Manual embedding in adapter | `IEmbeddingProvider` injected into provider |
| Manual chunking | `IngestionPipeline<T>` with built-in chunkers |

---

## Testing Strategy

- **Unit tests** for chunkers (text splitting correctness, overlap, boundary detection)
- **Unit tests** for `IngestionPipeline<T>` with in-memory mocks
- **Unit tests** for `OpenAiCompatibleEmbeddingProvider` with `MockHttpMessageHandler`
- **Integration tests** for `PgVectorObjectSetProvider` using Testcontainers (PostgreSQL + pgvector)
- **Snapshot tests** for SQL generation (similarity queries, schema DDL)
- **Existing test preservation** — all existing tests pass without modification

## Open Questions

1. **HNSW vs IVFFlat default** — IVFFlat is simpler but HNSW has better recall at scale. Default to IVFFlat with HNSW as a configuration option?
2. **Embedding caching** — Should the provider cache embeddings for repeated queries? Leaning no (consumer's responsibility via standard HTTP caching or decorator pattern).
3. **Multi-vector support** — Some domain objects may have multiple embedding properties. The current `ISearchable.Embedding` only supports one. Defer to a follow-up?
4. **Tokenizer accuracy** — The word-count heuristic (1 token ≈ 0.75 words) is approximate. Should we ship a proper BPE tokenizer for OpenAI models? Leaning no — keep it simple, users who need exact counts can implement `ITextChunker`.
