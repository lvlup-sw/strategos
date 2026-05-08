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
        var result = (QueryResult)await tool.QueryAsync(objectType: "TestPosition", domain: "trading");

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
        var semantic = (SemanticQueryResult)result;
        await Assert.That(semantic.Meta).IsNotNull();
        await Assert.That(semantic.Meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
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

    [Test]
    public async Task Query_FilterResult_Json_Contains_ResultKindFilter()
    {
        // Schema↔runtime contract test: ontology_query advertises a oneOf schema
        // discriminated by "resultKind". For the discriminator to land on the
        // wire, QueryAsync must return the polymorphic base (QueryResultUnion).
        // This test pins that behaviour for the non-semantic branch.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        provider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>([], 0, ObjectSetInclusion.Properties));

        var tool = new OntologyQueryTool(graph, provider, eventStream, NullLogger<OntologyQueryTool>.Instance);

        // Act — bind to QueryResultUnion (the declared return type) so STJ's
        // [JsonPolymorphic] machinery emits the discriminator. If the static
        // type were a concrete branch, the discriminator would be silently
        // dropped (the bug the schema↔runtime contract change fixes).
        QueryResultUnion result = await tool.QueryAsync(objectType: "TestPosition", domain: "trading");
        var json = JsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        await Assert.That(root.TryGetProperty("resultKind", out var kind)).IsTrue();
        await Assert.That(kind.GetString()).IsEqualTo("filter");
        // Regression on existing wire-format: _meta must still be present.
        await Assert.That(root.TryGetProperty("_meta", out _)).IsTrue();
    }

    [Test]
    public async Task Query_SemanticResult_Json_Contains_ResultKindSemantic()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        provider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>([], 0, ObjectSetInclusion.Properties, []));

        var tool = new OntologyQueryTool(graph, provider, eventStream, NullLogger<OntologyQueryTool>.Instance);

        QueryResultUnion result = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "any query");
        var json = JsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        await Assert.That(root.TryGetProperty("resultKind", out var kind)).IsTrue();
        await Assert.That(kind.GetString()).IsEqualTo("semantic");
        await Assert.That(root.TryGetProperty("_meta", out _)).IsTrue();
    }

    [Test]
    public async Task SemanticQueryResult_Json_Through_BaseType_StillEmitsUnderscoreMeta()
    {
        // Inheritance regression test: SemanticQueryResult inherits Meta from
        // QueryResult, which carries [property: JsonPropertyName("_meta")].
        // Serialize a SemanticQueryResult value through the QueryResult base
        // type (a representative non-leaf static type) and confirm "_meta"
        // is still emitted — i.e., the inherited property attribute carries
        // through and the wire shape is stable across binding choices.
        var meta = new ResponseMeta("sha256:test-version");
        var semantic = new SemanticQueryResult("TestPosition", [], meta)
        {
            SemanticQuery = "q",
            TopK = 5,
            MinRelevance = 0.7,
            Scores = [0.9],
        };

        var json = JsonSerializer.Serialize<QueryResult>(semantic);

        await Assert.That(json).Contains("\"_meta\"");
    }
}
