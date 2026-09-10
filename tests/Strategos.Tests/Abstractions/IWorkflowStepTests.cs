// =============================================================================
// <copyright file="IWorkflowStepTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Steps;

namespace Strategos.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="IWorkflowStep{TState}"/>.
/// </summary>
[Property("Category", "Unit")]
public class IWorkflowStepTests
{
    /// <summary>
    /// Verifies that IWorkflowStep.ExecuteAsync returns StepResult.
    /// </summary>
    [Test]
    public async Task IWorkflowStep_ExecuteAsync_ReturnsStepResult()
    {
        // Arrange
        var step = new TestWorkflowStep();
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), TestData = "initial" };
        var context = StepContext.Create(state.WorkflowId, "TestStep", "Testing");

        // Act
        var result = await step.ExecuteAsync(state, context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.UpdatedState).IsNotNull();
        await Assert.That(result.UpdatedState.TestData).IsEqualTo("initial-processed");
    }

    /// <summary>
    /// Verifies that IWorkflowStep implementation receives correct context.
    /// </summary>
    [Test]
    public async Task IWorkflowStep_ReceivesCorrectContext()
    {
        // Arrange
        var capturedContext = Substitute.For<IContextCapture>();
        var step = new ContextCapturingStep(capturedContext);
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid() };
        var context = StepContext.Create(state.WorkflowId, "CapturingStep", "Executing");

        // Act
        await step.ExecuteAsync(state, context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        capturedContext.Received(1).Capture(Arg.Is<StepContext>(c =>
            c.WorkflowId == state.WorkflowId &&
            c.StepName == "CapturingStep"));
    }

    /// <summary>
    /// Verifies that IWorkflowStep implementation respects cancellation.
    /// </summary>
    [Test]
    public async Task IWorkflowStep_RespectsCancellation()
    {
        // Arrange
        var step = new CancellableStep();
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid() };
        var context = StepContext.Create(state.WorkflowId, "CancellableStep", "Testing");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        // Act & Assert
        await Assert.That(async () => await step.ExecuteAsync(state, context, cts.Token).ConfigureAwait(false))
            .Throws<OperationCanceledException>();
    }

    /// <summary>
    /// Verifies that IWorkflowStep can return result with confidence.
    /// </summary>
    [Test]
    public async Task IWorkflowStep_CanReturnResultWithConfidence()
    {
        // Arrange
        var step = new ConfidenceReturningStep();
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid() };
        var context = StepContext.Create(state.WorkflowId, "ConfidenceStep", "Analyzing");

        // Act
        var result = await step.ExecuteAsync(state, context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(result.Confidence).IsEqualTo(0.85);
    }
}

/// <summary>
/// Test step implementation for basic execution testing.
/// </summary>
internal sealed class TestWorkflowStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var updatedState = state with { TestData = $"{state.TestData}-processed" };
        return Task.FromResult(StepResult<TestWorkflowState>.FromState(updatedState));
    }
}

/// <summary>
/// Interface for capturing context in tests.
/// </summary>
public interface IContextCapture
{
    void Capture(StepContext context);
}

/// <summary>
/// Step that captures context for verification.
/// </summary>
internal sealed class ContextCapturingStep(IContextCapture capture) : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        capture.Capture(context);
        return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }
}

/// <summary>
/// Step that respects cancellation token.
/// </summary>
internal sealed class CancellableStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }
}

/// <summary>
/// Step that returns result with confidence score.
/// </summary>
internal sealed class ConfidenceReturningStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(StepResult<TestWorkflowState>.WithConfidence(state, 0.85));
    }
}
