using Strategos.Ontology.Chunking;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Ingestion;

/// <summary>
/// Fluent builder for constructing an <see cref="IngestionPipeline{T}"/>.
/// </summary>
/// <typeparam name="T">The target domain object type produced by the pipeline.</typeparam>
public sealed class IngestionPipelineBuilder<T>
    where T : class
{
    private ITextChunker? _chunker;
    private ChunkOptions? _chunkOptions;
    private IEmbeddingProvider? _embedder;
    private Func<TextChunk, float[], T>? _mapper;
    private IObjectSetWriter? _writer;
    private string? _descriptorName;
    private IProgress<IngestionProgress>? _progress;

    /// <summary>
    /// Sets the text chunker to use for splitting input texts.
    /// </summary>
    /// <param name="chunker">The text chunker implementation.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> Chunk(ITextChunker chunker)
    {
        ArgumentNullException.ThrowIfNull(chunker);
        _chunker = chunker;
        _chunkOptions = null;
        return this;
    }

    /// <summary>
    /// Sets chunk options using a <see cref="SentenceBoundaryChunker"/> as the default chunker.
    /// </summary>
    /// <param name="options">The chunk options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> Chunk(ChunkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _chunker = new SentenceBoundaryChunker();
        _chunkOptions = options;
        return this;
    }

    /// <summary>
    /// Sets the embedding provider to use for vectorizing text chunks.
    /// </summary>
    /// <param name="provider">The embedding provider implementation.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> Embed(IEmbeddingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _embedder = provider;
        return this;
    }

    /// <summary>
    /// Sets the mapper function that transforms a text chunk and its embedding into the target type.
    /// </summary>
    /// <param name="mapper">A function that takes a <see cref="TextChunk"/> and its embedding vector and returns <typeparamref name="T"/>.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> Map(Func<TextChunk, float[], T> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _mapper = mapper;
        return this;
    }

    /// <summary>
    /// Sets the writer to use for storing mapped items. The pipeline will dispatch
    /// through the default <see cref="IObjectSetWriter.StoreBatchAsync{T}(IReadOnlyList{T}, CancellationToken)"/>
    /// overload, which targets the descriptor resolved by convention for <typeparamref name="T"/>.
    /// </summary>
    /// <param name="writer">The object set writer implementation.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> WriteTo(IObjectSetWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _descriptorName = null;
        return this;
    }

    /// <summary>
    /// Sets the writer and the descriptor name to use for storing mapped items.
    /// The pipeline will dispatch through the explicit-name
    /// <see cref="IObjectSetWriter.StoreBatchAsync{T}(string, IReadOnlyList{T}, CancellationToken)"/>
    /// overload, targeting the descriptor partition identified by
    /// <paramref name="descriptorName"/>. Use this overload when <typeparamref name="T"/>
    /// is registered against multiple descriptors and the target partition must
    /// be chosen explicitly.
    /// </summary>
    /// <param name="writer">The object set writer implementation.</param>
    /// <param name="descriptorName">
    /// The descriptor name selecting which registered partition to write to.
    /// </param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> WriteTo(IObjectSetWriter writer, string descriptorName)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrEmpty(descriptorName);
        _writer = writer;
        _descriptorName = descriptorName;
        return this;
    }

    /// <summary>
    /// Sets the progress reporter for tracking pipeline execution.
    /// </summary>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> OnProgress(IProgress<IngestionProgress> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured <see cref="IngestionPipeline{T}"/>.
    /// </summary>
    /// <returns>A ready-to-execute pipeline.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required components are missing.</exception>
    public IngestionPipeline<T> Build()
    {
        if (_embedder is null)
        {
            throw new InvalidOperationException("Embed(IEmbeddingProvider) must be called before Build().");
        }

        if (_mapper is null)
        {
            throw new InvalidOperationException("Map(Func<TextChunk, float[], T>) must be called before Build().");
        }

        if (_writer is null)
        {
            throw new InvalidOperationException("WriteTo(IObjectSetWriter) must be called before Build().");
        }

        // When no chunker is set, use a default that returns the full text as a single chunk.
        var chunker = _chunker ?? new PassthroughChunker();

        return new IngestionPipeline<T>(chunker, _chunkOptions, _embedder, _mapper, _writer, _progress, _descriptorName);
    }

    /// <summary>
    /// A minimal chunker that returns the entire text as a single chunk.
    /// Used when no explicit chunker is provided.
    /// </summary>
    private sealed class PassthroughChunker : ITextChunker
    {
        public IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions? options = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return [];
            }

            return [new TextChunk(text, 0, 0, text.Length)];
        }
    }
}
