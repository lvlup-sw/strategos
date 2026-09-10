// =============================================================================
// <copyright file="ApprovalRejectionDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ApprovalRejectionDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class ApprovalRejectionDefinitionTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid params returns a definition.
    /// </summary>
    [Test]
    public async Task Create_WithValidParams_ReturnsDefinition()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(TestRejectionNotifyStep))
        };

        // Act
        var rejection = ApprovalRejectionDefinition.Create(steps, isTerminal: true);

        // Assert
        await Assert.That(rejection.Steps.Count).IsEqualTo(1);
        await Assert.That(rejection.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Create generates a unique RejectionHandlerId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueRejectionHandlerId()
    {
        // Act
        var rejection1 = ApprovalRejectionDefinition.Create([], isTerminal: false);
        var rejection2 = ApprovalRejectionDefinition.Create([], isTerminal: false);

        // Assert
        await Assert.That(rejection1.RejectionHandlerId).IsNotNull();
        await Assert.That(rejection1.RejectionHandlerId).IsNotEqualTo(rejection2.RejectionHandlerId);
    }

    /// <summary>
    /// Verifies that Create throws for null steps.
    /// </summary>
    [Test]
    public async Task Create_WithNullSteps_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalRejectionDefinition.Create(null!, isTerminal: true))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Steps Tests
    // =============================================================================

    /// <summary>
    /// Verifies that empty steps is allowed.
    /// </summary>
    [Test]
    public async Task Create_WithEmptySteps_Succeeds()
    {
        // Act
        var rejection = ApprovalRejectionDefinition.Create([], isTerminal: true);

        // Assert
        await Assert.That(rejection.Steps).IsEmpty();
    }

    /// <summary>
    /// Verifies that step order is preserved.
    /// </summary>
    [Test]
    public async Task Steps_PreservesOrder()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(TestRejectionNotifyStep)),
            StepDefinition.Create(typeof(TestRejectionCleanupStep)),
        };

        // Act
        var rejection = ApprovalRejectionDefinition.Create(steps, isTerminal: true);

        // Assert
        await Assert.That(rejection.Steps[0].StepTypeName).IsEqualTo("TestRejectionNotifyStep");
        await Assert.That(rejection.Steps[1].StepTypeName).IsEqualTo("TestRejectionCleanupStep");
    }

    // =============================================================================
    // C. IsTerminal Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsTerminal true terminates workflow on rejection.
    /// </summary>
    [Test]
    public async Task IsTerminal_True_IndicatesWorkflowTermination()
    {
        // Act
        var rejection = ApprovalRejectionDefinition.Create([], isTerminal: true);

        // Assert
        await Assert.That(rejection.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that IsTerminal false rejoins main flow.
    /// </summary>
    [Test]
    public async Task IsTerminal_False_IndicatesRejoin()
    {
        // Act
        var rejection = ApprovalRejectionDefinition.Create([], isTerminal: false);

        // Assert
        await Assert.That(rejection.IsTerminal).IsFalse();
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ApprovalRejectionDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task ApprovalRejectionDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = ApprovalRejectionDefinition.Create([], isTerminal: false);

        // Act - Use record with syntax
        var modified = original with { IsTerminal = true };

        // Assert
        await Assert.That(original.IsTerminal).IsFalse();
        await Assert.That(modified.IsTerminal).IsTrue();
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that default Steps is empty list.
    /// </summary>
    [Test]
    public async Task Steps_DefaultsToEmptyList()
    {
        // Act
        var rejection = ApprovalRejectionDefinition.Create([], isTerminal: true);

        // Assert
        await Assert.That(rejection.Steps).IsNotNull();
        await Assert.That(rejection.Steps).IsEmpty();
    }
}

/// <summary>
/// Test step class for rejection testing.
/// </summary>
internal sealed class TestRejectionNotifyStep
{
}

/// <summary>
/// Test step class for rejection testing.
/// </summary>
internal sealed class TestRejectionCleanupStep
{
}
