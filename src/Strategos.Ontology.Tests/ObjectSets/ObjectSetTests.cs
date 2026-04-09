using System.Linq.Expressions;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetTests
{
    private IObjectSetProvider _provider = null!;
    private IActionDispatcher _dispatcher = null!;
    private IEventStreamProvider _eventProvider = null!;

    [Before(Test)]
    public Task Setup()
    {
        _provider = Substitute.For<IObjectSetProvider>();
        _dispatcher = Substitute.For<IActionDispatcher>();
        _eventProvider = Substitute.For<IEventStreamProvider>();
        return Task.CompletedTask;
    }

    [Test]
    public async Task ObjectSet_Create_HasRootExpression()
    {
        // Arrange & Act
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Assert
        await Assert.That(set.Expression).IsTypeOf<RootExpression>();
        await Assert.That(set.Expression.ObjectType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task ObjectSet_Where_ReturnsNewObjectSetWithFilterExpression()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var filtered = set.Where(s => s.Length > 5);

        // Assert
        await Assert.That(filtered.Expression).IsTypeOf<FilterExpression>();
        var filterExpr = (FilterExpression)filtered.Expression;
        await Assert.That(filterExpr.Source).IsTypeOf<RootExpression>();
    }

    [Test]
    public async Task ObjectSet_Where_PreservesOriginalObjectSet()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var filtered = set.Where(s => s.Length > 5);

        // Assert — original is unchanged
        await Assert.That(set.Expression).IsTypeOf<RootExpression>();
        await Assert.That(filtered).IsNotEqualTo(set);
    }

    [Test]
    public async Task ObjectSet_MultipleWheres_ChainsExpressions()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var filtered = set
            .Where(s => s.Length > 5)
            .Where(s => s.StartsWith("A"));

        // Assert
        await Assert.That(filtered.Expression).IsTypeOf<FilterExpression>();
        var outerFilter = (FilterExpression)filtered.Expression;
        await Assert.That(outerFilter.Source).IsTypeOf<FilterExpression>();
        var innerFilter = (FilterExpression)outerFilter.Source;
        await Assert.That(innerFilter.Source).IsTypeOf<RootExpression>();
    }

    [Test]
    public async Task ObjectSet_SimilarTo_ReturnsSimilarObjectSet()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var similar = set.SimilarTo("search query");

        // Assert
        await Assert.That(similar).IsNotNull();
        await Assert.That(similar.Expression).IsTypeOf<SimilarityExpression>();
    }

    [Test]
    public async Task ObjectSet_SimilarTo_FluentChain_ProducesCorrectExpression()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act — fluent chain replaces the legacy positional/optional-arg API
        var similar = set
            .SimilarTo("find related items")
            .Take(10)
            .WithMinRelevance(0.8)
            .WithMetric(DistanceMetric.L2);

        // Assert
        var expr = similar.Expression;
        await Assert.That(expr.QueryText).IsEqualTo("find related items");
        await Assert.That(expr.TopK).IsEqualTo(10);
        await Assert.That(expr.MinRelevance).IsEqualTo(0.8);
        await Assert.That(expr.Metric).IsEqualTo(DistanceMetric.L2);
        await Assert.That(expr.Source).IsTypeOf<RootExpression>();
    }

    [Test]
    public async Task ObjectSet_SimilarTo_AfterWhere_ChainsExpressions()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var similar = set.Where(s => s.Length > 5).SimilarTo("query");

        // Assert
        var expr = similar.Expression;
        await Assert.That(expr.Source).IsTypeOf<FilterExpression>();
    }

    [Test]
    public async Task SimilarTo_WithOnlyQueryText_ReturnsSimilarObjectSetWithDefaults()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var similar = set.SimilarTo("query text");

        // Assert
        await Assert.That(similar.Expression.QueryText).IsEqualTo("query text");
        await Assert.That(similar.Expression.TopK).IsEqualTo(5);
        await Assert.That(similar.Expression.MinRelevance).IsEqualTo(0.7);
        await Assert.That(similar.Expression.Metric).IsEqualTo(DistanceMetric.Cosine);
        await Assert.That(similar.Expression.EmbeddingPropertyName).IsNull();
        await Assert.That(similar.Expression.QueryVector).IsNull();
    }

    [Test]
    public async Task SimilarTo_HasOnlyOneParameter()
    {
        var method = typeof(ObjectSet<string>).GetMethod("SimilarTo");
        await Assert.That(method).IsNotNull();
        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(1);
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task ObjectSet_Constructor_ThreadsDescriptorNameIntoRootExpression()
    {
        // Arrange & Act — use the new descriptor-name-first constructor
        var set = new ObjectSet<Foo>(
            "trading_documents", _provider, _dispatcher, _eventProvider);

        // Assert — the root expression must carry the explicit descriptor name,
        // not the CLR type name (which would be "Foo").
        await Assert.That(set.Expression).IsTypeOf<RootExpression>();
        var root = (RootExpression)set.Expression;
        await Assert.That(root.ObjectTypeName).IsEqualTo("trading_documents");
        await Assert.That(root.ObjectType).IsEqualTo(typeof(Foo));
    }

    private sealed class Foo
    {
    }
}
