// -----------------------------------------------------------------------
// <copyright file="SagaEmissionContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

using TUnit.Core;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for <see cref="SagaEmissionContext"/>.
/// </summary>
[Property("Category", "Unit")]
public class SagaEmissionContextTests
{
    // ====================================================================
    // Section A: Guard Clause Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create throws ArgumentNullException when model is null.
    /// </summary>
    [Test]
    public async Task Create_NullModel_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => SagaEmissionContext.Create(null!))
            .Throws<ArgumentNullException>();
    }

    // ====================================================================
    // Section B: Basic Creation Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns a non-null context for a valid model.
    /// </summary>
    [Test]
    public async Task Create_ValidModel_ReturnsNonNullContext()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context).IsNotNull();
    }

    /// <summary>
    /// Verifies that Create stores the model.
    /// </summary>
    [Test]
    public async Task Create_ValidModel_StoresModel()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(ReferenceEquals(context.Model, model)).IsTrue();
    }

    /// <summary>
    /// Verifies that Create computes the saga class name.
    /// </summary>
    [Test]
    public async Task Create_ValidModel_ComputesSagaClassName()
    {
        // Arrange
        var model = CreateMinimalModel(pascalName: "ProcessOrder");

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.SagaClassName).IsEqualTo("ProcessOrderSaga");
    }

    /// <summary>
    /// Verifies that Create includes version suffix when version > 1.
    /// </summary>
    [Test]
    public async Task Create_ModelWithVersion_StoresSagaClassNameWithVersionSuffix()
    {
        // Arrange
        var model = CreateMinimalModel(pascalName: "ProcessOrder", version: 2);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.SagaClassName).IsEqualTo("ProcessOrderSagaV2");
    }

    // ====================================================================
    // Section C: Loop Lookup Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns empty loops dictionary when model has no loops.
    /// </summary>
    [Test]
    public async Task Create_ModelWithoutLoops_ReturnsEmptyLoopsDictionary()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.LoopsByLastStep).IsNotNull();
        await Assert.That(context.LoopsByLastStep.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Create builds loops lookup keyed by last step name.
    /// </summary>
    [Test]
    public async Task Create_ModelWithLoops_BuildsLoopsByLastStepLookup()
    {
        // Arrange
        var loop = CreateLoop("Refinement", lastBodyStepName: "Refine");
        var model = CreateMinimalModel(loops: [loop]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.LoopsByLastStep.ContainsKey("Refine")).IsTrue();
        await Assert.That(context.LoopsByLastStep["Refine"].Count).IsEqualTo(1);
        await Assert.That(context.LoopsByLastStep["Refine"][0].LoopName).IsEqualTo("Refinement");
    }

    /// <summary>
    /// Verifies that nested loops are ordered innermost first.
    /// </summary>
    [Test]
    public async Task Create_ModelWithNestedLoops_OrdersLoopsInnermostFirst()
    {
        // Arrange
        // Inner loop has parent, so its FullPrefix has more underscores (deeper nesting)
        var outerLoop = CreateLoop("Outer", lastBodyStepName: "SharedStep", parentLoopName: null);
        var innerLoop = CreateLoop("Inner", lastBodyStepName: "SharedStep", parentLoopName: "Outer");
        var model = CreateMinimalModel(loops: [outerLoop, innerLoop]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.LoopsByLastStep.ContainsKey("SharedStep")).IsTrue();
        var loops = context.LoopsByLastStep["SharedStep"];
        await Assert.That(loops.Count).IsEqualTo(2);
        await Assert.That(loops[0].LoopName).IsEqualTo("Inner"); // Innermost first
        await Assert.That(loops[1].LoopName).IsEqualTo("Outer"); // Outermost second
    }

    // ====================================================================
    // Section D: Branch Lookup Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns empty branches dictionary when model has no branches.
    /// </summary>
    [Test]
    public async Task Create_ModelWithoutBranches_ReturnsEmptyBranchesDictionary()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchesByPreviousStep).IsNotNull();
        await Assert.That(context.BranchesByPreviousStep.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Create builds branches lookup keyed by previous step name.
    /// </summary>
    [Test]
    public async Task Create_ModelWithBranches_BuildsBranchesByPreviousStepLookup()
    {
        // Arrange
        var branch = CreateBranch("Status", previousStepName: "Validate");
        var model = CreateMinimalModel(branches: [branch]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchesByPreviousStep.ContainsKey("Validate")).IsTrue();
        await Assert.That(context.BranchesByPreviousStep["Validate"].BranchId).IsEqualTo("Status");
    }

    /// <summary>
    /// Verifies that Create builds branch path info for non-terminal branch cases.
    /// </summary>
    [Test]
    public async Task Create_ModelWithBranchPaths_BuildsBranchPathInfoLookup()
    {
        // Arrange
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
        var model = CreateMinimalModel(branches: [branch]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchPathInfo.ContainsKey("Approved_Complete")).IsTrue();
        var pathInfo = context.BranchPathInfo["Approved_Complete"];
        await Assert.That(pathInfo.Branch.BranchId).IsEqualTo("Status");
        await Assert.That(pathInfo.Case.BranchPathPrefix).IsEqualTo("Approved");
    }

    /// <summary>
    /// Verifies that a workflow-ending branch case is included in path info even when a sibling
    /// case gives the branch a convergence point.
    /// </summary>
    /// <remarks>
    /// This lookup is the only live route into the path-end handler when the workflow is authored in
    /// C#. Excluding an ending case leaves its last step to the ordinary step handler, where
    /// terminality is decided by position in the step list rather than by the case's declaration —
    /// so the ending is right only by accident, and stops being right as soon as the list order
    /// changes. The mixed shape is asserted because it is the one an exclusion filter keyed on
    /// terminality would drop while a rejoining sibling keeps the branch-level flag true (#175).
    /// </remarks>
    [Test]
    public async Task Create_ModelWithTerminalBranch_IncludesInPathInfo()
    {
        // Arrange
        var terminalCase = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Rejected",
            branchPathPrefix: "Rejected",
            stepNames: ["Rejected_Handle"],
            isTerminal: true);
        var rejoiningCase = BranchCaseModel.Create(
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
            rejoinStepName: "Finalize",
            cases: [rejoiningCase, terminalCase]);
        var model = CreateMinimalModel(branches: [branch]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.BranchPathInfo.ContainsKey("Rejected_Handle")).IsTrue();
        await Assert.That(context.BranchPathInfo["Rejected_Handle"].Case.IsTerminal).IsTrue();
        await Assert.That(context.BranchPathInfo.ContainsKey("Approved_Process")).IsTrue();
        await Assert.That(context.BranchPathInfo["Approved_Process"].Case.IsTerminal).IsFalse();
    }

    /// <summary>
    /// Cases of a branch a loop evaluates on exit are included in path info even though that
    /// branch is attached to the loop and absent from the workflow's branch collection.
    /// </summary>
    /// <remarks>
    /// Walking only <c>model.Branches</c> leaves a rejoining loop-exit case with no path-end
    /// handler, so the declared terminal never starts (#184).
    /// </remarks>
    [Test]
    public async Task Create_LoopExitBranch_IncludesCasesInBranchPathInfo()
    {
        var loop = CreateFulfilmentLoopWithExitBranch();
        var model = CreateMinimalModel(
            stepNames:
            [
                "Fulfilment_AllocateStock", "Fulfilment_VerifyStock", "ShipOrder", "ExpediteShipment", "HoldShipment",
            ],
            loops: [loop]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(model.HasBranches)
            .IsFalse()
            .Because("a branch that follows a repeat-until loop is attached to the loop, not to the workflow");

        await Assert.That(context.BranchPathInfo.ContainsKey("ExpediteShipment")).IsTrue();
        await Assert.That(context.BranchPathInfo["ExpediteShipment"].Case.IsTerminal).IsFalse();
        await Assert.That(context.BranchPathInfo["ExpediteShipment"].Branch.RejoinStepName)
            .IsEqualTo("ShipOrder");
        await Assert.That(context.BranchPathInfo.ContainsKey("HoldShipment")).IsTrue();
        await Assert.That(context.BranchPathInfo["HoldShipment"].Case.IsTerminal).IsFalse();
    }

    /// <summary>
    /// Mixed loop-exit cases are both admitted: the rejoining case so it can dispatch the
    /// declared terminal, and the <c>.Complete()</c> sibling so it still marks the saga completed.
    /// </summary>
    [Test]
    public async Task Create_MixedLoopExitCases_IncludesRejoiningAndTerminalInPathInfo()
    {
        var exitBranch = BranchModel.Create(
            branchId: "claim-exit",
            previousStepName: "Investigation_GatherEvidence",
            discriminatorPropertyPath: "Outcome",
            discriminatorTypeName: "ClaimOutcome",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases:
            [
                BranchCaseModel.Create("ClaimOutcome.Pay", "Pay", ["PayClaim"], isTerminal: false),
                BranchCaseModel.Create("ClaimOutcome.Deny", "Deny", ["DenyClaim"], isTerminal: true),
            ],
            rejoinStepName: "CloseClaim");

        var loop = LoopModel.Create(
            loopName: "Investigation",
            conditionId: "SettleClaim-Investigation",
            maxIterations: 5,
            bodySteps: [StepModel.Create("GatherEvidence", "TestNamespace.GatherEvidence", loopName: "Investigation")],
            continuationStepName: null,
            parentLoopName: null,
            branchOnExitId: "claim-exit",
            branchOnExit: exitBranch);

        var model = CreateMinimalModel(
            stepNames: ["OpenClaim", "Investigation_GatherEvidence", "CloseClaim", "PayClaim", "DenyClaim"],
            loops: [loop]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.BranchPathInfo.ContainsKey("PayClaim")).IsTrue();
        await Assert.That(context.BranchPathInfo["PayClaim"].Case.IsTerminal).IsFalse();
        await Assert.That(context.BranchPathInfo["PayClaim"].Branch.RejoinStepName).IsEqualTo("CloseClaim");
        await Assert.That(context.BranchPathInfo.ContainsKey("DenyClaim")).IsTrue();
        await Assert.That(context.BranchPathInfo["DenyClaim"].Case.IsTerminal).IsTrue();
    }

    // ====================================================================
    // Section E: Step Lookup Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Create returns empty steps dictionary when model has no steps.
    /// </summary>
    [Test]
    public async Task Create_ModelWithoutSteps_ReturnsEmptyStepsDictionary()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.StepsByName).IsNotNull();
        await Assert.That(context.StepsByName.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that Create builds steps lookup keyed by step name.
    /// </summary>
    [Test]
    public async Task Create_ModelWithSteps_BuildsStepsByNameLookup()
    {
        // Arrange
        var step = StepModel.Create(
            stepName: "Validate",
            stepTypeName: "Test.ValidateStep",
            validationPredicate: "state.IsValid",
            validationErrorMessage: "State is not valid");
        var model = CreateMinimalModel(steps: [step]);

        // Act
        var context = SagaEmissionContext.Create(model);

        // Assert
        await Assert.That(context.StepsByName.ContainsKey("Validate")).IsTrue();
        await Assert.That(context.StepsByName["Validate"].HasValidation).IsTrue();
    }

    // ====================================================================
    // Section G: Off-Main-Flow Classification
    // ====================================================================
    //
    // Several lowering blocks append names to the workflow's step-name list so the
    // appended steps get a phase, a worker handler, commands and events. Those steps
    // are reached through their own construct, never by main-flow chaining, so a
    // successor scan that treats every later entry as a candidate strands the declared
    // terminal. The classification below is the single source those scans consult.

    /// <summary>
    /// A workflow with no branching construct contributes nothing off the main flow.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_LinearWorkflow_IsEmpty()
    {
        var model = CreateMinimalModel();

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.OffMainFlowSteps).IsEmpty();
    }

    /// <summary>
    /// Every contributing construct is classified from one derivation: fork paths, branch
    /// cases, the cases of a branch a loop runs on exit, failure-handler steps, approval
    /// rejection and escalation steps, and lowered low-confidence handler steps.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_AllFiveSources_ContainsEveryAppendedName()
    {
        var model = CreateFulfilmentModelWithEveryOffMainFlowSource();

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.OffMainFlowSteps.OrderBy(n => n, StringComparer.Ordinal).ToList())
            .IsEquivalentTo(new[]
            {
                "AbandonOrder",
                "CancelOrder",
                "ChargePayment",
                "EscalateToManager",
                "ExpediteShipment",
                "HoldShipment",
                "NotifyCustomer",
                "RefundPayment",
                "ReserveInventory",
                "ReviewAllocation",
            }.OrderBy(n => n, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// No main-flow step is classified off the main flow, including the loop body steps and
    /// the declared terminal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_AllFiveSources_ExcludesEveryMainFlowName()
    {
        var model = CreateFulfilmentModelWithEveryOffMainFlowSource();

        var context = SagaEmissionContext.Create(model);

        foreach (var mainFlowStep in new[]
                 {
                     "ValidateOrder",
                     "Fulfilment_AllocateStock",
                     "Fulfilment_VerifyStock",
                     "ConfirmAllocation",
                     "ShipOrder",
                 })
        {
            await Assert.That(context.OffMainFlowSteps.Contains(mainFlowStep))
                .IsFalse()
                .Because($"{mainFlowStep} is on the main flow");
        }
    }

    /// <summary>
    /// A fork's path steps are off the main flow.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_ForkPathSteps_AreClassified()
    {
        var model = CreateMinimalModel(
            stepNames: ["ValidateOrder", "ConfirmAllocation", "ShipOrder", "ChargePayment", "ReserveInventory"],
            forks: [CreateFork()]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.OffMainFlowSteps.Contains("ChargePayment")).IsTrue();
        await Assert.That(context.OffMainFlowSteps.Contains("ReserveInventory")).IsTrue();
    }

    /// <summary>
    /// A fork's JOIN step stays on the main flow. It is the step that resumes the workflow
    /// once every path has completed, so its handler is the one that must chain to the
    /// terminal — which is why the worker-command naming set, which deliberately includes the
    /// join, cannot be reused as the off-main-flow set.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_ForkJoinStep_StaysOnMainFlow()
    {
        var model = CreateMinimalModel(
            stepNames: ["ValidateOrder", "ConfirmAllocation", "ShipOrder", "ChargePayment", "ReserveInventory"],
            forks: [CreateFork()]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.ForkPathSteps.Contains("ConfirmAllocation"))
            .IsTrue()
            .Because("the worker-command naming set deliberately carries the join step");

        await Assert.That(context.OffMainFlowSteps.Contains("ConfirmAllocation"))
            .IsFalse()
            .Because("the join resumes the main flow and must remain a chaining target");
    }

    /// <summary>
    /// A branch case's steps are off the main flow.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_BranchCaseSteps_AreClassified()
    {
        var branch = BranchModel.Create(
            branchId: "claim-type",
            previousStepName: "ValidateOrder",
            discriminatorPropertyPath: "OrderKind",
            discriminatorTypeName: "TestNamespace.OrderKind",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases:
            [
                BranchCaseModel.Create("OrderKind.Express", "Express", ["ExpediteShipment"], isTerminal: false),
                BranchCaseModel.Create("OrderKind.Standard", "Standard", ["HoldShipment"], isTerminal: false),
            ],
            rejoinStepName: "ShipOrder");

        var model = CreateMinimalModel(
            stepNames: ["ValidateOrder", "ShipOrder", "ExpediteShipment", "HoldShipment"],
            branches: [branch]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.OffMainFlowSteps.Contains("ExpediteShipment")).IsTrue();
        await Assert.That(context.OffMainFlowSteps.Contains("HoldShipment")).IsTrue();
        await Assert.That(context.OffMainFlowSteps.Contains("ShipOrder"))
            .IsFalse()
            .Because("the rejoin step is on the main flow");
    }

    /// <summary>
    /// The cases of a branch a loop runs on exit are off the main flow. They are attached to
    /// the loop and are deliberately absent from the workflow's branch collection, so a set
    /// derived only from that collection misses this contributing construct entirely.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_LoopExitBranchCaseSteps_AreClassified()
    {
        var loop = CreateFulfilmentLoopWithExitBranch();

        var model = CreateMinimalModel(
            stepNames:
            [
                "Fulfilment_AllocateStock", "Fulfilment_VerifyStock", "ShipOrder", "ExpediteShipment", "HoldShipment",
            ],
            loops: [loop]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(model.Branches)
            .IsNull()
            .Because("a branch that follows a repeat-until loop is attached to the loop, not to the workflow");

        await Assert.That(context.OffMainFlowSteps.Contains("ExpediteShipment")).IsTrue();
        await Assert.That(context.OffMainFlowSteps.Contains("HoldShipment")).IsTrue();
    }

    /// <summary>
    /// Failure-handler steps are off the main flow.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_FailureHandlerSteps_AreClassified()
    {
        var model = CreateMinimalModel(
            stepNames: ["ValidateOrder", "ShipOrder", "RefundPayment", "NotifyCustomer"],
            failureHandlers: [CreateRefundFailureHandler()]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.OffMainFlowSteps.Contains("RefundPayment")).IsTrue();
        await Assert.That(context.OffMainFlowSteps.Contains("NotifyCustomer")).IsTrue();
    }

    /// <summary>
    /// Approval rejection and escalation steps are off the main flow, including those of a
    /// nested escalation approval.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_ApprovalRejectionAndEscalationSteps_AreClassified()
    {
        var model = CreateMinimalModel(
            stepNames:
            [
                "ValidateOrder", "ShipOrder", "CancelOrder", "EscalateToManager", "AbandonOrder",
            ],
            approvalPoints: [CreateCreditReviewApproval()]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.OffMainFlowSteps.Contains("CancelOrder")).IsTrue();
        await Assert.That(context.OffMainFlowSteps.Contains("EscalateToManager")).IsTrue();
        await Assert.That(context.OffMainFlowSteps.Contains("AbandonOrder"))
            .IsTrue()
            .Because("a nested escalation approval contributes its own rejection steps");
    }

    /// <summary>
    /// Lowered low-confidence handler steps are off the main flow.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OffMainFlowSteps_ConfidenceHandlerSteps_AreClassified()
    {
        var model = CreateMinimalModel(
            stepNames: ["ValidateOrder", "ShipOrder", "ReviewAllocation"],
            confidenceHandlerStepNames: ["ReviewAllocation"]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.OffMainFlowSteps.Contains("ReviewAllocation")).IsTrue();
    }

    /// <summary>
    /// A non-last step of a multi-step fork path chains to the next step of that same path.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SuccessorWithinPath_MultiStepForkPath_IsTheNextStepOfThatPath()
    {
        var fork = ForkModel.Create(
            forkId: "fulfilment",
            previousStepName: "ValidateOrder",
            paths:
            [
                ForkPathModel.Create(
                    pathIndex: 0,
                    steps:
                    [
                        StepModel.Create("ChargePayment", "TestNamespace.ChargePayment"),
                        StepModel.Create("CapturePayment", "TestNamespace.CapturePayment"),
                    ],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
                ForkPathModel.Create(
                    pathIndex: 1,
                    steps: [StepModel.Create("ReserveInventory", "TestNamespace.ReserveInventory")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
            ],
            joinStepName: "ConfirmAllocation");

        var model = CreateMinimalModel(stepNames: ["ValidateOrder", "ConfirmAllocation", "ShipOrder"], forks: [fork]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.MainFlow.TryGetSuccessorWithinPath("ChargePayment", out var successor)).IsTrue();
        await Assert.That(successor).IsEqualTo("CapturePayment");

        await Assert.That(context.MainFlow.TryGetSuccessorWithinPath("CapturePayment", out _))
            .IsFalse()
            .Because("a path's last step is intercepted by its own path-end handler");
    }

    /// <summary>
    /// A non-last step of a multi-step branch case chains to the next step of that same case.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SuccessorWithinPath_MultiStepBranchCase_IsTheNextStepOfThatCase()
    {
        var branch = BranchModel.Create(
            branchId: "order-kind",
            previousStepName: "ValidateOrder",
            discriminatorPropertyPath: "OrderKind",
            discriminatorTypeName: "TestNamespace.OrderKind",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases:
            [
                BranchCaseModel.Create(
                    "OrderKind.Express",
                    "Express",
                    ["ExpediteShipment", "NotifyCarrier"],
                    isTerminal: false),
            ],
            rejoinStepName: "ShipOrder");

        var model = CreateMinimalModel(
            stepNames: ["ValidateOrder", "ShipOrder", "ExpediteShipment", "NotifyCarrier"],
            branches: [branch]);

        var context = SagaEmissionContext.Create(model);

        await Assert.That(context.MainFlow.TryGetSuccessorWithinPath("ExpediteShipment", out var successor)).IsTrue();
        await Assert.That(successor).IsEqualTo("NotifyCarrier");

        await Assert.That(context.MainFlow.TryGetSuccessorWithinPath("NotifyCarrier", out _))
            .IsFalse()
            .Because("a case's last step is intercepted by its own path-end handler");
    }

    // ====================================================================
    // Section H: Identity-carrying routing keys
    // ====================================================================

    /// <summary>
    /// Two fork paths that end on instance-named steps of the same type each occupy
    /// <see cref="SagaEmissionContext.ForkPathInfo"/> under their phase name.
    /// </summary>
    [Test]
    public async Task Create_ForkPathsWithInstanceNames_KeysPathInfoByPhaseName()
    {
        var fork = ForkModel.Create(
            forkId: "analysis",
            previousStepName: "Prepare",
            paths:
            [
                ForkPathModel.Create(
                    pathIndex: 0,
                    steps: [StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "TechnicalAnalysis")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
                ForkPathModel.Create(
                    pathIndex: 1,
                    steps: [StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "FundamentalAnalysis")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
            ],
            joinStepName: "Synthesize");

        var context = SagaEmissionContext.Create(CreateMinimalModel(forks: [fork]));

        await Assert.That(context.ForkPathInfo.ContainsKey("TechnicalAnalysis")).IsTrue();
        await Assert.That(context.ForkPathInfo["TechnicalAnalysis"].Path.PathIndex).IsEqualTo(0);
        await Assert.That(context.ForkPathInfo.ContainsKey("FundamentalAnalysis")).IsTrue();
        await Assert.That(context.ForkPathInfo["FundamentalAnalysis"].Path.PathIndex).IsEqualTo(1);
        await Assert.That(context.ForkPathInfo.ContainsKey("AnalyzeStep")).IsFalse();
    }

    /// <summary>
    /// Two fork paths that share a bare type still last-write-win in
    /// <see cref="SagaEmissionContext.ForkPathInfo"/>, but each has its own
    /// <see cref="SagaEmissionContext.ForkPathsByRoutingKey"/> entry.
    /// </summary>
    [Test]
    public async Task Create_ForkPathsSharingType_RoutingKeyMapKeepsBothEnds()
    {
        var fork = ForkModel.Create(
            forkId: "analysis",
            previousStepName: "Prepare",
            paths:
            [
                ForkPathModel.Create(
                    pathIndex: 0,
                    steps: [StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
                ForkPathModel.Create(
                    pathIndex: 1,
                    steps: [StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep")],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
            ],
            joinStepName: "Synthesize");

        var context = SagaEmissionContext.Create(CreateMinimalModel(forks: [fork]));

        await Assert.That(context.ForkPathInfo.Count).IsEqualTo(1);
        await Assert.That(context.ForkPathInfo["AnalyzeStep"].Path.PathIndex).IsEqualTo(1);

        var path0 = PathRoutingKey.ForFork("analysis", 0, "AnalyzeStep");
        var path1 = PathRoutingKey.ForFork("analysis", 1, "AnalyzeStep");
        await Assert.That(context.ForkPathsByRoutingKey.ContainsKey(path0)).IsTrue();
        await Assert.That(context.ForkPathsByRoutingKey.ContainsKey(path1)).IsTrue();
        await Assert.That(context.ForkPathsByRoutingKey[path0].Path.PathIndex).IsEqualTo(0);
        await Assert.That(context.ForkPathsByRoutingKey[path1].Path.PathIndex).IsEqualTo(1);
    }

    /// <summary>
    /// Branch cases with instance names occupy <see cref="SagaEmissionContext.BranchPathInfo"/>
    /// under the effective name, not the bare type.
    /// </summary>
    [Test]
    public async Task Create_BranchCasesWithInstanceNames_KeysPathInfoByEffectiveName()
    {
        var branch = BranchModel.Create(
            branchId: "claim-type",
            previousStepName: "Intake",
            discriminatorPropertyPath: "Type",
            discriminatorTypeName: "ClaimType",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases:
            [
                BranchCaseModel.Create("ClaimType.Auto", "Auto", ["AutoClaim"], isTerminal: false),
                BranchCaseModel.Create("ClaimType.Home", "Home", ["HomeClaim"], isTerminal: false),
            ],
            rejoinStepName: "CloseClaim");

        var context = SagaEmissionContext.Create(CreateMinimalModel(branches: [branch]));

        await Assert.That(context.BranchPathInfo.ContainsKey("AutoClaim")).IsTrue();
        await Assert.That(context.BranchPathInfo.ContainsKey("HomeClaim")).IsTrue();
        await Assert.That(context.BranchPathInfo.ContainsKey("ProcessClaim")).IsFalse();

        var autoKey = PathRoutingKey.ForBranch("claim-type", "Auto", "AutoClaim");
        var homeKey = PathRoutingKey.ForBranch("claim-type", "Home", "HomeClaim");
        await Assert.That(context.BranchPathsByRoutingKey.ContainsKey(autoKey)).IsTrue();
        await Assert.That(context.BranchPathsByRoutingKey.ContainsKey(homeKey)).IsTrue();
    }

    /// <summary>
    /// A branch inside a loop keys path info by the loop-prefixed phase name so it matches
    /// the step list, while the case itself still stores the unprefixed effective name.
    /// </summary>
    [Test]
    public async Task Create_BranchInsideLoop_KeysPathInfoByPrefixedPhaseName()
    {
        var branch = BranchModel.Create(
            branchId: "loop-branch",
            previousStepName: "TargetLoop_SelectTarget",
            discriminatorPropertyPath: "Kind",
            discriminatorTypeName: "TargetKind",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases:
            [
                BranchCaseModel.Create("TargetKind.News", "News", ["NewsPath"], isTerminal: false),
            ],
            rejoinStepName: "TargetLoop_Aggregate",
            loopPrefix: "TargetLoop");

        var context = SagaEmissionContext.Create(CreateMinimalModel(branches: [branch]));

        await Assert.That(context.BranchPathInfo.ContainsKey("TargetLoop_NewsPath")).IsTrue();
        await Assert.That(context.BranchPathInfo.ContainsKey("NewsPath")).IsFalse();
        await Assert.That(context.BranchPathInfo["TargetLoop_NewsPath"].Case.LastStepName)
            .IsEqualTo("NewsPath");

        var key = PathRoutingKey.ForBranch("loop-branch", "News", "TargetLoop_NewsPath");
        await Assert.That(context.BranchPathsByRoutingKey.ContainsKey(key)).IsTrue();
    }

    /// <summary>
    /// Two fork paths that share a mid-path type keep distinct in-path successors when
    /// looked up by <see cref="PathRoutingKey"/>.
    /// </summary>
    [Test]
    public async Task SuccessorWithinPath_SharedTypeOnTwoForkPaths_RoutingKeySelectsOwnSuccessor()
    {
        var fork = ForkModel.Create(
            forkId: "fulfilment",
            previousStepName: "ValidateOrder",
            paths:
            [
                ForkPathModel.Create(
                    pathIndex: 0,
                    steps:
                    [
                        StepModel.Create("ChargePayment", "TestNamespace.ChargePayment"),
                        StepModel.Create("CapturePayment", "TestNamespace.CapturePayment"),
                    ],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
                ForkPathModel.Create(
                    pathIndex: 1,
                    steps:
                    [
                        StepModel.Create("ChargePayment", "TestNamespace.ChargePayment"),
                        StepModel.Create("ReserveInventory", "TestNamespace.ReserveInventory"),
                    ],
                    hasFailureHandler: false,
                    isTerminalOnFailure: false),
            ],
            joinStepName: "ConfirmAllocation");

        var context = SagaEmissionContext.Create(CreateMinimalModel(forks: [fork]));

        var path0 = PathRoutingKey.ForFork("fulfilment", 0, "ChargePayment");
        var path1 = PathRoutingKey.ForFork("fulfilment", 1, "ChargePayment");

        await Assert.That(context.MainFlow.TryGetSuccessorWithinPath(path0, out var successor0)).IsTrue();
        await Assert.That(successor0).IsEqualTo("CapturePayment");
        await Assert.That(context.MainFlow.TryGetSuccessorWithinPath(path1, out var successor1)).IsTrue();
        await Assert.That(successor1).IsEqualTo("ReserveInventory");
    }

    // ====================================================================
    // Helper Methods
    // ====================================================================

    /// <summary>
    /// A fulfilment workflow that carries every construct contributing off-main-flow step
    /// names, with the appended names sitting after the declared terminal exactly as the
    /// lowering blocks leave them.
    /// </summary>
    /// <returns>The workflow model.</returns>
    private static WorkflowModel CreateFulfilmentModelWithEveryOffMainFlowSource() =>
        new(
            WorkflowName: "fulfil-order",
            PascalName: "FulfilOrder",
            Namespace: "TestNamespace",
            StepNames:
            [
                "ValidateOrder",
                "Fulfilment_AllocateStock",
                "Fulfilment_VerifyStock",
                "ConfirmAllocation",
                "ShipOrder",
                "RefundPayment",
                "NotifyCustomer",
                "ChargePayment",
                "ReserveInventory",
                "ExpediteShipment",
                "HoldShipment",
                "CancelOrder",
                "EscalateToManager",
                "AbandonOrder",
                "ReviewAllocation",
            ],
            StateTypeName: "FulfilmentState",
            Loops: [CreateFulfilmentLoopWithExitBranch()],
            FailureHandlers: [CreateRefundFailureHandler()],
            ApprovalPoints: [CreateCreditReviewApproval()],
            Forks: [CreateFork()],
            ConfidenceHandlerStepNames: ["ReviewAllocation"]);

    /// <summary>
    /// A fork over two single-step paths joining at <c>ConfirmAllocation</c>.
    /// </summary>
    /// <returns>The fork model.</returns>
    private static ForkModel CreateFork() =>
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
    private static LoopModel CreateFulfilmentLoopWithExitBranch()
    {
        var exitBranch = BranchModel.Create(
            branchId: "fulfilment-exit",
            previousStepName: "Fulfilment_VerifyStock",
            discriminatorPropertyPath: "OrderKind",
            discriminatorTypeName: "TestNamespace.OrderKind",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases:
            [
                BranchCaseModel.Create("OrderKind.Express", "Express", ["ExpediteShipment"], isTerminal: false),
                BranchCaseModel.Create("OrderKind.Standard", "Standard", ["HoldShipment"], isTerminal: false),
            ],
            rejoinStepName: "ShipOrder");

        return LoopModel.Create(
            loopName: "Fulfilment",
            conditionId: "FulfilOrder-Fulfilment",
            maxIterations: 5,
            bodySteps:
            [
                StepModel.Create("AllocateStock", "TestNamespace.AllocateStock", loopName: "Fulfilment"),
                StepModel.Create("VerifyStock", "TestNamespace.VerifyStock", loopName: "Fulfilment"),
            ],
            continuationStepName: null,
            parentLoopName: null,
            branchOnExitId: "fulfilment-exit",
            branchOnExit: exitBranch);
    }

    /// <summary>
    /// A two-step failure handler.
    /// </summary>
    /// <returns>The failure handler model.</returns>
    private static FailureHandlerModel CreateRefundFailureHandler() =>
        FailureHandlerModel.Create(
            handlerId: "refund-order",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["RefundPayment", "NotifyCustomer"],
            isTerminal: true);

    /// <summary>
    /// An approval checkpoint with a rejection step, an escalation step, and a nested
    /// escalation approval that carries a rejection step of its own.
    /// </summary>
    /// <returns>The approval model.</returns>
    private static ApprovalModel CreateCreditReviewApproval()
    {
        var nested = ApprovalModel.Create(
            approvalPointName: "DirectorReview",
            approverTypeName: "TestNamespace.DirectorApprover",
            precedingStepName: "EscalateToManager",
            rejectionSteps: [StepModel.Create("AbandonOrder", "TestNamespace.AbandonOrder")]);

        return ApprovalModel.Create(
            approvalPointName: "CreditReview",
            approverTypeName: "TestNamespace.CreditApprover",
            precedingStepName: "ConfirmAllocation",
            escalationSteps: [StepModel.Create("EscalateToManager", "TestNamespace.EscalateToManager")],
            rejectionSteps: [StepModel.Create("CancelOrder", "TestNamespace.CancelOrder")],
            nestedEscalationApprovals: [nested]);
    }

    private static WorkflowModel CreateMinimalModel(
        string? pascalName = null,
        int version = 1,
        IReadOnlyList<LoopModel>? loops = null,
        IReadOnlyList<BranchModel>? branches = null,
        IReadOnlyList<StepModel>? steps = null,
        IReadOnlyList<string>? stepNames = null,
        IReadOnlyList<FailureHandlerModel>? failureHandlers = null,
        IReadOnlyList<ApprovalModel>? approvalPoints = null,
        IReadOnlyList<ForkModel>? forks = null,
        IReadOnlyList<string>? confidenceHandlerStepNames = null)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: pascalName ?? "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: stepNames ?? ["Step1", "Step2"],
            StateTypeName: "TestState",
            Version: version,
            Loops: loops,
            Branches: branches,
            FailureHandlers: failureHandlers,
            ApprovalPoints: approvalPoints,
            Forks: forks,
            ConfidenceHandlerStepNames: confidenceHandlerStepNames,
            Steps: steps);
    }

    private static LoopModel CreateLoop(
        string loopName,
        string? lastBodyStepName = null,
        string? parentLoopName = null)
    {
        var lastName = lastBodyStepName ?? $"{loopName}_End";
        return LoopModel.Create(
            loopName: loopName,
            conditionId: $"TestWorkflow-{loopName}",
            maxIterations: 5,
            bodySteps:
            [
                StepModel.Create($"{loopName}_Start", $"TestNamespace.{loopName}_Start"),
                StepModel.Create(lastName, $"TestNamespace.{lastName}"),
            ],
            continuationStepName: null,
            parentLoopName: parentLoopName);
    }

    private static BranchModel CreateBranch(
        string branchId,
        string previousStepName)
    {
        var branchCase = BranchCaseModel.Create(
            caseValueLiteral: "TestValue",
            branchPathPrefix: "TestPath",
            stepNames: ["TestPath_Step"],
            isTerminal: false);

        return BranchModel.Create(
            branchId: branchId,
            previousStepName: previousStepName,
            discriminatorPropertyPath: "Property",
            discriminatorTypeName: "TestType",
            isEnumDiscriminator: false,
            isMethodDiscriminator: false,
            rejoinStepName: "RejoinStep",
            cases: [branchCase]);
    }
}
