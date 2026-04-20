using System.Text.Json;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Tests for the upgraded <see cref="OntologyToolDescriptor"/> — adds
/// <c>Title</c>, <c>OutputSchema</c>, and <c>Annotations</c> per MCP 2025-11-25.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.3
/// </summary>
public class OntologyToolDescriptorTests
{
    [Test]
    public async Task OntologyToolDescriptor_TwoArgConstructor_DefaultsAllNewFields()
    {
        var desc = new OntologyToolDescriptor("name", "desc");

        await Assert.That(desc.Title).IsNull();
        await Assert.That(desc.OutputSchema).IsNull();
        await Assert.That(desc.Annotations).IsEqualTo(new ToolAnnotations(false, false, false, false));
        await Assert.That(desc.ConstraintSummaries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OntologyToolDescriptor_WithTitle_StoresValue()
    {
        var desc = new OntologyToolDescriptor("name", "desc") { Title = "T" };

        await Assert.That(desc.Title).IsEqualTo("T");
    }

    [Test]
    public async Task OntologyToolDescriptor_WithAnnotations_StoresValue()
    {
        var ann = new ToolAnnotations(true, false, true, false);
        var desc = new OntologyToolDescriptor("name", "desc") { Annotations = ann };

        await Assert.That(desc.Annotations).IsEqualTo(ann);
        await Assert.That(desc.Annotations.ReadOnlyHint).IsTrue();
        await Assert.That(desc.Annotations.IdempotentHint).IsTrue();
    }

    [Test]
    public async Task OntologyToolDescriptor_WithOutputSchema_StoresValue()
    {
        var element = JsonSerializer.SerializeToElement(new { type = "object" });
        var desc = new OntologyToolDescriptor("name", "desc") { OutputSchema = element };

        await Assert.That(desc.OutputSchema).IsNotNull();
        await Assert.That(desc.OutputSchema!.Value.GetRawText()).Contains("\"type\":\"object\"");
    }
}
