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
public sealed class IngestionPipeline<T> : IIngestionPipeline<T>
    where T : class
{
    private readonly ITextChunker _chunker;
    private readonly ChunkOptions? _chunkOptions;
    private readonly IEmbeddingProvider _embedder;
    private readonly Func<TextChunk, float[], T> _mapper;
    private readonly IObjectSetWriter _writer;
    private readonly IProgress<IngestionProgress>? _progress;
    private readonly string? _descriptorName;

    /// <summary>
    /// Creates a new <see cref="IngestionPipelineBuilder{T}"/> for fluent pipeline construction.
    /// </summary>
    /// <returns>A new builder instance.</returns>
    public static IngestionPipelineBuilder<T> Create() => new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestionPipeline{T}"/> class.
    /// </summary>
    /// <remarks>
    /// When <paramref name="descriptorName"/> is <c>null</c>, the pipeline dispatches
    /// through the default <see cref="IObjectSetWriter.StoreBatchAsync{T}(IReadOnlyList{T}, CancellationToken)"/>
    /// overload (conventional descriptor resolution). When non-null, it dispatches
    /// through the explicit-name overload, targeting the chosen descriptor partition.
    /// </remarks>
    internal IngestionPipeline(
        ITextChunker chunker,
        ChunkOptions? chunkOptions,
        IEmbeddingProvider embedder,
        Func<TextChunk, float[], T> mapper,
        IObjectSetWriter writer,
        IProgress<IngestionProgress>? progress,
        string? descriptorName = null)
    {
        _chunker = chunker;
        _chunkOptions = chunkOptions;
        _embedder = embedder;
        _mapper = mapper;
        _writer = writer;
        _progress = progress;
        _descriptorName = descriptorName;
    }

    /// <inheritdoc />
    public async Task<IngestionResult> ExecuteAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var sw = Stopwatch.StartNew();

        // Phase 1: Chunk all texts
        var allChunks = new List<TextChunk>();
        foreach (var text in texts)
        {
            ct.ThrowIfCancellationRequested();
            var chunks = _chunker.Chunk(text, _chunkOptions);
            allChunks.AddRange(chunks);
        }

        return await ExecuteCoreAsync(allChunks, sw, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IngestionResult> ExecuteAsync(IAsyncEnumerable<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var sw = Stopwatch.StartNew();

        // Phase 1: Chunk all texts
        var allChunks = new List<TextChunk>();
        await foreach (var text in texts.WithCancellation(ct).ConfigureAwait(false))
        {
            var chunks = _chunker.Chunk(text, _chunkOptions);
            allChunks.AddRange(chunks);
        }

        return await ExecuteCoreAsync(allChunks, sw, ct).ConfigureAwait(false);
    }

    private async Task<IngestionResult> ExecuteCoreAsync(
        List<TextChunk> allChunks, Stopwatch sw, CancellationToken ct)
    {
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

        if (embeddings.Count != totalChunks)
        {
            throw new InvalidOperationException(
                $"Embedding provider returned {embeddings.Count} embeddings for {totalChunks} chunks. Expected 1:1 alignment.");
        }

        _progress?.Report(new IngestionProgress(totalChunks, totalChunks, "Embedding"));

        // Phase 3: Map each (chunk, embedding) pair to the target type
        var mappedItems = new List<T>(totalChunks);
        for (var i = 0; i < totalChunks; i++)
        {
            ct.ThrowIfCancellationRequested();
            var mapped = _mapper(allChunks[i], embeddings[i]);
            mappedItems.Add(mapped);
        }

        // Phase 4: Store via writer — dispatch through the explicit-name overload
        // when a descriptor name was configured, otherwise use the default overload
        // (conventional descriptor resolution).
        if (_descriptorName is not null)
        {
            await _writer.StoreBatchAsync<T>(_descriptorName, mappedItems, ct).ConfigureAwait(false);
        }
        else
        {
            await _writer.StoreBatchAsync<T>(mappedItems, ct).ConfigureAwait(false);
        }

        _progress?.Report(new IngestionProgress(totalChunks, totalChunks, "Storing"));

        sw.Stop();
        return new IngestionResult(totalChunks, mappedItems.Count, sw.Elapsed);
    }
}
