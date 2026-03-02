using Strategos.Ontology.Chunking;

namespace Strategos.Ontology.Tests.Chunking;

public class SentenceBoundaryChunkerTests
{
    private readonly SentenceBoundaryChunker _sut = new();

    [Test]
    public async Task Chunk_SingleSentence_ReturnsSingleChunk()
    {
        var text = "This is a single short sentence.";

        var result = _sut.Chunk(text);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo(text);
    }

    [Test]
    public async Task Chunk_MultipleSentences_SplitsAtBoundaries()
    {
        // Build text with many sentences that together exceed MaxTokens=100 (~133 words)
        var sentences = Enumerable.Range(1, 20)
            .Select(i => $"Sentence number {i} has some words in it for testing.")
            .ToArray();
        var text = string.Join(" ", sentences);

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        await Assert.That(result.Count).IsGreaterThan(1);

        // Each chunk should end at a sentence boundary (period followed by end of content)
        foreach (var chunk in result)
        {
            var trimmed = chunk.Content.TrimEnd();
            var lastChar = trimmed[^1];
            await Assert.That(lastChar == '.' || lastChar == '!' || lastChar == '?').IsTrue();
        }
    }

    [Test]
    public async Task Chunk_LongSentence_FallsBackToWordSplit()
    {
        // Single sentence with 500+ words should fall back to word-boundary splitting
        var words = Enumerable.Range(1, 500).Select(i => $"word{i}");
        var text = string.Join(" ", words) + ".";

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        // Should still produce multiple chunks despite being a single sentence
        await Assert.That(result.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task Chunk_MixedPunctuation_HandlesAllTerminators()
    {
        var text = "First statement. Second exclamation! Third question? Fourth conclusion.";

        var result = _sut.Chunk(text);

        // With default MaxTokens=512, this short text should be a single chunk
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo(text);
    }

    [Test]
    public async Task Chunk_WithOverlap_IncludesTrailingSentences()
    {
        // Create sentences that will span multiple chunks
        var sentences = Enumerable.Range(1, 30)
            .Select(i => $"Sentence number {i} has several words to fill up token space.")
            .ToArray();
        var text = string.Join(" ", sentences);

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 30 });

        await Assert.That(result.Count).IsGreaterThan(1);

        // The end of chunk[0] should overlap with the start of chunk[1]
        var chunk0Content = result[0].Content;
        var chunk1Content = result[1].Content;

        // Find a sentence near the end of chunk 0 that also appears at the start of chunk 1
        // The overlap should mean some trailing content of chunk 0 appears at start of chunk 1
        var chunk0EndsWithSentence = chunk0Content.Contains('.');
        await Assert.That(chunk0EndsWithSentence).IsTrue();

        // Chunk 1's beginning should share some text with chunk 0's ending
        // Extract the last sentence from chunk 0
        var chunk0Sentences = SplitSentences(chunk0Content);
        var chunk1Sentences = SplitSentences(chunk1Content);

        // At least one trailing sentence from chunk 0 should appear at the start of chunk 1
        var lastSentenceOfChunk0 = chunk0Sentences[^1].Trim();
        var firstSentenceOfChunk1 = chunk1Sentences[0].Trim();

        // With overlap, the first sentence of chunk 1 should be from within chunk 0
        await Assert.That(chunk0Content).Contains(firstSentenceOfChunk1);
    }

    [Test]
    public async Task Chunk_Offsets_TrackCharacterPositions()
    {
        var sentences = Enumerable.Range(1, 20)
            .Select(i => $"Sentence number {i} has some words.")
            .ToArray();
        var text = string.Join(" ", sentences);

        var result = _sut.Chunk(text, new ChunkOptions { MaxTokens = 100, OverlapTokens = 0 });

        foreach (var chunk in result)
        {
            var extracted = text[chunk.StartOffset..chunk.EndOffset];
            await Assert.That(extracted).IsEqualTo(chunk.Content);
        }
    }

    private static string[] SplitSentences(string text)
    {
        var sentences = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if ((text[i] == '.' || text[i] == '!' || text[i] == '?') &&
                (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                sentences.Add(text[start..(i + 1)]);
                start = i + 1;

                // Skip whitespace after sentence end
                while (start < text.Length && char.IsWhiteSpace(text[start]))
                {
                    start++;
                }
            }
        }

        if (start < text.Length)
        {
            sentences.Add(text[start..]);
        }

        return sentences.ToArray();
    }
}
