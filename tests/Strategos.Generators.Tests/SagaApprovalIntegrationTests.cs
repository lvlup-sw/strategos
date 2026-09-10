// -----------------------------------------------------------------------
// <copyright file="SagaApprovalIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters;
using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests verifying the complete approval workflow generation.
/// </summary>
[Property("Category", "Integration")]
public sealed class SagaApprovalIntegrationTests
{
    // =============================================================================
    // A. Full Workflow with Basic Approval Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a workflow with basic approval generates all required commands.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithApproval_GeneratesAllApprovalCommands()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateModelWithApproval(approval);

        // Act
        var result = CommandsEmitter.Emit(model);

        // Assert - Core approval commands should be generated
        // Note: Timeout command is only generated when escalation is configured
        await Assert.That(result).Contains("ResumeManagerReviewApprovalCommand");
        await Assert.That(result).Contains("RequestManagerReviewApprovalEvent");
        await Assert.That(result).Contains("SetManagerReviewPendingApprovalCommand");
    }

    /// <summary>
    /// Verifies that a workflow with approval generates approval phases.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithApproval_GeneratesApprovalPhase()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateModelWithApproval(approval);

        // Act
        var result = PhaseEnumEmitter.Emit(model);

        // Assert
        await Assert.That(result).Contains("AwaitApproval_ManagerReview");
    }

    /// <summary>
    /// Verifies that a workflow with approval generates saga properties.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithApproval_GeneratesSagaProperties()
    {
        // Arrange
        var propertiesEmitter = new SagaPropertiesEmitter();
        var sb = new StringBuilder();
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateModelWithApproval(approval);

        // Act
        propertiesEmitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("PendingApprovalRequestId");
        await Assert.That(result).Contains("ApprovalInstructions");
    }

    /// <summary>
    /// Verifies that a workflow with escalation generates timeout command.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithEscalation_GeneratesTimeoutCommand()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);
        var model = CreateModelWithApproval(approval);

        // Act
        var result = CommandsEmitter.Emit(model);

        // Assert - Timeout command should be generated when escalation is configured
        await Assert.That(result).Contains("ManagerReviewApprovalTimeoutCommand");
    }

    // =============================================================================
    // B. Rejection Path Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a workflow with rejection steps generates rejection step phases.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithRejectionPath_GeneratesRejectionStepPhases()
    {
        // Arrange
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("LogRejection", "TestNamespace.LogRejection"),
            StepModel.Create("NotifyUser", "TestNamespace.NotifyUser"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);
        var model = CreateModelWithApproval(approval);

        // Act
        var result = PhaseEnumEmitter.Emit(model);

        // Assert - Rejection step phases should be generated
        await Assert.That(result).Contains("LogRejection");
        await Assert.That(result).Contains("NotifyUser");
    }

    /// <summary>
    /// Verifies that resume handler transitions to rejection step.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithRejectionPath_ResumeHandlerTransitionsToRejectionStep()
    {
        // Arrange
        var handlersEmitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("LogRejection", "TestNamespace.LogRejection"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);
        var model = CreateModelWithApproval(approval);
        var context = new ApprovalResumeContext(IsLastStep: false, NextStepName: "ProcessOrder");

        // Act
        handlersEmitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert - Should transition to rejection step on rejection
        await Assert.That(result).Contains("StartLogRejectionCommand");
    }

    // =============================================================================
    // C. Escalation Path Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a workflow with escalation steps generates escalation phases.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithEscalation_GeneratesEscalationStepPhases()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);
        var model = CreateModelWithApproval(approval);

        // Act
        var result = PhaseEnumEmitter.Emit(model);

        // Assert - Escalation step phases should be generated
        await Assert.That(result).Contains("NotifyEscalation");
    }

    /// <summary>
    /// Verifies that timeout handler transitions to escalation step.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithEscalation_TimeoutHandlerTransitionsToEscalationStep()
    {
        // Arrange
        var handlersEmitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);
        var model = CreateModelWithApproval(approval);

        // Act
        handlersEmitter.EmitTimeoutHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert - Should transition to escalation step on timeout
        await Assert.That(result).Contains("StartNotifyEscalationCommand");
    }

    // =============================================================================
    // D. Nested Approval Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a workflow with nested approval generates escalated approval phases.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithNestedApproval_GeneratesEscalatedApprovalPhases()
    {
        // Arrange
        var nestedApproval = ApprovalModel.Create(
            approvalPointName: "DirectorReview",
            approverTypeName: "TestNamespace.DirectorApprover",
            precedingStepName: "ManagerReview");
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            nestedEscalationApprovals: [nestedApproval]);
        var model = CreateModelWithApproval(approval);

        // Act
        var result = PhaseEnumEmitter.Emit(model);

        // Assert - Both approval phases should be generated
        await Assert.That(result).Contains("AwaitApproval_ManagerReview");
        await Assert.That(result).Contains("AwaitApproval_DirectorReview");
    }

    /// <summary>
    /// Verifies that nested approval generates its own commands.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithNestedApproval_GeneratesNestedApprovalCommands()
    {
        // Arrange
        var nestedApproval = ApprovalModel.Create(
            approvalPointName: "DirectorReview",
            approverTypeName: "TestNamespace.DirectorApprover",
            precedingStepName: "ManagerReview");
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            nestedEscalationApprovals: [nestedApproval]);
        var model = CreateModelWithApproval(approval);

        // Act
        var result = CommandsEmitter.Emit(model);

        // Assert - Both sets of approval commands should be generated
        await Assert.That(result).Contains("ResumeManagerReviewApprovalCommand");
        await Assert.That(result).Contains("ResumeDirectorReviewApprovalCommand");
        await Assert.That(result).Contains("RequestDirectorReviewApprovalEvent");
    }

    // =============================================================================
    // E. ApprovalIntegrationHandler Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ApprovalIntegrationHandler generates all approval handlers.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithApproval_GeneratesApprovalIntegrationHandler()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateModelWithApproval(approval);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert - Integration handler should be generated
        await Assert.That(result).Contains("TestWorkflowApprovalIntegrationHandler");
        await Assert.That(result).Contains("IHumanApprovalHandler");
        await Assert.That(result).Contains("RequestManagerReviewApprovalEvent");
    }

    // =============================================================================
    // F. Step Completed Handler Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that step completed handler emits approval request event.
    /// </summary>
    [Test]
    public async Task FullWorkflow_WithApproval_StepCompletedEmitsRequestApprovalEvent()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateModelWithApproval(approval);
        var context = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "ProcessOrder",
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: approval,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Act
        emitter.EmitHandler(sb, model, "ValidateOrder", context);
        var result = sb.ToString();

        // Assert - Should emit RequestApprovalEvent
        await Assert.That(result).Contains("RequestManagerReviewApprovalEvent");
        await Assert.That(result).Contains("AwaitApproval_ManagerReview");
    }

    // =============================================================================
    // G. Complete Approval Flow Integration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that all emitters produce consistent output for a complete approval workflow.
    /// </summary>
    [Test]
    public async Task CompleteApprovalFlow_AllEmitters_ProduceConsistentOutput()
    {
        // Arrange
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("LogRejection", "TestNamespace.LogRejection"),
        };
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps,
            escalationSteps: escalationSteps);
        var model = CreateModelWithApproval(approval);

        // Act - Generate with each emitter
        var commandsResult = CommandsEmitter.Emit(model);
        var phasesResult = PhaseEnumEmitter.Emit(model);

        var propertiesSb = new StringBuilder();
        new SagaPropertiesEmitter().Emit(propertiesSb, model);
        var propertiesResult = propertiesSb.ToString();

        var integrationSb = new StringBuilder();
        new ApprovalIntegrationHandlerEmitter().Emit(integrationSb, model);
        var integrationResult = integrationSb.ToString();

        // Assert - All key artifacts should be present
        await Assert.That(commandsResult).Contains("ResumeManagerReviewApprovalCommand");
        await Assert.That(commandsResult).Contains("ApprovalDecision Decision");
        await Assert.That(phasesResult).Contains("AwaitApproval_ManagerReview");
        await Assert.That(phasesResult).Contains("LogRejection");
        await Assert.That(phasesResult).Contains("NotifyEscalation");
        await Assert.That(propertiesResult).Contains("PendingApprovalRequestId");
        await Assert.That(integrationResult).Contains("ScheduleAsync");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateModelWithApproval(ApprovalModel approval)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessOrder"],
            StateTypeName: "TestState",
            Loops: null,
            ApprovalPoints: [approval]);
    }
}
