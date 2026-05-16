---
title: "RAG Types"
---

# RAG Types

The `Strategos.Rag` package provides vector store adapters for Retrieval-Augmented Generation (RAG) patterns.

## IVectorSearchAdapter

Interface for vector similarity search operations.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `SearchAsync` | `string query`, `int topK`, `double minRelevance`, `CancellationToken ct` | `Task<IReadOnlyList<SearchResult>>` | Searches for similar documents |
| `SearchAsync` | `float[] embedding`, `int topK`, `double minRelevance`, `CancellationToken ct` | `Task<IReadOnlyList<SearchResult>>` | Searches with pre-computed embedding |
| `UpsertAsync` | `Document document`, `CancellationToken ct` | `Task` | Inserts or updates document |
| `DeleteAsync` | `string documentId`, `CancellationToken ct` | `Task` | Removes document from index |

### Example

```csharp
public class RetrieveContextStep : IWorkflowStep<QueryState>
{
    private readonly IVectorSearchAdapter _vectorSearch;

    public RetrieveContextStep(IVectorSearchAdapter vectorSearch)
    {
        _vectorSearch = vectorSearch;
    }

    public async Task<StepResult<QueryState>> ExecuteAsync(
        QueryState state,
        StepContext context,
        CancellationToken ct)
    {
        var results = await _vectorSearch.SearchAsync(
            query: state.Query,
            topK: 10,
            minRelevance: 0.7,
            ct);

        return state
            .With(s => s.RetrievedContext, results)
            .AsResult();
    }
}
```

---

## SearchResult

Result from a vector similarity search.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `DocumentId` | `string` | Unique identifier of matched document |
| `Content` | `string` | Document text content |
| `Score` | `double` | Similarity score (0.0 to 1.0) |
| `Metadata` | `Dictionary<string, object>` | Additional document metadata |

### Example

```csharp
var results = await _vectorSearch.SearchAsync(query, topK: 5, minRelevance: 0.7, ct);

foreach (var result in results)
{
    Console.WriteLine($"ID: {result.DocumentId}");
    Console.WriteLine($"Score: {result.Score:F3}");
    Console.WriteLine($"Content: {result.Content.Substring(0, 100)}...");

    if (result.Metadata.TryGetValue("source", out var source))
    {
        Console.WriteLine($"Source: {source}");
    }
}
```

---

## Document

Represents a document for indexing.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique document identifier |
| `Content` | `string` | Text content to embed |
| `Embedding` | `float[]?` | Pre-computed embedding (optional) |
| `Metadata` | `Dictionary<string, object>` | Additional metadata |

### Example

```csharp
var document = new Document
{
    Id = "doc-123",
    Content = "Agentic workflows combine deterministic state machines with AI agents...",
    Metadata = new Dictionary<string, object>
    {
        ["source"] = "documentation",
        ["chapter"] = "introduction",
        ["created"] = DateTimeOffset.UtcNow
    }
};

await _vectorSearch.UpsertAsync(document, ct);
```

---

## Implemented Adapters

### InMemoryVectorSearchAdapter

In-memory adapter for development and testing.

| Aspect | Value |
|--------|-------|
| Status | Available |
| Use Case | Development, unit testing, prototyping |
| Persistence | None (data lost on restart) |

#### Configuration

```csharp
services.AddScoped<IVectorSearchAdapter, InMemoryVectorSearchAdapter>();
```

#### Limitations

- No persistence between restarts
- Not suitable for production
- Limited scalability

---

## Planned Adapters

### PgVectorAdapter

PostgreSQL with pgvector extension.

| Aspect | Value |
|--------|-------|
| Status | Planned |
| Use Case | Production with existing PostgreSQL |
| Dependencies | PostgreSQL with pgvector extension |

#### Planned Features

- Leverages existing PostgreSQL infrastructure
- HNSW and IVFFlat index support
- Transactional consistency with workflow state

#### Planned Configuration

```csharp
services.AddVectorSearch(options => options
    .UsePgVector(connectionString)
    .WithDimension(1536)
    .WithIndexType(IndexType.HNSW));
```

---

### AzureAISearchAdapter

Azure AI Search for enterprise-scale deployments.

| Aspect | Value |
|--------|-------|
| Status | Planned |
| Use Case | Enterprise production, Azure ecosystem |
| Dependencies | Azure AI Search service |

#### Planned Features

- Managed infrastructure
- Hybrid search (vector + keyword)
- Semantic ranking

#### Planned Configuration

```csharp
services.AddVectorSearch(options => options
    .UseAzureAISearch(
        endpoint: "https://your-search.search.windows.net",
        apiKey: "your-api-key",
        indexName: "documents"));
```

---

## RAG Workflow Patterns

### Basic Retrieval

```csharp
Workflow<QueryState>.Create("simple-rag")
    .StartWith<RetrieveContextStep>()
    .Then<GenerateResponseStep>()
    .Finally<FormatOutputStep>();
```

### Retrieval with Reranking

```csharp
Workflow<QueryState>.Create("reranked-rag")
    .StartWith<RetrieveContextStep>()
    .Then<RerankResultsStep>()
    .Then<GenerateResponseStep>()
    .Finally<FormatOutputStep>();
```

### Multi-Query Retrieval

```csharp
Workflow<QueryState>.Create("multi-query-rag")
    .StartWith<GenerateQueriesStep>()
    .Fork(
        path => path.Then<RetrieveForQuery1Step>(),
        path => path.Then<RetrieveForQuery2Step>(),
        path => path.Then<RetrieveForQuery3Step>())
    .Join<MergeResultsStep>()
    .Then<DeduplicateStep>()
    .Then<GenerateResponseStep>()
    .Finally<FormatOutputStep>();
```

---

## Example: Complete RAG Step

```csharp
public class RagQueryStep : IAgentStep<QueryState>
{
    private readonly IVectorSearchAdapter _vectorSearch;
    private readonly IChatClient _chatClient;

    public RagQueryStep(
        IVectorSearchAdapter vectorSearch,
        IChatClient chatClient)
    {
        _vectorSearch = vectorSearch;
        _chatClient = chatClient;
    }

    public async Task<StepResult<QueryState>> ExecuteAsync(
        QueryState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        // Retrieve relevant context
        var searchResults = await _vectorSearch.SearchAsync(
            state.Query,
            topK: 5,
            minRelevance: 0.7,
            ct);

        // Build context string
        var contextText = string.Join("\n\n",
            searchResults.Select(r => r.Content));

        // Generate response with context
        var prompt = $"""
            Answer the following question using only the provided context.

            Context:
            {contextText}

            Question: {state.Query}

            Answer:
            """;

        var response = await _chatClient.GetResponseAsync(prompt, ct);

        return state
            .With(s => s.RetrievedContext, searchResults.ToList())
            .With(s => s.Response, response)
            .AsResult();
    }
}
```

---

## Registration

```csharp
// Development
services.AddScoped<IVectorSearchAdapter, InMemoryVectorSearchAdapter>();

// Production (when available)
services.AddVectorSearch(options => options
    .UsePgVector(connectionString)
    .WithEmbeddingClient<OpenAIEmbeddingClient>());
```
