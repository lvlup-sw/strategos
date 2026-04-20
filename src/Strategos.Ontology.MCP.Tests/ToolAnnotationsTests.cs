namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Tests for the <see cref="ToolAnnotations"/> record — MCP 2025-11-25 tool
/// annotation hints (readOnly, destructive, idempotent, openWorld).
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.2
/// </summary>
public class ToolAnnotationsTests
{
    [Test]
    public async Task ToolAnnotations_Construction_StoresAllFourHints()
    {
        var ann = new ToolAnnotations(
            ReadOnlyHint: true,
            DestructiveHint: false,
            IdempotentHint: true,
            OpenWorldHint: false);

        await Assert.That(ann.ReadOnlyHint).IsTrue();
        await Assert.That(ann.DestructiveHint).IsFalse();
        await Assert.That(ann.IdempotentHint).IsTrue();
        await Assert.That(ann.OpenWorldHint).IsFalse();
    }

    [Test]
    public async Task ToolAnnotations_RecordEquality_HoldsForSameInputs()
    {
        var a = new ToolAnnotations(true, false, true, false);
        var b = new ToolAnnotations(true, false, true, false);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
    }
}
