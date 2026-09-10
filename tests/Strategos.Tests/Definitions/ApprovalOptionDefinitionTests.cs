// =============================================================================
// <copyright file="ApprovalOptionDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ApprovalOptionDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class ApprovalOptionDefinitionTests
{
    // =============================================================================
    // A. Construction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that constructor with valid params creates instance.
    /// </summary>
    [Test]
    public async Task Constructor_WithValidParams_CreatesInstance()
    {
        // Act
        var option = new ApprovalOptionDefinition("approve", "Approve", "Approve the request");

        // Assert
        await Assert.That(option.OptionId).IsEqualTo("approve");
        await Assert.That(option.Label).IsEqualTo("Approve");
        await Assert.That(option.Description).IsEqualTo("Approve the request");
    }

    /// <summary>
    /// Verifies that IsDefault defaults to false.
    /// </summary>
    [Test]
    public async Task Constructor_IsDefault_DefaultsToFalse()
    {
        // Act
        var option = new ApprovalOptionDefinition("approve", "Approve", "Approve the request");

        // Assert
        await Assert.That(option.IsDefault).IsFalse();
    }

    /// <summary>
    /// Verifies that IsDefault can be set to true.
    /// </summary>
    [Test]
    public async Task Constructor_WithIsDefaultTrue_SetsIsDefault()
    {
        // Act
        var option = new ApprovalOptionDefinition("approve", "Approve", "Approve the request", IsDefault: true);

        // Assert
        await Assert.That(option.IsDefault).IsTrue();
    }

    // =============================================================================
    // B. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ApprovalOptionDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task ApprovalOptionDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = new ApprovalOptionDefinition("approve", "Approve", "Approve the request");

        // Act - Use record with syntax
        var modified = original with { IsDefault = true };

        // Assert
        await Assert.That(original.IsDefault).IsFalse();
        await Assert.That(modified.IsDefault).IsTrue();
        await Assert.That(original).IsNotEqualTo(modified);
    }

    // =============================================================================
    // C. Equality Tests
    // =============================================================================

    /// <summary>
    /// Verifies that two options with same values are equal.
    /// </summary>
    [Test]
    public async Task Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var option1 = new ApprovalOptionDefinition("approve", "Approve", "Approve the request", true);
        var option2 = new ApprovalOptionDefinition("approve", "Approve", "Approve the request", true);

        // Assert
        await Assert.That(option1).IsEqualTo(option2);
    }

    /// <summary>
    /// Verifies that two options with different values are not equal.
    /// </summary>
    [Test]
    public async Task Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var option1 = new ApprovalOptionDefinition("approve", "Approve", "Approve the request");
        var option2 = new ApprovalOptionDefinition("reject", "Reject", "Reject the request");

        // Assert
        await Assert.That(option1).IsNotEqualTo(option2);
    }
}
