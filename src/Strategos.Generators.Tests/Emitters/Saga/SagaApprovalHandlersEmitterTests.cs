// -----------------------------------------------------------------------
// <copyright file="SagaApprovalHandlersEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="SagaApprovalHandlersEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class SagaApprovalHandlersEmitterTests
{
    // =============================================================================
    // A. Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitResumeHandler throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act & Assert
        await Assert.That(() => emitter.EmitResumeHandler(null!, model, approval, context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitResumeHandler throws for null model.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act & Assert
        await Assert.That(() => emitter.EmitResumeHandler(sb, null!, approval, context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitResumeHandler throws for null approval.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_NullApproval_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext("ProcessOrder");

        // Act & Assert
        await Assert.That(() => emitter.EmitResumeHandler(sb, model, null!, context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitResumeHandler throws for null context.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");

        // Act & Assert
        await Assert.That(() => emitter.EmitResumeHandler(sb, model, approval, null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Handler Signature Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitResumeHandler generates method with correct command type.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_GeneratesCorrectCommandType()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ResumePostValidationApprovalCommand cmd");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler generates method returning nullable object (for multiple command types).
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_GeneratesReturnType()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert - Return type is object? to allow returning different command types
        await Assert.That(result).Contains("public object? Handle(");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler generates guard clause.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_GeneratesGuardClause()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
    }

    // =============================================================================
    // C. Approval Flow Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitResumeHandler uses switch on Decision property (Phase 2 upgrade).
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_ChecksDecisionProperty()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert - Phase 2 uses switch on Decision enum instead of bool
        await Assert.That(result).Contains("switch (cmd.Decision)");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler sets Failed phase on rejection.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_RejectedPath_SetsFailedPhase()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.Failed;");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler calls MarkCompleted on rejection.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_RejectedPath_CallsMarkCompleted()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("MarkCompleted();");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler returns null on rejection.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_RejectedPath_ReturnsNull()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("return null;");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler returns next step command on approval.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_ApprovedPath_ReturnsNextStepCommand()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("return new StartProcessOrderCommand(WorkflowId);");
    }

    // =============================================================================
    // D. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitResumeHandler generates XML documentation.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// Handles the approval resume command");
        await Assert.That(result).Contains("/// </summary>");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler includes param documentation.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_IncludesParamDocumentation()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <param name=\"cmd\">");
    }

    // =============================================================================
    // E. Final Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitResumeHandler handles final step approval correctly.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_FinalStep_SetsCompletedPhase()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("FinalReview", "Manager", "FinalizeOrder");
        var context = CreateFinalStepContext();

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.Completed;");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler for final step returns void.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_FinalStep_ReturnsVoid()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("FinalReview", "Manager", "FinalizeOrder");
        var context = CreateFinalStepContext();

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public void Handle(");
    }

    /// <summary>
    /// A last-on-flow approval with a rejection chain must publish the chain's first
    /// start command. The void handler only mutated phase and commented that a
    /// subsequent handler would run the step — nothing published the command, so the
    /// saga parked forever (#186).
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_LastOnFlowWithRejection_DispatchesFirstRejectionStep()
    {
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create(
            approvalPointName: "FinalReview",
            approverTypeName: "Manager",
            precedingStepName: "FinalizeOrder",
            rejectionSteps:
            [
                StepModel.Create("LogRejection", "TestNamespace.LogRejection"),
                StepModel.Create("NotifyUser", "TestNamespace.NotifyUser"),
            ]);
        var context = CreateFinalStepContext();

        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        await Assert.That(result)
            .Contains("public object? Handle(")
            .Because("a void handler cannot publish the rejection-chain start command");

        await Assert.That(result)
            .Contains("StartLogRejectionCommand")
            .Because("the last-on-flow rejected arm must dispatch the chain's first step");

        await Assert.That(result)
            .DoesNotContain("subsequent handler")
            .Because("that comment described a handoff that had no trigger");
    }

    // =============================================================================
    // F. Phase 2 - ApprovalDecision Enum Handling Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitResumeHandler uses switch on ApprovalDecision.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_UsesSwitchOnDecision()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("switch (cmd.Decision)");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler handles Approved decision.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_HandlesApprovalDecisionApproved()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("case Strategos.Models.ApprovalDecision.Approved:");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler handles Rejected decision.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_HandlesApprovalDecisionRejected()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("case Strategos.Models.ApprovalDecision.Rejected:");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler handles Deferred decision.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_HandlesApprovalDecisionDeferred()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("case Strategos.Models.ApprovalDecision.Deferred:");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler clears PendingApprovalRequestId.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_ClearsPendingApprovalRequestId()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("PendingApprovalRequestId = null;");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler stores instructions when provided.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_StoresInstructions_WhenProvided()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("PostValidation", "LegalReviewer", "ValidateOrder");
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("cmd.Instructions");
        await Assert.That(result).Contains("ApprovalInstructions = cmd.Instructions;");
    }

    /// <summary>
    /// Verifies that EmitResumeHandler transitions to first rejection step when configured.
    /// </summary>
    [Test]
    public async Task EmitResumeHandler_WithRejectionSteps_TransitionsToFirstRejectionStep()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("LogRejection", "TestNamespace.LogRejection"),
            StepModel.Create("NotifyUser", "TestNamespace.NotifyUser"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "PostValidation",
            approverTypeName: "LegalReviewer",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);
        var context = CreateContext("ProcessOrder");

        // Act
        emitter.EmitResumeHandler(sb, model, approval, context);
        var result = sb.ToString();

        // Assert - Should transition to first rejection step, not directly to Failed
        await Assert.That(result).Contains("StartLogRejectionCommand");
    }

    // =============================================================================
    // G. SetPendingApproval Handler Tests (Phase 2)
    // =============================================================================

    /// <summary>
    /// Verifies that EmitSetPendingHandler generates handler for SetPendingApprovalCommand.
    /// </summary>
    [Test]
    public async Task EmitSetPendingHandler_GeneratesCorrectCommandType()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("ManagerReview", "ManagerApprover", "ValidateOrder");

        // Act
        emitter.EmitSetPendingHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("SetManagerReviewPendingApprovalCommand cmd");
    }

    /// <summary>
    /// Verifies that EmitSetPendingHandler sets PendingApprovalRequestId.
    /// </summary>
    [Test]
    public async Task EmitSetPendingHandler_SetsPendingApprovalRequestId()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("ManagerReview", "ManagerApprover", "ValidateOrder");

        // Act
        emitter.EmitSetPendingHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("PendingApprovalRequestId = cmd.ApprovalRequestId;");
    }

    /// <summary>
    /// Verifies that EmitSetPendingHandler generates void handler.
    /// </summary>
    [Test]
    public async Task EmitSetPendingHandler_GeneratesVoidReturn()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("ManagerReview", "ManagerApprover", "ValidateOrder");

        // Act
        emitter.EmitSetPendingHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public void Handle(");
    }

    /// <summary>
    /// Verifies that EmitSetPendingHandler generates guard clause.
    /// </summary>
    [Test]
    public async Task EmitSetPendingHandler_GeneratesGuardClause()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = CreateApproval("ManagerReview", "ManagerApprover", "ValidateOrder");

        // Act
        emitter.EmitSetPendingHandler(sb, model, approval);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
    }

    /// <summary>
    /// Verifies that EmitSetPendingHandler throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task EmitSetPendingHandler_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var model = CreateMinimalModel();
        var approval = CreateApproval("ManagerReview", "ManagerApprover", "ValidateOrder");

        // Act & Assert
        await Assert.That(() => emitter.EmitSetPendingHandler(null!, model, approval))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitSetPendingHandler throws for null model.
    /// </summary>
    [Test]
    public async Task EmitSetPendingHandler_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var approval = CreateApproval("ManagerReview", "ManagerApprover", "ValidateOrder");

        // Act & Assert
        await Assert.That(() => emitter.EmitSetPendingHandler(sb, null!, approval))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitSetPendingHandler throws for null approval.
    /// </summary>
    [Test]
    public async Task EmitSetPendingHandler_NullApproval_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaApprovalHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.EmitSetPendingHandler(sb, model, null!))
            .Throws<ArgumentNullException>();
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

    private static ApprovalModel CreateApproval(
        string approvalPointName,
        string approverTypeName,
        string precedingStepName)
    {
        return ApprovalModel.Create(
            approvalPointName: approvalPointName,
            approverTypeName: approverTypeName,
            precedingStepName: precedingStepName);
    }

    private static ApprovalResumeContext CreateContext(string nextStepName)
    {
        return new ApprovalResumeContext(
            IsLastStep: false,
            NextStepName: nextStepName);
    }

    private static ApprovalResumeContext CreateFinalStepContext()
    {
        return new ApprovalResumeContext(
            IsLastStep: true,
            NextStepName: null);
    }
}
