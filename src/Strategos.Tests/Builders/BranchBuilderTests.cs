// =============================================================================
// <copyright file="BranchBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IBranchBuilder{TState}"/> implementation.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Then adds steps to branch path</description></item>
///   <item><description>Complete marks branch as terminal</description></item>
///   <item><description>Branch paths are correctly built</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class BranchBuilderTests
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
        IBranchBuilder<TestWorkflowState>? capturedBuilder = null;

        // Act
        Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path =>
                    {
                        capturedBuilder = path.Then<AutoProcessStep>();
                    }))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(capturedBuilder).IsNotNull();
    }

    /// <summary>
    /// Verifies that Then adds steps to the branch path.
    /// </summary>
    [Test]
    public async Task Then_AddsStepToBranchPath()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()))
            .Finally<CompleteStep>();

        // Assert
        var branchPath = workflow.BranchPoints[0].Paths[0];
        await Assert.That(branchPath.Steps).Count().IsEqualTo(1);
        await Assert.That(branchPath.Steps[0].StepType).IsEqualTo(typeof(AutoProcessStep));
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
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path
                        .Then<AutoProcessStep>()
                        .Then<NotifyStep>()))
            .Finally<CompleteStep>();

        // Assert
        var branchPath = workflow.BranchPoints[0].Paths[0];
        await Assert.That(branchPath.Steps).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that multiple steps in branch have correct order.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_MaintainsOrder()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path
                        .Then<AutoProcessStep>()
                        .Then<NotifyStep>()))
            .Finally<CompleteStep>();

        // Assert
        var branchPath = workflow.BranchPoints[0].Paths[0];
        await Assert.That(branchPath.Steps[0].StepType).IsEqualTo(typeof(AutoProcessStep));
        await Assert.That(branchPath.Steps[1].StepType).IsEqualTo(typeof(NotifyStep));
    }

    // =============================================================================
    // B. Complete Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Complete marks the branch as terminal.
    /// </summary>
    [Test]
    public async Task Complete_MarksBranchAsTerminal()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path =>
                    {
                        path.Then<AutoProcessStep>();
                        path.Complete();
                    }))
            .Finally<CompleteStep>();

        // Assert
        var branchPath = workflow.BranchPoints[0].Paths[0];
        await Assert.That(branchPath.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that non-complete branch is not marked as terminal.
    /// </summary>
    [Test]
    public async Task NonCompleteBranch_IsNotTerminal()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()))
            .Finally<CompleteStep>();

        // Assert
        var branchPath = workflow.BranchPoints[0].Paths[0];
        await Assert.That(branchPath.IsTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that terminal branch does not set rejoin step.
    /// </summary>
    [Test]
    public async Task Complete_TerminalBranchDoesNotRejoin()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path =>
                    {
                        path.Then<AutoProcessStep>();
                        path.Complete();
                    }))
            .Finally<CompleteStep>();

        // Assert - Terminal branch should not contribute to rejoin
        // With only terminal branches, there should be no rejoin
        await Assert.That(workflow.BranchPoints[0].RejoinStepId).IsNull();
    }

    /// <summary>
    /// Verifies that mixed terminal and non-terminal branches work correctly.
    /// </summary>
    [Test]
    public async Task MixedBranches_TerminalAndNonTerminal()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path =>
                    {
                        path.Then<AutoProcessStep>();
                        path.Complete();
                    }),
                BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                    path => path.Then<ManualProcessStep>()))
            .Then<NotifyStep>()
            .Finally<CompleteStep>();

        // Assert
        var branchPoint = workflow.BranchPoints[0];

        // First path is terminal
        await Assert.That(branchPoint.Paths[0].IsTerminal).IsTrue();

        // Second path is not terminal
        await Assert.That(branchPoint.Paths[1].IsTerminal).IsFalse();

        // Branch point should have rejoin (from non-terminal path)
        await Assert.That(branchPoint.RejoinStepId).IsNotNull();
    }

    // =============================================================================
    // C. BranchCase Tests
    // =============================================================================

    /// <summary>
    /// Verifies that When creates a non-default branch case.
    /// </summary>
    [Test]
    public async Task When_CreatesNonDefaultCase()
    {
        // Act
        var branchCase = BranchCase<TestWorkflowState, ProcessingMode>.When(
            ProcessingMode.Auto,
            _ => { });

        // Assert
        await Assert.That(branchCase.IsDefault).IsFalse();
        await Assert.That(branchCase.Value).IsEqualTo(ProcessingMode.Auto);
    }

    /// <summary>
    /// Verifies that Otherwise creates a default branch case.
    /// </summary>
    [Test]
    public async Task Otherwise_CreatesDefaultCase()
    {
        // Act
        var branchCase = BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(_ => { });

        // Assert
        await Assert.That(branchCase.IsDefault).IsTrue();
    }

    /// <summary>
    /// Verifies that When with null path builder throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task When_WithNullPathBuilder_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => BranchCase<TestWorkflowState, ProcessingMode>.When(
            ProcessingMode.Auto,
            null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Otherwise with null path builder throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Otherwise_WithNullPathBuilder_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that condition description is set correctly for When.
    /// </summary>
    [Test]
    public async Task When_SetsConditionDescription()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()))
            .Finally<CompleteStep>();

        // Assert
        var branchPath = workflow.BranchPoints[0].Paths[0];
        await Assert.That(branchPath.ConditionDescription).IsEqualTo("When Auto");
    }

    /// <summary>
    /// Verifies that condition description is set correctly for Otherwise.
    /// </summary>
    [Test]
    public async Task Otherwise_SetsConditionDescription()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                    path => path.Then<ManualProcessStep>()))
            .Finally<CompleteStep>();

        // Assert
        var branchPath = workflow.BranchPoints[0].Paths[0];
        await Assert.That(branchPath.ConditionDescription).IsEqualTo("Otherwise");
    }
}
