namespace Strategos.Ontology.Chunking;

/// <summary>
/// Abstraction for splitting text into chunks suitable for embedding.
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Splits the given text into chunks according to the specified options.
    /// </summary>
    IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null);
}
