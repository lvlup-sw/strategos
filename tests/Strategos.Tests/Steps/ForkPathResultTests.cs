// =============================================================================
// <copyright file="ForkPathResultTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;
using Strategos.Steps;

namespace Strategos.Tests.Steps;

/// <summary>
/// Unit tests for <see cref="ForkPathResult{TState}"/>.
/// </summary>
[Property("Category", "Unit")]
public class ForkPathResultTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Success creates a result with Success status and state.
    /// </summary>
    [Test]
    public async Task Success_WithState_CreatesSuccessResult()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };

        // Act
        var result = ForkPathResult<TestWorkflowState>.Success(pathIndex: 0, state);

        // Assert
        await Assert.That(result.PathIndex).IsEqualTo(0);
        await Assert.That(result.Status).IsEqualTo(ForkPathStatus.Success);
        await Assert.That(result.State).IsEqualTo(state);
    }

    /// <summary>
    /// Verifies that Success throws for null state.
    /// </summary>
    [Test]
    public async Task Success_WithNullState_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ForkPathResult<TestWorkflowState>.Success(0, null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Success throws for negative path index.
    /// </summary>
    [Test]
    public async Task Success_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };

        // Act & Assert
        await Assert.That(() => ForkPathResult<TestWorkflowState>.Success(-1, state))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that Failed creates a result with Failed status and null state.
    /// </summary>
    [Test]
    public async Task Failed_CreatesFailedResultWithNullState()
    {
        // Act
        var result = ForkPathResult<TestWorkflowState>.Failed(pathIndex: 1);

        // Assert
        await Assert.That(result.PathIndex).IsEqualTo(1);
        await Assert.That(result.Status).IsEqualTo(ForkPathStatus.Failed);
        await Assert.That(result.State).IsNull();
    }

    /// <summary>
    /// Verifies that Failed throws for negative path index.
    /// </summary>
    [Test]
    public async Task Failed_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => ForkPathResult<TestWorkflowState>.Failed(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that FailedWithRecovery creates a result with recovery status and state.
    /// </summary>
    [Test]
    public async Task FailedWithRecovery_WithState_CreatesRecoveryResult()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };

        // Act
        var result = ForkPathResult<TestWorkflowState>.FailedWithRecovery(pathIndex: 2, state);

        // Assert
        await Assert.That(result.PathIndex).IsEqualTo(2);
        await Assert.That(result.Status).IsEqualTo(ForkPathStatus.FailedWithRecovery);
        await Assert.That(result.State).IsEqualTo(state);
    }

    /// <summary>
    /// Verifies that FailedWithRecovery throws for null state.
    /// </summary>
    [Test]
    public async Task FailedWithRecovery_WithNullState_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ForkPathResult<TestWorkflowState>.FailedWithRecovery(0, null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsSuccessful returns true for Success status.
    /// </summary>
    [Test]
    public async Task IsSuccessful_WithSuccessStatus_ReturnsTrue()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var result = ForkPathResult<TestWorkflowState>.Success(0, state);

        // Assert
        await Assert.That(result.IsSuccessful).IsTrue();
    }

    /// <summary>
    /// Verifies that IsSuccessful returns false for Failed status.
    /// </summary>
    [Test]
    public async Task IsSuccessful_WithFailedStatus_ReturnsFalse()
    {
        // Arrange
        var result = ForkPathResult<TestWorkflowState>.Failed(0);

        // Assert
        await Assert.That(result.IsSuccessful).IsFalse();
    }

    /// <summary>
    /// Verifies that IsSuccessful returns true for FailedWithRecovery status.
    /// </summary>
    [Test]
    public async Task IsSuccessful_WithRecoveryStatus_ReturnsTrue()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var result = ForkPathResult<TestWorkflowState>.FailedWithRecovery(0, state);

        // Assert
        await Assert.That(result.IsSuccessful).IsTrue();
    }

    /// <summary>
    /// Verifies that HasState returns true when state is present.
    /// </summary>
    [Test]
    public async Task HasState_WithState_ReturnsTrue()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var result = ForkPathResult<TestWorkflowState>.Success(0, state);

        // Assert
        await Assert.That(result.HasState).IsTrue();
    }

    /// <summary>
    /// Verifies that HasState returns false when state is null.
    /// </summary>
    [Test]
    public async Task HasState_WithNullState_ReturnsFalse()
    {
        // Arrange
        var result = ForkPathResult<TestWorkflowState>.Failed(0);

        // Assert
        await Assert.That(result.HasState).IsFalse();
    }

    // =============================================================================
    // C. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ForkPathResult is an immutable record.
    /// </summary>
    [Test]
    public async Task ForkPathResult_IsImmutableRecord()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var original = ForkPathResult<TestWorkflowState>.Success(0, state);

        // Act - Use record with syntax
        var modified = original with { PathIndex = 5 };

        // Assert
        await Assert.That(original.PathIndex).IsEqualTo(0);
        await Assert.That(modified.PathIndex).IsEqualTo(5);
        await Assert.That(original).IsNotEqualTo(modified);
    }
}
