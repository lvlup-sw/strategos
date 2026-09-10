// =============================================================================
// <copyright file="ForkBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Builders;

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IWorkflowBuilder{TState}.Fork"/> and <see cref="ForkPathBuilder{TState}"/>.
/// </summary>
[Property("Category", "Unit")]
public class ForkBuilderTests
{
    // =============================================================================
    // A. Fork Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Fork throws before StartWith is called.
    /// </summary>
    [Test]
    public async Task Fork_BeforeStartWith_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() =>
            builder.Fork(
                path => path.Then<TestStep1>(),
                path => path.Then<TestStep2>()))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that Fork throws for zero paths.
    /// </summary>
    [Test]
    public async Task Fork_WithZeroPaths_ThrowsArgumentException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>();

        // Act & Assert
        await Assert.That(() =>
            builder.Fork())
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Fork throws for single path.
    /// </summary>
    [Test]
    public async Task Fork_WithSinglePath_ThrowsArgumentException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>();

        // Act & Assert
        await Assert.That(() =>
            builder.Fork(path => path.Then<TestStep2>()))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Fork with two paths returns IForkJoinBuilder.
    /// </summary>
    [Test]
    public async Task Fork_WithTwoPaths_ReturnsIForkJoinBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>();

        // Act
        var forkJoinBuilder = builder.Fork(
            path => path.Then<TestStep2>(),
            path => path.Then<TestStep3>());

        // Assert
        await Assert.That(forkJoinBuilder).IsNotNull();
        await Assert.That(forkJoinBuilder).IsTypeOf<IForkJoinBuilder<TestWorkflowState>>();
    }

    // =============================================================================
    // B. ForkPath Builder Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Then adds steps to the fork path.
    /// </summary>
    [Test]
    public async Task ForkPath_Then_AddsStepToPath()
    {
        // Arrange
        var stepCount = 0;

        // Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path =>
                {
                    path.Then<TestStep2>().Then<TestStep3>();
                    stepCount = 2; // Two steps added
                },
                path => path.Then<TestStep4>())
            .Join<TestJoinStep>()
            .Finally<TestFinalStep>();

        // Assert - the workflow should have all steps
        await Assert.That(workflow.Steps.Count).IsGreaterThanOrEqualTo(5);
    }

    /// <summary>
    /// Verifies that OnFailure sets the failure handler on the path.
    /// </summary>
    [Test]
    public async Task ForkPath_OnFailure_SetsFailureHandler()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path
                    .Then<TestStep2>()
                    .OnFailure(f => f.Then<TestRecoveryStep>()),
                path => path.Then<TestStep3>())
            .Join<TestJoinStep>()
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints.Count).IsEqualTo(1);
        var forkPoint = workflow.ForkPoints[0];
        await Assert.That(forkPoint.Paths[0].FailureHandler).IsNotNull();
    }

    /// <summary>
    /// Verifies that OnFailure with Complete marks the handler as terminal.
    /// </summary>
    [Test]
    public async Task ForkPath_OnFailure_Complete_MarksTerminal()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path
                    .Then<TestStep2>()
                    .OnFailure(f => f
                        .Then<TestRecoveryStep>()
                        .Complete()),
                path => path.Then<TestStep3>())
            .Join<TestJoinStep>()
            .Finally<TestFinalStep>();

        // Assert
        var forkPoint = workflow.ForkPoints[0];
        await Assert.That(forkPoint.Paths[0].FailureHandler!.IsTerminal).IsTrue();
    }

    // =============================================================================
    // C. Join Builder Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Join returns the workflow builder for chaining.
    /// </summary>
    [Test]
    public async Task Join_AfterFork_ReturnsWorkflowBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path.Then<TestStep2>(),
                path => path.Then<TestStep3>());

        // Act
        var workflowBuilder = builder.Join<TestJoinStep>();

        // Assert
        await Assert.That(workflowBuilder).IsNotNull();
        await Assert.That(workflowBuilder).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Join sets the join step ID on the fork point.
    /// </summary>
    [Test]
    public async Task Join_SetsJoinStepId()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path.Then<TestStep2>(),
                path => path.Then<TestStep3>())
            .Join<TestJoinStep>()
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints.Count).IsEqualTo(1);
        await Assert.That(workflow.ForkPoints[0].JoinStepId).IsNotNull();
    }

    // =============================================================================
    // D. WorkflowDefinition Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that workflow definition contains the fork point definition.
    /// </summary>
    [Test]
    public async Task WorkflowDefinition_ForkPoints_ContainsForkDefinition()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path.Then<TestStep2>(),
                path => path.Then<TestStep3>())
            .Join<TestJoinStep>()
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints).IsNotNull();
        await Assert.That(workflow.ForkPoints.Count).IsEqualTo(1);
        await Assert.That(workflow.ForkPoints[0].Paths.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that fork paths preserve their step order.
    /// </summary>
    [Test]
    public async Task Fork_MultiplePaths_PreservesStepOrder()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path.Then<TestStep2>().Then<TestStep3>(),
                path => path.Then<TestStep4>().Then<TestStep5>())
            .Join<TestJoinStep>()
            .Finally<TestFinalStep>();

        // Assert
        var forkPoint = workflow.ForkPoints[0];
        await Assert.That(forkPoint.Paths[0].Steps.Count).IsEqualTo(2);
        await Assert.That(forkPoint.Paths[1].Steps.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that workflow can chain steps after Join.
    /// </summary>
    [Test]
    public async Task Fork_Join_Then_ChainsAfterJoin()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path.Then<TestStep2>(),
                path => path.Then<TestStep3>())
            .Join<TestJoinStep>()
            .Then<TestStep4>()
            .Finally<TestFinalStep>();

        // Assert - Check that all steps are present
        await Assert.That(workflow.Steps.Count).IsGreaterThanOrEqualTo(6);
    }

    // =============================================================================
    // Test Fixtures
    // =============================================================================

    private sealed class TestStep1 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class TestStep2 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class TestStep3 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class TestStep4 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class TestStep5 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class TestJoinStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class TestFinalStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class TestRecoveryStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }
}
