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

    private sealed record TestRecord(string A, int B);
}
