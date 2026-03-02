// =============================================================================
// <copyright file="InMemoryVectorSearchAdapterGeneric.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Abstractions;

namespace Strategos.Rag.Adapters;

/// <summary>
/// Generic in-memory implementation of <see cref="IVectorSearchAdapter{TCollection}"/> for testing and development.
/// </summary>
/// <typeparam name="TCollection">The collection marker type implementing <see cref="IRagCollection"/>.</typeparam>
/// <remarks>
/// This adapter is intended for testing, prototyping, and development environments only.
/// For production scenarios, use a dedicated vector database adapter (e.g., PgVector, Azure AI Search)
/// which will be provided in future updates.
/// </remarks>
[Obsolete("Use InMemoryObjectSetProvider from Strategos.Ontology.ObjectSets.", false)]
public sealed class InMemoryVectorSearchAdapter<TCollection> : IVectorSearchAdapter<TCollection>
    where TCollection : IRagCollection
{
    private readonly List<(string Content, double Score, string? Id, IReadOnlyDictionary<string, object?>? Metadata)> documents = [];

    /// <summary>
    /// Seeds a document into the in-memory store with a predefined score.
    /// </summary>
    /// <param name="content">The content of the document.</param>
    /// <param name="score">The relevance score for this document (0.0 to 1.0).</param>
    /// <param name="id">Optional unique identifier.</param>
    /// <param name="metadata">Optional metadata.</param>
    public void Seed(
        string content,
        double score,
        string? id = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        this.documents.Add((content, score, id ?? Guid.NewGuid().ToString(), metadata));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        double minRelevance = 0.7,
        IReadOnlyDictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var results = this.documents
            .Where(d => d.Score >= minRelevance)
            .OrderByDescending(d => d.Score)
            .Take(topK)
            .Select(d => new VectorSearchResult
            {
                Content = d.Content,
                Id = d.Id,
                Score = d.Score,
                Metadata = d.Metadata ?? new Dictionary<string, object?>(),
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }
}
