namespace Strategos.Ontology.MCP.Tests;

public class ToolAnnotationsTests
{
    [Test]
    public async Task ToolAnnotations_Construction_StoresAllFourHints()
    {
        // Arrange & Act
        var annotations = new ToolAnnotations(
            ReadOnlyHint: true,
            DestructiveHint: false,
            IdempotentHint: true,
            OpenWorldHint: false);

        // Assert
        await Assert.That(annotations.ReadOnlyHint).IsEqualTo(true);
        await Assert.That(annotations.DestructiveHint).IsEqualTo(false);
        await Assert.That(annotations.IdempotentHint).IsEqualTo(true);
        await Assert.That(annotations.OpenWorldHint).IsEqualTo(false);
    }

    [Test]
    public async Task ToolAnnotations_RecordEquality_HoldsForSameInputs()
    {
        // Arrange
        var a = new ToolAnnotations(true, false, true, false);
        var b = new ToolAnnotations(true, false, true, false);

        // Act & Assert — record value semantics
        await Assert.That(a == b).IsTrue();
        await Assert.That(a.Equals(b)).IsTrue();
    }
}
