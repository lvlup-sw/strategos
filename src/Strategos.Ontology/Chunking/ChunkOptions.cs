namespace Strategos.Ontology.Chunking;

/// <summary>
/// Options controlling how text is split into chunks.
/// </summary>
public sealed record ChunkOptions
{
    /// <summary>
    /// Maximum number of tokens per chunk. Defaults to 512.
    /// </summary>
    public int MaxTokens { get; init; } = 512;

    /// <summary>
    /// Number of tokens to overlap between consecutive chunks. Defaults to 64.
    /// </summary>
    public int OverlapTokens { get; init; } = 64;
}
