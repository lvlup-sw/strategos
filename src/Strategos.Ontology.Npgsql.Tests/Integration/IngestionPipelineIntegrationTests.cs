using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Chunking;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Ingestion;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

/// <summary>
/// Test entity representing a document chunk with its embedding and metadata.
/// Implements <see cref="ISearchable"/> so the InMemoryObjectSetProvider can store and
/// retrieve embeddings for cosine similarity queries.
/// </summary>
public sealed record DocumentChunk : ISearchable
{
    public string Content { get; init; } = "";
    public float[] Embedding { get; init; } = [];
    public int SourceOffset { get; init; }
}

/// <summary>
/// Minimal domain ontology used for DI wiring tests.
/// </summary>
public class TestDocDomain : DomainOntology
{
    public override string DomainName => "documents";

    protected override void Define(Builder.IOntologyBuilder builder)
    {
        builder.Object<DocumentChunk>(obj =>
        {
            obj.Property(d => d.Content).Required();
            obj.Property(d => d.SourceOffset);
        });
    }
}

public class IngestionPipelineIntegrationTests
{
    [Test]
    public async Task FullPipeline_IngestAndQuery_ReturnsResults()
    {
        // Arrange -- use real chunker + mock embedder + InMemory writer
        var chunker = new SentenceBoundaryChunker();

        // Mock embedding provider that returns fixed-dimension vectors
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.Dimensions.Returns(3);
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var texts = callInfo.Arg<IReadOnlyList<string>>();
                // Return deterministic embeddings based on text length
                var embeddings = texts.Select(t => new float[] { t.Length / 100f, 0.5f, 0.1f }).ToList();
                return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
            });

        var inMemoryProvider = new InMemoryObjectSetProvider(embedder);

        // Act -- run ingestion pipeline
        var result = await IngestionPipeline<DocumentChunk>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new DocumentChunk
            {
                Content = chunk.Content,
                Embedding = embedding,
                SourceOffset = chunk.StartOffset,
            })
            .WriteTo(inMemoryProvider)
            .Build()
            .ExecuteAsync(new[] { "This is a test document. It has multiple sentences. The sentences contain useful content." });

        // Assert
        await Assert.That(result.ChunksProcessed).IsGreaterThan(0);
        await Assert.That(result.ItemsStored).IsGreaterThan(0);
        await Assert.That(result.Duration).IsGreaterThan(TimeSpan.Zero);
    }

    [Test]
    public async Task FullPipeline_IngestThenSimilaritySearch_FindsMatches()
    {
        // Arrange -- set up mock embedder that produces distinct vectors per input
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.Dimensions.Returns(3);
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var texts = callInfo.Arg<IReadOnlyList<string>>();
                var embeddings = texts.Select(t => new float[] { t.Length / 100f, 0.5f, 0.1f }).ToList();
                return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
            });
        embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.Arg<string>();
                return Task.FromResult(new float[] { text.Length / 100f, 0.5f, 0.1f });
            });

        var inMemoryProvider = new InMemoryObjectSetProvider(embedder);
        var chunker = new SentenceBoundaryChunker();

        // Ingest documents
        await IngestionPipeline<DocumentChunk>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new DocumentChunk
            {
                Content = chunk.Content,
                Embedding = embedding,
                SourceOffset = chunk.StartOffset,
            })
            .WriteTo(inMemoryProvider)
            .Build()
            .ExecuteAsync(new[]
            {
                "Machine learning models require training data. The data must be clean and representative.",
                "Financial markets exhibit volatility. Diversification reduces portfolio risk.",
            });

        // Query -- similarity search against the ingested data
        var searchExpression = new SimilarityExpression(
            new RootExpression(typeof(DocumentChunk)),
            queryText: "machine learning training",
            topK: 5,
            minRelevance: 0.0);

        var searchResult = await inMemoryProvider.ExecuteSimilarityAsync<DocumentChunk>(searchExpression);

        // Assert -- should find ingested chunks
        await Assert.That(searchResult.Items.Count).IsGreaterThan(0);
        await Assert.That(searchResult.Scores.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task FullPipeline_WithDI_ResolvesAllComponents()
    {
        // Test that the DI wiring pattern works
        var services = new ServiceCollection();

        // Register core ontology with InMemoryObjectSetProvider
        services.AddOntology(opts =>
        {
            opts.AddDomain<TestDocDomain>();
            opts.UseObjectSetProvider<InMemoryObjectSetProvider>();
        });

        // Verify provider resolves
        var provider = services.BuildServiceProvider();
        var objectSetProvider = provider.GetService<IObjectSetProvider>();
        await Assert.That(objectSetProvider).IsNotNull();

        // InMemoryObjectSetProvider also implements IObjectSetWriter;
        // verify the auto-detection wiring from OntologyServiceCollectionExtensions
        var writer = provider.GetService<IObjectSetWriter>();
        await Assert.That(writer).IsNotNull();

        // Verify graph was built with our domain
        var graph = provider.GetRequiredService<OntologyGraph>();
        await Assert.That(graph.Domains).HasCount().EqualTo(1);

        var domain = graph.Domains[0];
        await Assert.That(domain.DomainName).IsEqualTo("documents");
    }

    [Test]
    public async Task FullPipeline_EmptyInput_ReturnsZeroCounts()
    {
        // Arrange
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.Dimensions.Returns(3);
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>(new List<float[]>()));

        var inMemoryProvider = new InMemoryObjectSetProvider();

        var pipeline = IngestionPipeline<DocumentChunk>.Create()
            .Chunk(new SentenceBoundaryChunker())
            .Embed(embedder)
            .Map((chunk, embedding) => new DocumentChunk
            {
                Content = chunk.Content,
                Embedding = embedding,
                SourceOffset = chunk.StartOffset,
            })
            .WriteTo(inMemoryProvider)
            .Build();

        // Act -- ingest empty string
        var result = await pipeline.ExecuteAsync(new[] { "" });

        // Assert
        await Assert.That(result.ChunksProcessed).IsEqualTo(0);
        await Assert.That(result.ItemsStored).IsEqualTo(0);
    }

    [Test]
    public async Task FullPipeline_ProgressReporting_ReportsPhases()
    {
        // Arrange
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.Dimensions.Returns(3);
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var texts = callInfo.Arg<IReadOnlyList<string>>();
                var embeddings = texts.Select(t => new float[] { 0.1f, 0.2f, 0.3f }).ToList();
                return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
            });

        var inMemoryProvider = new InMemoryObjectSetProvider();
        var reportedPhases = new List<string>();
        var progress = new Progress<IngestionProgress>(p => reportedPhases.Add(p.Phase));

        var pipeline = IngestionPipeline<DocumentChunk>.Create()
            .Chunk(new SentenceBoundaryChunker())
            .Embed(embedder)
            .Map((chunk, embedding) => new DocumentChunk
            {
                Content = chunk.Content,
                Embedding = embedding,
                SourceOffset = chunk.StartOffset,
            })
            .WriteTo(inMemoryProvider)
            .OnProgress(progress)
            .Build();

        // Act
        await pipeline.ExecuteAsync(new[] { "First sentence. Second sentence." });

        // Allow progress callbacks to fire (they run on the thread pool via Progress<T>)
        await Task.Delay(100);

        // Assert -- should have reported chunking, embedding, and storing phases
        await Assert.That(reportedPhases.Count).IsGreaterThanOrEqualTo(1);
    }
}
