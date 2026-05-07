using System.Text.Json;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyExploreToolMetaTests
{
    [Test]
    public async Task Explore_ResultCarriesMetaWithGraphVersion()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tool = new OntologyExploreTool(graph);

        // Act
        var result = tool.Explore("domains");

        // Assert — wire-format prefixed version
        await Assert.That(result.Meta).IsNotNull();
        await Assert.That(result.Meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
    }

    [Test]
    public async Task Explore_AllScopes_CarryMeta()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tool = new OntologyExploreTool(graph);
        var expected = "sha256:" + graph.Version;

        string[] scopes = [
            "domains",
            "objectTypes",
            "actions",
            "links",
            "events",
            "interfaces",
            "workflowChains",
            "vectorProperties",
            "this-scope-does-not-exist", // unknown-scope fallback
        ];

        foreach (var scope in scopes)
        {
            var result = tool.Explore(scope, domain: "trading", objectType: "TestPosition");
            await Assert.That(result.Meta).IsNotNull();
            await Assert.That(result.Meta.OntologyVersion).IsEqualTo(expected);
        }

        // Traversal branch (traverseFrom + domain set)
        var traversal = tool.Explore("links", domain: "trading", traverseFrom: "TestPosition", maxDepth: 1);
        await Assert.That(traversal.Meta.OntologyVersion).IsEqualTo(expected);
    }

    [Test]
    public async Task ExploreResult_Json_KeysMetaAsUnderscoreMeta()
    {
        // Wire-format proof: the JSON property name is "_meta", not "Meta".
        // MCP clients dispatch on _meta, so this is load-bearing.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var tool = new OntologyExploreTool(graph);
        var result = tool.Explore("domains");

        var json = JsonSerializer.Serialize(result);

        await Assert.That(json).Contains("\"_meta\"");
        await Assert.That(json).Contains("\"OntologyVersion\"");
    }
}
