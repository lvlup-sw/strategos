using Strategos.Ontology.Chunking;

namespace Strategos.Ontology.Tests.Chunking;

public class FixedSizeChunkerTests
{
    private readonly FixedSizeChunker _sut = new();

    [Test]
    public async Task Chunk_EmptyText_ReturnsEmptyList()
    {
        var result = _sut.Chunk(string.Empty);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Chunk_ShortText_ReturnsSingleChunk()
    {
        // ~50 words, well below MaxTokens=512 (~683 words)
        var text = string.Join(" ", Enumerable.Range(1, 50).Select(i => $"word{i}"));

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 512 });

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo(text);
    }

    [Test]
    public async Task Chunk_LongText_ReturnsMultipleChunks()
    {
        // 200+ words with MaxTokens=100 => max ~133 words per chunk
        var text = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}"));

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        await Assert.That(result.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task Chunk_WithOverlap_ChunksOverlapCorrectly()
    {
        // 200 words, MaxTokens=100 (~133 words), OverlapTokens=20 (~26 words)
        var words = Enumerable.Range(1, 200).Select(i => $"word{i}").ToArray();
        var text = string.Join(" ", words);

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 20 });

        await Assert.That(result.Count).IsGreaterThan(1);

        // The trailing words of chunk[0] should appear at the start of chunk[1]
        var chunk0Words = result[0].Content.Split(' ');
        var chunk1Words = result[1].Content.Split(' ');

        // Get the last ~26 words of chunk 0
        var overlapWordCount = (int)(20 / 0.75);
        var tailOfChunk0 = chunk0Words[^overlapWordCount..];

        // The start of chunk 1 should begin with these overlap words
        var headOfChunk1 = chunk1Words[..tailOfChunk0.Length];

        await Assert.That(headOfChunk1).IsEquivalentTo(tailOfChunk0);
    }

    [Test]
    public async Task Chunk_Offsets_TrackCharacterPositions()
    {
        var text = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}"));

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        foreach (var chunk in result)
        {
            var extracted = text[chunk.StartOffset..chunk.EndOffset];
            await Assert.That(extracted).IsEqualTo(chunk.Content);
        }
    }

    [Test]
    public async Task Chunk_Index_IsSequential()
    {
        var text = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}"));

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        for (var i = 0; i < result.Count; i++)
        {
            await Assert.That(result[i].Index).IsEqualTo(i);
        }
    }

    [Test]
    public async Task Chunk_WordBoundary_DoesNotSplitMidWord()
    {
        // Use long words to ensure we can check boundary behavior
        var text = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"superlongword{i}"));

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        foreach (var chunk in result)
        {
            // No chunk should start or end with whitespace
            await Assert.That(chunk.Content.StartsWith(' ')).IsFalse();
            await Assert.That(chunk.Content.EndsWith(' ')).IsFalse();

            // Every word should be a complete word from our input
            var chunkWords = chunk.Content.Split(' ');
            foreach (var word in chunkWords)
            {
                await Assert.That(word).StartsWith("superlongword");
            }
        }
    }
}
