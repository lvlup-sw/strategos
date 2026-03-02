// =============================================================================
// <copyright file="IVectorSearchAdapter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Abstractions;

namespace Strategos.Rag;

/// <summary>
/// Typed vector search adapter for a specific RAG collection.
/// </summary>
/// <typeparam name="TCollection">The collection marker type implementing <see cref="IRagCollection"/>.</typeparam>
[Obsolete("Implement IObjectSetProvider with ExecuteSimilarityAsync. See Strategos.Ontology.ObjectSets.", false)]
public interface IVectorSearchAdapter<TCollection>
    where TCollection : IRagCollection
{
    /// <summary>
    /// Searches the vector store for similar content.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="minRelevance">The minimum relevance score (0.0 to 1.0) for results.</param>
    /// <param name="filters">Optional filters to apply to the search.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of search results.</returns>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        double minRelevance = 0.7,
        IReadOnlyDictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);
}
