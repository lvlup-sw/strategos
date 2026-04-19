using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class SimilarObjectSetTests
{
    [Test]
    public async Task SimilarObjectSet_Create_HasExpression()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
        var similarity = new SimilarityExpression(root, "query", 5, 0.7);
        var provider = Substitute.For<IObjectSetProvider>();

        // Act
        var similarSet = new SimilarObjectSet<string>(similarity, provider);

        // Assert
        await Assert.That(similarSet.Expression).IsEqualTo(similarity);
    }

    [Test]
    public async Task SimilarObjectSet_ExecuteAsync_DelegatesToProvider()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
        var similarity = new SimilarityExpression(root, "query", 5, 0.7);
        var provider = Substitute.For<IObjectSetProvider>();
        var expected = new ScoredObjectSetResult<string>(
            ["a", "b"], 2, ObjectSetInclusion.Properties, [0.9, 0.8]);
        provider.ExecuteSimilarityAsync<string>(similarity, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        var similarSet = new SimilarObjectSet<string>(similarity, provider);

        // Act
        var result = await similarSet.ExecuteAsync();

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.Scores[0]).IsEqualTo(0.9);
        await Assert.That(result.Scores[1]).IsEqualTo(0.8);
    }

    [Test]
    public async Task SimilarObjectSet_ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
        var similarity = new SimilarityExpression(root, "query", 5, 0.7);
        var provider = Substitute.For<IObjectSetProvider>();
        var expected = new ScoredObjectSetResult<string>(
            [], 0, ObjectSetInclusion.Properties, []);
        provider.ExecuteSimilarityAsync<string>(similarity, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        var similarSet = new SimilarObjectSet<string>(similarity, provider);
        using var cts = new CancellationTokenSource();

        // Act
        await similarSet.ExecuteAsync(cts.Token);

        // Assert
        await provider.Received(1).ExecuteSimilarityAsync<string>(
            similarity, cts.Token);
    }

    [Test]
    public async Task WithMinRelevance_ReturnsNewInstanceWithUpdatedMinRelevance()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
        var original = new SimilarityExpression(root, "query", topK: 5, minRelevance: 0.7);
        var provider = Substitute.For<IObjectSetProvider>();
        var originalSet = new SimilarObjectSet<string>(original, provider);

        // Act
        var updated = originalSet.WithMinRelevance(0.9);

        // Assert
        await Assert.That(updated).IsNotSameReferenceAs(originalSet);
        await Assert.That(updated.Expression.MinRelevance).IsEqualTo(0.9);
        await Assert.That(originalSet.Expression.MinRelevance).IsEqualTo(0.7);
    }

    [Test]
    public async Task Take_ReturnsNewInstanceWithUpdatedTopK()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
        var original = new SimilarityExpression(root, "query", topK: 5, minRelevance: 0.7);
        var provider = Substitute.For<IObjectSetProvider>();
        var originalSet = new SimilarObjectSet<string>(original, provider);

        // Act
        var updated = originalSet.Take(20);

        // Assert
        await Assert.That(updated).IsNotSameReferenceAs(originalSet);
        await Assert.That(updated.Expression.TopK).IsEqualTo(20);
        await Assert.That(originalSet.Expression.TopK).IsEqualTo(5);
    }

    [Test]
    public async Task WithMetric_ReturnsNewInstanceWithUpdatedMetric()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
        var original = new SimilarityExpression(
            root, "query", topK: 5, minRelevance: 0.7, metric: DistanceMetric.Cosine);
        var provider = Substitute.For<IObjectSetProvider>();
        var originalSet = new SimilarObjectSet<string>(original, provider);

        // Act
        var updated = originalSet.WithMetric(DistanceMetric.L2);

        // Assert
        await Assert.That(updated).IsNotSameReferenceAs(originalSet);
        await Assert.That(updated.Expression.Metric).IsEqualTo(DistanceMetric.L2);
        await Assert.That(originalSet.Expression.Metric).IsEqualTo(DistanceMetric.Cosine);
    }

    [Test]
    public async Task FluentChain_PreservesQueryTextAndIsImmutable()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
        var original = new SimilarityExpression(root, "find docs", topK: 5, minRelevance: 0.7);
        var provider = Substitute.For<IObjectSetProvider>();
        var originalSet = new SimilarObjectSet<string>(original, provider);

        // Act
        var final = originalSet
            .WithMinRelevance(0.8)
            .Take(15)
            .WithMetric(DistanceMetric.Cosine);

        // Assert — final has updated values
        await Assert.That(final.Expression.QueryText).IsEqualTo("find docs");
        await Assert.That(final.Expression.MinRelevance).IsEqualTo(0.8);
        await Assert.That(final.Expression.TopK).IsEqualTo(15);
        await Assert.That(final.Expression.Metric).IsEqualTo(DistanceMetric.Cosine);

        // Assert — original is unchanged
        await Assert.That(originalSet.Expression.MinRelevance).IsEqualTo(0.7);
        await Assert.That(originalSet.Expression.TopK).IsEqualTo(5);
    }
}
