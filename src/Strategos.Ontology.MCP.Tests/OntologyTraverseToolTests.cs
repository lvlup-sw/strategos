using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// DR-15 / T18 (#125): the instance-anchored traversal tool. Walks from a specific
/// source instance across a reified association to a far endpoint, with closed-
/// vocabulary inputs (link names from the graph, integer depth ≤ the MCP max-depth
/// constant, a direction enum) and edge-attribute filterability matching the
/// in-process <c>ObjectSet</c> path. Provider-agnostic: dispatch goes through the
/// public <see cref="IObjectSetProvider.ExecuteAsync{T}"/> against an in-memory
/// provider double (no live database).
/// </summary>
public sealed class OntologyTraverseToolTests
{
    // Reuses the DR-3/DR-4 traversal fixture shape: TravNode entities linked by a
    // reified TravEdge association carrying a Status edge attribute.
    public sealed record TravNode(string Id, int Weight = 0);

    public sealed record TravEdge(string Id, TravNode From, TravNode To, string Status);

    private sealed class TravOntology : DomainOntology
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

    private static (OntologyGraph Graph, InMemoryObjectSetProvider Provider) BuildSeeded()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TravOntology>();
        var graph = graphBuilder.Build();

        var provider = new InMemoryObjectSetProvider(graph);
        provider.Seed(new TravNode("x"), "x", nameof(TravNode));
        provider.Seed(new TravNode("y1"), "y1", nameof(TravNode));
        provider.Seed(new TravNode("y2"), "y2", nameof(TravNode));

        IObjectSetWriter writer = provider;
        writer.RelateAsync(
            nameof(TravNode), "x", "link", nameof(TravNode), "y1",
            "TravEdge", new TravEdge("e1", new TravNode("x"), new TravNode("y1"), "active")).GetAwaiter().GetResult();
        writer.RelateAsync(
            nameof(TravNode), "x", "link", nameof(TravNode), "y2",
            "TravEdge", new TravEdge("e2", new TravNode("x"), new TravNode("y2"), "inactive")).GetAwaiter().GetResult();

        return (graph, provider);
    }

    [Test]
    public async Task TraversalTool_InstanceToAssociationToFarEndpoint_Succeeds()
    {
        // Arrange
        var (graph, provider) = BuildSeeded();
        var tool = new OntologyTraverseTool(graph, provider);

        // Act — from instance x, across the "link" association, to the destination
        // far endpoint. No edge filter: both far endpoints come back.
        var result = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 1));

        // Assert — succeeded, carrying both far-endpoint ids and the INV-3 _meta.
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Meta).IsNotNull();
        var ids = result.Endpoints.Select(e => e.DestinationId).OrderBy(s => s, StringComparer.Ordinal).ToList();
        await Assert.That(ids).IsEquivalentTo(new[] { "y1", "y2" });
    }

    [Test]
    public async Task TraversalTool_EdgeAttributeFilter_ParityWithInProcessObjectSet()
    {
        // Arrange
        var (graph, provider) = BuildSeeded();
        var tool = new OntologyTraverseTool(graph, provider);

        // Act — filter on the edge attribute BEFORE the far hop (Status == active).
        var result = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 1,
            EdgeFilter: new Dictionary<string, string> { ["Status"] = "active" }));

        // Assert — only the active edge's far endpoint (y1) returns, exactly as the
        // in-process ObjectSet edge-attribute filter does.
        await Assert.That(result.IsError).IsFalse();
        var ids = result.Endpoints.Select(e => e.DestinationId).ToList();
        await Assert.That(ids).IsEquivalentTo(new[] { "y1" });
    }

    [Test]
    public async Task TraversalTool_FreeTextLinkArg_Rejected_ClosedVocabulary()
    {
        // Arrange
        var (graph, provider) = BuildSeeded();
        var tool = new OntologyTraverseTool(graph, provider);

        // Act — a link name not in the source descriptor's vocabulary. Closed
        // vocabulary: the tool rejects it as a structured error (isError), NOT by
        // executing a free-text traversal.
        var result = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "'; DROP TABLE nodes; --",
            Direction: TraversalDirection.ToDestination,
            Depth: 1));

        // Assert — rejected as a validation error that still carries _meta.
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!).Contains("link");
        await Assert.That(result.Meta).IsNotNull();
    }

    [Test]
    public async Task TraversalTool_DepthBeyondMax_Rejected()
    {
        // Arrange
        var (graph, provider) = BuildSeeded();
        var tool = new OntologyTraverseTool(graph, provider);

        // Act — depth beyond the MCP max-depth constant.
        var result = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: OntologyTraversalLimits.MaxDepth + 1));

        // Assert — rejected (closed vocabulary on depth), with _meta.
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!).Contains("depth");
    }

    [Test]
    public async Task TraversalTool_MaxDepthConstant_IsThree()
    {
        // The MCP layer owns its own max-depth bound (matching the documented
        // join-chain budget), NOT a reference to the Npgsql JoinChainDepthBudget.
        await Assert.That(OntologyTraversalLimits.MaxDepth).IsEqualTo(3);
    }
}
