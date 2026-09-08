// -----------------------------------------------------------------------
// <copyright file="BranchHandlerEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="BranchHandlerEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class BranchHandlerEmitterTests
{
    // =============================================================================
    // A. Guard Tests - EmitRoutingHandler
    // =============================================================================

    /// <summary>
    /// Verifies that EmitRoutingHandler throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act & Assert
        await Assert.That(() => emitter.EmitRoutingHandler(null!, model, "ValidateStep", branch))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler throws for null model.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var branch = CreateBranch();

        // Act & Assert
        await Assert.That(() => emitter.EmitRoutingHandler(sb, null!, "ValidateStep", branch))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler throws for null stepName.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_NullStepName_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act & Assert
        await Assert.That(() => emitter.EmitRoutingHandler(sb, model, null!, branch))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler throws for null branch.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_NullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.EmitRoutingHandler(sb, model, "ValidateStep", null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Routing Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitRoutingHandler generates object return type.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_ValidInput_GeneratesObjectReturnType()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public object Handle(");
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler generates switch expression.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_ValidInput_GeneratesSwitchExpression()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("return State.Status switch");
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler generates cases.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_ValidInput_GeneratesCases()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("OrderStatus.Approved => new StartApproved_ProcessCommand(WorkflowId)");
        await Assert.That(result).Contains("OrderStatus.Rejected => new StartRejected_HandleCommand(WorkflowId)");
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler generates default throw when no otherwise case.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_NoOtherwiseCase_GeneratesDefaultThrow()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("_ => throw new InvalidOperationException");
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler uses otherwise case when present.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_WithOtherwiseCase_GeneratesOtherwiseBranch()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranchWithOtherwise();

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("_ => new StartDefault_HandleCommand(WorkflowId)");
        await Assert.That(result).DoesNotContain("throw new InvalidOperationException");
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler applies reducer.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_WithStateType_AppliesReducer()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("State = TestStateReducer.Reduce(State, evt.UpdatedState)");
    }

    /// <summary>A typed branch router sends reducer failure to rollback before selecting a case.</summary>
    [Test]
    public async Task EmitRoutingHandler_WithTypedCompensation_RoutesReducedFailureBeforeBranchDispatch()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var branch = CreateBranch();
        var model = CreateTypedCompensationModel(branch);

        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();
        var failureRoute = result.IndexOf("StateTransitionFailure", StringComparison.Ordinal);
        var branchDispatch = result.IndexOf("yield return State.Status switch", StringComparison.Ordinal);

        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("Phase = State.Phase;");
        await Assert.That(failureRoute).IsGreaterThan(-1);
        await Assert.That(branchDispatch).IsGreaterThan(failureRoute);
    }

    /// <summary>An OnFailure-only branch router does not dispatch a case after reducer failure.</summary>
    [Test]
    public async Task EmitRoutingHandler_WithFailureHandler_RoutesReducedFailureBeforeBranchDispatch()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var branch = CreateBranch();
        var model = CreateFailureHandlerModel();

        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();
        var failureRoute = result.IndexOf(
            "yield return new TriggerTestWorkflowFailureHandlerCommand(",
            StringComparison.Ordinal);
        var branchDispatch = result.IndexOf(
            "yield return State.Status switch",
            StringComparison.Ordinal);

        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(result).Contains("\"ValidateStep\"");
        await Assert.That(result).Contains("\"StateTransitionFailure\"");
        await Assert.That(failureRoute).IsGreaterThan(-1);
        await Assert.That(branchDispatch).IsGreaterThan(failureRoute);
    }

    // =============================================================================
    // C. Guard Tests - EmitPathEndHandler
    // =============================================================================

    /// <summary>
    /// Verifies that EmitPathEndHandler throws for null StringBuilder.
    /// </summary>
    [Test]
    public async Task EmitPathEndHandler_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var model = CreateMinimalModel();
        var branch = CreateBranch();
        var branchCase = CreateBranchCase();

        // Act & Assert
        await Assert.That(() => emitter.EmitPathEndHandler(null!, model, "Approved_Complete", branch, branchCase))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // D. Path End Handler with Rejoin Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitPathEndHandler generates rejoin command when branch has rejoin point.
    /// </summary>
    [Test]
    public async Task EmitPathEndHandler_WithRejoinPoint_GeneratesRejoinCommand()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranchWithRejoin();
        var branchCase = CreateBranchCase();

        // Act
        emitter.EmitPathEndHandler(sb, model, "Approved_Complete", branch, branchCase);
        var result = sb.ToString();

        // Assert - Handler now uses method injection for ILogger (multiline signature)
        await Assert.That(result).Contains("public StartFinalizeStepCommand Handle(");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
        await Assert.That(result).Contains("return new StartFinalizeStepCommand(WorkflowId)");
    }

    // =============================================================================
    // E. Path End Handler without Rejoin Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitPathEndHandler generates void handler when no rejoin.
    /// </summary>
    [Test]
    public async Task EmitPathEndHandler_NoRejoinPoint_GeneratesVoidHandler()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch(); // No rejoin
        var branchCase = CreateBranchCase();

        // Act
        emitter.EmitPathEndHandler(sb, model, "Approved_Complete", branch, branchCase);
        var result = sb.ToString();

        // Assert - Handler now uses method injection for ILogger (multiline signature)
        await Assert.That(result).Contains("public void Handle(");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    /// <summary>
    /// Verifies that EmitPathEndHandler marks completed when no rejoin.
    /// </summary>
    [Test]
    public async Task EmitPathEndHandler_NoRejoinPoint_MarksCompleted()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch(); // No rejoin
        var branchCase = CreateBranchCase();

        // Act
        emitter.EmitPathEndHandler(sb, model, "Approved_Complete", branch, branchCase);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.Completed");
        await Assert.That(result).Contains("MarkCompleted()");
    }

    /// <summary>A typed rejoining path rolls back reducer failure before dispatching the rejoin.</summary>
    [Test]
    public async Task EmitPathEndHandler_WithTypedCompensation_RoutesReducedFailureBeforeRejoin()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var branch = CreateBranchWithRejoin();
        var branchCase = CreateBranchCase();
        var model = CreateTypedCompensationModel(branch);

        emitter.EmitPathEndHandler(sb, model, "Approved_Complete", branch, branchCase);
        var result = sb.ToString();
        var failureRoute = result.IndexOf("StateTransitionFailure", StringComparison.Ordinal);
        var rejoin = result.IndexOf(
            "yield return new StartFinalizeStepCommand(WorkflowId)",
            StringComparison.Ordinal);

        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(failureRoute).IsGreaterThan(-1);
        await Assert.That(rejoin).IsGreaterThan(failureRoute);
    }

    /// <summary>A typed terminal path rolls back reducer failure instead of completing the saga.</summary>
    [Test]
    public async Task EmitPathEndHandler_WithTypedCompensation_RoutesReducedFailureBeforeCompletion()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var branch = CreateBranch();
        var branchCase = CreateBranchCase();
        var model = CreateTypedCompensationModel(branch);

        emitter.EmitPathEndHandler(sb, model, "Approved_Complete", branch, branchCase);
        var result = sb.ToString();
        var failureRoute = result.IndexOf("StateTransitionFailure", StringComparison.Ordinal);
        var completion = result.IndexOf("Phase = TestWorkflowPhase.Completed", StringComparison.Ordinal);

        await Assert.That(result).Contains("public IEnumerable<object> Handle(");
        await Assert.That(failureRoute).IsGreaterThan(-1);
        await Assert.That(completion).IsGreaterThan(failureRoute);
    }

    /// <summary>A typed confidence-gated path checks reducer failure before confidence routing.</summary>
    [Test]
    public async Task EmitPathEndHandler_WithTypedCompensation_RoutesReducedFailureBeforeConfidenceGate()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var branch = CreateBranchWithRejoin();
        var branchCase = CreateBranchCase();
        var model = CreateTypedCompensationModel(branch);
        var lowConfidenceStep = StepModel.Create("ManualReviewStep", "TestNamespace.ManualReviewStep");
        var confidence = new ConfidenceModel(
            0.8,
            "ManualReviewStep",
            lowConfidenceStep,
            new LowConfidenceHandlerChainModel([lowConfidenceStep]));

        emitter.EmitPathEndHandler(
            sb,
            model,
            "Approved_Complete",
            branch,
            branchCase,
            confidence);
        var result = sb.ToString();
        var failureRoute = result.IndexOf("StateTransitionFailure", StringComparison.Ordinal);
        var confidenceGate = result.IndexOf("if (evt.Confidence", StringComparison.Ordinal);

        await Assert.That(failureRoute).IsGreaterThan(-1);
        await Assert.That(confidenceGate).IsGreaterThan(failureRoute);
    }

    /// <summary>OnFailure-only branch endings stop before rejoin, completion, or confidence routing.</summary>
    [Test]
    public async Task EmitPathEndHandler_WithFailureHandler_RoutesBeforeEverySuccessorShape()
    {
        var emitter = new BranchHandlerEmitter();
        var branchCase = CreateBranchCase();
        var model = CreateFailureHandlerModel();
        var lowConfidenceStep = StepModel.Create("ManualReviewStep", "TestNamespace.ManualReviewStep");
        var confidence = new ConfidenceModel(
            0.8,
            "ManualReviewStep",
            lowConfidenceStep,
            new LowConfidenceHandlerChainModel([lowConfidenceStep]));
        var scenarios = new (BranchModel Branch, ConfidenceModel? Confidence, string Successor)[]
        {
            (CreateBranchWithRejoin(), null, "yield return new StartFinalizeStepCommand(WorkflowId)"),
            (CreateBranch(), null, "Phase = TestWorkflowPhase.Completed;"),
            (CreateBranchWithRejoin(), confidence, "if (evt.Confidence"),
        };

        foreach (var scenario in scenarios)
        {
            var sb = new StringBuilder();
            emitter.EmitPathEndHandler(
                sb,
                model,
                "Approved_Complete",
                scenario.Branch,
                branchCase,
                scenario.Confidence);
            var result = sb.ToString();
            var failureRoute = result.IndexOf(
                "yield return new TriggerTestWorkflowFailureHandlerCommand(",
                StringComparison.Ordinal);
            var successor = result.IndexOf(scenario.Successor, StringComparison.Ordinal);

            await Assert.That(result).Contains("public IEnumerable<object> Handle(");
            await Assert.That(result).Contains("\"Approved_Complete\"");
            await Assert.That(result).Contains("\"StateTransitionFailure\"");
            await Assert.That(failureRoute).IsGreaterThan(-1);
            await Assert.That(successor).IsGreaterThan(failureRoute);
        }
    }

    /// <summary>A shared branch completion keeps its pre-reducer case identity for rollback.</summary>
    [Test]
    public async Task EmitLiveCaseCompletedHandler_WithTypedCompensation_CapturesDiscriminatorBeforeReducer()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var sharedStep = StepModel.Create(
            "SharedStep",
            "TestNamespace.SharedStep",
            compensation: new CompensationModel(
                "TestNamespace.UndoSharedStep",
                InverseAction: new WorkflowActionReferenceModel("orders", "Order", "undo-shared"),
                InverseActionResolution: WorkflowActionReferenceResolution.Resolved),
            action: new WorkflowActionReferenceModel("orders", "Order", "shared"));
        var approved = BranchCaseModel.Create(
            "OrderStatus.Approved",
            "Approved",
            ["SharedStep"],
            isTerminal: true);
        var rejected = BranchCaseModel.Create(
            "OrderStatus.Rejected",
            "Rejected",
            ["SharedStep"],
            isTerminal: true);
        var branch = BranchModel.Create(
            "Test-Shared",
            "ValidateStep",
            "Status",
            "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            [approved, rejected]);
        var model = new WorkflowModel(
            "test-workflow",
            "TestWorkflow",
            "TestNamespace",
            ["ValidateStep", "SharedStep"],
            "TestState",
            Steps:
            [
                StepModel.Create("ValidateStep", "TestNamespace.ValidateStep"),
                sharedStep,
            ],
            Branches: [branch])
        {
            StateHasPhaseProperty = true,
        };
        var occurrences = new[]
        {
            new BranchCaseStepOccurrence(branch, approved, "SharedStep", null, sharedStep),
            new BranchCaseStepOccurrence(branch, rejected, "SharedStep", null, sharedStep),
        };

        emitter.EmitLiveCaseCompletedHandler(sb, model, "SharedStepCompleted", occurrences);
        var result = sb.ToString();
        var capture = result.IndexOf(
            "var liveCaseDiscriminator = State.Status;",
            StringComparison.Ordinal);
        var reducer = result.IndexOf(
            "State = TestStateReducer.Reduce(State, evt.UpdatedState);",
            StringComparison.Ordinal);

        await Assert.That(capture).IsGreaterThan(-1);
        await Assert.That(reducer).IsGreaterThan(capture);
        await Assert.That(result).Contains("if (liveCaseDiscriminator == OrderStatus.Approved)");
        await Assert.That(result).Contains("if (liveCaseDiscriminator == OrderStatus.Rejected)");
    }

    /// <summary>An OnFailure-only shared branch completion routes reducer failure before any case arm.</summary>
    [Test]
    public async Task EmitLiveCaseCompletedHandler_WithFailureHandler_TriggersRecoveryBeforeCaseRouting()
    {
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var approved = BranchCaseModel.Create(
            "OrderStatus.Approved",
            "Approved",
            ["Approved_SharedStep"],
            isTerminal: true);
        var rejected = BranchCaseModel.Create(
            "OrderStatus.Rejected",
            "Rejected",
            ["Rejected_SharedStep"],
            isTerminal: true);
        var branch = BranchModel.Create(
            "Test-Shared",
            "ValidateStep",
            "Status",
            "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            [approved, rejected]);
        var model = CreateFailureHandlerModel() with { Branches = [branch] };
        var occurrences = new[]
        {
            new BranchCaseStepOccurrence(branch, approved, "Approved_SharedStep", null),
            new BranchCaseStepOccurrence(branch, rejected, "Rejected_SharedStep", null),
        };

        emitter.EmitLiveCaseCompletedHandler(sb, model, "SharedStepCompleted", occurrences);
        var result = sb.ToString();
        var failureRoute = result.IndexOf(
            "yield return new TriggerTestWorkflowFailureHandlerCommand(",
            StringComparison.Ordinal);
        var firstCaseArm = result.IndexOf(
            "if (liveCasePhase == TestWorkflowPhase.Approved_SharedStep)",
            StringComparison.Ordinal);

        await Assert.That(result).Contains("var liveCasePhase = Phase;");
        await Assert.That(result).Contains("Phase = State.Phase;");
        await Assert.That(result).Contains("\"SharedStep\"");
        await Assert.That(result).Contains("\"StateTransitionFailure\"");
        await Assert.That(failureRoute).IsGreaterThan(-1);
        await Assert.That(firstCaseArm).IsGreaterThan(failureRoute);
    }

    // =============================================================================
    // F. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitRoutingHandler generates XML documentation including logger param.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_ValidInput_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch();

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert - XML docs include logger param
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// </summary>");
        await Assert.That(result).Contains("/// <param name=\"logger\">");
        await Assert.That(result).Contains("/// <returns>");
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
            StepNames: ["ValidateStep", "Approved_Process", "Rejected_Handle", "FinalizeStep"],
            StateTypeName: "TestState",
            Loops: null);
    }

    private static WorkflowModel CreateTypedCompensationModel(BranchModel branch)
    {
        var inverse = new WorkflowActionReferenceModel("orders", "Order", "undo-validate");
        var compensation = new CompensationModel(
            "TestNamespace.UndoValidateStep",
            InverseAction: inverse,
            InverseActionResolution: WorkflowActionReferenceResolution.Resolved);
        var steps = new[]
        {
            StepModel.Create(
                "ValidateStep",
                "TestNamespace.ValidateStep",
                compensation: compensation,
                action: new WorkflowActionReferenceModel("orders", "Order", "validate")),
            StepModel.Create(
                "Approved_Process",
                "TestNamespace.ApprovedProcessStep",
                action: new WorkflowActionReferenceModel("orders", "Order", "process")),
            StepModel.Create(
                "Approved_Complete",
                "TestNamespace.ApprovedCompleteStep",
                action: new WorkflowActionReferenceModel("orders", "Order", "complete")),
            StepModel.Create(
                "Rejected_Handle",
                "TestNamespace.RejectedHandleStep",
                action: new WorkflowActionReferenceModel("orders", "Order", "reject")),
            StepModel.Create(
                "FinalizeStep",
                "TestNamespace.FinalizeStep",
                action: new WorkflowActionReferenceModel("orders", "Order", "finalize")),
        };

        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "Approved_Process", "Approved_Complete", "Rejected_Handle", "FinalizeStep"],
            StateTypeName: "TestState",
            Steps: steps,
            Branches: [branch])
        {
            StateHasPhaseProperty = true,
        };
    }

    private static WorkflowModel CreateFailureHandlerModel()
    {
        var failureHandler = FailureHandlerModel.Create(
            handlerId: "workflow-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["FailedStep"],
            isTerminal: true);

        return CreateMinimalModel() with
        {
            FailureHandlers = [failureHandler],
            StateHasPhaseProperty = true,
        };
    }

    private static BranchModel CreateBranch()
    {
        var case1 = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process", "Approved_Complete"],
            isTerminal: false);
        var case2 = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Rejected",
            branchPathPrefix: "Rejected",
            stepNames: ["Rejected_Handle"],
            isTerminal: false);

        return BranchModel.Create(
            branchId: "Test-Status",
            previousStepName: "ValidateStep",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases: [case1, case2]);
    }

    private static BranchModel CreateBranchWithOtherwise()
    {
        var case1 = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process"],
            isTerminal: false);
        var defaultCase = BranchCaseModel.Create(
            caseValueLiteral: "_",
            branchPathPrefix: "Default",
            stepNames: ["Default_Handle"],
            isTerminal: false);

        return BranchModel.Create(
            branchId: "Test-Status",
            previousStepName: "ValidateStep",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases: [case1, defaultCase]);
    }

    private static BranchModel CreateBranchWithRejoin()
    {
        var case1 = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process", "Approved_Complete"],
            isTerminal: false);

        return BranchModel.Create(
            branchId: "Test-Status",
            previousStepName: "ValidateStep",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases: [case1],
            rejoinStepName: "FinalizeStep");
    }

    private static BranchCaseModel CreateBranchCase()
    {
        return BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process", "Approved_Complete"],
            isTerminal: false);
    }

    // =============================================================================
    // G. Loop Prefix Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitRoutingHandler applies loop prefix to Start commands when branch is inside a loop.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_WithLoopPrefix_GeneratesPrefixedStartCommands()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranchWithLoopPrefix();

        // Act
        emitter.EmitRoutingHandler(sb, model, "TargetLoop_ValidateStep", branch);
        var result = sb.ToString();

        // Assert - Start commands should use loop-prefixed step names
        await Assert.That(result).Contains("StartTargetLoop_Approved_ProcessCommand");
        await Assert.That(result).Contains("StartTargetLoop_Rejected_HandleCommand");
    }

    /// <summary>
    /// Verifies that EmitRoutingHandler does NOT apply prefix when branch is NOT inside a loop.
    /// </summary>
    [Test]
    public async Task EmitRoutingHandler_WithoutLoopPrefix_GeneratesUnprefixedStartCommands()
    {
        // Arrange
        var emitter = new BranchHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var branch = CreateBranch(); // No loop prefix

        // Act
        emitter.EmitRoutingHandler(sb, model, "ValidateStep", branch);
        var result = sb.ToString();

        // Assert - Start commands should use unprefixed step names
        await Assert.That(result).Contains("StartApproved_ProcessCommand");
        await Assert.That(result).Contains("StartRejected_HandleCommand");
        await Assert.That(result).DoesNotContain("StartTargetLoop_");
    }

    private static BranchModel CreateBranchWithLoopPrefix()
    {
        var case1 = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process", "Approved_Complete"],
            isTerminal: false);
        var case2 = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Rejected",
            branchPathPrefix: "Rejected",
            stepNames: ["Rejected_Handle"],
            isTerminal: false);

        return BranchModel.Create(
            branchId: "Test-Status",
            previousStepName: "TargetLoop_ValidateStep",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases: [case1, case2],
            loopPrefix: "TargetLoop");
    }
}
