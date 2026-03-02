namespace Strategos.Ontology.Embeddings;

/// <summary>
/// Provides text embedding generation for ontology vector search.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Gets the dimensionality of the embedding vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Generates an embedding vector for a single text input.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The embedding vector as a float array.</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding vectors for multiple text inputs in batch.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The embedding vectors as a list of float arrays, in the same order as the input texts.</returns>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
