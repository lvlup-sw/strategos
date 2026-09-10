// =============================================================================
// <copyright file="LowConfidenceHandlerDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="LowConfidenceHandlerDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class LowConfidenceHandlerDefinitionTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with steps captures the handler steps.
    /// </summary>
    [Test]
    public async Task Create_WithSteps_CapturesSteps()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(TestEnrichContextStep)),
            StepDefinition.Create(typeof(TestHumanReviewStep)),
        };

        // Act
        var handler = LowConfidenceHandlerDefinition.Create(steps);

        // Assert
        await Assert.That(handler.HandlerSteps.Count).IsEqualTo(2);
        await Assert.That(handler.HandlerSteps[0].StepTypeName).IsEqualTo("TestEnrichContextStep");
        await Assert.That(handler.HandlerSteps[1].StepTypeName).IsEqualTo("TestHumanReviewStep");
    }

    /// <summary>
    /// Verifies that Create with empty steps succeeds (just rejoin).
    /// </summary>
    [Test]
    public async Task Create_WithEmptySteps_Succeeds()
    {
        // Arrange
        var steps = new List<StepDefinition>();

        // Act
        var handler = LowConfidenceHandlerDefinition.Create(steps);

        // Assert
        await Assert.That(handler.HandlerSteps.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Create throws for null steps.
    /// </summary>
    [Test]
    public async Task Create_WithNullSteps_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => LowConfidenceHandlerDefinition.Create(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create generates a unique HandlerId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueHandlerId()
    {
        // Arrange
        var steps = new List<StepDefinition>();

        // Act
        var handler1 = LowConfidenceHandlerDefinition.Create(steps);
        var handler2 = LowConfidenceHandlerDefinition.Create(steps);

        // Assert
        await Assert.That(handler1.HandlerId).IsNotNull();
        await Assert.That(handler1.HandlerId).IsNotEqualTo(handler2.HandlerId);
    }

    // =============================================================================
    // B. Default Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsTerminal defaults to false.
    /// </summary>
    [Test]
    public async Task Create_IsTerminal_DefaultsToFalse()
    {
        // Act
        var handler = LowConfidenceHandlerDefinition.Create([]);

        // Assert
        await Assert.That(handler.IsTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that RejoinStepId defaults to null.
    /// </summary>
    [Test]
    public async Task Create_RejoinStepId_DefaultsToNull()
    {
        // Act
        var handler = LowConfidenceHandlerDefinition.Create([]);

        // Assert
        await Assert.That(handler.RejoinStepId).IsNull();
    }

    // =============================================================================
    // C. Terminal Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create as terminal sets IsTerminal to true.
    /// </summary>
    [Test]
    public async Task Create_AsTerminal_SetsIsTerminal()
    {
        // Arrange
        var steps = new List<StepDefinition>();

        // Act
        var handler = LowConfidenceHandlerDefinition.Create(steps, isTerminal: true);

        // Assert
        await Assert.That(handler.IsTerminal).IsTrue();
    }

    // =============================================================================
    // D. WithRejoin Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithRejoin sets the RejoinStepId.
    /// </summary>
    [Test]
    public async Task WithRejoin_SetsRejoinStepId()
    {
        // Arrange
        var handler = LowConfidenceHandlerDefinition.Create([]);
        const string stepId = "step-123";

        // Act
        var updated = handler.WithRejoin(stepId);

        // Assert
        await Assert.That(updated.RejoinStepId).IsEqualTo("step-123");
    }

    /// <summary>
    /// Verifies that WithRejoin preserves the original instance.
    /// </summary>
    [Test]
    public async Task WithRejoin_PreservesOriginal()
    {
        // Arrange
        var original = LowConfidenceHandlerDefinition.Create([]);

        // Act
        var updated = original.WithRejoin("step-123");

        // Assert
        await Assert.That(original.RejoinStepId).IsNull();
        await Assert.That(updated.RejoinStepId).IsNotNull();
    }

    /// <summary>
    /// Verifies that WithRejoin throws for null step ID.
    /// </summary>
    [Test]
    public async Task WithRejoin_WithNullStepId_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = LowConfidenceHandlerDefinition.Create([]);

        // Act & Assert
        await Assert.That(() => handler.WithRejoin(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // E. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that LowConfidenceHandlerDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task LowConfidenceHandlerDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = LowConfidenceHandlerDefinition.Create([]);

        // Act - Use record with syntax
        var modified = original with { IsTerminal = true };

        // Assert
        await Assert.That(original.IsTerminal).IsFalse();
        await Assert.That(modified.IsTerminal).IsTrue();
        await Assert.That(original).IsNotEqualTo(modified);
    }
}

/// <summary>
/// Test step class for unit testing.
/// </summary>
internal sealed class TestEnrichContextStep
{
}

/// <summary>
/// Test step class for unit testing.
/// </summary>
internal sealed class TestHumanReviewStep
{
}
