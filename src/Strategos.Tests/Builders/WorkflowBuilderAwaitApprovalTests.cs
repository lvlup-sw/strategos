// =============================================================================
// <copyright file="WorkflowBuilderAwaitApprovalTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IWorkflowBuilder{TState}.AwaitApproval{TApprover}"/> method.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>AwaitApproval adds approval point to workflow</description></item>
///   <item><description>Approval point captures approver type</description></item>
///   <item><description>Approval point captures preceding step</description></item>
///   <item><description>AwaitApproval returns builder for fluent chaining</description></item>
///   <item><description>Multiple approval points can be added</description></item>
///   <item><description>Approval configuration is captured correctly</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class WorkflowBuilderAwaitApprovalTests
{
    // =============================================================================
    // Test Marker Types
    // =============================================================================

    /// <summary>
    /// Marker type for manager approver role.
    /// </summary>
    private sealed class ManagerApprover;

    /// <summary>
    /// Marker type for director approver role.
    /// </summary>
    private sealed class DirectorApprover;

    // =============================================================================
    // A. Basic AwaitApproval Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AwaitApproval returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task AwaitApproval_ReturnsBuilder()
    {
        // Act
        var result = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve"));

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that AwaitApproval adds an approval point to the workflow.
    /// </summary>
    [Test]
    public async Task AwaitApproval_AddsApprovalPointToWorkflow()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve"))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that approval point captures the correct approver type.
    /// </summary>
    [Test]
    public async Task AwaitApproval_CapturesApproverType()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve"))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints[0].ApproverType).IsEqualTo(typeof(ManagerApprover));
    }

    /// <summary>
    /// Verifies that approval point captures the preceding step ID.
    /// </summary>
    [Test]
    public async Task AwaitApproval_CapturesPrecedingStep()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve"))
            .Finally<CompleteStep>();

        // Assert
        var validateStepId = workflow.Steps[0].StepId;
        await Assert.That(workflow.ApprovalPoints[0].PrecedingStepId).IsEqualTo(validateStepId);
    }

    /// <summary>
    /// Verifies that AwaitApproval with null configure throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task AwaitApproval_WithNullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act & Assert
        await Assert.That(() => builder.AwaitApproval<ManagerApprover>(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AwaitApproval before StartWith throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task AwaitApproval_BeforeStartWith_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.That(() =>
        {
            Workflow<TestWorkflowState>
                .Create("test-workflow")
                .AwaitApproval<ManagerApprover>(approval => approval.WithContext("test"));
        }).Throws<InvalidOperationException>();
    }

    // =============================================================================
    // B. Multiple Approval Points Tests
    // =============================================================================

    /// <summary>
    /// Verifies that multiple approval points can be added to a workflow.
    /// </summary>
    [Test]
    public async Task AwaitApproval_MultipleCalls_AddsMultipleApprovalPoints()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Manager review"))
            .Then<ProcessStep>()
            .AwaitApproval<DirectorApprover>(approval => approval
                .WithContext("Director review"))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that multiple approval points have correct preceding steps.
    /// </summary>
    [Test]
    public async Task AwaitApproval_MultipleCalls_HasCorrectPrecedingSteps()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Manager review"))
            .Then<ProcessStep>()
            .AwaitApproval<DirectorApprover>(approval => approval
                .WithContext("Director review"))
            .Finally<CompleteStep>();

        // Assert
        var validateStepId = workflow.Steps[0].StepId;
        var processStepId = workflow.Steps[1].StepId;

        await Assert.That(workflow.ApprovalPoints[0].PrecedingStepId).IsEqualTo(validateStepId);
        await Assert.That(workflow.ApprovalPoints[1].PrecedingStepId).IsEqualTo(processStepId);
    }

    // =============================================================================
    // C. Configuration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that approval configuration captures static context.
    /// </summary>
    [Test]
    public async Task AwaitApproval_WithContext_CapturesStaticContext()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please review this request"))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints[0].Configuration.StaticContext)
            .IsEqualTo("Please review this request");
    }

    /// <summary>
    /// Verifies that approval configuration captures timeout.
    /// </summary>
    [Test]
    public async Task AwaitApproval_WithTimeout_CapturesTimeout()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve")
                .WithTimeout(TimeSpan.FromHours(4)))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints[0].Configuration.Timeout)
            .IsEqualTo(TimeSpan.FromHours(4));
    }

    /// <summary>
    /// Verifies that approval configuration captures options.
    /// </summary>
    [Test]
    public async Task AwaitApproval_WithOptions_CapturesOptions()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve")
                .WithOption("approve", "Approve", "Approve this request", isDefault: true)
                .WithOption("reject", "Reject", "Reject this request"))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints[0].Configuration.Options).Count().IsEqualTo(2);
        await Assert.That(workflow.ApprovalPoints[0].Configuration.Options[0].OptionId).IsEqualTo("approve");
        await Assert.That(workflow.ApprovalPoints[0].Configuration.Options[1].OptionId).IsEqualTo("reject");
    }

    /// <summary>
    /// Verifies that approval configuration captures escalation handler.
    /// </summary>
    [Test]
    public async Task AwaitApproval_OnTimeout_CapturesEscalationHandler()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve")
                .OnTimeout(escalation => escalation.Complete()))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints[0].EscalationHandler).IsNotNull();
        await Assert.That(workflow.ApprovalPoints[0].EscalationHandler!.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that approval configuration captures rejection handler.
    /// </summary>
    [Test]
    public async Task AwaitApproval_OnRejection_CapturesRejectionHandler()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve")
                .OnRejection(rejection => rejection
                    .Then<NotifyAdminStep>()
                    .Complete()))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.ApprovalPoints[0].RejectionHandler).IsNotNull();
        await Assert.That(workflow.ApprovalPoints[0].RejectionHandler!.Steps).Count().IsEqualTo(1);
    }

    // =============================================================================
    // D. Chaining Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Then can be called after AwaitApproval.
    /// </summary>
    [Test]
    public async Task Then_AfterAwaitApproval_Works()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve"))
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Steps).Count().IsEqualTo(3);
        await Assert.That(workflow.Steps[1].StepType).IsEqualTo(typeof(ProcessStep));
    }

    /// <summary>
    /// Verifies that Branch can be called after AwaitApproval.
    /// </summary>
    [Test]
    public async Task Branch_AfterAwaitApproval_Works()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .AwaitApproval<ManagerApprover>(approval => approval
                .WithContext("Please approve"))
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()),
                BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                    path => path.Then<ManualProcessStep>()))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.BranchPoints).Count().IsEqualTo(1);
    }
}
