// =============================================================================
// <copyright file="ForkContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;
using Strategos.Steps;

namespace Strategos.Tests.Steps;

/// <summary>
/// Unit tests for <see cref="ForkContext{TState}"/>.
/// </summary>
[Property("Category", "Unit")]
public class ForkContextTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create creates a context with path results.
    /// </summary>
    [Test]
    public async Task Create_WithPathResults_CreatesContext()
    {
        // Arrange
        var state1 = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var state2 = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-2" };
        var results = new[]
        {
            ForkPathResult<TestWorkflowState>.Success(0, state1),
            ForkPathResult<TestWorkflowState>.Success(1, state2),
        };

        // Act
        var context = ForkContext<TestWorkflowState>.Create(results);

        // Assert
        await Assert.That(context.PathResults.Count).IsEqualTo(2);
        await Assert.That(context.PathResults[0].State).IsEqualTo(state1);
        await Assert.That(context.PathResults[1].State).IsEqualTo(state2);
    }

    /// <summary>
    /// Verifies that Create throws for null results.
    /// </summary>
    [Test]
    public async Task Create_WithNullResults_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ForkContext<TestWorkflowState>.Create(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for empty results.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyResults_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => ForkContext<TestWorkflowState>.Create([]))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // B. Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AllSucceeded returns true when all paths succeed.
    /// </summary>
    [Test]
    public async Task AllSucceeded_WithAllSuccess_ReturnsTrue()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var context = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state),
            ForkPathResult<TestWorkflowState>.Success(1, state),
        ]);

        // Assert
        await Assert.That(context.AllSucceeded).IsTrue();
    }

    /// <summary>
    /// Verifies that AllSucceeded returns false when any path failed.
    /// </summary>
    [Test]
    public async Task AllSucceeded_WithAnyFailed_ReturnsFalse()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var context = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state),
            ForkPathResult<TestWorkflowState>.Failed(1),
        ]);

        // Assert
        await Assert.That(context.AllSucceeded).IsFalse();
    }

    /// <summary>
    /// Verifies that AnyFailed returns true when any path failed terminally.
    /// </summary>
    [Test]
    public async Task AnyFailed_WithFailedPath_ReturnsTrue()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var context = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state),
            ForkPathResult<TestWorkflowState>.Failed(1),
        ]);

        // Assert
        await Assert.That(context.AnyFailed).IsTrue();
    }

    /// <summary>
    /// Verifies that AnyFailed returns false when no paths failed terminally.
    /// </summary>
    [Test]
    public async Task AnyFailed_WithNoFailedPaths_ReturnsFalse()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var context = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state),
            ForkPathResult<TestWorkflowState>.FailedWithRecovery(1, state),
        ]);

        // Assert
        await Assert.That(context.AnyFailed).IsFalse();
    }

    /// <summary>
    /// Verifies that AnyRecovered returns true when any path recovered from failure.
    /// </summary>
    [Test]
    public async Task AnyRecovered_WithRecoveredPath_ReturnsTrue()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var context = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state),
            ForkPathResult<TestWorkflowState>.FailedWithRecovery(1, state),
        ]);

        // Assert
        await Assert.That(context.AnyRecovered).IsTrue();
    }

    /// <summary>
    /// Verifies that SuccessfulStates returns states from successful paths.
    /// </summary>
    [Test]
    public async Task SuccessfulStates_ReturnsOnlySuccessfulStates()
    {
        // Arrange
        var state1 = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var state2 = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-2" };
        var context = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state1),
            ForkPathResult<TestWorkflowState>.Failed(1),
            ForkPathResult<TestWorkflowState>.FailedWithRecovery(2, state2),
        ]);

        // Act
        var successfulStates = context.SuccessfulStates.ToList();

        // Assert
        await Assert.That(successfulStates.Count).IsEqualTo(2);
        await Assert.That(successfulStates).Contains(state1);
        await Assert.That(successfulStates).Contains(state2);
    }

    // =============================================================================
    // C. Indexer Tests
    // =============================================================================

    /// <summary>
    /// Verifies that indexer returns correct path result.
    /// </summary>
    [Test]
    public async Task Indexer_WithValidIndex_ReturnsPathResult()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var result = ForkPathResult<TestWorkflowState>.Success(0, state);
        var context = ForkContext<TestWorkflowState>.Create([result]);

        // Act
        var retrieved = context[0];

        // Assert
        await Assert.That(retrieved).IsEqualTo(result);
    }

    /// <summary>
    /// Verifies that indexer throws for invalid index.
    /// </summary>
    [Test]
    public async Task Indexer_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var context = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state),
        ]);

        // Act & Assert
        await Assert.That(() => context[5]).Throws<ArgumentOutOfRangeException>();
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ForkContext is an immutable record.
    /// </summary>
    [Test]
    public async Task ForkContext_IsImmutableRecord()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-1" };
        var original = ForkContext<TestWorkflowState>.Create(
        [
            ForkPathResult<TestWorkflowState>.Success(0, state),
        ]);

        // Assert - record type
        await Assert.That(typeof(ForkContext<TestWorkflowState>).IsSealed).IsTrue();
    }
}
