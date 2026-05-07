namespace Strategos.Ontology.MCP.Tests;

public class OntologyToolDiscoveryAnnotationTests
{
    [Test]
    public async Task Discover_OntologyExplore_HasReadOnlyAndIdempotentHints()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tool = new OntologyToolDiscovery(graph).Discover()
            .First(t => t.Name == "ontology_explore");

        await Assert.That(tool.Annotations.ReadOnlyHint).IsTrue();
        await Assert.That(tool.Annotations.IdempotentHint).IsTrue();
        await Assert.That(tool.Annotations.DestructiveHint).IsFalse();
        await Assert.That(tool.Annotations.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task Discover_OntologyQuery_HasReadOnlyAndIdempotentHints()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tool = new OntologyToolDiscovery(graph).Discover()
            .First(t => t.Name == "ontology_query");

        await Assert.That(tool.Annotations.ReadOnlyHint).IsTrue();
        await Assert.That(tool.Annotations.IdempotentHint).IsTrue();
        await Assert.That(tool.Annotations.DestructiveHint).IsFalse();
        await Assert.That(tool.Annotations.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task Discover_OntologyAction_HasDestructiveHint()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tool = new OntologyToolDiscovery(graph).Discover()
            .First(t => t.Name == "ontology_action");

        await Assert.That(tool.Annotations.DestructiveHint).IsTrue();
        await Assert.That(tool.Annotations.ReadOnlyHint).IsFalse();
        await Assert.That(tool.Annotations.IdempotentHint).IsFalse();
        await Assert.That(tool.Annotations.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task Discover_AllTools_HaveNonNullTitle()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tools = new OntologyToolDiscovery(graph).Discover();

        foreach (var tool in tools)
        {
            await Assert.That(tool.Title).IsNotNull();
            await Assert.That(tool.Title!).IsNotEmpty();
        }
    }

    [Test]
    public async Task Discover_AllTools_HaveNonNullOutputSchema()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tools = new OntologyToolDiscovery(graph).Discover();

        foreach (var tool in tools)
        {
            await Assert.That(tool.OutputSchema.HasValue).IsTrue();
            await Assert.That(tool.OutputSchema!.Value.GetRawText()).Contains("\"type\"");
        }
    }

    [Test]
    public async Task GetServerCapabilities_ReturnsCurrentGraphVersion()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var capabilities = discovery.GetServerCapabilities();

        // Assert — wire-format prefixed version, same envelope as ResponseMeta
        await Assert.That(capabilities.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
    }

    [Test]
    public async Task Discover_OntologyQuery_OutputSchemaIsOneOfUnion()
    {
        // The query tool returns a discriminated union of QueryResult / SemanticQueryResult;
        // its OutputSchema must reflect that with oneOf + the resultKind discriminator.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tool = new OntologyToolDiscovery(graph).Discover()
            .First(t => t.Name == "ontology_query");

        var raw = tool.OutputSchema!.Value.GetRawText();
        await Assert.That(raw).Contains("oneOf");
        await Assert.That(raw).Contains("resultKind");
    }
}
