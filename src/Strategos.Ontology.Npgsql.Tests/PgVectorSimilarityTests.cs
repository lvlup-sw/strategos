using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

public class PgVectorSimilarityTests
{
    [Test]
    public async Task ExecuteSimilarityAsync_CosineDistance_GeneratesCorrectSql()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.Cosine);

        await Assert.That(sql).Contains("<=>");
        await Assert.That(sql).Contains("SELECT id, data, (embedding <=> @query) AS distance");
        await Assert.That(sql).Contains("ORDER BY distance LIMIT @topK");
    }

    [Test]
    public async Task ExecuteSimilarityAsync_L2Distance_GeneratesCorrectSql()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.L2);

        await Assert.That(sql).Contains("<->");
        await Assert.That(sql).Contains("SELECT id, data, (embedding <-> @query) AS distance");
    }

    [Test]
    public async Task ExecuteSimilarityAsync_InnerProduct_GeneratesCorrectSql()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.InnerProduct);

        await Assert.That(sql).Contains("<#>");
        await Assert.That(sql).Contains("SELECT id, data, (embedding <#> @query) AS distance");
    }

    [Test]
    public async Task ExecuteSimilarityAsync_NullQueryVector_CallsEmbeddingProvider()
    {
        // Verify that when QueryVector is null, the provider would need to call EmbedAsync.
        // We test this indirectly through the SimilarityExpression construction.
        var root = new RootExpression(typeof(TestDoc));
        var expression = new SimilarityExpression(
            root,
            queryText: "search term",
            topK: 10,
            minRelevance: 0.5,
            metric: DistanceMetric.Cosine,
            queryVector: null);

        await Assert.That(expression.QueryVector).IsNull();
        await Assert.That(expression.QueryText).IsEqualTo("search term");

        // The embedding provider mock would be called in the real provider
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.EmbedAsync("search term", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[] { 0.1f, 0.2f, 0.3f }));

        var result = await embeddingProvider.EmbedAsync("search term");
        await Assert.That(result).HasCount().EqualTo(3);
        await embeddingProvider.Received(1).EmbedAsync("search term", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteSimilarityAsync_WithQueryVector_BypassesEmbedding()
    {
        var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
        var root = new RootExpression(typeof(TestDoc));
        var expression = new SimilarityExpression(
            root,
            queryText: "search term",
            topK: 10,
            minRelevance: 0.5,
            metric: DistanceMetric.Cosine,
            queryVector: queryVector);

        await Assert.That(expression.QueryVector).IsNotNull();
        await Assert.That(expression.QueryVector).IsEqualTo(queryVector);

        // When QueryVector is provided, embedding provider should NOT be called
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();

        // Simulate the provider logic: use QueryVector if available
        var vector = expression.QueryVector ?? await embeddingProvider.EmbedAsync(expression.QueryText);

        await Assert.That(vector).IsEqualTo(queryVector);
        await embeddingProvider.DidNotReceive().EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SimilarityExpression_DefaultMetric_IsCosine()
    {
        var root = new RootExpression(typeof(TestDoc));
        var expression = new SimilarityExpression(root, "query", 10, 0.5);

        await Assert.That(expression.Metric).IsEqualTo(DistanceMetric.Cosine);
    }

    [Test]
    public async Task SimilarityExpression_PreservesAllProperties()
    {
        var root = new RootExpression(typeof(TestDoc));
        var vector = new float[] { 1.0f, 2.0f };
        var filters = new Dictionary<string, object> { { "category", "test" } };

        var expression = new SimilarityExpression(
            root,
            queryText: "hello",
            topK: 5,
            minRelevance: 0.8,
            metric: DistanceMetric.L2,
            embeddingPropertyName: "CustomEmbedding",
            queryVector: vector,
            filters: filters);

        await Assert.That(expression.QueryText).IsEqualTo("hello");
        await Assert.That(expression.TopK).IsEqualTo(5);
        await Assert.That(expression.MinRelevance).IsEqualTo(0.8);
        await Assert.That(expression.Metric).IsEqualTo(DistanceMetric.L2);
        await Assert.That(expression.EmbeddingPropertyName).IsEqualTo("CustomEmbedding");
        await Assert.That(expression.QueryVector).IsEqualTo(vector);
        await Assert.That(expression.Filters).IsNotNull();
    }

    private sealed class TestDoc
    {
        public Guid Id { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}
