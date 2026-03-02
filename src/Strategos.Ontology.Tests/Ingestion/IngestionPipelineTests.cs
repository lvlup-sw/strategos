using Strategos.Ontology.Chunking;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Ingestion;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Ingestion;

public sealed record PipelineTestEntity(string Text, float[] Vector);

public class IngestionPipelineTests
{
    private static ITextChunker CreateMockChunker(params TextChunk[] chunks)
    {
        var chunker = Substitute.For<ITextChunker>();
        chunker.Chunk(Arg.Any<string>(), Arg.Any<ChunkOptions?>())
            .Returns(new List<TextChunk>(chunks));
        return chunker;
    }

    private static IEmbeddingProvider CreateMockEmbedder(params float[][] embeddings)
    {
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>(embeddings));
        return embedder;
    }

    [Test]
    public async Task ExecuteAsync_SingleText_ChunksEmbedsMapAndStores()
    {
        // Arrange
        var chunker = CreateMockChunker(new TextChunk("hello world", 0, 0, 11));
        var embedder = CreateMockEmbedder([1f, 2f]);
        var writer = Substitute.For<IObjectSetWriter>();

        PipelineTestEntity? captured = null;
        writer.StoreBatchAsync(Arg.Any<IReadOnlyList<PipelineTestEntity>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var items = callInfo.Arg<IReadOnlyList<PipelineTestEntity>>();
                if (items.Count > 0)
                {
                    captured = items[0];
                }

                return Task.CompletedTask;
            });

        var pipeline = IngestionPipeline<PipelineTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new PipelineTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .Build();

        // Act
        var result = await pipeline.ExecuteAsync(["hello world"]);

        // Assert
        await Assert.That(result.ChunksProcessed).IsEqualTo(1);
        await Assert.That(result.ItemsStored).IsEqualTo(1);
        chunker.Received(1).Chunk(Arg.Any<string>(), Arg.Any<ChunkOptions?>());
        await embedder.Received(1).EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await writer.Received(1).StoreBatchAsync(Arg.Any<IReadOnlyList<PipelineTestEntity>>(), Arg.Any<CancellationToken>());
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Text).IsEqualTo("hello world");
    }

    [Test]
    public async Task ExecuteAsync_MultipleTexts_ProcessesAll()
    {
        // Arrange — each text produces 1 chunk
        var chunker = Substitute.For<ITextChunker>();
        chunker.Chunk("text1", Arg.Any<ChunkOptions?>())
            .Returns(new List<TextChunk> { new("text1", 0, 0, 5) });
        chunker.Chunk("text2", Arg.Any<ChunkOptions?>())
            .Returns(new List<TextChunk> { new("text2", 0, 0, 5) });

        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>(new[] { new float[] { 1f }, new float[] { 2f } }));

        var writer = Substitute.For<IObjectSetWriter>();

        var pipeline = IngestionPipeline<PipelineTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new PipelineTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .Build();

        // Act
        var result = await pipeline.ExecuteAsync(["text1", "text2"]);

        // Assert
        await Assert.That(result.ChunksProcessed).IsEqualTo(2);
        await Assert.That(result.ItemsStored).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_EmptyInput_ReturnsZeroCounts()
    {
        // Arrange
        var chunker = Substitute.For<ITextChunker>();
        var embedder = Substitute.For<IEmbeddingProvider>();
        var writer = Substitute.For<IObjectSetWriter>();

        var pipeline = IngestionPipeline<PipelineTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new PipelineTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .Build();

        // Act
        var result = await pipeline.ExecuteAsync([]);

        // Assert
        await Assert.That(result.ChunksProcessed).IsEqualTo(0);
        await Assert.That(result.ItemsStored).IsEqualTo(0);
        await Assert.That(result.Duration).IsGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task ExecuteAsync_BatchesEmbeddings_CallsEmbedBatchAsync()
    {
        // Arrange — 2 chunks from 1 text, verify batch call not individual
        var chunker = Substitute.For<ITextChunker>();
        chunker.Chunk(Arg.Any<string>(), Arg.Any<ChunkOptions?>())
            .Returns(new List<TextChunk>
            {
                new("chunk1", 0, 0, 6),
                new("chunk2", 1, 7, 13),
            });

        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>(new[] { new float[] { 1f }, new float[] { 2f } }));

        var writer = Substitute.For<IObjectSetWriter>();

        var pipeline = IngestionPipeline<PipelineTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new PipelineTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .Build();

        // Act
        await pipeline.ExecuteAsync(["some long text"]);

        // Assert — EmbedBatchAsync called, not individual EmbedAsync
        await embedder.Received(1).EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await embedder.DidNotReceive().EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ReportsProgress_WhenProgressProvided()
    {
        // Arrange
        var chunker = CreateMockChunker(new TextChunk("chunk1", 0, 0, 6));
        var embedder = CreateMockEmbedder([1f, 2f]);
        var writer = Substitute.For<IObjectSetWriter>();

        var reports = new List<IngestionProgress>();
        var progress = Substitute.For<IProgress<IngestionProgress>>();
        progress.When(p => p.Report(Arg.Any<IngestionProgress>()))
            .Do(callInfo => reports.Add(callInfo.Arg<IngestionProgress>()));

        var pipeline = IngestionPipeline<PipelineTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new PipelineTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .OnProgress(progress)
            .Build();

        // Act
        await pipeline.ExecuteAsync(["hello"]);

        // Assert — at least one progress report should have been made
        await Assert.That(reports.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsCorrectResult_ChunksAndItemCounts()
    {
        // Arrange — 1 text with 3 chunks
        var chunker = Substitute.For<ITextChunker>();
        chunker.Chunk(Arg.Any<string>(), Arg.Any<ChunkOptions?>())
            .Returns(new List<TextChunk>
            {
                new("a", 0, 0, 1),
                new("b", 1, 2, 3),
                new("c", 2, 4, 5),
            });

        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>(new[] { new float[] { 1f }, new float[] { 2f }, new float[] { 3f } }));

        var writer = Substitute.For<IObjectSetWriter>();

        var pipeline = IngestionPipeline<PipelineTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new PipelineTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .Build();

        // Act
        var result = await pipeline.ExecuteAsync(["some text"]);

        // Assert
        await Assert.That(result.ChunksProcessed).IsEqualTo(3);
        await Assert.That(result.ItemsStored).IsEqualTo(3);
        await Assert.That(result.Duration).IsGreaterThanOrEqualTo(TimeSpan.Zero);
    }
}
