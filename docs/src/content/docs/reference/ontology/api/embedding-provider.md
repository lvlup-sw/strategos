---
title: IEmbeddingProvider
sidebar:
  order: 4
---

`IEmbeddingProvider` is the abstraction over text-embedding services consumed by the similarity-search path. A registered provider is injected into every backend that runs a `SimilarityExpression` (e.g. `PgVectorObjectSetProvider`), where it converts a `queryText` into a dense float vector at query time and converts stored documents into embeddings at write time.

Namespace: `Strategos.Ontology.Embeddings`. Source: `src/Strategos.Ontology/Embeddings/IEmbeddingProvider.cs`.

## Members

| Member | Signature | Returns |
|---|---|---|
| `Dimensions` | `int Dimensions { get; }` | Vector dimensionality |
| `EmbedAsync` | `Task<float[]> EmbedAsync(string text, CancellationToken ct = default)` | Single embedding |
| `EmbedBatchAsync` | `Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)` | One embedding per input |

`Dimensions` is read at registration time so storage backends can size their vector columns to match (for example, `PgVectorObjectSetProvider.EnsureSchemaAsync<T>` allocates a `vector(D)` column where `D == provider.Dimensions`). A mismatch between stored vector length and the registered provider's dimensions surfaces as `InvalidOperationException` when the write path validates inputs.

`EmbedAsync` and `EmbedBatchAsync` are independent — backends call the batch overload when ingesting documents and the single overload when embedding a similarity query. Implementations are expected to honour the supplied `CancellationToken` on long-running embedding calls.

## Registration

Embedding providers register through `OntologyOptions.UseEmbeddingProvider<T>` during `AddOntology`, or through a package's own helper extension (the recommended path for shipped providers):

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();
    options.UseEmbeddingProvider<OpenAiCompatibleEmbeddingProvider>();
    options.UsePgVector(connectionString); // consumes IEmbeddingProvider
});

// Or use the package's canonical helper, which also wires HttpClient and options:
services.AddOpenAiEmbeddings(opts =>
{
    opts.ApiKey = configuration["OpenAi:ApiKey"]!;
    opts.Model = "text-embedding-3-small";
});
```

`UseEmbeddingProvider<T>` adds `T` as a singleton against the `IEmbeddingProvider` contract. `AddOpenAiEmbeddings` (from `Strategos.Ontology.Embeddings`) is the typical entry point because it also configures the `HttpClient` and `OpenAiEmbeddingOptions`. Downstream registrations (`UsePgVector`, custom providers) resolve `IEmbeddingProvider` through the same container. Pre-built implementations:

| Implementation | Package | Notes |
|---|---|---|
| `OpenAiCompatibleEmbeddingProvider` | `Strategos.Ontology.Embeddings` | Targets the OpenAI HTTP embedding endpoint or any OpenAI-compatible API. Use `AddOpenAiEmbeddings(...)` to register. |
| Custom | (your own assembly) | Any class implementing `IEmbeddingProvider` is accepted. Test wiring typically registers a stub that returns a deterministic vector for assertion. |

## Interaction with `SimilarityExpression`

`SimilarityExpression.QueryVector` is optional — when null, the provider embeds `QueryText` at execution time; when supplied, the precomputed vector is used directly and `EmbedAsync` is not called. Callers pre-embed when they want to amortize the embedding cost across many similarity calls or when running an offline test that should not hit the network.

`SimilarityExpression.Metric` (`Cosine`, `L2`, `InnerProduct`) selects the distance operator the backend applies to the embedding — see [`PgVectorOptions`](/reference/ontology/npgsql/) for how that maps to a physical pgvector index.
