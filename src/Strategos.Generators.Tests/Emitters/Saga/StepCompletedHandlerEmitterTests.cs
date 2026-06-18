// -----------------------------------------------------------------------
// <copyright file="StepCompletedHandlerEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="StepCompletedHandlerEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class StepCompletedHandlerEmitterTests
{
    // =============================================================================
    // A. Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(null!, model, "ValidateStep", context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null model.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, null!, "ValidateStep", context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null stepName.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullStepName_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, model, null!, context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null context.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, model, "ValidateStep", null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Non-Final Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates handler returning next step command.
    /// </summary>
    [Test]
    public async Task EmitHandler_NonFinalStep_GeneratesReturnType()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses IEnumerable<object> with yield return pattern and ILogger injection
        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitHandler generates handler accepting completed event.
    /// </summary>
    [Test]
    public async Task EmitHandler_NonFinalStep_AcceptsCompletedEvent()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Signature now has ILogger method injection
        await Assert.That(result).Contains("ValidateStepCompleted evt,");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitHandler generates guard clause.
    /// </summary>
    [Test]
    public async Task EmitHandler_NonFinalStep_GeneratesGuardClause()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("ArgumentNullException.ThrowIfNull(evt, nameof(evt))");
    }

    /// <summary>
    /// Verifies that EmitHandler applies reducer when state type exists.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithStateType_AppliesReducer()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("State = TestStateReducer.Reduce(State, evt.UpdatedState)");
    }

    /// <summary>
    /// Verifies that EmitHandler returns next step command.
    /// </summary>
    [Test]
    public async Task EmitHandler_NonFinalStep_ReturnsNextCommand()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses yield return pattern
        await Assert.That(result).Contains("yield return new StartProcessStepCommand(WorkflowId)");
    }

    // =============================================================================
    // C. Final Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates void handler for final step.
    /// </summary>
    [Test]
    public async Task EmitHandler_FinalStep_GeneratesVoidHandler()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: true, nextStepName: null);

        // Act
        emitter.EmitHandler(sb, model, "FinalStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public void Handle(");
    }

    /// <summary>
    /// Verifies that EmitHandler sets completed phase for final step.
    /// </summary>
    [Test]
    public async Task EmitHandler_FinalStep_SetsCompletedPhase()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: true, nextStepName: null);

        // Act
        emitter.EmitHandler(sb, model, "FinalStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.Completed");
    }

    /// <summary>
    /// Verifies that EmitHandler calls MarkCompleted for final step.
    /// </summary>
    [Test]
    public async Task EmitHandler_FinalStep_CallsMarkCompleted()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: true, nextStepName: null);

        // Act
        emitter.EmitHandler(sb, model, "FinalStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("MarkCompleted();");
    }

    // =============================================================================
    // D. No State Type Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler does NOT apply reducer when no state type.
    /// </summary>
    [Test]
    public async Task EmitHandler_NoStateType_DoesNotApplyReducer()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "Test",
            StepNames: ["ValidateStep", "ProcessStep"],
            StateTypeName: null,
            Loops: null);
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).DoesNotContain("Reducer.Reduce");
    }

    // =============================================================================
    // E. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates XML documentation.
    /// </summary>
    [Test]
    public async Task EmitHandler_ValidInput_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// </summary>");
        await Assert.That(result).Contains("/// <param name=\"evt\">");
    }

    /// <summary>
    /// Verifies that EmitHandler generates returns documentation for non-final step.
    /// </summary>
    [Test]
    public async Task EmitHandler_NonFinalStep_GeneratesReturnsDoc()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("/// <returns>");
    }

    // =============================================================================
    // E. Approval Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates IEnumerable return type for steps with approval.
    /// </summary>
    /// <remarks>
    /// Returns IEnumerable to support cascading events and ILogger method injection.
    /// </remarks>
    [Test]
    public async Task EmitHandler_StepWithApproval_GeneratesEnumerableReturn()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create("PostValidation", "LegalReviewer", "ValidateStep");
        var context = CreateContextWithApproval(approval, "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses IEnumerable<object> with ILogger method injection
        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitHandler generates approval phase transition.
    /// </summary>
    [Test]
    public async Task EmitHandler_StepWithApproval_SetsApprovalPhase()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create("PostValidation", "LegalReviewer", "ValidateStep");
        var context = CreateContextWithApproval(approval, "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.AwaitApproval_PostValidation;");
    }

    /// <summary>
    /// Verifies that EmitHandler applies reducer for steps with approval.
    /// </summary>
    [Test]
    public async Task EmitHandler_StepWithApproval_AppliesReducer()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create("PostValidation", "LegalReviewer", "ValidateStep");
        var context = CreateContextWithApproval(approval, "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - reducer type is derived from StateTypeName ("TestState" → "TestStateReducer")
        await Assert.That(result).Contains("State = TestStateReducer.Reduce(State, evt.UpdatedState);");
    }

    /// <summary>
    /// Verifies that EmitHandler does NOT return next step command for steps with approval.
    /// </summary>
    [Test]
    public async Task EmitHandler_StepWithApproval_DoesNotReturnNextStepCommand()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create("PostValidation", "LegalReviewer", "ValidateStep");
        var context = CreateContextWithApproval(approval, "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).DoesNotContain("return new StartProcessStepCommand");
    }

    // =============================================================================
    // F. Phase 2 - Approval Request Event Emission Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler emits RequestApprovalEvent for steps with approval.
    /// </summary>
    [Test]
    public async Task EmitHandler_StepWithApproval_EmitsRequestApprovalEvent()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create("PostValidation", "LegalReviewer", "ValidateStep");
        var context = CreateContextWithApproval(approval, "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("RequestPostValidationApprovalEvent");
    }

    /// <summary>
    /// Verifies that EmitHandler yields RequestApprovalEvent for steps with approval.
    /// </summary>
    [Test]
    public async Task EmitHandler_StepWithApproval_YieldsRequestApprovalEvent()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create("PostValidation", "LegalReviewer", "ValidateStep");
        var context = CreateContextWithApproval(approval, "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses yield return pattern
        await Assert.That(result).Contains("yield return new RequestPostValidationApprovalEvent");
    }

    /// <summary>
    /// Verifies that EmitHandler returns IEnumerable for steps with approval (to support cascading and logging).
    /// </summary>
    [Test]
    public async Task EmitHandler_StepWithApproval_ReturnsEnumerableType()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var approval = ApprovalModel.Create("PostValidation", "LegalReviewer", "ValidateStep");
        var context = CreateContextWithApproval(approval, "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses IEnumerable<object> with ILogger method injection
        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    // =============================================================================
    // G. Phase-Aware Routing Tests (Failure Handling)
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates IEnumerable return type when failure handlers exist.
    /// </summary>
    /// <remarks>
    /// When a workflow has failure handlers, the step completed handler uses IEnumerable
    /// to support polymorphic routing with yield return to either the next step or the FailedStep.
    /// </remarks>
    [Test]
    public async Task EmitHandler_WithFailureHandlers_GeneratesEnumerableReturnType()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithFailureHandlers();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Uses IEnumerable<object> with ILogger method injection
        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitHandler generates phase check when failure handlers exist.
    /// </summary>
    /// <remarks>
    /// After applying the reducer, the handler must check if State.Phase == Failed
    /// to route to the FailedStep instead of the next step.
    /// </remarks>
    [Test]
    public async Task EmitHandler_WithFailureHandlers_GeneratesPhaseCheck()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithFailureHandlers();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Must check Phase for failure routing
        await Assert.That(result).Contains("Phase == TestWorkflowPhase.Failed");
    }

    /// <summary>
    /// Verifies that EmitHandler routes to FailedStep when phase is Failed.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithFailureHandlers_RoutesToFailedStep()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithFailureHandlers();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses yield return pattern
        await Assert.That(result).Contains("yield return new StartFailedStepCommand(WorkflowId)");
    }

    /// <summary>
    /// Verifies that EmitHandler still routes to next step when phase is not Failed.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithFailureHandlers_RoutesToNextStepWhenNotFailed()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithFailureHandlers();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses yield return pattern
        await Assert.That(result).Contains("yield return new StartProcessStepCommand(WorkflowId)");
    }

    /// <summary>
    /// Verifies that EmitHandler generates complete conditional structure.
    /// </summary>
    /// <remarks>
    /// The generated code should have an if-else structure:
    /// if (Phase == Failed) return StartFailedStepCommand
    /// else return StartNextStepCommand.
    /// </remarks>
    [Test]
    public async Task EmitHandler_WithFailureHandlers_GeneratesConditionalStructure()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithFailureHandlers();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Must have proper if structure
        await Assert.That(result).Contains("if (Phase == TestWorkflowPhase.Failed)");
    }

    /// <summary>
    /// Verifies that EmitHandler without failure handlers retains original behavior.
    /// </summary>
    /// <remarks>
    /// Workflows without failure handlers should not have phase checking logic.
    /// </remarks>
    [Test]
    public async Task EmitHandler_WithoutFailureHandlers_DoesNotGeneratePhaseCheck()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(); // No failure handlers
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Should NOT have phase check
        await Assert.That(result).DoesNotContain("Phase == TestWorkflowPhase.Failed");
    }

    /// <summary>
    /// Verifies that EmitHandler without failure handlers returns IEnumerable.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithoutFailureHandlers_ReturnsEnumerableType()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(); // No failure handlers
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Now uses IEnumerable<object> with ILogger method injection
        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitHandler synchronizes saga Phase from State.Phase after reducer
    /// for state types that have Phase property (not ending in "WorkflowState").
    /// </summary>
    /// <remarks>
    /// This is critical for failure routing to work correctly. The saga's Phase property
    /// must be synchronized from State.Phase after the reducer is applied, otherwise
    /// the Phase check will always use the step-start phase (e.g., FetchPortfolioStep)
    /// rather than the step-returned phase (e.g., Failed).
    /// </remarks>
    [Test]
    public async Task EmitHandler_WithFailureHandlers_SynchronizesPhasaFromState()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        // Use state type without "WorkflowState" suffix - these have Phase property
        var model = CreateModelWithFailureHandlers();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Must synchronize Phase from State after reducer
        await Assert.That(result).Contains("Phase = State.Phase;");
    }

    /// <summary>
    /// Verifies that EmitHandler does NOT synchronize Phase for WorkflowState types
    /// (which don't have Phase property).
    /// </summary>
    [Test]
    public async Task EmitHandler_WithWorkflowStateSuffix_DoesNotSyncPhase()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        // Use state type WITH "WorkflowState" suffix - these don't have Phase property
        var model = CreateModelWithFailureHandlersAndWorkflowState();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Should NOT have phase sync for WorkflowState types
        await Assert.That(result).DoesNotContain("Phase = State.Phase;");
    }

    /// <summary>
    /// Verifies that Phase synchronization occurs BEFORE the phase check.
    /// </summary>
    /// <remarks>
    /// The order matters: reducer → phase sync → phase check.
    /// If the phase check happens before sync, failure routing won't work.
    /// </remarks>
    [Test]
    public async Task EmitHandler_WithFailureHandlers_PhaseSyncOccursBeforePhaseCheck()
    {
        // Arrange
        var emitter = new StepCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateModelWithFailureHandlers();
        var context = CreateContext(isLastStep: false, nextStepName: "ProcessStep");

        // Act
        emitter.EmitHandler(sb, model, "ValidateStep", context);
        var result = sb.ToString();

        // Assert - Phase sync must come before phase check
        var phaseSyncIndex = result.IndexOf("Phase = State.Phase;", StringComparison.Ordinal);
        var phaseCheckIndex = result.IndexOf("Phase == TestWorkflowPhase.Failed", StringComparison.Ordinal);

        await Assert.That(phaseSyncIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(phaseCheckIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(phaseSyncIndex).IsLessThan(phaseCheckIndex);
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateMinimalModel()
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "ProcessStep"],
            StateTypeName: "TestState",
            Loops: null);
    }

    private static WorkflowModel CreateModelWithFailureHandlers()
    {
        var failureHandler = FailureHandlerModel.Create(
            handlerId: "workflow-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["FailedStep"],
            isTerminal: true);

        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "ProcessStep", "CompleteStep", "FailedStep"],
            StateTypeName: "TestState",
            Loops: null,
            FailureHandlers: [failureHandler])
        {
            // This model represents a state type that carries a Phase property, so
            // the saga syncs Phase = State.Phase (the route-1 OnFailure path). The
            // emitter only emits that sync when StateHasPhaseProperty is true (#140).
            StateHasPhaseProperty = true,
        };
    }

    private static WorkflowModel CreateModelWithFailureHandlersAndWorkflowState()
    {
        var failureHandler = FailureHandlerModel.Create(
            handlerId: "workflow-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["FailedStep"],
            isTerminal: true);

        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "ProcessStep", "CompleteStep", "FailedStep"],
            StateTypeName: "TestWorkflowState", // Ends in "WorkflowState" - no Phase property
            Loops: null,
            FailureHandlers: [failureHandler]);
    }

    private static HandlerContext CreateContext(bool isLastStep, string? nextStepName)
    {
        return new HandlerContext(
            StepIndex: 0,
            IsLastStep: isLastStep,
            IsTerminalStep: false,
            NextStepName: nextStepName,
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);
    }

    private static HandlerContext CreateContextWithApproval(ApprovalModel approval, string? nextStepName)
    {
        return new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: nextStepName,
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: approval,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);
    }
}
