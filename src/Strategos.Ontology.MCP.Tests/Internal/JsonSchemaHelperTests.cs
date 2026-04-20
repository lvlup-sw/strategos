using System.Text.Json;
using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP.Tests.Internal;

/// <summary>
/// Tests for the internal <see cref="JsonSchemaHelper"/> utility used to
/// generate <c>OutputSchema</c> for tool descriptors.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.6
/// </summary>
public class JsonSchemaHelperTests
{
    [Test]
    public async Task JsonSchemaFor_PrimitiveType_ReturnsValidSchemaElement()
    {
        var schema = JsonSchemaHelper.JsonSchemaFor<string>();
        var raw = schema.GetRawText();

        await Assert.That(raw).IsNotEmpty();

        // Parses cleanly — guards against returning a non-JSON-encoded payload.
        using var doc = JsonDocument.Parse(raw);
        await Assert.That(raw).Contains("\"type\"");
        await Assert.That(raw).Contains("\"string\"");
    }

    [Test]
    public async Task JsonSchemaFor_RecordType_ReturnsObjectSchemaWithProperties()
    {
        var schema = JsonSchemaHelper.JsonSchemaFor<TestRecord>();
        var raw = schema.GetRawText();

        await Assert.That(raw).Contains("\"type\"");
        await Assert.That(raw).Contains("\"object\"");
        await Assert.That(raw).Contains("\"A\"");
        await Assert.That(raw).Contains("\"B\"");
    }

    public sealed record TestRecord(string A, int B);
}
