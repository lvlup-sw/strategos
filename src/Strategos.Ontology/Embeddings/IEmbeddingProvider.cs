namespace Strategos.Ontology.Embeddings;

/// <summary>
/// Provider abstraction for generating text embeddings.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Gets the number of dimensions in the embedding vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Generates embedding vectors for a batch of texts.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
