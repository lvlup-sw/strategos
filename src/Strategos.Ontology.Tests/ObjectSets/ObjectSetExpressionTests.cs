using System.Linq.Expressions;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetExpressionTests
{
    [Test]
    public async Task RootExpression_Create_HasObjectType()
    {
        // Arrange & Act
        var expression = new RootExpression(typeof(string));

        // Assert
        await Assert.That(expression.ObjectType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task FilterExpression_Create_HasPredicateAndObjectType()
    {
        // Arrange
        var root = new RootExpression(typeof(string));
        Expression<Func<string, bool>> predicate = s => s.Length > 0;

        // Act
        var filter = new FilterExpression(root, predicate);

        // Assert
        await Assert.That(filter.Predicate).IsNotNull();
        await Assert.That(filter.ObjectType).IsEqualTo(typeof(string));
        await Assert.That(filter.Source).IsEqualTo(root);
    }

    [Test]
    public async Task TraverseLinkExpression_Create_HasLinkNameAndSourceExpression()
    {
        // Arrange
        var root = new RootExpression(typeof(string));

        // Act
        var traverse = new TraverseLinkExpression(root, "Children", typeof(int));

        // Assert
        await Assert.That(traverse.LinkName).IsEqualTo("Children");
        await Assert.That(traverse.Source).IsEqualTo(root);
        await Assert.That(traverse.ObjectType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task InterfaceNarrowExpression_Create_HasInterfaceType()
    {
        // Arrange
        var root = new RootExpression(typeof(string));

        // Act
        var narrow = new InterfaceNarrowExpression(root, typeof(IDisposable));

        // Assert
        await Assert.That(narrow.InterfaceType).IsEqualTo(typeof(IDisposable));
        await Assert.That(narrow.ObjectType).IsEqualTo(typeof(IDisposable));
        await Assert.That(narrow.Source).IsEqualTo(root);
    }

    [Test]
    public async Task SimilarityExpression_Create_HasAllProperties()
    {
        // Arrange
        var root = new RootExpression(typeof(string));

        // Act
        var similarity = new SimilarityExpression(
            root, "find similar items", 10, 0.8, DistanceMetric.L2,
            "ContentEmbedding", null);

        // Assert
        await Assert.That(similarity.Source).IsEqualTo(root);
        await Assert.That(similarity.QueryText).IsEqualTo("find similar items");
        await Assert.That(similarity.TopK).IsEqualTo(10);
        await Assert.That(similarity.MinRelevance).IsEqualTo(0.8);
        await Assert.That(similarity.Metric).IsEqualTo(DistanceMetric.L2);
        await Assert.That(similarity.EmbeddingPropertyName).IsEqualTo("ContentEmbedding");
        await Assert.That(similarity.QueryVector).IsNull();
        await Assert.That(similarity.Filters).IsNull();
        await Assert.That(similarity.ObjectType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task SimilarityExpression_WithQueryVector_StoresVector()
    {
        // Arrange
        var root = new RootExpression(typeof(string));
        var vector = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var similarity = new SimilarityExpression(
            root, "query", 5, 0.7, DistanceMetric.Cosine,
            null, vector);

        // Assert
        await Assert.That(similarity.QueryVector).IsNotNull();
        await Assert.That(similarity.QueryVector!.Length).IsEqualTo(3);
    }

    [Test]
    public async Task SimilarityExpression_WithFilters_StoresFilters()
    {
        // Arrange
        var root = new RootExpression(typeof(string));
        var filters = new Dictionary<string, object> { ["category"] = "tech" };

        // Act
        var similarity = new SimilarityExpression(
            root, "query", 5, 0.7, DistanceMetric.Cosine,
            null, null, filters);

        // Assert
        await Assert.That(similarity.Filters).IsNotNull();
        await Assert.That(similarity.Filters!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SimilarityExpression_DefaultMetric_IsCosine()
    {
        // Arrange
        var root = new RootExpression(typeof(string));

        // Act
        var similarity = new SimilarityExpression(root, "query", 5, 0.7);

        // Assert
        await Assert.That(similarity.Metric).IsEqualTo(DistanceMetric.Cosine);
    }
}
