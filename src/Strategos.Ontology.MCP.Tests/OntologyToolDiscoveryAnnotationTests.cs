namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Tests verifying that <see cref="OntologyToolDiscovery.Discover"/> populates
/// the per-tool annotation matrix from design §5.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.4 + §5
/// </summary>
public class OntologyToolDiscoveryAnnotationTests
{
    [Test]
    public async Task Discover_OntologyExplore_HasReadOnlyAndIdempotentHints()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        var tools = discovery.Discover();
        var explore = tools.Single(t => t.Name == "ontology_explore");

        await Assert.That(explore.Annotations.ReadOnlyHint).IsTrue();
        await Assert.That(explore.Annotations.IdempotentHint).IsTrue();
        await Assert.That(explore.Annotations.DestructiveHint).IsFalse();
        await Assert.That(explore.Annotations.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task Discover_OntologyQuery_HasReadOnlyAndIdempotentHints()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        var tools = discovery.Discover();
        var query = tools.Single(t => t.Name == "ontology_query");

        await Assert.That(query.Annotations.ReadOnlyHint).IsTrue();
        await Assert.That(query.Annotations.IdempotentHint).IsTrue();
        await Assert.That(query.Annotations.DestructiveHint).IsFalse();
        await Assert.That(query.Annotations.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task Discover_OntologyAction_HasDestructiveHint()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        var tools = discovery.Discover();
        var action = tools.Single(t => t.Name == "ontology_action");

        await Assert.That(action.Annotations.DestructiveHint).IsTrue();
        await Assert.That(action.Annotations.ReadOnlyHint).IsFalse();
        await Assert.That(action.Annotations.IdempotentHint).IsFalse();
        await Assert.That(action.Annotations.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task Discover_AllTools_HaveNonNullTitle()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        var tools = discovery.Discover();

        foreach (var tool in tools)
        {
            await Assert.That(tool.Title).IsNotNull();
            await Assert.That(tool.Title).IsNotEmpty();
        }
    }

    [Test]
    public async Task Discover_AllTools_HaveNonNullOutputSchema()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        var tools = discovery.Discover();

        foreach (var tool in tools)
        {
            await Assert.That(tool.OutputSchema).IsNotNull();
            await Assert.That(tool.OutputSchema!.Value.GetRawText()).Contains("\"type\"");
        }
    }
}
