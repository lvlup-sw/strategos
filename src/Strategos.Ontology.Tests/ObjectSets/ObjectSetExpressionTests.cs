using System.Linq.Expressions;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetExpressionTests
{
    [Test]
    public async Task RootExpression_Create_HasObjectType()
    {
        // Arrange & Act
        var expression = new RootExpression(typeof(string), "string");

        // Assert
        await Assert.That(expression.ObjectType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task RootExpression_Constructor_RequiresObjectTypeName()
    {
        // Arrange & Act
        var expression = new RootExpression(typeof(string), "trading_documents");

        // Assert — explicit descriptor name is stored on the expression
        await Assert.That(expression.ObjectTypeName).IsEqualTo("trading_documents");

        // Assert — null descriptor name is rejected
        await Assert.That(() => new RootExpression(typeof(string), null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task FilterExpression_Create_HasPredicateAndObjectType()
    {
        // Arrange
        var root = new RootExpression(typeof(string), typeof(string).Name);
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
        var root = new RootExpression(typeof(string), typeof(string).Name);

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
        var root = new RootExpression(typeof(string), typeof(string).Name);

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
        var root = new RootExpression(typeof(string), typeof(string).Name);

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
        var root = new RootExpression(typeof(string), typeof(string).Name);
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
        var root = new RootExpression(typeof(string), typeof(string).Name);
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
        var root = new RootExpression(typeof(string), typeof(string).Name);

        // Act
        var similarity = new SimilarityExpression(root, "query", 5, 0.7);

        // Assert
        await Assert.That(similarity.Metric).IsEqualTo(DistanceMetric.Cosine);
    }

    // -----------------------------------------------------------------------
    // A2: ObjectSetExpression.RootObjectTypeName walk-to-root computed property
    // -----------------------------------------------------------------------

    [Test]
    public async Task FilterExpression_RootObjectTypeName_WalksToSourceRoot()
    {
        // Arrange
        var root = new RootExpression(typeof(Foo), "foo_table");
        Expression<Func<Foo, bool>> predicate = x => true;
        var filter = new FilterExpression(root, predicate);

        // Assert — walks through the filter to the root and returns the explicit name
        await Assert.That(filter.RootObjectTypeName).IsEqualTo("foo_table");
    }

    [Test]
    public async Task InterfaceNarrowExpression_RootObjectTypeName_WalksToSourceRoot()
    {
        // Arrange
        var root = new RootExpression(typeof(Foo), "foo_table");
        var narrow = new InterfaceNarrowExpression(root, typeof(IDisposable));

        // Assert
        await Assert.That(narrow.RootObjectTypeName).IsEqualTo("foo_table");
    }

    [Test]
    public async Task IncludeExpression_RootObjectTypeName_WalksToSourceRoot()
    {
        // Arrange
        var root = new RootExpression(typeof(Foo), "foo_table");
        var include = new IncludeExpression(root, ObjectSetInclusion.Properties);

        // Assert
        await Assert.That(include.RootObjectTypeName).IsEqualTo("foo_table");
    }

    [Test]
    public async Task RawFilterExpression_RootObjectTypeName_WalksToSourceRoot()
    {
        // Arrange
        var root = new RootExpression(typeof(Foo), "foo_table");
        var raw = new RawFilterExpression(root, "Name = 'bar'");

        // Assert
        await Assert.That(raw.RootObjectTypeName).IsEqualTo("foo_table");
    }

    [Test]
    public async Task SimilarityExpression_RootObjectTypeName_WalksToSourceRoot()
    {
        // Arrange — this is the Basileus call site: similarity queries must
        // route to the descriptor the caller dispatched against, not typeof(T).Name
        var root = new RootExpression(typeof(Foo), "foo_table");
        var similarity = new SimilarityExpression(root, "query", 10, 0.5);

        // Assert
        await Assert.That(similarity.RootObjectTypeName).IsEqualTo("foo_table");
    }

    [Test]
    public async Task ComposedExpression_Root_Filter_Similarity_ReturnsRootName()
    {
        // Arrange — nested composition: Similarity(Filter(Root)) must walk two hops
        // back to the original RootExpression's descriptor name.
        var root = new RootExpression(typeof(Foo), "foo_table");
        Expression<Func<Foo, bool>> predicate = x => true;
        var filter = new FilterExpression(root, predicate);
        var similarity = new SimilarityExpression(filter, "query", 10, 0.5);

        // Assert
        await Assert.That(similarity.RootObjectTypeName).IsEqualTo("foo_table");
    }

    // -----------------------------------------------------------------------
    // A3: TraverseLinkExpression.RootObjectTypeName override
    // -----------------------------------------------------------------------

    [Test]
    public async Task TraverseLinkExpression_RootObjectTypeName_ReturnsLinkedTypeName()
    {
        // Arrange — traverse from Position into TradeOrder via the "Orders" link.
        // After traversal, further query operations target the linked type's
        // descriptor, not the source's. Under Option X multi-registered types
        // cannot be link targets (AONT041, later track), so typeof(TLinked).Name
        // is always unambiguous here.
        var root = new RootExpression(typeof(Position), "positions");
        var traverse = new TraverseLinkExpression(root, "Orders", typeof(TradeOrder));

        // Assert — walking should not return "positions"; it should return "TradeOrder"
        await Assert.That(traverse.RootObjectTypeName).IsEqualTo(nameof(TradeOrder));
    }

    // Helper types for A2/A3 tests
    private sealed class Foo
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Position
    {
        public string Symbol { get; set; } = string.Empty;
    }

    private sealed class TradeOrder
    {
        public string Id { get; set; } = string.Empty;
    }
}
