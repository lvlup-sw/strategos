---
title: Strategos.Ontology.Npgsql
sidebar:
  order: 8
---

`LevelUp.Strategos.Ontology.Npgsql` is a PostgreSQL-backed implementation of `IObjectSetProvider` and `IObjectSetWriter`, using the [`pgvector`](https://github.com/pgvector/pgvector) extension for similarity search. Objects are stored as JSONB rows with an optional dense-vector column for similarity queries; reads route through the same `ObjectSetExpression` tree as every other backend.

Namespace: `Strategos.Ontology.Npgsql`. Source: `src/Strategos.Ontology.Npgsql/`.

## Prerequisites

PostgreSQL 13+ with the `vector` extension installed and loadable. The provider issues `CREATE EXTENSION IF NOT EXISTS vector;` during schema creation, so the database role must have permission to create extensions (or the extension must be pre-installed by a superuser).

## PgVectorOptions

| Field | Type | Default | Notes |
|---|---|---|---|
| `ConnectionString` | `string` | `""` | PostgreSQL connection string. Required. |
| `Schema` | `string` | `"public"` | Database schema for tables and indexes. |
| `AutoCreateSchema` | `bool` | `false` | When `true`, the provider creates tables and indexes on first use. When `false`, callers must invoke `EnsureSchemaAsync<T>` explicitly. |
| `IndexType` | `PgVectorIndexType` | `IvfFlat` | Vector index type — see below. |

## PgVectorIndexType

| Value | Notes |
|---|---|
| `IvfFlat` | IVFFlat index — balanced build time and query performance. Default. The DDL appends `WITH (lists = 100)`. |
| `Hnsw` | HNSW index — faster queries, slower builds, higher memory cost. |

## Distance metrics

`SimilarityExpression.Metric` selects which pgvector operator the backend applies. The provider implements all three operators defined by the `DistanceMetric` enum:

| Metric | pgvector operator | Index operator class | Similarity conversion |
|---|---|---|---|
| `Cosine` | `<=>` | `vector_cosine_ops` | `similarity = 1.0 - distance` |
| `L2` | `<->` | `vector_l2_ops` | `similarity = 1.0 / (1.0 + distance)` |
| `InnerProduct` | `<#>` | `vector_ip_ops` | `similarity = -distance` (pgvector returns negative inner product) |

`ExecuteSimilarityAsync` returns `ScoredObjectSetResult<T>` with the converted similarity score (not the raw pgvector distance), so callers can compare scores across metrics on a roughly comparable scale.

## Registration

Two extension methods register the provider. Choose by where in the bootstrap pipeline you are wiring services:

```csharp
// Inside AddOntology — preferred for full ontology setups.
services.AddOpenAiEmbeddings(opts => opts.ApiKey = apiKey);
services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();
    options.UsePgVector(connectionString);
});

// Or directly on IServiceCollection when the ontology graph is wired separately.
services.AddPgVectorObjectSets(opts =>
{
    opts.ConnectionString = connectionString;
    opts.Schema = "ontology";
    opts.AutoCreateSchema = true;
    opts.IndexType = PgVectorIndexType.Hnsw;
});
```

Both extension methods register the same `PgVectorObjectSetProvider` as a singleton bound to both `IObjectSetProvider` and `IObjectSetWriter`. `UsePgVector` additionally configures `NpgsqlDataSourceBuilder.UseVector()` so the Npgsql connection mapper recognises pgvector types.

Both methods carry `[RequiresDynamicCode]` and `[RequiresUnreferencedCode]` attributes — the provider uses `System.Text.Json` generic serialisation, which is not trim-safe.

## EnsureSchemaAsync

```csharp
public Task EnsureSchemaAsync<T>(string? descriptorName = null, CancellationToken ct = default)
    where T : class;
```

Creates the `vector` extension, the backing table, and the index for `T`. The generated DDL uses `CREATE EXTENSION IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`, and `CREATE INDEX IF NOT EXISTS` — so the method is idempotent and non-destructive. Calling it twice is a no-op; calling it after the schema has drifted will not migrate, drop, or rebuild anything.

Generated DDL (per call):

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS "<schema>"."<table>" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    data jsonb NOT NULL,
    embedding vector(<dimensions>),
    created_at timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS "idx_<table>_embedding"
    ON "<schema>"."<table>"
    USING <method> (embedding <ops_class>) [WITH (lists = 100)];
```

Where `<dimensions>` is sourced from the registered `IEmbeddingProvider.Dimensions`, `<method>` is `ivfflat` or `hnsw`, and `<ops_class>` corresponds to the configured distance metric (`vector_cosine_ops` for Cosine by default).

The `descriptorName` parameter resolves the target table:

- When non-null, the table name is the snake-cased descriptor name (e.g. `"TradingDocuments"` → `"trading_documents"`).
- When null, resolution falls back to the registered descriptor name for `T` via the optional `OntologyGraph` passed to the provider constructor. For a type registered exactly once, the default works. For a type registered under multiple descriptor names (multi-registration), the default-null call throws — callers must supply `descriptorName` explicitly, one call per descriptor.

## Multi-registration partitioning

The same CLR carrier type can be registered under multiple descriptor names — for example a shared content-carrier registered separately for "trading documents" and "knowledge documents," each backed by an independent table partition. The write path mirrors the read path:

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();   // registers MyCarrier as "trading_documents"
    options.AddDomain<KnowledgeOntology>(); // registers MyCarrier as "knowledge_documents"
    options.UsePgVector(connectionString);
});

// Bootstrap each partition's schema explicitly:
await provider.EnsureSchemaAsync<MyCarrier>("trading_documents", ct);
await provider.EnsureSchemaAsync<MyCarrier>("knowledge_documents", ct);

// Use the explicit-name writer overloads to route writes to the chosen partition:
await writer.StoreAsync<MyCarrier>("trading_documents", item, ct);
```

The default-named `StoreAsync<T>(item, ct)` and `EnsureSchemaAsync<T>(ct)` overloads inspect the optional `OntologyGraph` injected into the provider (`PgVectorObjectSetProvider`'s last constructor parameter) to find a unique descriptor for `T`. When `OntologyGraph` is unavailable or `T` is registered under multiple names, the default overloads fall back to or throw against the snake-cased `typeof(T).Name`. This is the behaviour the 2.4.1 multi-registration work introduced: explicit-name overloads are the safe choice for any shared-carrier scenario.

## Read path

The provider implements all three `IObjectSetProvider` members:

- `ExecuteAsync<T>(ObjectSetExpression, ct)` — translates the expression to SQL, executes, and materializes results as `ObjectSetResult<T>`.
- `StreamAsync<T>(ObjectSetExpression, ct)` — the streaming variant, returning `IAsyncEnumerable<T>`.
- `ExecuteSimilarityAsync<T>(SimilarityExpression, ct)` — embeds `QueryText` via the registered `IEmbeddingProvider` (or uses `QueryVector` when supplied), runs `SELECT id, data, (embedding <op> @query) AS distance ... ORDER BY distance LIMIT @topK`, and converts each row's distance to a similarity score.

Table-name resolution walks back to the expression's root and snake-cases the root's `ObjectTypeName` — this is the same mechanism that resolves the descriptor name for multi-registered types, so reads route to the same physical table the writes target.

## Related

- [`IObjectSetProvider` & expressions](/reference/ontology/api/object-set-provider/) — the abstractions this package implements.
- [`IEmbeddingProvider`](/reference/ontology/api/embedding-provider/) — required for similarity search; the provider injects it at construction time.
- [Similarity search guide](/guide/ontology/similarity-search/) — task-oriented walkthrough from defining `ISearchable` through running a query.
