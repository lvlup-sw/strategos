using Strategos.Ontology.Embeddings;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public sealed record WriterTestEntity(string Name) : ISearchable
{
    public float[] Embedding { get; init; } = [];
}

public sealed record SimpleEntity(string Name);

public class InMemoryObjectSetWriterTests
{
    [Test]
    public async Task StoreAsync_Item_CanBeQueriedBack()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        IObjectSetWriter writer = provider;
        var entity = new SimpleEntity("hello");

        // Act
        await writer.StoreAsync(entity);
        var result = await provider.ExecuteAsync<SimpleEntity>(new RootExpression(typeof(SimpleEntity)));

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("hello");
    }

    [Test]
    public async Task StoreAsync_ISearchableItem_UsesEmbedding()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        IObjectSetWriter writer = provider;
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var entity = new WriterTestEntity("test") { Embedding = embedding };

        // Act
        await writer.StoreAsync(entity);
        var result = await provider.ExecuteAsync<WriterTestEntity>(new RootExpression(typeof(WriterTestEntity)));

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("test");
        await Assert.That(result.Items[0].Embedding).IsEqualTo(embedding);
    }

    [Test]
    public async Task StoreBatchAsync_MultipleItems_AllQueryable()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        IObjectSetWriter writer = provider;
        var items = new List<SimpleEntity>
        {
            new("Alice"),
            new("Bob"),
            new("Charlie"),
        };

        // Act
        await writer.StoreBatchAsync<SimpleEntity>(items);
        var result = await provider.ExecuteAsync<SimpleEntity>(new RootExpression(typeof(SimpleEntity)));

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(3);
    }

    [Test]
    public async Task ExecuteSimilarityAsync_WithEmbeddingProvider_UsesRealCosine()
    {
        // Arrange
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.EmbedAsync("search query", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[] { 1f, 0f, 0f }));

        var provider = new InMemoryObjectSetProvider(embedder);
        IObjectSetWriter writer = provider;

        // Store items with known embeddings
        var close = new WriterTestEntity("close") { Embedding = [0.9f, 0.1f, 0f] };
        var far = new WriterTestEntity("far") { Embedding = [0f, 0f, 1f] };

        await writer.StoreAsync(close);
        await writer.StoreAsync(far);

        var expression = new SimilarityExpression(
            new RootExpression(typeof(WriterTestEntity)),
            "search query",
            topK: 10,
            minRelevance: 0.0);

        // Act
        var result = await provider.ExecuteSimilarityAsync<WriterTestEntity>(expression);

        // Assert — "close" should rank higher than "far"
        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(result.Items[0].Name).IsEqualTo("close");
    }

    [Test]
    public async Task ExecuteSimilarityAsync_WithoutEmbeddingProvider_UsesKeywordScoring()
    {
        // Arrange — no embedding provider, should fall back to keyword scoring
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new WriterTestEntity("match") { Embedding = [1f, 0f] }, "the search query text");
        provider.Seed(new WriterTestEntity("nomatch") { Embedding = [0f, 1f] }, "unrelated content");

        var expression = new SimilarityExpression(
            new RootExpression(typeof(WriterTestEntity)),
            "search query",
            topK: 10,
            minRelevance: 0.0);

        // Act
        var result = await provider.ExecuteSimilarityAsync<WriterTestEntity>(expression);

        // Assert — keyword scoring should rank "match" higher
        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("match");
    }

    [Test]
    public async Task Seed_ExistingMethod_StillWorks()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new SimpleEntity("seeded"), "seeded content");

        // Act
        var result = await provider.ExecuteAsync<SimpleEntity>(new RootExpression(typeof(SimpleEntity)));

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("seeded");
    }
}
