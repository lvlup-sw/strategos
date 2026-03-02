namespace Strategos.Ontology.Embeddings;

/// <summary>
/// Configuration options for the OpenAI-compatible embedding provider.
/// </summary>
public sealed class OpenAiEmbeddingOptions
{
    /// <summary>
    /// Gets or sets the base URL of the OpenAI-compatible API endpoint.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key used for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model identifier to use for embeddings.
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Gets or sets the dimensionality of the embedding vectors.
    /// </summary>
    public int Dimensions { get; set; } = 1536;

    /// <summary>
    /// Gets or sets the maximum number of texts to include in a single API call.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}
