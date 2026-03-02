namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Provides text-to-vector embedding capabilities for semantic search.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// The number of dimensions in the embedding vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Embeds a single text string into a vector.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Embeds a batch of text strings into vectors.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
