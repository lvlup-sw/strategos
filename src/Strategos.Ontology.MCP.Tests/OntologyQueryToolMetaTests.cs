using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyQueryToolMetaTests
{
    [Test]
    public async Task Query_NonSemantic_ResultCarriesMetaWithGraphVersion()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        provider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>([], 0, ObjectSetInclusion.Properties));

        var tool = new OntologyQueryTool(graph, provider, eventStream, NullLogger<OntologyQueryTool>.Instance);

        // Act
        var result = await tool.QueryAsync(objectType: "TestPosition", domain: "trading");

        // Assert
        await Assert.That(result.Meta).IsNotNull();
        await Assert.That(result.Meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
    }

    [Test]
    public async Task Query_Semantic_ResultCarriesMetaWithGraphVersion()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        provider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>([], 0, ObjectSetInclusion.Properties, []));

        var tool = new OntologyQueryTool(graph, provider, eventStream, NullLogger<OntologyQueryTool>.Instance);

        // Act
        var result = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "any query");

        // Assert — semantic branch returns SemanticQueryResult with Meta populated
        await Assert.That(result).IsTypeOf<SemanticQueryResult>();
        await Assert.That(result.Meta).IsNotNull();
        await Assert.That(result.Meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
    }

    [Test]
    public async Task QueryResult_Json_KeysMetaAsUnderscoreMeta()
    {
        // Wire-format proof: the JSON property name is "_meta", not "Meta".
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        provider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>([], 0, ObjectSetInclusion.Properties));

        var tool = new OntologyQueryTool(graph, provider, eventStream, NullLogger<OntologyQueryTool>.Instance);
        var result = await tool.QueryAsync(objectType: "TestPosition", domain: "trading");

        var json = JsonSerializer.Serialize(result);

        await Assert.That(json).Contains("\"_meta\"");
    }

    [Test]
    public async Task SemanticQueryResult_Json_KeysMetaAsUnderscoreMeta()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        provider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>([], 0, ObjectSetInclusion.Properties, []));

        var tool = new OntologyQueryTool(graph, provider, eventStream, NullLogger<OntologyQueryTool>.Instance);
        var result = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "any query");

        // Serialize as the runtime type so the [JsonPropertyName("_meta")]
        // attribute on the Meta member is honored.
        var json = JsonSerializer.Serialize((SemanticQueryResult)result);

        await Assert.That(json).Contains("\"_meta\"");
    }
}
