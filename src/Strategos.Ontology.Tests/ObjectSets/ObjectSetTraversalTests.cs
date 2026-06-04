using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;

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

// ---------------------------------------------------------------------------
// DR-3 instance-anchored traversal (Tasks 10–13).
//
// These exercise the rewritten EvaluateTraverseLink end-to-end: a graph-aware
// InMemoryObjectSetProvider stores instances, RelateAsync materializes rows,
// and TraverseLink resolves by SOURCE INSTANCE (not target type) — closing
// the #114 defect.
// ---------------------------------------------------------------------------

public sealed record TravNode(string Id, int Weight = 0);

public sealed class TravNodeOntology : DomainOntology
{
    public override string DomainName => "trav";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TravNode>(obj =>
        {
            obj.Key(n => n.Id);
            obj.Property(n => n.Weight);
            obj.HasMany<TravNode>("link");
        });
    }
}

public class InstanceAnchoredTraversalTests
{
    private static OntologyGraph BuildGraph()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TravNodeOntology>();
        return graphBuilder.Build();
    }

    private static (InMemoryObjectSetProvider Provider, IOntologyQuery Query) BuildQuery(OntologyGraph graph)
    {
        var provider = new InMemoryObjectSetProvider(graph);
        var query = new OntologyQueryService(
            graph,
            provider,
            Substitute.For<IActionDispatcher>(),
            Substitute.For<IEventStreamProvider>());
        return (provider, query);
    }

    [Test]
    public async Task TraverseLink_FromInstance_ReturnsOnlyRelatedTargets()
    {
        // Arrange — source x, targets a and b, plus an UNRELATED c of the same
        // target type. The old type-based traversal would have returned a, b AND c.
        var graph = BuildGraph();
        var (provider, query) = BuildQuery(graph);
        provider.Seed(new TravNode("x"), "x", nameof(TravNode));
        provider.Seed(new TravNode("a"), "a", nameof(TravNode));
        provider.Seed(new TravNode("b"), "b", nameof(TravNode));
        provider.Seed(new TravNode("c"), "c", nameof(TravNode));

        IObjectSetWriter writer = provider;
        await writer.RelateAsync(nameof(TravNode), "x", "link", nameof(TravNode), "a");
        await writer.RelateAsync(nameof(TravNode), "x", "link", nameof(TravNode), "b");

        // Act — traverse from the x instance.
        var traversed = query
            .GetObjectSet<TravNode>(nameof(TravNode))
            .Where(t => t.Id == "x")
            .TraverseLink<TravNode>("link");
        var result = await traversed.ExecuteAsync();

        // Assert — exactly {a, b}, NOT the unrelated c.
        var ids = result.Items.Select(n => n.Id).OrderBy(s => s, StringComparer.Ordinal).ToList();
        await Assert.That(ids).IsEquivalentTo(new[] { "a", "b" });
    }

    [Test]
    public async Task TraverseLink_InstanceWithNoRelations_ReturnsEmptySet()
    {
        // #114 regression guard: an instance with NO relation rows must traverse
        // to the EMPTY set — and crucially must NOT return all target-type items.
        // Targets a, b exist in the store but the source x is related to none.
        var graph = BuildGraph();
        var (provider, query) = BuildQuery(graph);
        provider.Seed(new TravNode("x"), "x", nameof(TravNode));
        provider.Seed(new TravNode("a"), "a", nameof(TravNode));
        provider.Seed(new TravNode("b"), "b", nameof(TravNode));

        // No RelateAsync calls — x has zero relations under "link".

        // Act
        var traversed = query
            .GetObjectSet<TravNode>(nameof(TravNode))
            .Where(t => t.Id == "x")
            .TraverseLink<TravNode>("link");
        var result = await traversed.ExecuteAsync();

        // Assert — empty, NOT {a, b} (which the old type-based traversal returned).
        await Assert.That(result.Items).IsEmpty();
    }
}
