using Strategos.Ontology.Chunking;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Ingestion;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Ingestion;

public sealed record BuilderTestEntity(string Text, float[] Vector);

public class IngestionPipelineBuilderTests
{
    [Test]
    public void Build_MissingEmbed_ThrowsInvalidOperationException()
    {
        // Arrange
        var chunker = Substitute.For<ITextChunker>();
        var writer = Substitute.For<IObjectSetWriter>();

        var builder = IngestionPipeline<BuilderTestEntity>.Create()
            .Chunk(chunker)
            .Map((chunk, embedding) => new BuilderTestEntity(chunk.Content, embedding))
            .WriteTo(writer);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void Build_MissingMap_ThrowsInvalidOperationException()
    {
        // Arrange
        var chunker = Substitute.For<ITextChunker>();
        var embedder = Substitute.For<IEmbeddingProvider>();
        var writer = Substitute.For<IObjectSetWriter>();

        var builder = IngestionPipeline<BuilderTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .WriteTo(writer);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void Build_MissingWriteTo_ThrowsInvalidOperationException()
    {
        // Arrange
        var chunker = Substitute.For<ITextChunker>();
        var embedder = Substitute.For<IEmbeddingProvider>();

        var builder = IngestionPipeline<BuilderTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new BuilderTestEntity(chunk.Content, embedding));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public async Task Build_AllComponentsProvided_ReturnsInstance()
    {
        // Arrange
        var chunker = Substitute.For<ITextChunker>();
        var embedder = Substitute.For<IEmbeddingProvider>();
        var writer = Substitute.For<IObjectSetWriter>();

        // Act
        var pipeline = IngestionPipeline<BuilderTestEntity>.Create()
            .Chunk(chunker)
            .Embed(embedder)
            .Map((chunk, embedding) => new BuilderTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .Build();

        // Assert
        await Assert.That(pipeline).IsNotNull();
        await Assert.That(pipeline).IsTypeOf<IngestionPipeline<BuilderTestEntity>>();
    }

    [Test]
    public void Build_ChunkOptionsOverload_ThrowsInvalidOperationException()
    {
        // Arrange & Act
        var builder = IngestionPipeline<BuilderTestEntity>.Create()
            .Chunk(new ChunkOptions { MaxTokens = 256 });

        // Assert — should throw because no default chunker is available
        Assert.Throws<InvalidOperationException>(() =>
            builder.Embed(Substitute.For<IEmbeddingProvider>())
                .Map((chunk, embedding) => new BuilderTestEntity(chunk.Content, embedding))
                .WriteTo(Substitute.For<IObjectSetWriter>())
                .Build());
    }

    [Test]
    public async Task Build_WithoutChunker_UsesDefaultChunker()
    {
        // When no chunker is explicitly provided, Build should still succeed
        // using a basic default chunker that returns the whole text as one chunk.
        var embedder = Substitute.For<IEmbeddingProvider>();
        var writer = Substitute.For<IObjectSetWriter>();

        var pipeline = IngestionPipeline<BuilderTestEntity>.Create()
            .Embed(embedder)
            .Map((chunk, embedding) => new BuilderTestEntity(chunk.Content, embedding))
            .WriteTo(writer)
            .Build();

        await Assert.That(pipeline).IsNotNull();
    }
}
