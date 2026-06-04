using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
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

// DR-4 reified association carrying a Status edge attribute, used by the
// association-backed traversal test (Task 12). Endpoints From (source role,
// index 0) and To (destination role, index 1).
public sealed record TravEdge(string Id, TravNode From, TravNode To, string Status);

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

        builder.Association<TravEdge>("TravEdge", a =>
        {
            a.Key(e => e.Id);
            a.Between(e => e.From).And(e => e.To);
            a.Property(e => e.Status).Required();
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

    [Test]
    public async Task TraverseLink_OverAssociation_ExposesEdgeAttributesForFilter()
    {
        // Arrange — x is related to y1 via an "active" edge and to y2 via an
        // "inactive" edge. The association objects carry the Status edge attribute.
        var graph = BuildGraph();
        var (provider, query) = BuildQuery(graph);
        provider.Seed(new TravNode("x"), "x", nameof(TravNode));
        provider.Seed(new TravNode("y1"), "y1", nameof(TravNode));
        provider.Seed(new TravNode("y2"), "y2", nameof(TravNode));

        IObjectSetWriter writer = provider;
        await writer.RelateAsync(
            nameof(TravNode), "x", "link", nameof(TravNode), "y1",
            "TravEdge", new TravEdge("e1", new TravNode("x"), new TravNode("y1"), "active"));
        await writer.RelateAsync(
            nameof(TravNode), "x", "link", nameof(TravNode), "y2",
            "TravEdge", new TravEdge("e2", new TravNode("x"), new TravNode("y2"), "inactive"));

        // Act 1 — traverse to the association objects and assert the edge
        // attributes are exposed for filtering.
        var edges = query
            .GetObjectSet<TravNode>(nameof(TravNode))
            .Where(t => t.Id == "x")
            .TraverseLink<TravEdge>("link");
        var allEdges = await edges.ExecuteAsync();
        var statuses = allEdges.Items.Select(e => e.Status).OrderBy(s => s, StringComparer.Ordinal).ToList();
        await Assert.That(statuses).IsEquivalentTo(new[] { "active", "inactive" });

        // Act 2 — filter on the edge attribute BEFORE the far hop, then hop to the
        // destination endpoint ("To" role). Only the active edge's far node returns.
        var activeFar = await query
            .GetObjectSet<TravNode>(nameof(TravNode))
            .Where(t => t.Id == "x")
            .TraverseLink<TravEdge>("link")
            .Where(e => e.Status == "active")
            .TraverseLink<TravNode>("To")
            .ExecuteAsync();
        var activeIds = activeFar.Items.Select(n => n.Id).ToList();
        await Assert.That(activeIds).IsEquivalentTo(new[] { "y1" });

        // Act 3 — the unfiltered far hop returns BOTH endpoints, proving the
        // edge-attribute filter changed the far-endpoint result set.
        var allFar = await query
            .GetObjectSet<TravNode>(nameof(TravNode))
            .Where(t => t.Id == "x")
            .TraverseLink<TravEdge>("link")
            .TraverseLink<TravNode>("To")
            .ExecuteAsync();
        var allFarIds = allFar.Items.Select(n => n.Id).OrderBy(s => s, StringComparer.Ordinal).ToList();
        await Assert.That(allFarIds).IsEquivalentTo(new[] { "y1", "y2" });
    }

    [Test]
    public async Task TraverseLink_OverAssociation_ToSourceRoleEndpoint_ReturnsSourceNode()
    {
        // F-MED-1: hopping to the SOURCE-role endpoint ("From", index 0) must
        // resolve the originating source node — not silently drop. Two distinct
        // sources (x1, x2) each relate to a shared far node via an edge, so the
        // source-role hop must surface the correct originating node per edge.
        var graph = BuildGraph();
        var (provider, query) = BuildQuery(graph);
        provider.Seed(new TravNode("x1"), "x1", nameof(TravNode));
        provider.Seed(new TravNode("x2"), "x2", nameof(TravNode));
        provider.Seed(new TravNode("y"), "y", nameof(TravNode));

        IObjectSetWriter writer = provider;
        await writer.RelateAsync(
            nameof(TravNode), "x1", "link", nameof(TravNode), "y",
            "TravEdge", new TravEdge("e1", new TravNode("x1"), new TravNode("y"), "active"));
        await writer.RelateAsync(
            nameof(TravNode), "x2", "link", nameof(TravNode), "y",
            "TravEdge", new TravEdge("e2", new TravNode("x2"), new TravNode("y"), "active"));

        // Act — from x1, traverse to the edge then back to its SOURCE-role
        // ("From") endpoint. The far node is x1, NOT the destination y.
        var sourceFar = await query
            .GetObjectSet<TravNode>(nameof(TravNode))
            .Where(t => t.Id == "x1")
            .TraverseLink<TravEdge>("link")
            .TraverseLink<TravNode>("From")
            .ExecuteAsync();

        // Assert — exactly {x1}: the source-role hop resolved the originating
        // node rather than silently dropping it.
        var sourceIds = sourceFar.Items.Select(n => n.Id).ToList();
        await Assert.That(sourceIds).IsEquivalentTo(new[] { "x1" });
    }
}

// ---------------------------------------------------------------------------
// Task 13 (DR-8): SymbolKey-only (polyglot) relate -> traverse, NO reflection.
//
// An ingested descriptor has ClrType = null and SymbolKey set; its identity
// flows ONLY through a source-supplied IdAccessor (DR-1). The instance below
// hides its true id behind a transformation ("node:" + RawId) that NO
// reflection-by-convention could reproduce, so any reflective id fallback
// would yield the wrong id and the relate/traverse would resolve nothing. A
// correct end-to-end result therefore proves the IdAccessor is the only id
// path (INV-8).
// ---------------------------------------------------------------------------

public sealed record PolyglotNode(string RawId);

public class SymbolKeyOnlyTraversalTests
{
    private const string Descriptor = "PolyNode";
    private const string LinkName = "relates";

    private static OntologyGraph BuildSymbolKeyOnlyGraph()
    {
        // The supplied accessor is the ONLY id path: id = "node:" + RawId.
        Func<object, object?> idAccessor = instance => "node:" + ((PolyglotNode)instance).RawId;

        var descriptor = new ObjectTypeDescriptor
        {
            Name = Descriptor,
            DomainName = "poly",
            ClrType = null, // SymbolKey-only: no loaded CLR identity.
            SymbolKey = "scip-typescript ./poly.ts#PolyNode",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "poly-source",
            IdAccessor = idAccessor,
            Links =
            [
                new LinkDescriptor(LinkName, Descriptor, LinkCardinality.OneToMany),
            ],
        };

        return new OntologyGraph(
            domains: [new DomainDescriptor("poly") { ObjectTypes = [descriptor] }],
            objectTypes: [descriptor],
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    [Test]
    public async Task RelateThenTraverse_SymbolKeyOnlyDescriptor_NoReflection()
    {
        // Arrange — a SymbolKey-only graph, instances stored under the ingested
        // descriptor. Projected ids are "node:x", "node:a", "node:b", "node:c".
        var graph = BuildSymbolKeyOnlyGraph();
        var provider = new InMemoryObjectSetProvider(graph);
        var query = new OntologyQueryService(
            graph,
            provider,
            Substitute.For<IActionDispatcher>(),
            Substitute.For<IEventStreamProvider>());

        provider.Seed(new PolyglotNode("x"), "x", Descriptor);
        provider.Seed(new PolyglotNode("a"), "a", Descriptor);
        provider.Seed(new PolyglotNode("b"), "b", Descriptor);
        provider.Seed(new PolyglotNode("c"), "c", Descriptor); // unrelated

        IObjectSetWriter writer = provider;
        // Relate via the projected ids (the accessor's transformed form).
        await writer.RelateAsync(Descriptor, "node:x", LinkName, Descriptor, "node:a");
        await writer.RelateAsync(Descriptor, "node:x", LinkName, Descriptor, "node:b");

        // Act — traverse from the x instance end-to-end through the polyglot path.
        var traversed = query
            .GetObjectSet<PolyglotNode>(Descriptor)
            .Where(n => n.RawId == "x")
            .TraverseLink<PolyglotNode>(LinkName);
        var result = await traversed.ExecuteAsync();

        // Assert — exactly the related {a, b}, NOT the unrelated c. Correct
        // resolution is only possible if every id was projected via the supplied
        // IdAccessor (no reflection on PolyglotNode's CLR shape).
        var rawIds = result.Items.Select(n => n.RawId).OrderBy(s => s, StringComparer.Ordinal).ToList();
        await Assert.That(rawIds).IsEquivalentTo(new[] { "a", "b" });
    }
}
