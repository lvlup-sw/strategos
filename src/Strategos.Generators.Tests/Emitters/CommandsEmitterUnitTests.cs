// -----------------------------------------------------------------------
// <copyright file="CommandsEmitterUnitTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="CommandsEmitter"/> class.
/// </summary>
/// <remarks>
/// These tests verify command generation in isolation, independent of the source generator.
/// </remarks>
[Property("Category", "Unit")]
public class CommandsEmitterUnitTests
{
    // =============================================================================
    // A. Start Command Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates a Start command record.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_GeneratesStartCommand()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record StartProcessOrderCommand");
    }

    /// <summary>
    /// Verifies that the Start command has a WorkflowId parameter.
    /// </summary>
    [Test]
    public async Task Emit_StartCommand_HasWorkflowIdParameter()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Guid WorkflowId");
    }

    /// <summary>
    /// Verifies that the Start command has an InitialState parameter with the state type.
    /// </summary>
    [Test]
    public async Task Emit_StartCommand_HasInitialStateParameter()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("OrderState InitialState");
    }

    /// <summary>
    /// Verifies that the Start workflow command does NOT have SagaIdentity attribute.
    /// Per design: Start commands create sagas via Start() method, not routing.
    /// Step commands (StartValidateOrderCommand) DO have SagaIdentity for routing.
    /// </summary>
    [Test]
    public async Task Emit_StartCommand_NoSagaIdentityAttribute()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert - Verify SagaIdentity is not on StartProcessOrderCommand (workflow start)
        // Only step commands get SagaIdentity for saga routing
        await Assert.That(source).Contains("StartProcessOrderCommand(");
        await Assert.That(source).Contains($"StartProcessOrderCommand({Environment.NewLine}    Guid WorkflowId,");
    }

    /// <summary>
    /// Verifies that the emitter uses the correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_Commands_UsesCorrectNamespace()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("namespace TestNamespace;");
    }

    // =============================================================================
    // B. Execute Command Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates Execute commands for each step.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_GeneratesExecuteCommandPerStep()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record ExecuteValidateOrderCommand");
        await Assert.That(source).Contains("public sealed partial record ExecuteProcessPaymentCommand");
        await Assert.That(source).Contains("public sealed partial record ExecuteSendConfirmationCommand");
    }

    /// <summary>
    /// Verifies that Execute commands have WorkflowId and StepExecutionId parameters.
    /// </summary>
    [Test]
    public async Task Emit_ExecuteCommand_HasRequiredParameters()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert - Check that the Execute command pattern contains both required parameters
        // Each Execute command should have (Guid WorkflowId, Guid StepExecutionId)
        await Assert.That(source).Contains("ExecuteValidateOrderCommand(");
        await Assert.That(source).Contains("Guid StepExecutionId");
    }

    /// <summary>
    /// Verifies that step commands HAVE SagaIdentity attribute for saga routing.
    /// Per design: Step commands (Start{Step}, Execute{Step}, Execute{Step}Worker) need
    /// SagaIdentity so Wolverine can correlate them back to the saga instance.
    /// </summary>
    [Test]
    public async Task Emit_StepCommands_HaveSagaIdentityAttribute()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert - Step commands should have [property: Wolverine.Persistence.Sagas.SagaIdentity]
        await Assert.That(source).Contains("[property: Wolverine.Persistence.Sagas.SagaIdentity] Guid WorkflowId);");
        await Assert.That(source).Contains("[property: Wolverine.Persistence.Sagas.SagaIdentity] Guid WorkflowId,");
    }

    /// <summary>
    /// Verifies that loop-prefixed step names generate properly named Execute commands.
    /// </summary>
    [Test]
    public async Task Emit_LoopWorkflow_GeneratesPrefixedCommands()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "iterative-refinement",
            PascalName: "IterativeRefinement",
            Namespace: "TestNamespace",
            StepNames: ["ValidateInput", "Refinement_CritiqueStep", "Refinement_RefineStep", "PublishResult"],
            StateTypeName: "RefinementState");

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ExecuteRefinement_CritiqueStepCommand");
        await Assert.That(source).Contains("ExecuteRefinement_RefineStepCommand");
    }

    // =============================================================================
    // C. Header and Attributes Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the auto-generated header is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesAutoGeneratedHeader()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("// <auto-generated/>");
    }

    /// <summary>
    /// Verifies that nullable enable directive is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesNullableEnable()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that XML documentation is included for the Start command.
    /// </summary>
    [Test]
    public async Task Emit_StartCommand_IncludesXmlDocumentation()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("/// <summary>");
        await Assert.That(source).Contains("Start the process-order workflow");
    }

    // =============================================================================
    // C. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null model throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Emit_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => CommandsEmitter.Emit(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // D. Start Step Command Tests (Message Tripling - Milestone 8b)
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates a StartStep command for each step.
    /// This is the lightweight internal routing command (Saga → Saga).
    /// </summary>
    [Test]
    public async Task Emit_GeneratesStartStepCommand_PerStep()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record StartValidateOrderCommand");
        await Assert.That(source).Contains("public sealed partial record StartProcessPaymentCommand");
        await Assert.That(source).Contains("public sealed partial record StartSendConfirmationCommand");
    }

    /// <summary>
    /// Verifies that StartStep commands only have WorkflowId (lightweight) with SagaIdentity.
    /// </summary>
    [Test]
    public async Task Emit_StartStepCommand_HasWorkflowIdOnly()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert - StartStepCommand should only have WorkflowId (with SagaIdentity), not State
        await Assert.That(source).Contains("StartValidateOrderCommand(");
        // The line should end with just WorkflowId (with SagaIdentity attribute)
        await Assert.That(source).Contains($"StartValidateOrderCommand({Environment.NewLine}    [property: Wolverine.Persistence.Sagas.SagaIdentity] Guid WorkflowId);");
    }

    // =============================================================================
    // E. Execute Worker Command Tests (Message Tripling - Milestone 8b)
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates an ExecuteWorker command for each step.
    /// This is the worker dispatch command (Saga → Worker) that includes state.
    /// </summary>
    [Test]
    public async Task Emit_GeneratesExecuteWorkerCommand_PerStep()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record ExecuteValidateOrderWorkerCommand");
        await Assert.That(source).Contains("public sealed partial record ExecuteProcessPaymentWorkerCommand");
        await Assert.That(source).Contains("public sealed partial record ExecuteSendConfirmationWorkerCommand");
    }

    /// <summary>
    /// Verifies that ExecuteWorker commands include the State parameter.
    /// </summary>
    [Test]
    public async Task Emit_WorkerCommand_IncludesStateParameter()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert - Worker command should have State parameter with correct type
        await Assert.That(source).Contains("ExecuteValidateOrderWorkerCommand(");
        await Assert.That(source).Contains("OrderState State");
    }

    /// <summary>
    /// Verifies that ExecuteWorker commands have StepExecutionId.
    /// </summary>
    [Test]
    public async Task Emit_WorkerCommand_HasStepExecutionId()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ExecuteValidateOrderWorkerCommand(");
        await Assert.That(source).Contains("Guid StepExecutionId");
    }

    // =============================================================================
    // F. Approval Command Tests (Phase 2 - Enhanced Commands)
    // =============================================================================

    /// <summary>
    /// Verifies that the Resume command uses ApprovalDecision enum instead of bool.
    /// </summary>
    [Test]
    public async Task EmitResumeApprovalCommand_IncludesApprovalDecision()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Strategos.Models.ApprovalDecision Decision");
    }

    /// <summary>
    /// Verifies that the Resume command includes SelectedOptionId parameter.
    /// </summary>
    [Test]
    public async Task EmitResumeApprovalCommand_IncludesSelectedOptionId()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("string? SelectedOptionId");
    }

    /// <summary>
    /// Verifies that the Resume command includes Instructions parameter.
    /// </summary>
    [Test]
    public async Task EmitResumeApprovalCommand_IncludesInstructions()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("string? Instructions");
    }

    /// <summary>
    /// Verifies that timeout command is generated when approval has escalation configured.
    /// </summary>
    [Test]
    public async Task Emit_WithEscalation_GeneratesTimeoutCommand()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyEscalation", "TestNamespace.Steps.NotifyEscalation")
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ManagerReviewApprovalTimeoutCommand");
        await Assert.That(source).Contains("string ApprovalRequestId");
    }

    /// <summary>
    /// Verifies that Request event is generated for approval points.
    /// </summary>
    [Test]
    public async Task Emit_WithApproval_GeneratesRequestApprovalEvent()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("RequestManagerReviewApprovalEvent");
    }

    /// <summary>
    /// Verifies that SetPendingApproval command is generated for approval points.
    /// </summary>
    [Test]
    public async Task Emit_WithApproval_GeneratesSetPendingApprovalCommand()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder");
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("SetManagerReviewPendingApprovalCommand");
    }

    /// <summary>
    /// Verifies that nested approval commands are generated for escalation chains.
    /// </summary>
    [Test]
    public async Task Emit_WithNestedApproval_GeneratesEscalationResumeCommand()
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
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert - Both approval commands should be generated
        await Assert.That(source).Contains("ResumeManagerReviewApprovalCommand");
        await Assert.That(source).Contains("ResumeDirectorReviewApprovalCommand");
    }

    /// <summary>
    /// Verifies that Worker command is generated for approval rejection steps.
    /// </summary>
    [Test]
    public async Task Emit_WithRejectionSteps_GeneratesWorkerCommand()
    {
        // Arrange
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("TerminateStep", "TestNamespace.Steps.TerminateStep"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ExecuteTerminateStepWorkerCommand");
    }

    /// <summary>
    /// Verifies that Worker command is generated for approval escalation steps.
    /// </summary>
    [Test]
    public async Task Emit_WithEscalationSteps_GeneratesWorkerCommand()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("AutoFailStep", "TestNamespace.Steps.AutoFailStep"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = CommandsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ExecuteAutoFailStepWorkerCommand");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateTestModel()
    {
        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "SendConfirmation"],
            StateTypeName: "OrderState");
    }

    private static WorkflowModel CreateTestModelWithApproval(ApprovalModel approval)
    {
        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "SendConfirmation"],
            StateTypeName: "OrderState",
            ApprovalPoints: [approval]);
    }
}
