namespace Strategos.Ontology.Chunking;

/// <summary>
/// Splits text into fixed-size chunks based on approximate token count, splitting at word boundaries.
/// Token estimation uses the heuristic: tokens = wordCount * 0.75.
/// </summary>
public sealed class FixedSizeChunker : ITextChunker
{
    private const double TokensPerWord = 0.75;

    /// <inheritdoc />
    public IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        options ??= new ChunkOptions();

        var maxWords = (int)(options.MaxTokens / TokensPerWord);
        var overlapWords = (int)(options.OverlapTokens / TokensPerWord);

        var words = SplitWords(text);

        if (words.Count <= maxWords)
        {
            return [new TextChunk(text, 0, 0, text.Length)];
        }

        var chunks = new List<TextChunk>();
        var wordIndex = 0;
        var chunkIndex = 0;

        while (wordIndex < words.Count)
        {
            var endWordIndex = Math.Min(wordIndex + maxWords, words.Count);

            var startOffset = words[wordIndex].Offset;
            var lastWord = words[endWordIndex - 1];
            var endOffset = lastWord.Offset + lastWord.Length;

            var content = text[startOffset..endOffset];
            chunks.Add(new TextChunk(content, chunkIndex, startOffset, endOffset));

            chunkIndex++;

            var advance = maxWords - overlapWords;
            if (advance <= 0)
            {
                advance = 1;
            }

            wordIndex += advance;
        }

        return chunks;
    }

    private static List<WordSpan> SplitWords(string text)
    {
        var words = new List<WordSpan>();
        var i = 0;

        while (i < text.Length)
        {
            // Skip whitespace
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            var start = i;

            // Read word
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            words.Add(new WordSpan(start, i - start));
        }

        return words;
    }

    private readonly record struct WordSpan(int Offset, int Length);
}
