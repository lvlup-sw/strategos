using System.Text.Json;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyToolDescriptorTests
{
    [Test]
    public async Task OntologyToolDescriptor_TwoArgConstructor_DefaultsAllNewFields()
    {
        // Arrange & Act — backward-compat: existing two-arg form continues to compile.
        var descriptor = new OntologyToolDescriptor("name", "desc");

        // Assert
        await Assert.That(descriptor.Title).IsNull();
        await Assert.That(descriptor.OutputSchema.HasValue).IsFalse();
        await Assert.That(descriptor.Annotations).IsEqualTo(
            new ToolAnnotations(false, false, false, false));
        await Assert.That(descriptor.ConstraintSummaries).HasCount().EqualTo(0);
        await Assert.That(descriptor.Icons).IsNull();
    }

    [Test]
    public async Task OntologyToolDescriptor_WithIcons_StoresValue()
    {
        var icon = new ToolIcon("https://example.test/icon.png")
        {
            MimeType = "image/png",
            Sizes = ["48x48"],
            Theme = "light",
        };
        var descriptor = new OntologyToolDescriptor("name", "desc");

        var updated = descriptor with { Icons = [icon] };

        await Assert.That(updated.Icons).IsNotNull();
        await Assert.That(updated.Icons!).HasCount().EqualTo(1);
        await Assert.That(updated.Icons[0].Source).IsEqualTo("https://example.test/icon.png");
        await Assert.That(updated.Icons[0].MimeType).IsEqualTo("image/png");
        await Assert.That(updated.Icons[0].Sizes).IsEquivalentTo(["48x48"]);
        await Assert.That(updated.Icons[0].Theme).IsEqualTo("light");
    }

    [Test]
    public async Task OntologyToolDescriptor_WithoutIcons_DoesNotInventPlaceholder()
    {
        var descriptor = new OntologyToolDescriptor("name", "desc")
        {
            Title = "Display Title",
        };

        await Assert.That(descriptor.Icons).IsNull();
    }

    [Test]
    public async Task OntologyToolDescriptor_WithTitle_StoresValue()
    {
        // Arrange
        var descriptor = new OntologyToolDescriptor("name", "desc");

        // Act — record `with` round-trip through the init-only property
        var updated = descriptor with { Title = "Display Title" };

        // Assert
        await Assert.That(updated.Title).IsEqualTo("Display Title");
    }

    [Test]
    public async Task OntologyToolDescriptor_WithAnnotations_StoresValue()
    {
        // Arrange
        var descriptor = new OntologyToolDescriptor("name", "desc");
        var annotations = new ToolAnnotations(true, false, true, false);

        // Act
        var updated = descriptor with { Annotations = annotations };

        // Assert
        await Assert.That(updated.Annotations).IsEqualTo(annotations);
    }

    [Test]
    public async Task OntologyToolDescriptor_WithOutputSchema_StoresValue()
    {
        // Arrange
        var descriptor = new OntologyToolDescriptor("name", "desc");
        var element = JsonSerializer.SerializeToElement(new { type = "object" });

        // Act
        var updated = descriptor with { OutputSchema = element };

        // Assert
        await Assert.That(updated.OutputSchema.HasValue).IsTrue();
        await Assert.That(updated.OutputSchema!.Value.GetRawText()).Contains("\"type\"");
        await Assert.That(updated.OutputSchema.Value.GetRawText()).Contains("\"object\"");
    }
}
