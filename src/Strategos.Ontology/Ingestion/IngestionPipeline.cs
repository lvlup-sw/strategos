using System.Diagnostics;
using Strategos.Ontology.Chunking;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Ingestion;

/// <summary>
/// A pipeline that chunks text, generates embeddings, maps to domain objects,
/// and stores them via an <see cref="IObjectSetWriter"/>.
/// </summary>
/// <typeparam name="T">The target domain object type.</typeparam>
public sealed class IngestionPipeline<T>
    where T : class
{
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingProvider _embedder;
    private readonly Func<TextChunk, float[], T> _mapper;
    private readonly IObjectSetWriter _writer;
    private readonly IProgress<IngestionProgress>? _progress;

    /// <summary>
    /// Creates a new <see cref="IngestionPipelineBuilder{T}"/> for fluent pipeline construction.
    /// </summary>
    /// <returns>A new builder instance.</returns>
    public static IngestionPipelineBuilder<T> Create() => new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestionPipeline{T}"/> class.
    /// </summary>
    internal IngestionPipeline(
        ITextChunker chunker,
        IEmbeddingProvider embedder,
        Func<TextChunk, float[], T> mapper,
        IObjectSetWriter writer,
        IProgress<IngestionProgress>? progress)
    {
        _chunker = chunker;
        _embedder = embedder;
        _mapper = mapper;
        _writer = writer;
        _progress = progress;
    }

    /// <summary>
    /// Executes the ingestion pipeline over the provided texts.
    /// </summary>
    /// <param name="texts">The input texts to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IngestionResult"/> describing what was processed.</returns>
    public async Task<IngestionResult> ExecuteAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var sw = Stopwatch.StartNew();

        // Phase 1: Chunk all texts
        var allChunks = new List<TextChunk>();
        foreach (var text in texts)
        {
            ct.ThrowIfCancellationRequested();
            var chunks = _chunker.Chunk(text);
            allChunks.AddRange(chunks);
        }

        var totalChunks = allChunks.Count;

        if (totalChunks == 0)
        {
            sw.Stop();
            return new IngestionResult(0, 0, sw.Elapsed);
        }

        _progress?.Report(new IngestionProgress(0, totalChunks, "Chunking"));

        // Phase 2: Embed all chunk contents in a single batch
        var chunkContents = allChunks.Select(c => c.Content).ToList();
        var embeddings = await _embedder.EmbedBatchAsync(chunkContents, ct).ConfigureAwait(false);

        _progress?.Report(new IngestionProgress(totalChunks, totalChunks, "Embedding"));

        // Phase 3: Map each (chunk, embedding) pair to the target type
        var mappedItems = new List<T>(totalChunks);
        for (var i = 0; i < totalChunks; i++)
        {
            ct.ThrowIfCancellationRequested();
            var mapped = _mapper(allChunks[i], embeddings[i]);
            mappedItems.Add(mapped);
        }

        // Phase 4: Store via writer
        await _writer.StoreBatchAsync<T>(mappedItems, ct).ConfigureAwait(false);

        _progress?.Report(new IngestionProgress(totalChunks, totalChunks, "Storing"));

        sw.Stop();
        return new IngestionResult(totalChunks, mappedItems.Count, sw.Elapsed);
    }
}
