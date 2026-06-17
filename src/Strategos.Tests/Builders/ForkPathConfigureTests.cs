// =============================================================================
// <copyright file="ForkPathConfigureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Builders;

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for the configure-lambda overload of <see cref="IForkPathBuilder{TState}.Then{TStep}(System.Action{IStepConfiguration{TState}})"/>.
/// </summary>
/// <remarks>
/// Fork paths are the only sequencing context that previously lacked the
/// <c>Then&lt;TStep&gt;(Action&lt;IStepConfiguration&lt;TState&gt;&gt;)</c> overload that the
/// top-level <see cref="IWorkflowBuilder{TState}"/> and loop-body <see cref="ILoopBuilder{TState}"/>
/// builders already expose. These tests assert that a configured fork-path step
/// carries its <see cref="Strategos.Definitions.StepConfigurationDefinition"/> through to
/// the immutable workflow definition.
/// </remarks>
[Property("Category", "Unit")]
public class ForkPathConfigureTests
{
    /// <summary>
    /// Verifies that the configure-lambda overload attaches step configuration
    /// (retry/timeout/compensate) to the fork-path step in the workflow definition.
    /// </summary>
    [Test]
    public async Task ForkPath_ThenWithConfigure_AttachesStepConfiguration()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>()
            .Fork(
                path => path.Then<TestStep2>(step => step
                    .WithRetry(3, TimeSpan.FromSeconds(5))
                    .WithTimeout(TimeSpan.FromMinutes(2))
                    .Compensate<TestRecoveryStep>()),
                path => path.Then<TestStep3>())
            .Join<TestJoinStep>()
            .Finally<TestFinalStep>();

        // Assert - the configured fork-path step carries its configuration
        var configuredStep = workflow.ForkPoints[0].Paths[0].Steps[0];
        await Assert.That(configuredStep.Configuration).IsNotNull();
        await Assert.That(configuredStep.Configuration!.Retry).IsNotNull();
        await Assert.That(configuredStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(configuredStep.Configuration!.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        await Assert.That(configuredStep.Configuration!.Compensation).IsNotNull();
    }

    /// <summary>
    /// Verifies that the configure-lambda overload throws when the configure action is null.
    /// </summary>
    [Test]
    public async Task ForkPath_ThenWithNullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStep1>();

        // Act & Assert
        await Assert.That(() =>
            builder.Fork(
                path => path.Then<TestStep2>((Action<IStepConfiguration<TestWorkflowState>>)null!),
                path => path.Then<TestStep3>()))
            .Throws<ArgumentNullException>();
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
