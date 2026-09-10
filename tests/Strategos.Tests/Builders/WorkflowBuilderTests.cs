// =============================================================================
// <copyright file="WorkflowBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="WorkflowBuilder{TState}"/> fluent DSL methods.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>StartWith sets entry step correctly</description></item>
///   <item><description>Then adds sequential steps with transitions</description></item>
///   <item><description>Finally completes the workflow definition</description></item>
///   <item><description>Guard clauses enforce correct method call order</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class WorkflowBuilderTests
{
    // =============================================================================
    // A. StartWith Tests (Iteration 7)
    // =============================================================================

    /// <summary>
    /// Verifies that StartWith returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task StartWith_WithStepType_ReturnsBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act
        var result = builder.StartWith<ValidateStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that StartWith called twice throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task StartWith_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder
            .StartWith<ValidateStep>()
            .StartWith<ProcessStep>())
            .Throws<InvalidOperationException>();
    }

    // =============================================================================
    // B. Then Tests (Iteration 8)
    // =============================================================================

    /// <summary>
    /// Verifies that Then after StartWith returns the builder for chaining.
    /// </summary>
    [Test]
    public async Task Then_AfterStartWith_ReturnsBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act
        var result = builder.Then<ProcessStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Then before StartWith throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task Then_BeforeStartWith_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.Then<ProcessStep>())
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that multiple Then calls can be chained.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_CanBeChained()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act
        var result = builder
            .Then<ProcessStep>()
            .Then<NotifyStep>();

        // Assert
        await Assert.That(result).IsNotNull();
    }

    // =============================================================================
    // C. Finally Tests (Iteration 9)
    // =============================================================================

    /// <summary>
    /// Verifies that Finally returns a WorkflowDefinition.
    /// </summary>
    [Test]
    public async Task Finally_ReturnsWorkflowDefinition()
    {
        // Arrange & Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow).IsNotNull();
        await Assert.That(workflow).IsTypeOf<WorkflowDefinition<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Finally sets the workflow name correctly.
    /// </summary>
    [Test]
    public async Task Finally_SetsWorkflowName()
    {
        // Arrange
        const string workflowName = "my-test-workflow";

        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create(workflowName)
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Name).IsEqualTo(workflowName);
    }

    /// <summary>
    /// Verifies that Finally sets the terminal step.
    /// </summary>
    [Test]
    public async Task Finally_SetsTerminalStep()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.TerminalStep).IsNotNull();
        await Assert.That(workflow.TerminalStep!.StepType).IsEqualTo(typeof(CompleteStep));
    }

    /// <summary>
    /// Verifies that Finally marks the terminal step as terminal.
    /// </summary>
    [Test]
    public async Task Finally_MarksStepAsTerminal()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.TerminalStep!.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Finally sets the entry step.
    /// </summary>
    [Test]
    public async Task Finally_SetsEntryStep()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.EntryStep).IsNotNull();
        await Assert.That(workflow.EntryStep!.StepType).IsEqualTo(typeof(ValidateStep));
    }

    /// <summary>
    /// Verifies that Finally includes all steps in the definition.
    /// </summary>
    [Test]
    public async Task Finally_IncludesAllSteps()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert - 3 steps: Validate, Process, Complete
        await Assert.That(workflow.Steps).HasCount().EqualTo(3);
    }

    /// <summary>
    /// Verifies that Finally includes all transitions.
    /// </summary>
    [Test]
    public async Task Finally_IncludesAllTransitions()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert - 2 transitions: Validate->Process, Process->Complete
        await Assert.That(workflow.Transitions).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that Finally without StartWith throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task Finally_WithoutStartWith_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.Finally<CompleteStep>())
            .Throws<InvalidOperationException>();
    }

    // =============================================================================
    // D. Branch Tests (Iteration 10)
    // =============================================================================

    /// <summary>
    /// Verifies that Branch returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task Branch_WithCases_ReturnsBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act
        var result = builder.Branch(
            state => state.ProcessingMode,
            BranchCase<TestWorkflowState, ProcessingMode>.When(
                ProcessingMode.Auto,
                path => path.Then<AutoProcessStep>()),
            BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                path => path.Then<ManualProcessStep>()));

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Branch before StartWith throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task Branch_BeforeStartWith_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.Branch(
            state => state.ProcessingMode,
            BranchCase<TestWorkflowState, ProcessingMode>.When(
                ProcessingMode.Auto,
                path => path.Then<AutoProcessStep>())))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that Branch with null discriminator throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Branch_WithNullDiscriminator_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act & Assert
        await Assert.That(() => builder.Branch<ProcessingMode>(
            null!,
            BranchCase<TestWorkflowState, ProcessingMode>.When(
                ProcessingMode.Auto,
                path => path.Then<AutoProcessStep>())))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Branch with empty cases throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Branch_WithNoCases_ThrowsArgumentException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act & Assert
        await Assert.That(() => builder.Branch<ProcessingMode>(
            state => state.ProcessingMode))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that workflow with branch includes branch point in definition.
    /// </summary>
    [Test]
    public async Task Branch_CreatesBranchPointInDefinition()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()),
                BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                    path => path.Then<ManualProcessStep>()))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.BranchPoints).HasCount().EqualTo(1);
    }

    /// <summary>
    /// Verifies that branch point has correct number of paths.
    /// </summary>
    [Test]
    public async Task Branch_CreatesCorrectNumberOfPaths()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()),
                BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                    path => path.Then<ManualProcessStep>()))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.BranchPoints[0].Paths).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that branch paths include their steps.
    /// </summary>
    [Test]
    public async Task Branch_PathsIncludeSteps()
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
        await Assert.That(branchPath.Steps).HasCount().EqualTo(1);
        await Assert.That(branchPath.Steps[0].StepType).IsEqualTo(typeof(AutoProcessStep));
    }

    /// <summary>
    /// Verifies that workflow includes all steps from branches.
    /// </summary>
    [Test]
    public async Task Branch_AllStepsIncludedInWorkflow()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()),
                BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                    path => path.Then<ManualProcessStep>()))
            .Finally<CompleteStep>();

        // Assert - 4 steps: Validate, AutoProcess, ManualProcess, Complete
        await Assert.That(workflow.Steps).HasCount().EqualTo(4);
    }

    /// <summary>
    /// Verifies that Branch followed by Then continues after rejoin.
    /// </summary>
    [Test]
    public async Task Branch_FollowedByThen_ContinuesAfterRejoin()
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
            .Then<NotifyStep>()
            .Finally<CompleteStep>();

        // Assert - Branch should rejoin at NotifyStep
        // Steps: Validate, AutoProcess, Notify, Complete
        await Assert.That(workflow.Steps).HasCount().EqualTo(4);
    }

    /// <summary>
    /// Verifies that branch point tracks rejoin step.
    /// </summary>
    [Test]
    public async Task Branch_TracksRejoinStep()
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
            .Then<NotifyStep>()
            .Finally<CompleteStep>();

        // Assert - RejoinStepId should point to NotifyStep
        await Assert.That(workflow.BranchPoints[0].RejoinStepId).IsNotNull();
    }
}
