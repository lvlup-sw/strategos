using System.Collections.Concurrent;

using Strategos.Agents.Abstractions;

namespace Strategos.Rag.Adapters;

/// <summary>
/// An in-memory implementation of IVectorSearchAdapter for testing and development.
/// Does not use actual embeddings, but matches based on keyword presence for simplicity in tests.
/// </summary>
/// <remarks>
/// This adapter is intended for testing, prototyping, and development environments only.
/// For production scenarios, use a dedicated vector database adapter (e.g., PgVector, Azure AI Search)
/// which will be provided in future updates.
/// </remarks>
[Obsolete("Use InMemoryObjectSetProvider from Strategos.Ontology.ObjectSets.", false)]
public class InMemoryVectorSearchAdapter : IVectorSearchAdapter
{
    private readonly ConcurrentBag<VectorSearchResult> _documents = new();

    /// <summary>
    /// Adds a document to the in-memory store.
    /// </summary>
    /// <param name="content">The content of the document.</param>
    /// <param name="id">Optional unique identifier.</param>
    /// <param name="metadata">Optional metadata.</param>
    public void AddDocument(string content, string? id = null, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        _documents.Add(new VectorSearchResult
        {
            Content = content,
            Id = id ?? Guid.NewGuid().ToString(),
            Score = 1.0, // Default score, will be recalculated on search
            Metadata = metadata ?? new Dictionary<string, object?>()
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        double minRelevance = 0.7,
        CancellationToken cancellationToken = default)
    {
        // Simple keyword-based scoring for mock purposes
        // In a real adapter, this would use cosine similarity of embeddings
        var results = _documents
            .Select(d =>
            {
                var score = CalculateMockScore(query, d.Content);
                return d with { Score = score };
            })
            .Where(d => d.Score >= minRelevance)
            .OrderByDescending(d => d.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    private static double CalculateMockScore(string query, string content)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(content))
        {
            return 0.0;
        }

        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matchCount = queryTerms.Count(q => content.Contains(q, StringComparison.OrdinalIgnoreCase));

        return (double)matchCount / queryTerms.Length;
    }
}
