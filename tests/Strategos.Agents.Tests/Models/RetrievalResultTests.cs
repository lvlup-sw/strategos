// =============================================================================
// <copyright file="RetrievalResultTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="RetrievalResult"/> record.
/// </summary>
[Property("Category", "Unit")]
public class RetrievalResultTests
{
    // =============================================================================
    // A. Construction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that content is stored correctly.
    /// </summary>
    [Test]
    public async Task RetrievalResult_WithContent_StoresContent()
    {
        // Arrange
        var content = "This is retrieved content";

        // Act
        var result = new RetrievalResult(content, 0.85);

        // Assert
        await Assert.That(result.Content).IsEqualTo(content);
    }

    /// <summary>
    /// Verifies that score is stored correctly.
    /// </summary>
    [Test]
    public async Task RetrievalResult_WithScore_StoresScore()
    {
        // Arrange
        var score = 0.95;

        // Act
        var result = new RetrievalResult("content", score);

        // Assert
        await Assert.That(result.Score).IsEqualTo(score);
    }

    /// <summary>
    /// Verifies that metadata is stored correctly.
    /// </summary>
    [Test]
    public async Task RetrievalResult_WithMetadata_StoresMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object?>
        {
            ["source"] = "knowledge-base",
            ["page"] = 42,
        };

        // Act
        var result = new RetrievalResult("content", 0.9, "doc-123", metadata);

        // Assert
        await Assert.That(result.Metadata).IsNotNull();
        await Assert.That(result.Metadata!["source"]).IsEqualTo("knowledge-base");
        await Assert.That(result.Metadata!["page"]).IsEqualTo(42);
    }

    /// <summary>
    /// Verifies that null metadata is accepted.
    /// </summary>
    [Test]
    public async Task RetrievalResult_WithNullMetadata_AcceptsNull()
    {
        // Arrange & Act
        var result = new RetrievalResult("content", 0.9, null, null);

        // Assert
        await Assert.That(result.Metadata).IsNull();
    }

    // =============================================================================
    // B. Source ID Tests
    // =============================================================================

    /// <summary>
    /// Verifies that source ID is stored correctly.
    /// </summary>
    [Test]
    public async Task RetrievalResult_WithSourceId_StoresSourceId()
    {
        // Arrange
        var sourceId = "document-abc-123";

        // Act
        var result = new RetrievalResult("content", 0.9, sourceId);

        // Assert
        await Assert.That(result.SourceId).IsEqualTo(sourceId);
    }

    /// <summary>
    /// Verifies that null source ID is accepted.
    /// </summary>
    [Test]
    public async Task RetrievalResult_WithNullSourceId_AcceptsNull()
    {
        // Arrange & Act
        var result = new RetrievalResult("content", 0.9);

        // Assert
        await Assert.That(result.SourceId).IsNull();
    }

    // =============================================================================
    // C. Record Equality Tests
    // =============================================================================

    /// <summary>
    /// Verifies that records with same values are equal.
    /// </summary>
    [Test]
    public async Task Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var result1 = new RetrievalResult("content", 0.9, "source-1");
        var result2 = new RetrievalResult("content", 0.9, "source-1");

        // Assert
        await Assert.That(result1).IsEqualTo(result2);
    }

    /// <summary>
    /// Verifies that records with different content are not equal.
    /// </summary>
    [Test]
    public async Task Equals_WithDifferentContent_ReturnsFalse()
    {
        // Arrange
        var result1 = new RetrievalResult("content-1", 0.9);
        var result2 = new RetrievalResult("content-2", 0.9);

        // Assert
        await Assert.That(result1).IsNotEqualTo(result2);
    }
}
