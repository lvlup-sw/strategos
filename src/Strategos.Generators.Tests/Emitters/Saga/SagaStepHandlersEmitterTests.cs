// -----------------------------------------------------------------------
// <copyright file="SagaStepHandlersEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

using TUnit.Core;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for <see cref="SagaStepHandlersEmitter"/>.
/// </summary>
[Property("Category", "Unit")]
public class SagaStepHandlersEmitterTests
{
    // ====================================================================
    // Section A: Guard Clause Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit throws ArgumentNullException when StringBuilder is null.
    /// </summary>
    [Test]
    public async Task Emit_NullStringBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.Emit(null!, model))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Emit throws ArgumentNullException when model is null.
    /// </summary>
    [Test]
    public async Task Emit_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();

        // Act & Assert
        await Assert.That(() => emitter.Emit(sb, null!))
            .Throws<ArgumentNullException>();
    }

    // ====================================================================
    // Section B: Interface Implementation Tests
    // ====================================================================

    /// <summary>
    /// Verifies that the class implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task Class_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();

        // Assert
        await Assert.That(emitter is ISagaComponentEmitter).IsTrue();
    }

    // ====================================================================
    // Section C: Linear Workflow Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit emits start and completed handlers for each step.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_EmitsStartAndCompletedForEachStep()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(stepNames: ["Analyze", "Process", "Complete"]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();

        // Start handlers
        await Assert.That(output).Contains("StartAnalyzeCommand");
        await Assert.That(output).Contains("StartProcessCommand");
        await Assert.That(output).Contains("StartCompleteCommand");

        // Completed event handlers
        await Assert.That(output).Contains("AnalyzeCompleted");
        await Assert.That(output).Contains("ProcessCompleted");
        await Assert.That(output).Contains("CompleteCompleted");
    }

    /// <summary>
    /// Verifies that Emit emits handlers in the correct order (Start before Completed for each step).
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_EmitsHandlersInStepOrder()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(stepNames: ["First", "Second"]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();

        // Start handler should come before completed for the same step
        var startFirstIndex = output.IndexOf("StartFirstCommand", StringComparison.Ordinal);
        var completedFirstIndex = output.IndexOf("FirstCompleted", StringComparison.Ordinal);
        await Assert.That(startFirstIndex).IsLessThan(completedFirstIndex);

        // First step handlers should come before second step handlers
        var startSecondIndex = output.IndexOf("StartSecondCommand", StringComparison.Ordinal);
        await Assert.That(completedFirstIndex).IsLessThan(startSecondIndex);
    }

    /// <summary>
    /// Verifies that the last step handler calls MarkCompleted.
    /// </summary>
    [Test]
    public async Task Emit_LastStep_EmitsMarkCompletedHandler()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel(stepNames: ["FirstStep", "LastStep"]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();
        await Assert.That(output).Contains("MarkCompleted();");
    }

    // ====================================================================
    // Section D: Loop Handling Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit emits loop completed handler for last step in loop body.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithLoop_EmitsLoopCompletedHandlerForLastLoopStep()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();
        var loop = LoopModel.Create(
            loopName: "Refinement",
            conditionId: "TestWorkflow-Refinement",
            maxIterations: 5,
            bodySteps:
            [
                StepModel.Create("Analyze", "TestNamespace.Analyze"),
                StepModel.Create("Refine", "TestNamespace.Refine"),
            ],
            continuationStepName: "Complete",
            parentLoopName: null);
        var model = CreateMinimalModel(
            stepNames: ["Analyze", "Refine", "Complete"],
            loops: [loop]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();

        // Should contain loop-related condition check
        await Assert.That(output).Contains("ShouldExitRefinementLoop");
        await Assert.That(output).Contains("RefinementIterationCount");
    }

    // ====================================================================
    // Section E: Branch Handling Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit emits routing handler after branch step.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithBranch_EmitsRoutingHandlerAfterBranchStep()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();
        var branchCase = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process"],
            isTerminal: false);
        var branch = BranchModel.Create(
            branchId: "Status",
            previousStepName: "Validate",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            rejoinStepName: "Complete",
            cases: [branchCase]);
        var model = CreateMinimalModel(
            stepNames: ["Validate", "Approved_Process", "Complete"],
            branches: [branch]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();

        // Should contain switch expression for routing
        await Assert.That(output).Contains("State.Status switch");
        await Assert.That(output).Contains("OrderStatus.Approved");
    }

    /// <summary>
    /// Verifies that Emit emits path end handler for last step in branch path.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithBranchPath_EmitsPathEndHandler()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();
        var branchCase = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process", "Approved_Complete"],
            isTerminal: false);
        var branch = BranchModel.Create(
            branchId: "Status",
            previousStepName: "Validate",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            rejoinStepName: "Finalize",
            cases: [branchCase]);
        var model = CreateMinimalModel(
            stepNames: ["Validate", "Approved_Process", "Approved_Complete", "Finalize"],
            branches: [branch]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();

        // Should contain handler for last branch step
        await Assert.That(output).Contains("Approved_CompleteCompleted");

        // Should route to rejoin step
        await Assert.That(output).Contains("StartFinalizeCommand");
    }

    // ====================================================================
    // Section F: Validation Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit emits yield-based handler for step with validation.
    /// </summary>
    [Test]
    public async Task Emit_StepWithValidation_EmitsYieldBasedHandler()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();
        var sb = new StringBuilder();
        var step = StepModel.Create(
            stepName: "Process",
            stepTypeName: "Test.ProcessStep",
            validationPredicate: "state.IsReady",
            validationErrorMessage: "State is not ready for processing");
        var model = CreateMinimalModel(
            stepNames: ["Process", "Complete"],
            steps: [step]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();
        await Assert.That(output).Contains("IEnumerable<object>");
        await Assert.That(output).Contains("yield return");
        await Assert.That(output).Contains("State.IsReady");
    }

    // ====================================================================
    // Helper Methods
    // ====================================================================

    private static WorkflowModel CreateMinimalModel(
        IReadOnlyList<string>? stepNames = null,
        IReadOnlyList<LoopModel>? loops = null,
        IReadOnlyList<BranchModel>? branches = null,
        IReadOnlyList<StepModel>? steps = null)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: stepNames ?? ["Step1", "Step2"],
            StateTypeName: "TestState",
            Version: 1,
            Loops: loops,
            Branches: branches,
            Steps: steps);
    }

    // ====================================================================
    // Section Z: Successor Resolution Across The Off-Main-Flow Classification
    // ====================================================================
    //
    // Three places resolve a step's successor: this emitter's handler context, the
    // workflow model's main-flow lookup (reached through low-confidence chain rejoin),
    // and the approval resume. Each must take its skip set from the shared
    // classification; a private skip list in any of them re-opens the defect for every
    // construct that list does not know about.

    /// <summary>
    /// For each construct that appends names after the declared terminal, the terminal's
    /// completed handler marks the saga completed instead of chaining into the appended step.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Successor_TerminalFollowedByOffMainFlow_ResolvesToNull()
    {
        foreach (var (source, model) in TerminalFollowedByEachAppendingSource())
        {
            var classification = MainFlowClassification.For(model);

            await Assert.That(classification.NextMainFlowStepNameAfter("ShipOrder"))
                .IsNull()
                .Because($"nothing after the terminal is on the main flow when the appended step came from {source}");

            var output = EmitStepHandlers(model);

            await Assert.That(CompletedHandlerBodyFor(output, "ShipOrder"))
                .Contains("MarkCompleted();")
                .Because($"the terminal must complete the saga when followed by a step appended by {source}");
        }
    }

    /// <summary>
    /// All three successor scans resolve the same successor. Each is observed through what it
    /// actually produces, so re-introducing a private skip list in any of them turns this red.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SuccessorScans_AllThree_AgreeOnEveryStep()
    {
        var model = CreateApprovalAndForkModel();
        var classification = MainFlowClassification.For(model);

        var afterJoin = classification.NextMainFlowStepNameAfter("ConfirmAllocation");

        await Assert.That(afterJoin)
            .IsEqualTo("ShipOrder")
            .Because("every entry between the join and the terminal is a fork-path step");

        // Scan one: the workflow model's main-flow lookup, observed through the rejoin target
        // of the low-confidence handler chain gated on the join step.
        var (_, _, rejoinStepName) = model.GetConfidenceHandlerChainRouting("ReviewAllocation");

        await Assert.That(rejoinStepName)
            .IsEqualTo(afterJoin)
            .Because("the model's main-flow lookup must agree with the shared classification");

        // Scan two: this emitter's handler context, observed through the terminal's completed
        // handler. The terminal is followed only by the lowered handler step, so it has no
        // successor and must complete the saga.
        await Assert.That(classification.NextMainFlowStepNameAfter("ShipOrder")).IsNull();

        var shipOrder = CompletedHandlerBodyFor(EmitStepHandlers(model), "ShipOrder");

        await Assert.That(shipOrder)
            .Contains("MarkCompleted();")
            .Because("the handler context must agree with the shared classification");

        await Assert.That(shipOrder)
            .DoesNotContain("StartReviewAllocationCommand")
            .Because("a lowered handler step is never a main-flow successor");

        // Scan three: the approval resume, observed through the command it returns.
        await Assert.That(EmitApprovalHandlers(model))
            .Contains($"Start{afterJoin}Command")
            .Because("the approval resume must agree with the shared classification");
    }

    /// <summary>
    /// An approval whose gated step is immediately followed in the step-name list by a fork-path
    /// step resumes onto the next MAIN-FLOW step. Indexing the list raw resumes onto the path
    /// step, bypassing the fork dispatch handler and leaving the fork's path status pending
    /// forever.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApprovalResume_TargetFollowedByForkPath_SkipsToMainFlow()
    {
        var model = CreateApprovalAndForkModel();

        var output = EmitApprovalHandlers(model);

        await Assert.That(output)
            .Contains("StartShipOrderCommand")
            .Because("ShipOrder is the next main-flow step after the approved checkpoint");

        await Assert.That(output)
            .DoesNotContain("StartChargePaymentCommand")
            .Because("ChargePayment sits on a fork path and is reached only through the fork dispatch");
    }

    /// <summary>
    /// A step in the middle of a fork path chains to the next step of its OWN path. Only a
    /// path's last step is intercepted by a path-end handler, so an intermediate path step
    /// falls through to the generic completed handler — and applying the main-flow skip set to
    /// it would either send it down the main flow or, with nothing later, complete the saga in
    /// the middle of the path.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkPath_IntermediateStep_ChainsWithinItsOwnPath()
    {
        var result = GeneratorTestHelper.RunGenerator(MultiStepForkPathWorkflow);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "FulfilOrderSaga.g.cs");

        var chargePayment = CompletedHandlerBodyFor(saga, "ChargePayment");

        await Assert.That(chargePayment)
            .Contains("StartCapturePaymentCommand")
            .Because("CapturePayment is the next step of the same fork path");

        await Assert.That(chargePayment)
            .DoesNotContain("MarkCompleted();")
            .Because("completing the saga in the middle of a fork path strands the other path");

        await Assert.That(chargePayment)
            .DoesNotContain("StartShipOrderCommand")
            .Because("a path step must never chain onto the main flow");

        await Assert.That(CompletedHandlerBodyFor(saga, "ShipOrder"))
            .Contains("MarkCompleted();")
            .Because("the declared terminal still completes the saga");
    }

    /// <summary>
    /// A step in the middle of a branch case chains to the next step of its OWN case.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchCase_IntermediateStep_ChainsWithinItsOwnCase()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithMultiStepBranch);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "ProcessTicketSaga.g.cs");

        var assignAgent = CompletedHandlerBodyFor(saga, "AssignAgent");

        await Assert.That(assignAgent)
            .Contains("StartEscalateToManagerCommand")
            .Because("EscalateToManager is the next step of the same branch case");

        await Assert.That(assignAgent)
            .DoesNotContain("MarkCompleted();")
            .Because("completing the saga in the middle of a branch case skips the rest of the case");

        await Assert.That(CompletedHandlerBodyFor(saga, "EscalateToManager"))
            .Contains("StartNotifyCustomerCommand")
            .Because("the case continues to its own last step");

        await Assert.That(CompletedHandlerBodyFor(saga, "CloseTicket"))
            .Contains("MarkCompleted();")
            .Because("the declared terminal still completes the saga");
    }

    // ====================================================================
    // Successor Resolution Helpers
    // ====================================================================

    /// <summary>
    /// A fork whose first path runs two steps, so the path has a step that is neither its first
    /// nor its last — the shape no fork fixture in the corpus had.
    /// </summary>
    private const string MultiStepForkPathWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record FulfilmentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ChargePayment : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class CapturePayment : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ReserveInventory : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ConfirmAllocation : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ShipOrder : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        [Workflow("fulfil-order")]
        public static partial class FulfilOrderWorkflow
        {
            public static WorkflowDefinition<FulfilmentState> Definition => Workflow<FulfilmentState>
                .Create("fulfil-order")
                .StartWith<ValidateOrder>()
                .Fork(
                    path => path.Then<ChargePayment>().Then<CapturePayment>(),
                    path => path.Then<ReserveInventory>())
                .Join<ConfirmAllocation>()
                .Finally<ShipOrder>();
        }
        """;

    /// <summary>
    /// One model per construct that appends step names after the declared terminal, each with
    /// <c>ShipOrder</c> as its terminal.
    /// </summary>
    /// <returns>The labelled models.</returns>
    private static IEnumerable<(string Source, WorkflowModel Model)> TerminalFollowedByEachAppendingSource()
    {
        yield return ("a failure handler", new WorkflowModel(
            WorkflowName: "fulfil-order",
            PascalName: "FulfilOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ShipOrder", "RefundPayment"],
            StateTypeName: "FulfilmentState",
            FailureHandlers:
            [
                FailureHandlerModel.Create(
                    handlerId: "refund-order",
                    scope: FailureHandlerScope.Workflow,
                    stepNames: ["RefundPayment"],
                    isTerminal: true),
            ]));

        yield return ("a fork path", new WorkflowModel(
            WorkflowName: "fulfil-order",
            PascalName: "FulfilOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ConfirmAllocation", "ShipOrder", "ChargePayment", "ReserveInventory"],
            StateTypeName: "FulfilmentState",
            Forks: [CreateFulfilmentFork()]));

        yield return ("a branch a loop runs on exit", new WorkflowModel(
            WorkflowName: "fulfil-order",
            PascalName: "FulfilOrder",
            Namespace: "TestNamespace",
            StepNames: ["Fulfilment_AllocateStock", "ShipOrder", "ExpediteShipment"],
            StateTypeName: "FulfilmentState",
            Loops: [CreateLoopWithExitBranch()]));

        yield return ("an approval rejection handler", new WorkflowModel(
            WorkflowName: "fulfil-order",
            PascalName: "FulfilOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ShipOrder", "CancelOrder"],
            StateTypeName: "FulfilmentState",
            ApprovalPoints:
            [
                ApprovalModel.Create(
                    approvalPointName: "CreditReview",
                    approverTypeName: "TestNamespace.CreditApprover",
                    precedingStepName: "ValidateOrder",
                    rejectionSteps: [StepModel.Create("CancelOrder", "TestNamespace.CancelOrder")]),
            ]));

        yield return ("a low-confidence handler chain", new WorkflowModel(
            WorkflowName: "fulfil-order",
            PascalName: "FulfilOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ShipOrder", "ReviewAllocation"],
            StateTypeName: "FulfilmentState",
            ConfidenceHandlerStepNames: ["ReviewAllocation"]));
    }

    /// <summary>
    /// A workflow whose approved checkpoint is gated on the fork's join step, with the fork's
    /// path steps appended immediately after it and a low-confidence chain rejoining from it.
    /// </summary>
    /// <returns>The workflow model.</returns>
    private static WorkflowModel CreateApprovalAndForkModel()
    {
        var reviewAllocation = StepModel.Create("ReviewAllocation", "TestNamespace.ReviewAllocation");

        var confirmAllocation = StepModel.Create("ConfirmAllocation", "TestNamespace.ConfirmAllocation") with
        {
            Confidence = new ConfidenceModel(
                Threshold: 0.8,
                OnLowConfidenceHandlerStep: reviewAllocation,
                OnLowConfidenceHandlerChain: new LowConfidenceHandlerChainModel(
                    Steps: [reviewAllocation],
                    RejoinsMainFlow: true)),
        };

        return new WorkflowModel(
            WorkflowName: "fulfil-order",
            PascalName: "FulfilOrder",
            Namespace: "TestNamespace",
            StepNames:
            [
                "ValidateOrder",
                "ConfirmAllocation",
                "ChargePayment",
                "ReserveInventory",
                "ShipOrder",
                "ReviewAllocation",
            ],
            StateTypeName: "FulfilmentState",
            Steps:
            [
                StepModel.Create("ValidateOrder", "TestNamespace.ValidateOrder"),
                confirmAllocation,
                StepModel.Create("ShipOrder", "TestNamespace.ShipOrder"),
            ],
            ApprovalPoints:
            [
                ApprovalModel.Create(
                    approvalPointName: "CreditReview",
                    approverTypeName: "TestNamespace.CreditApprover",
                    precedingStepName: "ConfirmAllocation"),
            ],
            Forks: [CreateFulfilmentFork()],
            ConfidenceHandlerStepNames: ["ReviewAllocation"]);
    }

    /// <summary>
    /// A fork over two single-step paths joining at <c>ConfirmAllocation</c>.
    /// </summary>
    /// <returns>The fork model.</returns>
    private static ForkModel CreateFulfilmentFork() =>
        ForkModel.Create(
            forkId: "fulfilment",
            previousStepName: "ValidateOrder",
            paths:
            [
                ForkPathModel.Create(
                    pathIndex: 0,
                    steps: [StepModel.Create("ChargePayment", "TestNamespace.ChargePayment")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
                ForkPathModel.Create(
                    pathIndex: 1,
                    steps: [StepModel.Create("ReserveInventory", "TestNamespace.ReserveInventory")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
            ],
            joinStepName: "ConfirmAllocation");

    /// <summary>
    /// A repeat-until loop carrying a branch that runs when the loop exits.
    /// </summary>
    /// <returns>The loop model.</returns>
    private static LoopModel CreateLoopWithExitBranch() =>
        LoopModel.Create(
            loopName: "Fulfilment",
            conditionId: "FulfilOrder-Fulfilment",
            maxIterations: 5,
            bodySteps: [StepModel.Create("AllocateStock", "TestNamespace.AllocateStock", loopName: "Fulfilment")],
            continuationStepName: null,
            parentLoopName: null,
            branchOnExitId: "fulfilment-exit",
            branchOnExit: BranchModel.Create(
                branchId: "fulfilment-exit",
                previousStepName: "Fulfilment_AllocateStock",
                discriminatorPropertyPath: "OrderKind",
                discriminatorTypeName: "TestNamespace.OrderKind",
                isEnumDiscriminator: true,
                isMethodDiscriminator: false,
                cases:
                [
                    BranchCaseModel.Create("OrderKind.Express", "Express", ["ExpediteShipment"], isTerminal: false),
                ],
                rejoinStepName: "ShipOrder"));

    /// <summary>
    /// Emits the step handlers for a model.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <returns>The emitted source.</returns>
    private static string EmitStepHandlers(WorkflowModel model)
    {
        var sb = new StringBuilder();
        new SagaStepHandlersEmitter().Emit(sb, model);
        return sb.ToString();
    }

    /// <summary>
    /// Emits the approval resume handlers for a model.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <returns>The emitted source.</returns>
    private static string EmitApprovalHandlers(WorkflowModel model)
    {
        var sb = new StringBuilder();
        new SagaApprovalComponentEmitter().Emit(sb, model);
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the body of the handler for a step's completed event, so an assertion about one
    /// step's routing cannot be satisfied by text belonging to a different step.
    /// </summary>
    /// <param name="source">The emitted saga source.</param>
    /// <param name="stepName">The step whose completed handler is wanted.</param>
    /// <returns>The handler body.</returns>
    private static string CompletedHandlerBodyFor(string source, string stepName)
    {
        var marker = $"{stepName}Completed evt";
        var start = source.IndexOf(marker, StringComparison.Ordinal);

        if (start < 0)
        {
            return string.Empty;
        }

        var next = source.IndexOf("    /// <summary>", start, StringComparison.Ordinal);
        return next < 0 ? source.Substring(start) : source.Substring(start, next - start);
    }
}
