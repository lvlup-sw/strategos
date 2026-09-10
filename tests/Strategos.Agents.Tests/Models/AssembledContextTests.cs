// =============================================================================
// <copyright file="AssembledContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="AssembledContext"/> class.
/// </summary>
[Property("Category", "Unit")]
public class AssembledContextTests
{
    // =============================================================================
    // A. Empty Context Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Empty has no segments.
    /// </summary>
    [Test]
    public async Task AssembledContext_Empty_HasNoSegments()
    {
        // Arrange & Act
        var context = AssembledContext.Empty;

        // Assert
        await Assert.That(context.Segments).HasCount(0);
    }

    /// <summary>
    /// Verifies that Empty IsEmpty returns true.
    /// </summary>
    [Test]
    public async Task AssembledContext_Empty_IsEmptyReturnsTrue()
    {
        // Arrange & Act
        var context = AssembledContext.Empty;

        // Assert
        await Assert.That(context.IsEmpty).IsTrue();
    }

    // =============================================================================
    // B. Context with Segments Tests
    // =============================================================================

    /// <summary>
    /// Verifies that context with segments stores them correctly.
    /// </summary>
    [Test]
    public async Task AssembledContext_WithSegments_StoresSegments()
    {
        // Arrange
        var segments = new List<ContextSegment>
        {
            new LiteralContextSegment("First segment"),
            new StateContextSegment("field", "value"),
        };

        // Act
        var context = new AssembledContext(segments);

        // Assert
        await Assert.That(context.Segments).HasCount(2);
        await Assert.That(context.Segments[0]).IsTypeOf<LiteralContextSegment>();
        await Assert.That(context.Segments[1]).IsTypeOf<StateContextSegment>();
    }

    /// <summary>
    /// Verifies that context with segments IsEmpty returns false.
    /// </summary>
    [Test]
    public async Task AssembledContext_WithSegments_IsEmptyReturnsFalse()
    {
        // Arrange
        var segments = new List<ContextSegment>
        {
            new LiteralContextSegment("Content"),
        };

        // Act
        var context = new AssembledContext(segments);

        // Assert
        await Assert.That(context.IsEmpty).IsFalse();
    }

    // =============================================================================
    // C. ToPromptString Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ToPromptString joins segments with double newlines.
    /// </summary>
    [Test]
    public async Task AssembledContext_ToPromptString_JoinsSegmentsWithNewlines()
    {
        // Arrange
        var segments = new List<ContextSegment>
        {
            new LiteralContextSegment("First segment content"),
            new StateContextSegment("field", "value content"),
            new LiteralContextSegment("Third segment content"),
        };
        var context = new AssembledContext(segments);

        // Act
        var result = context.ToPromptString();

        // Assert
        await Assert.That(result).Contains("First segment content");
        await Assert.That(result).Contains("value content");
        await Assert.That(result).Contains("Third segment content");
        await Assert.That(result).Contains("\n\n");
    }

    /// <summary>
    /// Verifies that ToPromptString on empty context returns empty string.
    /// </summary>
    [Test]
    public async Task AssembledContext_ToPromptString_EmptyContextReturnsEmpty()
    {
        // Arrange
        var context = AssembledContext.Empty;

        // Act
        var result = context.ToPromptString();

        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Verifies that segments are returned in order.
    /// </summary>
    [Test]
    public async Task AssembledContext_ToPromptString_PreservesOrder()
    {
        // Arrange
        var segments = new List<ContextSegment>
        {
            new LiteralContextSegment("AAA"),
            new LiteralContextSegment("BBB"),
            new LiteralContextSegment("CCC"),
        };
        var context = new AssembledContext(segments);

        // Act
        var result = context.ToPromptString();

        // Assert
        var aIndex = result.IndexOf("AAA", StringComparison.Ordinal);
        var bIndex = result.IndexOf("BBB", StringComparison.Ordinal);
        var cIndex = result.IndexOf("CCC", StringComparison.Ordinal);

        await Assert.That(aIndex).IsLessThan(bIndex);
        await Assert.That(bIndex).IsLessThan(cIndex);
    }

    // =============================================================================
    // D. Singleton Pattern Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Empty is a singleton.
    /// </summary>
    [Test]
    public async Task AssembledContext_Empty_IsSingleton()
    {
        // Arrange & Act
        var empty1 = AssembledContext.Empty;
        var empty2 = AssembledContext.Empty;

        // Assert
        await Assert.That(ReferenceEquals(empty1, empty2)).IsTrue();
    }
}
