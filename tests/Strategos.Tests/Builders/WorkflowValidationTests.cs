// =============================================================================
// <copyright file="WorkflowValidationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for workflow validation in <see cref="IWorkflowBuilder{TState}"/>.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Workflow structure validation</description></item>
///   <item><description>Entry step requirement</description></item>
///   <item><description>Transition connectivity</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class WorkflowValidationTests
{
    // =============================================================================
    // A. Entry Step Validation (already covered in WorkflowBuilderTests)
    // =============================================================================

    /// <summary>
    /// Verifies that Finally without StartWith throws InvalidOperationException.
    /// This validates the entry step requirement.
    /// </summary>
    [Test]
    public async Task Build_RequiresEntryStep()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.Finally<CompleteStep>())
            .Throws<InvalidOperationException>();
    }

    // =============================================================================
    // B. Workflow Structure Validation
    // =============================================================================

    /// <summary>
    /// Verifies that a valid linear workflow passes validation.
    /// </summary>
    [Test]
    public async Task Build_ValidLinearWorkflow_Succeeds()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert - Workflow should build successfully with proper structure
        await Assert.That(workflow).IsNotNull();
        await Assert.That(workflow.EntryStep).IsNotNull();
        await Assert.That(workflow.TerminalStep).IsNotNull();
        await Assert.That(workflow.Steps.Count).IsEqualTo(3);
        await Assert.That(workflow.Transitions.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that all steps are reachable from entry step.
    /// </summary>
    [Test]
    public async Task Build_AllStepsReachable_FromEntryStep()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert - All steps should be connected via transitions
        var entryId = workflow.EntryStep!.StepId;
        var terminalId = workflow.TerminalStep!.StepId;

        // First transition: Entry -> Process
        var firstTransition = workflow.Transitions[0];
        await Assert.That(firstTransition.FromStepId).IsEqualTo(entryId);

        // Second transition: Process -> Terminal
        var secondTransition = workflow.Transitions[1];
        await Assert.That(secondTransition.ToStepId).IsEqualTo(terminalId);
    }

    /// <summary>
    /// Verifies that minimal workflow (StartWith + Finally) is valid.
    /// </summary>
    [Test]
    public async Task Build_MinimalWorkflow_Succeeds()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Steps.Count).IsEqualTo(2);
        await Assert.That(workflow.Transitions.Count).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that branching workflow has all paths connected.
    /// </summary>
    [Test]
    public async Task Build_BranchingWorkflow_AllPathsConnected()
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

        // Assert - Both branch paths should have transitions to terminal
        await Assert.That(workflow.BranchPoints.Count).IsEqualTo(1);

        var branchPoint = workflow.BranchPoints[0];
        await Assert.That(branchPoint.Paths.Count).IsEqualTo(2);

        // RejoinStepId should be set (the terminal step in this case)
        await Assert.That(branchPoint.RejoinStepId).IsEqualTo(workflow.TerminalStep!.StepId);
    }

    // =============================================================================
    // C. Step ID Uniqueness
    // =============================================================================

    /// <summary>
    /// Verifies that all step IDs are unique within a workflow.
    /// </summary>
    [Test]
    public async Task Build_StepIds_AreUnique()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Then<NotifyStep>()
            .Finally<CompleteStep>();

        // Assert
        var stepIds = workflow.Steps.Select(s => s.StepId).ToList();
        var uniqueIds = stepIds.Distinct().ToList();

        await Assert.That(uniqueIds.Count).IsEqualTo(stepIds.Count);
    }

    /// <summary>
    /// Verifies that transition IDs are unique.
    /// </summary>
    [Test]
    public async Task Build_TransitionIds_AreUnique()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Then<NotifyStep>()
            .Finally<CompleteStep>();

        // Assert
        var transitionIds = workflow.Transitions.Select(t => t.TransitionId).ToList();
        var uniqueIds = transitionIds.Distinct().ToList();

        await Assert.That(uniqueIds.Count).IsEqualTo(transitionIds.Count);
    }

    // =============================================================================
    // D. Terminal Step Validation
    // =============================================================================

    /// <summary>
    /// Verifies that terminal step is marked as terminal.
    /// </summary>
    [Test]
    public async Task Build_TerminalStep_IsMarkedAsTerminal()
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
    /// Verifies that entry step is not marked as terminal.
    /// </summary>
    [Test]
    public async Task Build_EntryStep_IsNotTerminal()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.EntryStep!.IsTerminal).IsFalse();
    }

    // =============================================================================
    // E. Transition Validation
    // =============================================================================

    /// <summary>
    /// Verifies that transitions reference valid step IDs.
    /// </summary>
    [Test]
    public async Task Build_Transitions_ReferenceValidSteps()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert
        var stepIds = workflow.Steps.Select(s => s.StepId).ToHashSet();

        foreach (var transition in workflow.Transitions)
        {
            await Assert.That(stepIds.Contains(transition.FromStepId)).IsTrue();
            await Assert.That(stepIds.Contains(transition.ToStepId)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that transitions form a proper chain.
    /// </summary>
    [Test]
    public async Task Build_Transitions_FormProperChain()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Then<NotifyStep>()
            .Finally<CompleteStep>();

        // Assert - Verify chain: Entry -> Process -> Notify -> Terminal
        var transitions = workflow.Transitions.ToList();

        // First transition starts from entry
        await Assert.That(transitions[0].FromStepId).IsEqualTo(workflow.EntryStep!.StepId);

        // Last transition ends at terminal
        await Assert.That(transitions[^1].ToStepId).IsEqualTo(workflow.TerminalStep!.StepId);

        // Chain is connected: each ToStepId matches next FromStepId
        for (int i = 0; i < transitions.Count - 1; i++)
        {
            await Assert.That(transitions[i].ToStepId).IsEqualTo(transitions[i + 1].FromStepId);
        }
    }
}
