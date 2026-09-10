// =============================================================================
// <copyright file="LoopBuilderForkTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Builders;
using Strategos.Tests.Fixtures;

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for Fork/Join support inside <see cref="ILoopBuilder{TState}"/>.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Fork with two paths returns ILoopForkJoinBuilder</description></item>
///   <item><description>Fork with less than two paths throws ArgumentException</description></item>
///   <item><description>Fork paths can have multiple steps</description></item>
///   <item><description>Join returns ILoopBuilder for continued chaining</description></item>
///   <item><description>Fork inside loop body creates ForkPoint in workflow definition</description></item>
///   <item><description>Fork steps are marked as loop body steps</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class LoopBuilderForkTests
{
    // =============================================================================
    // A. Fork Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Fork with zero paths throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Fork_WithZeroPaths_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        await Assert.That(() =>
            Workflow<TestWorkflowState>.Create("test-workflow")
                .StartWith<TestStartStep>()
                .RepeatUntil(
                    state => state.QualityScore >= 1.0m,
                    "ProcessLoop",
                    loop => loop.Fork(),
                    maxIterations: 10)
                .Finally<TestFinalStep>())
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Fork with single path throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Fork_WithSinglePath_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        await Assert.That(() =>
            Workflow<TestWorkflowState>.Create("test-workflow")
                .StartWith<TestStartStep>()
                .RepeatUntil(
                    state => state.QualityScore >= 1.0m,
                    "ProcessLoop",
                    loop => loop.Fork(path => path.Then<ParallelStep1>()),
                    maxIterations: 10)
                .Finally<TestFinalStep>())
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Fork with empty path throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Fork_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        await Assert.That(() =>
            Workflow<TestWorkflowState>.Create("test-workflow")
                .StartWith<TestStartStep>()
                .RepeatUntil(
                    state => state.QualityScore >= 1.0m,
                    "ProcessLoop",
                    loop => loop.Fork(
                        path => { }, // Empty path
                        path => path.Then<ParallelStep2>()),
                    maxIterations: 10)
                .Finally<TestFinalStep>())
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // B. Fork Builder Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Fork with two paths returns ILoopForkJoinBuilder.
    /// </summary>
    [Test]
    public async Task Fork_WithTwoPaths_ReturnsILoopForkJoinBuilder()
    {
        // Arrange
        ILoopForkJoinBuilder<TestWorkflowState>? capturedBuilder = null;

        // Act
        Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop =>
                {
                    capturedBuilder = loop.Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>());
                    capturedBuilder.Join<JoinStep>();
                },
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(capturedBuilder).IsNotNull();
        await Assert.That(capturedBuilder).IsTypeOf<ILoopForkJoinBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Fork with three paths returns ILoopForkJoinBuilder.
    /// </summary>
    [Test]
    public async Task Fork_WithThreePaths_ReturnsILoopForkJoinBuilder()
    {
        // Arrange
        ILoopForkJoinBuilder<TestWorkflowState>? capturedBuilder = null;

        // Act
        Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop =>
                {
                    capturedBuilder = loop.Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>(),
                        path => path.Then<ParallelStep3>());
                    capturedBuilder.Join<JoinStep>();
                },
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(capturedBuilder).IsNotNull();
    }

    /// <summary>
    /// Verifies that Fork paths can have multiple steps.
    /// </summary>
    [Test]
    public async Task Fork_PathWithMultipleSteps_AddsAllStepsToPath()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Fork(
                        path => path.Then<ParallelStep1>().Then<ParallelStep2>(),
                        path => path.Then<ParallelStep3>())
                    .Join<JoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints).HasCount().EqualTo(1);
        await Assert.That(workflow.ForkPoints[0].Paths[0].Steps).HasCount().EqualTo(2);
        await Assert.That(workflow.ForkPoints[0].Paths[1].Steps).HasCount().EqualTo(1);
    }

    // =============================================================================
    // C. Join Builder Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Join returns ILoopBuilder for continued chaining.
    /// </summary>
    [Test]
    public async Task Join_AfterFork_ReturnsILoopBuilder()
    {
        // Arrange
        ILoopBuilder<TestWorkflowState>? capturedBuilder = null;

        // Act
        Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop =>
                {
                    capturedBuilder = loop
                        .Fork(
                            path => path.Then<ParallelStep1>(),
                            path => path.Then<ParallelStep2>())
                        .Join<JoinStep>();
                },
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(capturedBuilder).IsNotNull();
        await Assert.That(capturedBuilder).IsTypeOf<ILoopBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that steps can be added after Join.
    /// </summary>
    [Test]
    public async Task Join_ThenAfterJoin_AddsStepToLoopBody()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Then<PreForkStep>()
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>()
                    .Then<PostJoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.Loops).HasCount().EqualTo(1);
        var bodySteps = workflow.Loops[0].BodySteps;

        // Should have: PreForkStep, ParallelStep1, ParallelStep2, JoinStep, PostJoinStep
        await Assert.That(bodySteps).HasCount().EqualTo(5);
        await Assert.That(bodySteps.Last().StepType).IsEqualTo(typeof(PostJoinStep));
    }

    // =============================================================================
    // D. WorkflowDefinition Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Fork inside loop creates ForkPoint in workflow definition.
    /// </summary>
    [Test]
    public async Task Fork_InsideLoop_CreatesForkPointDefinition()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints).IsNotNull();
        await Assert.That(workflow.ForkPoints).HasCount().EqualTo(1);
        await Assert.That(workflow.ForkPoints[0].Paths).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that Fork sets JoinStepId on the ForkPoint.
    /// </summary>
    [Test]
    public async Task Fork_Join_SetsJoinStepId()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints[0].JoinStepId).IsNotNull();
        await Assert.That(workflow.ForkPoints[0].JoinStepId).IsNotEmpty();
    }

    /// <summary>
    /// Verifies that Fork steps are marked as loop body steps.
    /// </summary>
    [Test]
    public async Task Fork_Steps_AreMarkedAsLoopBodySteps()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        var loopId = workflow.Loops[0].LoopId;
        var bodySteps = workflow.Loops[0].BodySteps;

        await Assert.That(bodySteps.All(s => s.IsLoopBodyStep)).IsTrue();
        await Assert.That(bodySteps.All(s => s.ParentLoopId == loopId)).IsTrue();
    }

    /// <summary>
    /// Verifies that Fork join step is marked as loop body step.
    /// </summary>
    [Test]
    public async Task Fork_JoinStep_IsMarkedAsLoopBodyStep()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        var loopId = workflow.Loops[0].LoopId;
        var joinStepId = workflow.ForkPoints[0].JoinStepId;
        var joinStep = workflow.Steps.FirstOrDefault(s => s.StepId == joinStepId);

        await Assert.That(joinStep).IsNotNull();
        await Assert.That(joinStep!.IsLoopBodyStep).IsTrue();
        await Assert.That(joinStep.ParentLoopId).IsEqualTo(loopId);
    }

    // =============================================================================
    // E. Complex Scenarios
    // =============================================================================

    /// <summary>
    /// Verifies that Fork can be preceded by Then steps in loop body.
    /// </summary>
    [Test]
    public async Task Fork_AfterThenStep_WorksCorrectly()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Then<PreForkStep>()
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.Loops[0].BodySteps).HasCount().EqualTo(4);
        await Assert.That(workflow.Loops[0].BodySteps[0].StepType).IsEqualTo(typeof(PreForkStep));
    }

    /// <summary>
    /// Verifies that workflow can have both loop Fork and non-loop Fork.
    /// </summary>
    [Test]
    public async Task Workflow_WithBothLoopForkAndRegularFork_WorksCorrectly()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .Fork(
                path => path.Then<PreLoopParallel1>(),
                path => path.Then<PreLoopParallel2>())
            .Join<PreLoopJoin>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that multiple Forks inside same loop work correctly.
    /// </summary>
    [Test]
    public async Task Loop_WithMultipleForks_CreatesMultipleForkPoints()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<TestStartStep>()
            .RepeatUntil(
                state => state.QualityScore >= 1.0m,
                "ProcessLoop",
                loop => loop
                    .Fork(
                        path => path.Then<ParallelStep1>(),
                        path => path.Then<ParallelStep2>())
                    .Join<JoinStep>()
                    .Then<IntermediateStep>()
                    .Fork(
                        path => path.Then<ParallelStep3>(),
                        path => path.Then<PostJoinStep>())
                    .Join<SecondJoinStep>(),
                maxIterations: 10)
            .Finally<TestFinalStep>();

        // Assert
        await Assert.That(workflow.ForkPoints).HasCount().EqualTo(2);
    }

    // =============================================================================
    // Test Fixtures
    // =============================================================================

    private sealed class TestStartStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class PreForkStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class ParallelStep1 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class ParallelStep2 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class ParallelStep3 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class JoinStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class SecondJoinStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class PostJoinStep : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class IntermediateStep : IWorkflowStep<TestWorkflowState>
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

    private sealed class PreLoopParallel1 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class PreLoopParallel2 : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }

    private sealed class PreLoopJoin : IWorkflowStep<TestWorkflowState>
    {
        public Task<StepResult<TestWorkflowState>> ExecuteAsync(
            TestWorkflowState state,
            StepContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
    }
}
