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

    // Multi-hop fixture (F2): a chain x -> y1/y2 (level 1) -> z1/z2 (level 2) ->
    // w1/w2 (level 3), each hop a reified TravEdge so a depth-N request walks N hops
    // and reaches DISTINCT far endpoints per level. Every edge carries Status=active
    // so the depth tests are not filtered out.
    private static (OntologyGraph Graph, InMemoryObjectSetProvider Provider) BuildChain()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TravOntology>();
        var graph = graphBuilder.Build();

        var provider = new InMemoryObjectSetProvider(graph);
        foreach (var id in new[] { "x", "y1", "y2", "z1", "z2", "w1", "w2" })
        {
            provider.Seed(new TravNode(id), id, nameof(TravNode));
        }

        IObjectSetWriter writer = provider;
        var edgeCounter = 0;
        void Relate(string from, string to)
        {
            edgeCounter++;
            writer.RelateAsync(
                nameof(TravNode), from, "link", nameof(TravNode), to,
                "TravEdge", new TravEdge($"e{edgeCounter}", new TravNode(from), new TravNode(to), "active"))
                .GetAwaiter().GetResult();
        }

        // Level 1: x -> y1, y2
        Relate("x", "y1");
        Relate("x", "y2");
        // Level 2: y1 -> z1, y2 -> z2
        Relate("y1", "z1");
        Relate("y2", "z2");
        // Level 3: z1 -> w1, z2 -> w2
        Relate("z1", "w1");
        Relate("z2", "w2");

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

    [Test]
    public async Task TraversalTool_ReachedEndpoint_CarriesEdgeAttributes()
    {
        // F1: every reached endpoint must carry the backing association object's
        // edge-attribute VALUES (not the always-empty map the projection used to
        // emit). The Status attribute rides through from the TravEdge that produced
        // each far endpoint.
        var (graph, provider) = BuildSeeded();
        var tool = new OntologyTraverseTool(graph, provider);

        var result = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 1));

        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Endpoints.Count).IsEqualTo(2);

        // Every endpoint carries a NON-EMPTY edgeAttributes map with the Status edge
        // attribute, paired positionally from the surviving association object.
        foreach (var endpoint in result.Endpoints)
        {
            await Assert.That(endpoint.EdgeAttributes.Count).IsGreaterThan(0);
            await Assert.That(endpoint.EdgeAttributes.ContainsKey("Status")).IsTrue();
        }

        // The pairing is correct: y1's edge is active, y2's is inactive.
        var byId = result.Endpoints.ToDictionary(e => e.DestinationId);
        await Assert.That(byId["y1"].EdgeAttributes["Status"]).IsEqualTo("active");
        await Assert.That(byId["y2"].EdgeAttributes["Status"]).IsEqualTo("inactive");
    }

    [Test]
    public async Task TraversalTool_Depth2_WalksTwoHops_DistinctFarEndpoints()
    {
        // F2: depth must actually chain. Depth 2 from x walks x -> {y1,y2} -> {z1,z2},
        // so the reached far endpoints are the LEVEL-2 nodes, distinct from depth 1.
        var (graph, provider) = BuildChain();
        var tool = new OntologyTraverseTool(graph, provider);

        var depth1 = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 1));
        var depth2 = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 2));

        await Assert.That(depth1.IsError).IsFalse();
        await Assert.That(depth2.IsError).IsFalse();

        var depth1Ids = depth1.Endpoints.Select(e => e.DestinationId).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var depth2Ids = depth2.Endpoints.Select(e => e.DestinationId).OrderBy(s => s, StringComparer.Ordinal).ToList();

        // Depth 1 reaches level-1 nodes; depth 2 reaches level-2 nodes — they differ,
        // proving the chain is not a fixed 2-hop that ignores Depth.
        await Assert.That(depth1Ids).IsEquivalentTo(new[] { "y1", "y2" });
        await Assert.That(depth2Ids).IsEquivalentTo(new[] { "z1", "z2" });
    }

    [Test]
    public async Task TraversalTool_Depth3_WalksThreeHops_DistinctFarEndpoints()
    {
        // F2: depth 3 from x walks x -> {y1,y2} -> {z1,z2} -> {w1,w2}, reaching the
        // LEVEL-3 nodes. This exercises the full MaxDepth chain.
        var (graph, provider) = BuildChain();
        var tool = new OntologyTraverseTool(graph, provider);

        var depth3 = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 3));

        await Assert.That(depth3.IsError).IsFalse();
        var ids = depth3.Endpoints.Select(e => e.DestinationId).OrderBy(s => s, StringComparer.Ordinal).ToList();
        await Assert.That(ids).IsEquivalentTo(new[] { "w1", "w2" });
    }

    [Test]
    public async Task TraversalTool_Cursor_AdvancesPage()
    {
        // F3: a continuation cursor must advance past the prior page rather than
        // restart at offset 0. With far endpoints exceeding the row budget, the
        // first page truncates and emits a nextCursor; re-calling WITH that cursor
        // must surface the remaining (non-overlapping) far endpoints.
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TravOntology>();
        var graph = graphBuilder.Build();

        var provider = new InMemoryObjectSetProvider(graph);
        provider.Seed(new TravNode("x"), "x", nameof(TravNode));
        IObjectSetWriter writer = provider;

        var farCount = OntologyTraverseTool.RowBudget + 5;
        var expectedIds = new List<string>();
        for (var i = 0; i < farCount; i++)
        {
            // Zero-pad so ordinal ordering matches numeric ordering (the store reads
            // rows ordinal-by-TargetId).
            var farId = $"y{i:D4}";
            expectedIds.Add(farId);
            provider.Seed(new TravNode(farId), farId, nameof(TravNode));
            writer.RelateAsync(
                nameof(TravNode), "x", "link", nameof(TravNode), farId,
                "TravEdge", new TravEdge($"e{i}", new TravNode("x"), new TravNode(farId), "active"))
                .GetAwaiter().GetResult();
        }

        expectedIds.Sort(StringComparer.Ordinal);
        var tool = new OntologyTraverseTool(graph, provider);

        // First page: offset 0, truncated, carries a nextCursor.
        var page1 = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 1));

        await Assert.That(page1.IsError).IsFalse();
        await Assert.That(page1.Truncated).IsTrue();
        await Assert.That(page1.Endpoints.Count).IsEqualTo(OntologyTraverseTool.RowBudget);
        await Assert.That(page1.NextCursor).IsNotNull();

        // Second page: re-call WITH the cursor — it advances to the remaining rows.
        var page2 = await tool.TraverseAsync(new TraversalRequest(
            ObjectType: nameof(TravNode),
            ObjectId: "x",
            LinkName: "link",
            Direction: TraversalDirection.ToDestination,
            Depth: 1,
            Cursor: page1.NextCursor));

        await Assert.That(page2.IsError).IsFalse();
        await Assert.That(page2.Endpoints.Count).IsEqualTo(farCount - OntologyTraverseTool.RowBudget);

        // The two pages partition the far-endpoint set without overlap.
        var page1Ids = page1.Endpoints.Select(e => e.DestinationId).ToList();
        var page2Ids = page2.Endpoints.Select(e => e.DestinationId).ToList();
        await Assert.That(page1Ids.Intersect(page2Ids).Any()).IsFalse();

        var combined = page1Ids.Concat(page2Ids).OrderBy(s => s, StringComparer.Ordinal).ToList();
        await Assert.That(combined).IsEquivalentTo(expectedIds);
    }
}
