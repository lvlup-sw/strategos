using System.Text.Json;
using System.Text.Json.Serialization;
using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP.Tests.Internal;

public class JsonSchemaHelperTests
{
    [Test]
    public async Task JsonSchemaFor_PrimitiveType_ReturnsValidSchemaElement()
    {
        // Act
        var schema = JsonSchemaHelper.JsonSchemaFor<string>();
        var raw = schema.GetRawText();

        // Assert
        await Assert.That(raw).IsNotNull();
        await Assert.That(raw).IsNotEmpty();
        await Assert.That(raw).Contains("\"type\"");
        await Assert.That(raw).Contains("\"string\"");
    }

    [Test]
    public async Task JsonSchemaFor_RecordType_ReturnsObjectSchemaWithProperties()
    {
        // Act
        var schema = JsonSchemaHelper.JsonSchemaFor<TestRecord>();
        var raw = schema.GetRawText();

        // Assert
        await Assert.That(raw).Contains("\"type\"");
        await Assert.That(raw).Contains("\"object\"");
        await Assert.That(raw).Contains("\"A\"");
        await Assert.That(raw).Contains("\"B\"");
    }

    [Test]
    public async Task JsonSchemaFor_NonPolymorphic_DoesNotInjectOneOf()
    {
        // Pin that JsonSchemaFor is a genuine passthrough — it must NOT rewrite
        // anything. A future caller passing an intentionally-anyOf polymorphic
        // type should have its schema preserved verbatim. The union-aware
        // overload (JsonSchemaForUnion) is the one that performs the rewrite.
        var schema = JsonSchemaHelper.JsonSchemaFor<TestRecord>();
        var raw = schema.GetRawText();

        await Assert.That(raw).DoesNotContain("\"oneOf\"");
    }

    [Test]
    public async Task JsonSchemaForUnion_NonPolymorphicType_Throws()
    {
        // Guard rail: JsonSchemaForUnion is only valid for [JsonPolymorphic]
        // types. Calling it on a plain record must fail loudly so the rewrite
        // (and its silent corruption potential for non-union anyOf shapes)
        // can never apply to a non-union type by mistake.
        await Assert.That(() => JsonSchemaHelper.JsonSchemaForUnion<TestRecord>())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task JsonSchemaForUnion_QueryResultUnion_HasOneOfAtRoot()
    {
        // Positive root-shape assertion: the polymorphic union schema must
        // carry "oneOf" as a top-level property. Pinning this protects against
        // a JsonSchemaExporter output-shape change (e.g. wrapping in $defs)
        // silently no-op'ing the rewrite.
        var schema = JsonSchemaHelper.JsonSchemaForUnion<QueryResultUnion>();

        using var doc = JsonDocument.Parse(schema.GetRawText());
        await Assert.That(doc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(doc.RootElement.TryGetProperty("oneOf", out _)).IsTrue();
    }

    private sealed record TestRecord(string A, int B);
}
