using System.Text.Json;

using Strategos.Ontology.MCP.Hosting;

namespace Strategos.Ontology.MCP.Hosting.Tests;

public sealed class OntologyServerToolFactoryTests
{
    [Test]
    public async Task CreateServerTools_PreservesOutputSchemaAndAnnotations()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var descriptors = new Strategos.Ontology.MCP.OntologyToolDiscovery(graph)
            .Discover()
            .ToDictionary(d => d.Name);

        // Act
        var serverTools = OntologyServerToolFactory.CreateServerTools(graph).ToList();

        // The factory also registers the DR-15 ontology_traverse tool (a distinct
        // capability not derived from OntologyToolDiscovery); this assertion covers the
        // four discovery-derived tools, so scope to those by name.
        var discoveryTools = serverTools
            .Where(t => descriptors.ContainsKey(t.ProtocolTool.Name))
            .ToList();

        // Assert — one discovery-derived tool per descriptor.
        await Assert.That(discoveryTools).HasCount().EqualTo(descriptors.Count);

        foreach (var tool in discoveryTools)
        {
            var protocolTool = tool.ProtocolTool;
            await Assert.That(descriptors.ContainsKey(protocolTool.Name)).IsTrue();
            var descriptor = descriptors[protocolTool.Name];

            // OutputSchema survives the adapter. The SDK re-emits the schema in its own
            // canonical form (notably it widens the root "type":"object" to admit null),
            // so the load-bearing content to compare is the untouched "properties" subtree.
            await Assert.That(protocolTool.OutputSchema.HasValue).IsEqualTo(descriptor.OutputSchema.HasValue);
            if (descriptor.OutputSchema.HasValue)
            {
                var expectedProps = PropertiesJson(descriptor.OutputSchema.Value);
                var actualProps = PropertiesJson(protocolTool.OutputSchema!.Value);
                await Assert.That(actualProps).IsEqualTo(expectedProps);
            }

            // Annotations map field-by-field.
            var annotations = protocolTool.Annotations;
            await Assert.That(annotations).IsNotNull();
            await Assert.That(annotations!.ReadOnlyHint).IsEqualTo(descriptor.Annotations.ReadOnlyHint);
            await Assert.That(annotations.DestructiveHint).IsEqualTo(descriptor.Annotations.DestructiveHint);
            await Assert.That(annotations.IdempotentHint).IsEqualTo(descriptor.Annotations.IdempotentHint);
            await Assert.That(annotations.OpenWorldHint).IsEqualTo(descriptor.Annotations.OpenWorldHint);

            await Assert.That(protocolTool.Title).IsEqualTo(descriptor.Title);
            await Assert.That(protocolTool.Description).IsEqualTo(descriptor.Description);
        }
    }

    [Test]
    public async Task CreateServerTools_PreservesConstraintSummaries()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateConstrainedGraph();
        var actionDescriptor = new Strategos.Ontology.MCP.OntologyToolDiscovery(graph)
            .Discover()
            .Single(d => d.Name == "ontology_action");

        // Precondition: the fixture yields at least one constraint summary to preserve.
        await Assert.That(actionDescriptor.ConstraintSummaries.Count).IsGreaterThan(0);

        // Act
        var actionTool = OntologyServerToolFactory.CreateServerTools(graph)
            .Single(t => t.ProtocolTool.Name == "ontology_action");

        // Assert — constraint summaries survive via the tool's _meta carrier.
        var meta = actionTool.ProtocolTool.Meta;
        await Assert.That(meta).IsNotNull();
        await Assert.That(meta!.ContainsKey("constraintSummaries")).IsTrue();

        var carried = JsonSerializer.Deserialize<List<Strategos.Ontology.MCP.ActionConstraintSummary>>(
            meta["constraintSummaries"]!.ToJsonString());
        await Assert.That(carried).IsNotNull();
        await Assert.That(carried!.Count).IsEqualTo(actionDescriptor.ConstraintSummaries.Count);

        var expectedFirst = actionDescriptor.ConstraintSummaries[0];
        var actualFirst = carried[0];
        await Assert.That(actualFirst.ObjectTypeName).IsEqualTo(expectedFirst.ObjectTypeName);
        await Assert.That(actualFirst.ActionName).IsEqualTo(expectedFirst.ActionName);
        await Assert.That(actualFirst.HardConstraintCount).IsEqualTo(expectedFirst.HardConstraintCount);
        await Assert.That(actualFirst.SoftConstraintCount).IsEqualTo(expectedFirst.SoftConstraintCount);
    }

    private static string PropertiesJson(JsonElement schema)
    {
        // Re-serialize the "properties" subtree to a canonical string. This is the part
        // of the JSON schema the SDK preserves verbatim, independent of its root-type
        // nullability normalization.
        return schema.TryGetProperty("properties", out var props)
            ? JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(props.GetRawText()))
            : "<no-properties>";
    }
}
