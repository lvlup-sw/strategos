// -----------------------------------------------------------------------
// <copyright file="ApprovalIntegrationHandlerEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="ApprovalIntegrationHandlerEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class ApprovalIntegrationHandlerEmitterTests
{
    // =============================================================================
    // A. Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task Emit_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.Emit(null!, model))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Emit throws for null model.
    /// </summary>
    [Test]
    public async Task Emit_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();

        // Act & Assert
        await Assert.That(() => emitter.Emit(sb, null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Class Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates handler class with correct name.
    /// </summary>
    [Test]
    public async Task Emit_GeneratesHandlerClass()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public sealed class TestWorkflowApprovalIntegrationHandler");
    }

    // =============================================================================
    // C. Dependency Injection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit injects IHumanApprovalHandler.
    /// </summary>
    [Test]
    public async Task Emit_InjectsIHumanApprovalHandler()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("IHumanApprovalHandler approvalHandler");
    }

    /// <summary>
    /// Verifies that Emit injects ILogger.
    /// </summary>
    [Test]
    public async Task Emit_InjectsILogger()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ILogger<TestWorkflowApprovalIntegrationHandler> logger");
    }

    // =============================================================================
    // D. Handler Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates handler for request approval event.
    /// </summary>
    [Test]
    public async Task Emit_GeneratesHandlerForRequestApprovalEvent()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("RequestManagerReviewApprovalEvent evt");
    }

    /// <summary>
    /// Verifies that Emit injects IMessageContext.
    /// </summary>
    [Test]
    public async Task EmitHandleAsync_InjectsIMessageContext()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("IMessageContext context");
    }

    /// <summary>
    /// Verifies that Emit creates ApprovalRequest.
    /// </summary>
    [Test]
    public async Task EmitHandleAsync_CreatesApprovalRequest()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("new ApprovalRequest");
    }

    /// <summary>
    /// Verifies that Emit schedules timeout command.
    /// </summary>
    [Test]
    public async Task EmitHandleAsync_SchedulesTimeoutCommand()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("context.ScheduleAsync(");
        await Assert.That(result).Contains("ManagerReviewApprovalTimeoutCommand");
    }

    /// <summary>
    /// Verifies that Emit returns SetPendingApprovalCommand.
    /// </summary>
    [Test]
    public async Task EmitHandleAsync_ReturnsSetPendingApprovalCommand()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("SetManagerReviewPendingApprovalCommand");
    }

    /// <summary>
    /// Verifies that Emit handles failure by returning rejected resume command.
    /// </summary>
    [Test]
    public async Task EmitHandleAsync_OnFailure_ReturnsRejectedResumeCommand()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ResumeManagerReviewApprovalCommand");
        await Assert.That(result).Contains("ApprovalDecision.Rejected");
    }

    // =============================================================================
    // E. No Approval Points Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates nothing when no approval points exist.
    /// </summary>
    [Test]
    public async Task Emit_NoApprovalPoints_GeneratesNothing()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).IsEmpty();
    }

    // =============================================================================
    // F. Multiple Approval Points Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates handlers for multiple approval points.
    /// </summary>
    [Test]
    public async Task Emit_MultipleApprovalPoints_GeneratesHandlersForEach()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var approvals = new List<ApprovalModel>
        {
            ApprovalModel.Create("ManagerReview", "ManagerApprover", "ValidateOrder"),
            ApprovalModel.Create("DirectorReview", "DirectorApprover", "ProcessOrder"),
        };
        var model = CreateModelWithApprovals(approvals);

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("RequestManagerReviewApprovalEvent");
        await Assert.That(result).Contains("RequestDirectorReviewApprovalEvent");
    }

    // =============================================================================
    // G. Namespace Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit uses correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_UsesCorrectNamespace()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("namespace TestNamespace;");
    }

    // =============================================================================
    // H. Request ID Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates unique request ID.
    /// </summary>
    [Test]
    public async Task EmitHandleAsync_GeneratesUniqueRequestId()
    {
        // Arrange
        var emitter = new ApprovalIntegrationHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithApproval();

        // Act
        emitter.Emit(sb, model);
        var result = sb.ToString();

        // Assert - should create a unique ID combining workflow ID and timestamp
        await Assert.That(result).Contains("requestId");
        await Assert.That(result).Contains("evt.WorkflowId");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateMinimalModel()
    {
        return WorkflowModel.Create(
            workflowName: "test-workflow",
            pascalName: "TestWorkflow",
            @namespace: "TestNamespace",
            stepNames: ["ValidateOrder", "ProcessOrder"],
            stateTypeName: "TestState");
    }

    private static WorkflowModel CreateModelWithApproval()
    {
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        return CreateModelWithApprovals([approval]);
    }

    private static WorkflowModel CreateModelWithApprovals(IReadOnlyList<ApprovalModel> approvals)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessOrder"],
            StateTypeName: "TestState",
            Loops: null,
            ApprovalPoints: approvals);
    }
}
