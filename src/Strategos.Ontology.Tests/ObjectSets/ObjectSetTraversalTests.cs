using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetTraversalTests
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
    public async Task ObjectSet_TraverseLink_ReturnsObjectSetOfLinkedType()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var linked = set.TraverseLink<object>("Children");

        // Assert
        await Assert.That(linked).IsNotNull();
        await Assert.That(linked.Expression.ObjectType).IsEqualTo(typeof(object));
    }

    [Test]
    public async Task ObjectSet_TraverseLink_AddsTraverseLinkExpression()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var linked = set.TraverseLink<object>("Children");

        // Assert
        await Assert.That(linked.Expression).IsTypeOf<TraverseLinkExpression>();
        var traverseExpr = (TraverseLinkExpression)linked.Expression;
        await Assert.That(traverseExpr.LinkName).IsEqualTo("Children");
        await Assert.That(traverseExpr.Source).IsTypeOf<RootExpression>();
    }

    [Test]
    public async Task ObjectSet_OfInterface_ReturnsObjectSetOfInterfaceType()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var narrowed = set.OfInterface<IDisposable>();

        // Assert
        await Assert.That(narrowed).IsNotNull();
        await Assert.That(narrowed.Expression.ObjectType).IsEqualTo(typeof(IDisposable));
    }

    [Test]
    public async Task ObjectSet_OfInterface_AddsInterfaceNarrowExpression()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var narrowed = set.OfInterface<IDisposable>();

        // Assert
        await Assert.That(narrowed.Expression).IsTypeOf<InterfaceNarrowExpression>();
        var narrowExpr = (InterfaceNarrowExpression)narrowed.Expression;
        await Assert.That(narrowExpr.InterfaceType).IsEqualTo(typeof(IDisposable));
        await Assert.That(narrowExpr.Source).IsTypeOf<RootExpression>();
    }
}
