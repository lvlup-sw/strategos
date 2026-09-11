# Strategos.Ontology.Embeddings

An embedding provider for Strategos.Ontology vector search. It speaks the OpenAI embeddings protocol.

The provider works with any endpoint that accepts that protocol. OpenAI, Azure OpenAI and a local server are all valid endpoints. Set `Endpoint` to the one you use.

## Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.Embeddings
```

## Register the provider

```csharp
services.AddOpenAiEmbeddings(options =>
{
    options.Endpoint = "https://api.openai.com/v1";
    options.ApiKey = configuration["Embeddings:ApiKey"]!;
    options.Model = "text-embedding-3-small";
    options.Dimensions = 1536;
});
```

The call registers `IEmbeddingProvider` on a named `HttpClient`.

## Options

| Option | Default | Purpose |
|---|---|---|
| `Endpoint` | (none) | The base address of the embeddings API. Required. |
| `ApiKey` | (none) | The key sent with each request. Required. |
| `Model` | `text-embedding-3-small` | The embedding model |
| `Dimensions` | `1536` | The length of one vector |
| `BatchSize` | `100` | The number of inputs sent in one request |
| `Timeout` | 30 seconds | The request timeout |

`Dimensions` must equal the length the vector column declares. If the two disagree, the similarity query fails at the database.

Do not put the key in source. Read it from configuration, from a secret store or from an environment variable.

## Related packages

- `LevelUp.Strategos.Ontology` — the descriptor model and the query surface.
- `LevelUp.Strategos.Ontology.Npgsql` — the pgvector provider that stores the vectors.
