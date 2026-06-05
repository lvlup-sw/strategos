using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

/// <summary>
/// DR-10 (keystone, part 1 of 3): instance-anchored traversal must be able to
/// carry an EXPLICIT target descriptor name through the expression tree, mirroring
/// the <see cref="RootExpression"/>(<c>Type objectType, string objectTypeName</c>)
/// precedent. This task ONLY plumbs the capability — evaluators/providers do not
/// yet consume <see cref="TraverseLinkExpression.TargetDescriptorName"/> (later tasks).
/// </summary>
public class TraverseLinkExpressionTests
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
    public async Task TraverseLink_WithDescriptorNameOverride_CarriesTargetDescriptorName()
    {
        // Arrange
        var set = new ObjectSet<Source>("positions", _provider, _dispatcher, _eventProvider);

        // Act — the new two-arg overload threads an explicit target descriptor name
        var traversed = set.TraverseLink<Linked>("Orders", "trading_orders");

        // Assert
        await Assert.That(traversed.Expression).IsTypeOf<TraverseLinkExpression>();
        var traverseExpr = (TraverseLinkExpression)traversed.Expression;
        await Assert.That(traverseExpr.LinkName).IsEqualTo("Orders");
        await Assert.That(traverseExpr.TargetDescriptorName).IsEqualTo("trading_orders");
    }

    [Test]
    public async Task TraverseLink_WithoutOverride_TargetDescriptorNameIsNull()
    {
        // Arrange
        var set = new ObjectSet<Source>("positions", _provider, _dispatcher, _eventProvider);

        // Act — the existing single-arg overload supplies no override
        var traversed = set.TraverseLink<Linked>("Orders");

        // Assert
        await Assert.That(traversed.Expression).IsTypeOf<TraverseLinkExpression>();
        var traverseExpr = (TraverseLinkExpression)traversed.Expression;
        await Assert.That(traverseExpr.LinkName).IsEqualTo("Orders");
        await Assert.That(traverseExpr.TargetDescriptorName).IsNull();
    }

    private sealed class Source
    {
        public string Symbol { get; set; } = string.Empty;
    }

    private sealed class Linked
    {
        public string Id { get; set; } = string.Empty;
    }
}
