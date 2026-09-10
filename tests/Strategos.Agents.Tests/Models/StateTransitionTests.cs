// =============================================================================
// <copyright file="StateTransitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for <see cref="StateTransition"/> covering creation and properties.
/// </summary>
[Property("Category", "Unit")]
public class StateTransitionTests
{
    /// <summary>
    /// Verifies that StateTransition constructor sets all properties correctly.
    /// </summary>
    [Test]
    public async Task Create_WithValidValues_SetsAllProperties()
    {
        // Arrange
        var from = SpecialistState.Receiving;
        var to = SpecialistState.Reasoning;
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var transition = new StateTransition(from, to, timestamp);

        // Assert
        await Assert.That(transition.From).IsEqualTo(SpecialistState.Receiving);
        await Assert.That(transition.To).IsEqualTo(SpecialistState.Reasoning);
        await Assert.That(transition.Timestamp).IsEqualTo(timestamp);
    }

    /// <summary>
    /// Verifies that StateTransition supports with-expression for immutable updates.
    /// </summary>
    [Test]
    public async Task WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new StateTransition(
            SpecialistState.Generating,
            SpecialistState.Executing,
            DateTimeOffset.UtcNow);

        // Act
        var updated = original with { To = SpecialistState.Signaling };

        // Assert
        await Assert.That(updated.To).IsEqualTo(SpecialistState.Signaling);
        await Assert.That(original.To).IsEqualTo(SpecialistState.Executing);
    }

    /// <summary>
    /// Verifies that two StateTransitions with same values are equal.
    /// </summary>
    [Test]
    public async Task Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var transition1 = new StateTransition(SpecialistState.Waiting, SpecialistState.Interpreting, timestamp);
        var transition2 = new StateTransition(SpecialistState.Waiting, SpecialistState.Interpreting, timestamp);

        // Assert
        await Assert.That(transition1).IsEqualTo(transition2);
    }

    /// <summary>
    /// Verifies that two StateTransitions with different values are not equal.
    /// </summary>
    [Test]
    public async Task Equality_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var transition1 = new StateTransition(SpecialistState.Receiving, SpecialistState.Reasoning, timestamp);
        var transition2 = new StateTransition(SpecialistState.Generating, SpecialistState.Executing, timestamp);

        // Assert
        await Assert.That(transition1).IsNotEqualTo(transition2);
    }
}

