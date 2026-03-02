namespace Strategos.Ontology.Chunking;

/// <summary>
/// Represents a chunk of text produced by an <see cref="ITextChunker"/>.
/// </summary>
public readonly record struct TextChunk(string Content, int Index, int StartOffset, int EndOffset);
