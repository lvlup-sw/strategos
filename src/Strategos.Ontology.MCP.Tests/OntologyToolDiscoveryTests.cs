using Strategos.Ontology;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyToolDiscoveryTests
{
    [Test]
    public async Task OntologyToolDiscovery_Discover_ReturnsOntologyTools()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();

        // Assert — should return exactly 3 tools
        await Assert.That(tools).HasCount().EqualTo(3);
    }

    [Test]
    public async Task OntologyToolDiscovery_Discover_IncludesQueryActionExplore()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert
        await Assert.That(toolNames).Contains("ontology_query");
        await Assert.That(toolNames).Contains("ontology_action");
        await Assert.That(toolNames).Contains("ontology_explore");
    }

    [Test]
    public async Task OntologyToolDiscovery_EnrichWithOntology_AddsSemanticMetadata()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var exploreTool = tools.First(t => t.Name == "ontology_explore");

        // Assert — enriched description should reference the ontology domains
        await Assert.That(exploreTool.Description).Contains("trading");
        await Assert.That(exploreTool.Description.Length).IsGreaterThan(0);
    }
}
