using Strategos.Ontology.Chunking;

namespace Strategos.Ontology.Tests.Chunking;

public class ParagraphChunkerTests
{
    private readonly ParagraphChunker _sut = new();

    [Test]
    public async Task Chunk_SingleParagraph_ReturnsSingleChunk()
    {
        var text = "This is a single paragraph with several sentences. It has no double newlines.";

        var result = _sut.Chunk(text);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo(text);
    }

    [Test]
    public async Task Chunk_MultipleParagraphs_SplitsAtDoubleNewline()
    {
        var paragraphs = Enumerable.Range(1, 5)
            .Select(i => $"Paragraph {i} has some content. It contains multiple sentences.")
            .ToArray();
        var text = string.Join("\n\n", paragraphs);

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 30, OverlapTokens = 0 });

        // Each paragraph is ~12 words (~9 tokens), with MaxTokens=30 (~40 words)
        // we should get multiple chunks since 5 paragraphs x 12 words = 60 words
        await Assert.That(result.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task Chunk_LargeParagraph_FallsBackToSentenceSplitting()
    {
        // Single paragraph with 500+ words - no double newlines
        var words = Enumerable.Range(1, 500).Select(i => $"word{i}");
        var text = string.Join(" ", words) + ".";

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        // Should still produce multiple chunks via sentence/word fallback
        await Assert.That(result.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task Chunk_MixedLineEndings_HandlesRNAndN()
    {
        var text = "First paragraph.\r\n\r\nSecond paragraph.\n\nThird paragraph.";

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 10, OverlapTokens = 0 });

        // Each paragraph is ~2 words (~1.5 tokens), with MaxTokens=10 (~13 words)
        // All paragraphs should fit in one chunk or split based on the boundary
        // At minimum, verify we get at least 1 chunk and offsets are correct
        await Assert.That(result.Count).IsGreaterThanOrEqualTo(1);

        foreach (var chunk in result)
        {
            var extracted = text[chunk.StartOffset..chunk.EndOffset];
            await Assert.That(extracted).IsEqualTo(chunk.Content);
        }
    }

    [Test]
    public async Task Chunk_Offsets_TrackCharacterPositions()
    {
        var paragraphs = Enumerable.Range(1, 10)
            .Select(i => $"Paragraph {i} has multiple words for testing the chunker implementation.")
            .ToArray();
        var text = string.Join("\n\n", paragraphs);

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 30, OverlapTokens = 0 });

        foreach (var chunk in result)
        {
            var extracted = text[chunk.StartOffset..chunk.EndOffset];
            await Assert.That(extracted).IsEqualTo(chunk.Content);
        }
    }
}
