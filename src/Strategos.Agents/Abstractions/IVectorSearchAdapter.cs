using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Strategos.Agents.Abstractions;

/// <summary>
/// Defines a contract for vector search operations used by RAG middleware.
/// Mirrors the shape of Microsoft.Extensions.AI.TextSearchProvider but tailored for our workflow.
/// </summary>
[Obsolete("Implement IObjectSetProvider with ExecuteSimilarityAsync. See Strategos.Ontology.ObjectSets.", false)]
public interface IVectorSearchAdapter
{
    /// <summary>
    /// Searches for documents relevant to the specified query.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="minRelevance">The minimum relevance score (0.0 to 1.0) for results.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of search results.</returns>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        double minRelevance = 0.7,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a result from a vector search operation.
/// </summary>
[Obsolete("Use ScoredObjectSetResult<T> from Strategos.Ontology.ObjectSets.", false)]
public record VectorSearchResult
{
    /// <summary>
    /// Gets the textual content of the result.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the unique identifier of the source document or chunk.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the relevance score (typically cosine similarity) of the result.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Gets additional metadata associated with the result.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
}
