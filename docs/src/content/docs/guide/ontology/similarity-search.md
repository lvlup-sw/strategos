---
title: Similarity Search
description: Run vector similarity queries against ontology-typed domain objects.
sidebar:
  order: 2
---

The ontology layer exposes embedding-based search as a first-class operation on `ObjectSet<T>`. Any CLR type that carries an embedding can be queried with `SimilarTo(text)` and materialized through `ExecuteSimilarityAsync`. This page covers the four moving parts: marking a type as searchable, registering an `IEmbeddingProvider`, executing the query, and the pgvector setup that backs the production provider.

## Mark the type as searchable

A domain type opts into vector search by implementing `ISearchable`, which contributes a `float[] Embedding` property the provider can write and read.

```csharp
using Strategos.Ontology.ObjectSets;

public sealed record DocumentChunk : ISearchable
{
    public Guid Id { get; init; }
    public string Content { get; init; } = "";
    public float[] Embedding { get; init; } = [];
    public int SourceOffset { get; init; }
}
```

Register the type in your domain like any other object, then build and query it through the standard pipeline. Nothing about the `DomainOntology.Define` call changes — the embedding contract is at the CLR-type level.

## Register an embedding provider

`IEmbeddingProvider` lives in `Strategos.Ontology.Embeddings` and has three members: `Dimensions`, `EmbedAsync(text, ct)`, and `EmbedBatchAsync(texts, ct)`. The `Strategos.Ontology.Embeddings` package ships an OpenAI-compatible implementation; register it alongside your ontology:

```csharp
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Npgsql;

services.AddOpenAiEmbeddings(opts =>
{
    opts.ApiKey = configuration["OpenAI:ApiKey"]!;
    opts.Model = "text-embedding-3-small";
    opts.Dimensions = 1536;
});

services.AddOntology(options =>
{
    options.AddDomain<KnowledgeOntology>();
    options.UsePgVector(configuration.GetConnectionString("Ontology")!);
});
```

`UsePgVector` registers a `PgVectorObjectSetProvider` against both `IObjectSetProvider` and `IObjectSetWriter`, configures an `NpgsqlDataSource` with the pgvector extension, and binds the connection string to `PgVectorOptions`. For test or in-process scenarios, register `InMemoryObjectSetProvider` instead — when constructed with an `IEmbeddingProvider` it computes cosine similarity in memory and matches the production semantics closely enough to drive unit tests.

## Build a similarity query

Pull an `ObjectSet<T>` from `IOntologyQuery` and chain `SimilarTo(text)`. The result is a `SimilarObjectSet<T>` with three fluent knobs: `Take(topK)`, `WithMinRelevance(score)`, and `WithMetric(metric)`. Calling `ExecuteAsync(ct)` materializes the query by routing the `SimilarityExpression` through `IObjectSetProvider.ExecuteSimilarityAsync<T>`.

```csharp
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;

public sealed class DocumentSearchService
{
    private readonly IOntologyQuery _query;

    public DocumentSearchService(IOntologyQuery query) => _query = query;

    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        string queryText, CancellationToken ct)
    {
        var docs = _query.GetObjectSet<DocumentChunk>("DocumentChunk");

        var result = await docs
            .SimilarTo(queryText)
            .Take(10)
            .WithMinRelevance(0.75)
            .WithMetric(DistanceMetric.Cosine)
            .ExecuteAsync(ct);

        return result.Items;
    }
}
```

`ExecuteSimilarityAsync` returns `ScoredObjectSetResult<T>`, which carries `Items`, `TotalCount`, `Inclusion`, and a `Scores` list aligned 1-to-1 with `Items`. The relevance score is normalized — a higher value means a closer match regardless of the underlying distance operator.

The `SimilarTo` default is `topK: 5` and `minRelevance: 0.7`; override either with the fluent setters before calling `ExecuteAsync`. Every fluent call returns a new immutable `SimilarObjectSet<T>` so composed queries are safe to share.

## pgvector setup

`Strategos.Ontology.Npgsql` requires the [pgvector](https://github.com/pgvector/pgvector) PostgreSQL extension. Install it once per database:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

`PgVectorOptions` configures three settings beyond the connection string: `Schema` (defaults to `public`), `AutoCreateSchema` (off by default), and `IndexType` (`PgVectorIndexType.IvfFlat` or `Hnsw`). When `AutoCreateSchema` is off, call `EnsureSchemaAsync<T>(descriptorName, ct)` on the provider once per searchable type at startup. It runs the DDL that creates the snake-cased table, the `vector(dimensions)` column matching `IEmbeddingProvider.Dimensions`, and the chosen index.

```csharp
var provider = host.Services.GetRequiredService<PgVectorObjectSetProvider>();
await provider.EnsureSchemaAsync<DocumentChunk>(ct: stoppingToken);
```

## Choose a distance metric

`DistanceMetric` selects which pgvector operator the query uses:

| Metric | pgvector operator | Use when |
|---|---|---|
| `Cosine` (default) | `<=>` | Comparing text embeddings where direction matters more than magnitude. |
| `L2` | `<->` | Comparing fixed-magnitude vectors where Euclidean distance is meaningful. |
| `InnerProduct` | `<#>` | Vectors are already normalized; want the cheapest computation. |

The metric only affects ordering, not the embedding pipeline. Most OpenAI-compatible text models are designed for cosine similarity — stick with the default unless your embedding model documents otherwise.

## Where to go next

- [Text Chunking](/strategos/guide/ontology/text-chunking/) — split source documents before embedding.
- [Polyglot Descriptors](/strategos/guide/ontology/polyglot-descriptors/) — register descriptors that span runtimes.
- [Platform Architecture §4.14](/strategos/reference/platform-architecture/#414-ontology-layer-strategosontology) for the surrounding ontology surface.
