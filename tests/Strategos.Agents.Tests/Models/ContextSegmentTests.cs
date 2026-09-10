// =============================================================================
// <copyright file="ContextSegmentTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="ContextSegment"/> hierarchy.
/// </summary>
[Property("Category", "Unit")]
public class ContextSegmentTests
{
    // =============================================================================
    // A. Base Class Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ContextSegment is abstract and cannot be instantiated directly.
    /// </summary>
    [Test]
    public async Task ContextSegment_IsAbstract_CannotInstantiate()
    {
        // Arrange
        var segmentType = typeof(ContextSegment);

        // Assert
        await Assert.That(segmentType.IsAbstract).IsTrue();
    }

    // =============================================================================
    // B. StateContextSegment Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StateContextSegment returns value string via ToPromptString.
    /// </summary>
    [Test]
    public async Task StateContextSegment_ToPromptString_ReturnsValueString()
    {
        // Arrange
        var segment = new StateContextSegment("userName", "John Doe");

        // Act
        var result = segment.ToPromptString();

        // Assert
        await Assert.That(result).IsEqualTo("John Doe");
    }

    /// <summary>
    /// Verifies that StateContextSegment with null value returns empty string.
    /// </summary>
    [Test]
    public async Task StateContextSegment_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        var segment = new StateContextSegment("nullField", null);

        // Act
        var result = segment.ToPromptString();

        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Verifies that StateContextSegment stores name correctly.
    /// </summary>
    [Test]
    public async Task StateContextSegment_StoresName()
    {
        // Arrange
        var segment = new StateContextSegment("fieldName", "value");

        // Assert
        await Assert.That(segment.Name).IsEqualTo("fieldName");
    }

    /// <summary>
    /// Verifies that StateContextSegment stores value correctly.
    /// </summary>
    [Test]
    public async Task StateContextSegment_StoresValue()
    {
        // Arrange
        var value = new { Key = "test" };
        var segment = new StateContextSegment("field", value);

        // Assert
        await Assert.That(segment.Value).IsEqualTo(value);
    }

    // =============================================================================
    // C. LiteralContextSegment Tests
    // =============================================================================

    /// <summary>
    /// Verifies that LiteralContextSegment returns literal value via ToPromptString.
    /// </summary>
    [Test]
    public async Task LiteralContextSegment_ToPromptString_ReturnsLiteral()
    {
        // Arrange
        var literal = "This is a literal context value.";
        var segment = new LiteralContextSegment(literal);

        // Act
        var result = segment.ToPromptString();

        // Assert
        await Assert.That(result).IsEqualTo(literal);
    }

    /// <summary>
    /// Verifies that LiteralContextSegment stores value correctly.
    /// </summary>
    [Test]
    public async Task LiteralContextSegment_StoresValue()
    {
        // Arrange
        var value = "Test literal";
        var segment = new LiteralContextSegment(value);

        // Assert
        await Assert.That(segment.Value).IsEqualTo(value);
    }

    // =============================================================================
    // D. RetrievalContextSegment Tests
    // =============================================================================

    /// <summary>
    /// Verifies that RetrievalContextSegment joins results correctly via ToPromptString.
    /// </summary>
    [Test]
    public async Task RetrievalContextSegment_ToPromptString_JoinsResults()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new("First document content", 0.95),
            new("Second document content", 0.85),
            new("Third document content", 0.75),
        };
        var segment = new RetrievalContextSegment("knowledge-base", results);

        // Act
        var result = segment.ToPromptString();

        // Assert
        await Assert.That(result).Contains("First document content");
        await Assert.That(result).Contains("Second document content");
        await Assert.That(result).Contains("Third document content");
        await Assert.That(result).Contains("\n---\n");
    }

    /// <summary>
    /// Verifies that RetrievalContextSegment stores collection name.
    /// </summary>
    [Test]
    public async Task RetrievalContextSegment_StoresCollectionName()
    {
        // Arrange
        var segment = new RetrievalContextSegment("my-collection", []);

        // Assert
        await Assert.That(segment.CollectionName).IsEqualTo("my-collection");
    }

    /// <summary>
    /// Verifies that RetrievalContextSegment with empty results returns empty string.
    /// </summary>
    [Test]
    public async Task RetrievalContextSegment_WithEmptyResults_ReturnsEmptyString()
    {
        // Arrange
        var segment = new RetrievalContextSegment("collection", []);

        // Act
        var result = segment.ToPromptString();

        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Verifies that RetrievalContextSegment with single result returns content without separator.
    /// </summary>
    [Test]
    public async Task RetrievalContextSegment_WithSingleResult_ReturnsContentWithoutSeparator()
    {
        // Arrange
        var results = new List<RetrievalResult>
        {
            new("Single document content", 0.95),
        };
        var segment = new RetrievalContextSegment("collection", results);

        // Act
        var result = segment.ToPromptString();

        // Assert
        await Assert.That(result).IsEqualTo("Single document content");
        await Assert.That(result).DoesNotContain("---");
    }
}
