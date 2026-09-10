// =============================================================================
// <copyright file="LoopBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ILoopBuilder{TState}"/> implementation.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Then adds steps to loop body</description></item>
///   <item><description>Loop body maintains step order</description></item>
///   <item><description>Builder returns self for fluent chaining</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class LoopBuilderTests
{
    // =============================================================================
    // A. Then Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Then returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task Then_WithStepType_ReturnsBuilder()
    {
        // Arrange
        ILoopBuilder<TestWorkflowState>? capturedBuilder = null;

        // Act
        Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop =>
                {
                    capturedBuilder = loop.Then<CritiqueStep>();
                },
                maxIterations: 5)
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(capturedBuilder).IsNotNull();
    }

    /// <summary>
    /// Verifies that Then adds a step to the loop body.
    /// </summary>
    [Test]
    public async Task Then_AddsStepToLoopBody()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop.Then<CritiqueStep>(),
                maxIterations: 5)
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Loops).HasCount().EqualTo(1);
        await Assert.That(workflow.Loops[0].BodySteps).HasCount().EqualTo(1);
        await Assert.That(workflow.Loops[0].BodySteps[0].StepType).IsEqualTo(typeof(CritiqueStep));
    }

    /// <summary>
    /// Verifies that multiple Then calls can be chained.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_CanBeChained()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop
                    .Then<CritiqueStep>()
                    .Then<RefineStep>(),
                maxIterations: 5)
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Loops[0].BodySteps).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that multiple steps in loop have correct order.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_MaintainsOrder()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop
                    .Then<CritiqueStep>()
                    .Then<RefineStep>(),
                maxIterations: 5)
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Loops[0].BodySteps[0].StepType).IsEqualTo(typeof(CritiqueStep));
        await Assert.That(workflow.Loops[0].BodySteps[1].StepType).IsEqualTo(typeof(RefineStep));
    }

    // =============================================================================
    // B. Loop Definition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that loop captures the loop name.
    /// </summary>
    [Test]
    public async Task RepeatUntil_CapturesLoopName()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop.Then<CritiqueStep>(),
                maxIterations: 5)
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Loops[0].LoopName).IsEqualTo("Refinement");
    }

    /// <summary>
    /// Verifies that loop captures max iterations.
    /// </summary>
    [Test]
    public async Task RepeatUntil_CapturesMaxIterations()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop.Then<CritiqueStep>(),
                maxIterations: 7)
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Loops[0].MaxIterations).IsEqualTo(7);
    }

    /// <summary>
    /// Verifies that loop body steps are marked as loop body steps.
    /// </summary>
    [Test]
    public async Task RepeatUntil_BodyStepsAreMarkedAsLoopBodySteps()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop
                    .Then<CritiqueStep>()
                    .Then<RefineStep>(),
                maxIterations: 5)
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Loops[0].BodySteps[0].IsLoopBodyStep).IsTrue();
        await Assert.That(workflow.Loops[0].BodySteps[1].IsLoopBodyStep).IsTrue();
    }

    /// <summary>
    /// Verifies that loop body steps have parent loop ID set.
    /// </summary>
    [Test]
    public async Task RepeatUntil_BodyStepsHaveParentLoopId()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop.Then<CritiqueStep>(),
                maxIterations: 5)
            .Finally<CompleteStep>();

        // Assert
        var loopId = workflow.Loops[0].LoopId;
        await Assert.That(workflow.Loops[0].BodySteps[0].ParentLoopId).IsEqualTo(loopId);
    }
}
