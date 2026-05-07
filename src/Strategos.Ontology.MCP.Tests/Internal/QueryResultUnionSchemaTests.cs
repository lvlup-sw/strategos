using System.Text.Json;
using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP.Tests.Internal;

public class QueryResultUnionSchemaTests
{
    [Test]
    public async Task JsonSchemaFor_QueryResultUnion_EmitsOneOfWithResultKindDiscriminator()
    {
        // Act
        var schema = JsonSchemaHelper.JsonSchemaFor<QueryResultUnion>();
        var raw = schema.GetRawText();

        // Parse to JSON and assert that oneOf is at the root and that
        // "resultKind" appears as the discriminator. We don't pin the exact
        // shape (the JSON Schema spec leaves leeway in how oneOf may be
        // phrased), but both signals must be present.
        using var doc = JsonDocument.Parse(raw);

        await Assert.That(raw).Contains("oneOf");
        await Assert.That(raw).Contains("resultKind");
    }

    [Test]
    public async Task QueryResult_InheritsQueryResultUnion()
    {
        // The discriminated base is the union root; QueryResult is the
        // "filter" branch. Confirm the inheritance — it's load-bearing for
        // OntologyQueryTool's return type.
        await Assert.That(typeof(QueryResultUnion).IsAssignableFrom(typeof(QueryResult))).IsTrue();
    }

    [Test]
    public async Task SemanticQueryResult_InheritsQueryResultUnion()
    {
        await Assert.That(typeof(QueryResultUnion).IsAssignableFrom(typeof(SemanticQueryResult))).IsTrue();
    }
}
