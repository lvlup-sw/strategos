using System.Text.Json;

using Strategos.Ontology;
using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// DR-15 / T16 (#125): association objects and edge endpoints must be representable
/// as a result shape DISTINCT from a plain object row, while every result still
/// carries the INV-3 <c>_meta</c> envelope and a tool-level <c>OutputSchema</c>.
/// </summary>
public sealed class AssociationResultShapeTests
{
    [Test]
    public async Task ExploreResult_AssociationDistinctFromObject_InResultShape()
    {
        // Arrange — a graph with both a plain entity (TestPosition) and a reified
        // association (TestCounterparty between two entities).
        var graph = TestOntologyGraphFactory.CreateAssociationGraph();
        var tool = new OntologyExploreTool(graph);

        // Act — the objectTypes scope must tag each item with its ObjectKind so an
        // association is distinguishable from a plain entity IN THE RESULT SHAPE.
        var result = tool.Explore(scope: "objectTypes", domain: "trading");

        // Assert — every item carries an "objectKind"; the entity is "Entity", the
        // association is "Association" and additionally surfaces its endpoints.
        var byName = result.Items.ToDictionary(i => i["name"]?.ToString() ?? "");

        await Assert.That(byName.ContainsKey("TestParty")).IsTrue();
        await Assert.That(byName["TestParty"]["objectKind"]?.ToString()).IsEqualTo("Entity");

        await Assert.That(byName.ContainsKey("TestCounterparty")).IsTrue();
        var assoc = byName["TestCounterparty"];
        await Assert.That(assoc["objectKind"]?.ToString()).IsEqualTo("Association");

        // The association row carries its endpoints (distinct from a plain object,
        // which has none). Endpoints name their type by descriptor (INV-8).
        await Assert.That(assoc.ContainsKey("endpoints")).IsTrue();
        var endpoints = (IReadOnlyList<Dictionary<string, object?>>)assoc["endpoints"]!;
        await Assert.That(endpoints.Count).IsEqualTo(2);
        var roles = endpoints
            .Select(e => e["role"]?.ToString() ?? "")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        await Assert.That(roles).IsEquivalentTo(new[] { "Buyer", "Seller" });
    }

    [Test]
    public async Task Result_CarriesMetaAndOutputSchema()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateAssociationGraph();

        // An AssociationQueryResult is a QueryResultUnion branch DISTINCT from the
        // plain QueryResult, carrying edge endpoints rather than plain object items.
        var edges = new List<AssociationEdgeRow>
        {
            new("TestCounterparty", "TestParty", "p1", "TestParty", "p2")
            {
                EdgeAttributes = new Dictionary<string, object?> { ["role"] = "primary" },
            },
        };
        var result = new AssociationQueryResult("TestCounterparty", edges, ResponseMeta.ForGraph(graph));

        // Assert — it is a QueryResultUnion (same union as QueryResult), so the
        // query tool can return either branch.
        QueryResultUnion union = result;
        await Assert.That(union).IsNotNull();

        // INV-3: _meta envelope present and stamped from the graph.
        await Assert.That(result.Meta).IsNotNull();
        await Assert.That(result.Meta.OntologyVersion).StartsWith("sha256:");

        // INV-3: the union still produces a valid oneOf OutputSchema covering the
        // new branch (the schema export must not regress when a branch is added).
        var schema = JsonSchemaHelper.JsonSchemaForUnion<QueryResultUnion>();
        var raw = schema.GetRawText();
        await Assert.That(raw).Contains("oneOf");
        await Assert.That(raw).Contains("association");
    }

    [Test]
    public async Task AssociationQueryResult_SerializesWithDiscriminatorAndMeta()
    {
        // The wire shape must carry the resultKind discriminator (so a client
        // dispatches the branch) and the _meta envelope.
        var graph = TestOntologyGraphFactory.CreateAssociationGraph();
        var edges = new List<AssociationEdgeRow>
        {
            new("TestCounterparty", "TestParty", "p1", "TestParty", "p2"),
        };
        QueryResultUnion result = new AssociationQueryResult("TestCounterparty", edges, ResponseMeta.ForGraph(graph));

        var json = JsonSerializer.Serialize(result);

        await Assert.That(json).Contains("\"resultKind\":\"association\"");
        await Assert.That(json).Contains("_meta");
        await Assert.That(json).Contains("\"sourceDescriptor\":\"TestParty\"");
        await Assert.That(json).Contains("\"sourceId\":\"p1\"");
    }
}
