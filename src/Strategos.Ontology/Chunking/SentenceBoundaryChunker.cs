namespace Strategos.Ontology.Chunking;

/// <summary>
/// Splits text into chunks at sentence boundaries (., !, ? followed by whitespace or end of text).
/// Falls back to word-boundary splitting for single sentences that exceed MaxTokens.
/// </summary>
public sealed class SentenceBoundaryChunker : ITextChunker
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

        var sentences = SplitSentences(text);

        if (sentences.Count == 0)
        {
            return [];
        }

        // If total word count fits in a single chunk, return as-is
        var totalWords = CountWords(text);
        if (totalWords <= maxWords)
        {
            return [new TextChunk(text, 0, 0, text.Length)];
        }

        var chunks = new List<TextChunk>();
        var sentenceIndex = 0;
        var chunkIndex = 0;

        while (sentenceIndex < sentences.Count)
        {
            var currentWords = 0;
            var startSentenceIndex = sentenceIndex;

            // Accumulate sentences until adding the next would exceed maxWords
            while (sentenceIndex < sentences.Count)
            {
                var sentenceWordCount = CountWords(sentences[sentenceIndex].Content);

                if (currentWords > 0 && currentWords + sentenceWordCount > maxWords)
                {
                    break;
                }

                currentWords += sentenceWordCount;
                sentenceIndex++;
            }

            // If we only got one sentence and it exceeds maxWords, fall back to word splitting
            if (sentenceIndex == startSentenceIndex + 1 && currentWords > maxWords)
            {
                var sentence = sentences[startSentenceIndex];
                var wordChunks = SplitByWords(sentence.Content, sentence.Offset, maxWords, chunkIndex);
                chunks.AddRange(wordChunks);
                chunkIndex += wordChunks.Count;
            }
            else
            {
                var startOffset = sentences[startSentenceIndex].Offset;
                var lastSentence = sentences[sentenceIndex - 1];
                var endOffset = lastSentence.Offset + lastSentence.Content.Length;
                var content = text[startOffset..endOffset];

                chunks.Add(new TextChunk(content, chunkIndex, startOffset, endOffset));
                chunkIndex++;
            }

            // Apply overlap: move sentenceIndex back to include trailing sentences
            if (overlapWords > 0 && sentenceIndex < sentences.Count)
            {
                var overlapWordCount = 0;
                var overlapSentenceIndex = sentenceIndex - 1;

                while (overlapSentenceIndex >= startSentenceIndex)
                {
                    var sentenceWordCount = CountWords(sentences[overlapSentenceIndex].Content);
                    overlapWordCount += sentenceWordCount;

                    if (overlapWordCount >= overlapWords)
                    {
                        break;
                    }

                    overlapSentenceIndex--;
                }

                if (overlapSentenceIndex > startSentenceIndex)
                {
                    sentenceIndex = overlapSentenceIndex;
                }
            }
        }

        return chunks;
    }

    internal static List<SentenceSpan> SplitSentences(string text)
    {
        var sentences = new List<SentenceSpan>();
        var start = 0;

        // Skip leading whitespace
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        for (var i = start; i < text.Length; i++)
        {
            if (IsSentenceTerminator(text[i]) &&
                (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                var content = text[start..(i + 1)];
                sentences.Add(new SentenceSpan(content, start));

                // Move past the sentence and any trailing whitespace
                start = i + 1;
                while (start < text.Length && char.IsWhiteSpace(text[start]))
                {
                    start++;
                }

                i = start - 1;
            }
        }

        // If there's remaining text without a sentence terminator, include it
        if (start < text.Length)
        {
            var content = text[start..];
            sentences.Add(new SentenceSpan(content, start));
        }

        return sentences;
    }

    private static bool IsSentenceTerminator(char c) =>
        c is '.' or '!' or '?';

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    private static List<TextChunk> SplitByWords(
        string text,
        int baseOffset,
        int maxWords,
        int startChunkIndex)
    {
        var chunks = new List<TextChunk>();
        var words = SplitWordSpans(text);

        var wordIndex = 0;
        var chunkIndex = startChunkIndex;

        while (wordIndex < words.Count)
        {
            var endWordIndex = Math.Min(wordIndex + maxWords, words.Count);

            var startOff = words[wordIndex].Offset;
            var lastWord = words[endWordIndex - 1];
            var endOff = lastWord.Offset + lastWord.Length;

            var content = text[startOff..endOff];
            chunks.Add(new TextChunk(content, chunkIndex, baseOffset + startOff, baseOffset + endOff));

            chunkIndex++;
            wordIndex = endWordIndex;
        }

        return chunks;
    }

    private static List<WordSpan> SplitWordSpans(string text)
    {
        var words = new List<WordSpan>();
        var i = 0;

        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            var start = i;

            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            words.Add(new WordSpan(start, i - start));
        }

        return words;
    }

    internal readonly record struct SentenceSpan(string Content, int Offset);

    private readonly record struct WordSpan(int Offset, int Length);
}
