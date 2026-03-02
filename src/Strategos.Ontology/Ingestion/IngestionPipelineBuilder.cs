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
    private bool _chunkerFromOptions;
    private IEmbeddingProvider? _embedder;
    private Func<TextChunk, float[], T>? _mapper;
    private IObjectSetWriter? _writer;
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
        _chunkerFromOptions = false;
        return this;
    }

    /// <summary>
    /// Sets chunk options. Currently no default chunker is available; calling this
    /// without also calling <see cref="Chunk(ITextChunker)"/> will cause
    /// <see cref="Build"/> to throw.
    /// </summary>
    /// <param name="options">The chunk options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> Chunk(ChunkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _chunkerFromOptions = true;
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
    /// Sets the writer to use for storing mapped items.
    /// </summary>
    /// <param name="writer">The object set writer implementation.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IngestionPipelineBuilder<T> WriteTo(IObjectSetWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
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
        if (_chunkerFromOptions && _chunker is null)
        {
            throw new InvalidOperationException(
                "No chunker provided and no default available. Use Chunk(ITextChunker) instead.");
        }

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

        return new IngestionPipeline<T>(chunker, _embedder, _mapper, _writer, _progress);
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
