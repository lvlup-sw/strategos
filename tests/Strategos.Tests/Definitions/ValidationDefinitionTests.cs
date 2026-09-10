// =============================================================================
// <copyright file="ValidationDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ValidationDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class ValidationDefinitionTests
{
    // =============================================================================
    // A. Create Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid inputs returns a definition.
    /// </summary>
    [Test]
    public async Task Create_WithValidInputs_ReturnsDefinition()
    {
        // Arrange
        const string predicate = "state.Order.Items.Any()";
        const string errorMessage = "Order must have items";

        // Act
        var definition = ValidationDefinition.Create(predicate, errorMessage);

        // Assert
        await Assert.That(definition).IsNotNull();
        await Assert.That(definition.PredicateExpression).IsEqualTo(predicate);
        await Assert.That(definition.ErrorMessage).IsEqualTo(errorMessage);
    }

    /// <summary>
    /// Verifies that Create throws for null predicate expression.
    /// </summary>
    [Test]
    public async Task Create_WithNullPredicateExpression_ThrowsArgumentNullException()
    {
        // Arrange
        const string errorMessage = "Order must have items";

        // Act & Assert
        await Assert.That(() => ValidationDefinition.Create(null!, errorMessage))
            .Throws<ArgumentNullException>()
            .WithMessageContaining("predicateExpression");
    }

    /// <summary>
    /// Verifies that Create throws for null error message.
    /// </summary>
    [Test]
    public async Task Create_WithNullErrorMessage_ThrowsArgumentNullException()
    {
        // Arrange
        const string predicate = "state.Order.Items.Any()";

        // Act & Assert
        await Assert.That(() => ValidationDefinition.Create(predicate, null!))
            .Throws<ArgumentNullException>()
            .WithMessageContaining("errorMessage");
    }

    /// <summary>
    /// Verifies that Create throws for empty error message.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyErrorMessage_ThrowsArgumentException()
    {
        // Arrange
        const string predicate = "state.Order.Items.Any()";

        // Act & Assert
        await Assert.That(() => ValidationDefinition.Create(predicate, string.Empty))
            .Throws<ArgumentException>()
            .WithMessageContaining("errorMessage");
    }

    /// <summary>
    /// Verifies that Create throws for whitespace-only error message.
    /// </summary>
    [Test]
    public async Task Create_WithWhitespaceErrorMessage_ThrowsArgumentException()
    {
        // Arrange
        const string predicate = "state.Order.Items.Any()";

        // Act & Assert
        await Assert.That(() => ValidationDefinition.Create(predicate, "   "))
            .Throws<ArgumentException>()
            .WithMessageContaining("errorMessage");
    }

    // =============================================================================
    // B. Property Access Tests
    // =============================================================================

    /// <summary>
    /// Verifies that PredicateExpression stores the expression text correctly.
    /// </summary>
    [Test]
    public async Task PredicateExpression_StoresExpressionText()
    {
        // Arrange
        const string predicate = "state.Order.Total > 0";
        var definition = ValidationDefinition.Create(predicate, "Total must be positive");

        // Act & Assert
        await Assert.That(definition.PredicateExpression).IsEqualTo(predicate);
    }

    /// <summary>
    /// Verifies that ErrorMessage stores the message correctly.
    /// </summary>
    [Test]
    public async Task ErrorMessage_StoresMessageText()
    {
        // Arrange
        const string message = "Order total must be positive";
        var definition = ValidationDefinition.Create("state.Order.Total > 0", message);

        // Act & Assert
        await Assert.That(definition.ErrorMessage).IsEqualTo(message);
    }

    // =============================================================================
    // C. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ValidationDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task ValidationDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = ValidationDefinition.Create("state.Value > 0", "Value must be positive");

        // Act - Use record with syntax
        var modified = original with { ErrorMessage = "Updated message" };

        // Assert
        await Assert.That(original.ErrorMessage).IsEqualTo("Value must be positive");
        await Assert.That(modified.ErrorMessage).IsEqualTo("Updated message");
        await Assert.That(original).IsNotEqualTo(modified);
    }
}
