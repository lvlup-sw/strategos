namespace Strategos.Ontology.Chunking;

/// <summary>
/// Splits text into chunks at paragraph boundaries (double newlines).
/// Delegates to <see cref="SentenceBoundaryChunker"/> for paragraphs that exceed MaxTokens.
/// </summary>
public sealed class ParagraphChunker : ITextChunker
{
    private const double TokensPerWord = 0.75;
    private readonly SentenceBoundaryChunker _sentenceChunker;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParagraphChunker"/> class.
    /// </summary>
    /// <param name="sentenceChunker">
    /// Optional sentence boundary chunker for fallback splitting.
    /// If null, a new instance is created internally.
    /// </param>
    public ParagraphChunker(SentenceBoundaryChunker? sentenceChunker = null)
    {
        _sentenceChunker = sentenceChunker ?? new SentenceBoundaryChunker();
    }

    /// <inheritdoc />
    public IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        options ??= new ChunkOptions();

        var maxWords = (int)(options.MaxTokens / TokensPerWord);

        var paragraphs = SplitParagraphs(text);

        if (paragraphs.Count == 0)
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
        var paragraphIndex = 0;
        var chunkIndex = 0;

        while (paragraphIndex < paragraphs.Count)
        {
            var currentWords = 0;
            var startParagraphIndex = paragraphIndex;

            // Accumulate paragraphs until adding the next would exceed maxWords
            while (paragraphIndex < paragraphs.Count)
            {
                var paraWordCount = CountWords(paragraphs[paragraphIndex].Content);

                if (currentWords > 0 && currentWords + paraWordCount > maxWords)
                {
                    break;
                }

                currentWords += paraWordCount;
                paragraphIndex++;
            }

            // If a single paragraph exceeds maxWords, delegate to sentence chunker
            if (paragraphIndex == startParagraphIndex + 1 && currentWords > maxWords)
            {
                var para = paragraphs[startParagraphIndex];
                var subChunks = _sentenceChunker.Chunk(para.Content, options);

                foreach (var subChunk in subChunks)
                {
                    chunks.Add(new TextChunk(
                        subChunk.Content,
                        chunkIndex,
                        para.Offset + subChunk.StartOffset,
                        para.Offset + subChunk.EndOffset));
                    chunkIndex++;
                }
            }
            else
            {
                var startOffset = paragraphs[startParagraphIndex].Offset;
                var lastPara = paragraphs[paragraphIndex - 1];
                var endOffset = lastPara.Offset + lastPara.Content.Length;
                var content = text[startOffset..endOffset];

                chunks.Add(new TextChunk(content, chunkIndex, startOffset, endOffset));
                chunkIndex++;
            }
        }

        return chunks;
    }

    private static List<ParagraphSpan> SplitParagraphs(string text)
    {
        var paragraphs = new List<ParagraphSpan>();
        var i = 0;

        while (i < text.Length)
        {
            // Find the start of the paragraph (skip leading paragraph separators)
            var paragraphStart = i;

            // Find the end of this paragraph (look for \n\n or \r\n\r\n)
            var paragraphEnd = FindParagraphEnd(text, i);

            if (paragraphEnd > paragraphStart)
            {
                var content = text[paragraphStart..paragraphEnd];
                paragraphs.Add(new ParagraphSpan(content, paragraphStart));
            }

            // Skip past the paragraph separator
            i = paragraphEnd;
            i = SkipParagraphSeparator(text, i);
        }

        return paragraphs;
    }

    private static int FindParagraphEnd(string text, int start)
    {
        var i = start;

        while (i < text.Length)
        {
            if (text[i] == '\n' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                return i;
            }

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' &&
                i + 2 < text.Length && text[i + 2] == '\r' && i + 3 < text.Length && text[i + 3] == '\n')
            {
                return i;
            }

            i++;
        }

        return text.Length;
    }

    private static int SkipParagraphSeparator(string text, int i)
    {
        if (i >= text.Length)
        {
            return i;
        }

        // Skip \r\n\r\n
        if (i + 3 < text.Length && text[i] == '\r' && text[i + 1] == '\n' &&
            text[i + 2] == '\r' && text[i + 3] == '\n')
        {
            return i + 4;
        }

        // Skip \n\n
        if (i + 1 < text.Length && text[i] == '\n' && text[i + 1] == '\n')
        {
            return i + 2;
        }

        return i;
    }

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

    private readonly record struct ParagraphSpan(string Content, int Offset);
}
