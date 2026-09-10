// -----------------------------------------------------------------------
// <copyright file="SagaApprovalTimeoutHandlerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the timeout handler generation in <see cref="SagaApprovalHandlersEmitter"/>.
/// </summary>
[Property("Category", "Unit")]
public class SagaApprovalTimeoutHandlerTests
{
    // =============================================================================
    // A. Race Condition Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that timeout handler checks if approval was already received.
    /// </summary>
    [Test]
    public async Task EmitTimeoutHandler_ChecksApprovalAlreadyReceived()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);

        // Act
        emitter.EmitTimeoutHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert - Should check PendingApprovalRequestId for race condition
        await Assert.That(result).Contains("PendingApprovalRequestId != cmd.ApprovalRequestId");
    }

    // =============================================================================
    // B. Escalation Step Transition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that timeout handler transitions to first escalation step when configured.
    /// </summary>
    [Test]
    public async Task EmitTimeoutHandler_WithEscalationSteps_TransitionsToFirstEscalationStep()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
            StepModel.Create("LogTimeout", "TestNamespace.LogTimeout"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);

        // Act
        emitter.EmitTimeoutHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert - Should transition to first escalation step
        await Assert.That(result).Contains("StartNotifyEscalationCommand");
    }

    // =============================================================================
    // C. Nested Approval Transition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that timeout handler transitions to nested approval phase.
    /// </summary>
    [Test]
    public async Task EmitTimeoutHandler_WithNestedApproval_TransitionsToEscalationApprovalPhase()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var nestedApproval = ApprovalModel.Create(
            approvalPointName: "DirectorReview",
            approverTypeName: "TestNamespace.DirectorApprover",
            precedingStepName: "ManagerReview");
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            nestedEscalationApprovals: [nestedApproval]);

        // Act
        emitter.EmitTimeoutHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert - Should transition to nested approval phase
        await Assert.That(result).Contains("AwaitApproval_DirectorReview");
    }

    // =============================================================================
    // D. Terminal Escalation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that terminal escalation fails the workflow.
    /// </summary>
    [Test]
    public async Task EmitTimeoutHandler_Terminal_FailsWorkflow()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            isEscalationTerminal: true);

        // Act
        emitter.EmitTimeoutHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert - Should transition to Failed
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.Failed;");
        await Assert.That(result).Contains("MarkCompleted();");
    }

    /// <summary>
    /// Verifies that timeout handler clears pending request ID.
    /// </summary>
    [Test]
    public async Task EmitTimeoutHandler_ClearsPendingApprovalRequestId()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.NotifyEscalation"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);

        // Act
        emitter.EmitTimeoutHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("PendingApprovalRequestId = null;");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateMinimalModel()
    {
        return WorkflowModel.Create(
            workflowName: "Test Workflow",
            pascalName: "TestWorkflow",
            @namespace: "TestNamespace",
            stepNames: ["ValidateOrder", "ProcessOrder", "FinalizeOrder"],
            stateTypeName: "TestState");
    }
}
