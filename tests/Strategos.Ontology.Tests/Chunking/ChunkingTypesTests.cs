using Strategos.Ontology.Chunking;

namespace Strategos.Ontology.Tests.Chunking;

public class StubTextChunker : ITextChunker
{
    public IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null)
    {
        var opts = options ?? new ChunkOptions();
        // Simple stub: return a single chunk containing all the text
        return [new TextChunk(text, 0, 0, text.Length)];
    }
}

public class ChunkingTypesTests
{
    [Test]
    public async Task TextChunk_Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var chunk = new TextChunk("hello world", 2, 10, 21);

        // Assert
        await Assert.That(chunk.Content).IsEqualTo("hello world");
        await Assert.That(chunk.Index).IsEqualTo(2);
        await Assert.That(chunk.StartOffset).IsEqualTo(10);
        await Assert.That(chunk.EndOffset).IsEqualTo(21);
    }

    [Test]
    public async Task ChunkOptions_Defaults_MaxTokens512OverlapTokens64()
    {
        // Arrange & Act
        var options = new ChunkOptions();

        // Assert
        await Assert.That(options.MaxTokens).IsEqualTo(512);
        await Assert.That(options.OverlapTokens).IsEqualTo(64);
    }

    [Test]
    public async Task ChunkOptions_CustomValues_OverridesDefaults()
    {
        // Arrange & Act
        var options = new ChunkOptions { MaxTokens = 1024, OverlapTokens = 128 };

        // Assert
        await Assert.That(options.MaxTokens).IsEqualTo(1024);
        await Assert.That(options.OverlapTokens).IsEqualTo(128);
    }

    [Test]
    public async Task ITextChunker_Chunk_ReturnsReadOnlyList()
    {
        // Arrange
        var chunker = new StubTextChunker();
        var text = "This is some text to chunk.";

        // Act
        var result = chunker.Chunk(text);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo(text);
        await Assert.That(result[0].Index).IsEqualTo(0);
        await Assert.That(result[0].StartOffset).IsEqualTo(0);
        await Assert.That(result[0].EndOffset).IsEqualTo(text.Length);
    }
}
