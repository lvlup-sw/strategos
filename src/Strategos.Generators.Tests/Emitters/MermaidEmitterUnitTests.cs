// -----------------------------------------------------------------------
// <copyright file="MermaidEmitterUnitTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="MermaidEmitter"/> class.
/// </summary>
/// <remarks>
/// These tests verify the Mermaid state diagram emitter in isolation.
/// </remarks>
[Property("Category", "Unit")]
public class MermaidEmitterUnitTests
{
    // =============================================================================
    // A. Basic Emission Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter returns valid, non-empty source code.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_ReturnsNonEmptyString()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).IsNotNull();
        await Assert.That(source.Length).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that the emitter includes the Mermaid state diagram header.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesMermaidHeader()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("stateDiagram-v2");
    }

    /// <summary>
    /// Verifies that the emitter includes the workflow name as a comment.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesWorkflowNameComment()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("%% Workflow: process-order");
    }

    // =============================================================================
    // B. Standard States Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the diagram includes the start transition to first step.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_IncludesNotStartedTransition()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("[*] --> ValidateOrder");
    }

    /// <summary>
    /// Verifies that the diagram includes the completion transition.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_IncludesCompletedTransition()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("SendConfirmation --> [*]");
    }

    /// <summary>
    /// Verifies that the diagram includes the Failed state.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_IncludesFailedState()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("state Failed");
    }

    // =============================================================================
    // C. Linear Step Transition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the diagram includes sequential transitions between steps.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_IncludesSequentialTransitions()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidateOrder --> ProcessPayment");
        await Assert.That(source).Contains("ProcessPayment --> SendConfirmation");
    }

    /// <summary>
    /// Verifies that each step has a transition to Failed.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_StepsTransitionToFailed()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidateOrder --> Failed");
        await Assert.That(source).Contains("ProcessPayment --> Failed");
        await Assert.That(source).Contains("SendConfirmation --> Failed");
    }

    // =============================================================================
    // D. Validation Failure Transition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ValidationFailed state is included when workflow has validation.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithValidation_IncludesValidationFailedState()
    {
        // Arrange
        var model = CreateModelWithValidation();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("state ValidationFailed");
    }

    /// <summary>
    /// Verifies that steps with validation have a transition to ValidationFailed.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithValidation_ShowsValidationTransitions()
    {
        // Arrange
        var model = CreateModelWithValidation();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessPayment --> ValidationFailed : guard failed");
    }

    /// <summary>
    /// Verifies that ValidationFailed state is NOT included when workflow has no validation.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithoutValidation_OmitsValidationFailedState()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).DoesNotContain("ValidationFailed");
    }

    // =============================================================================
    // E. Loop Diagram Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a loop includes a note with the loop name and max iterations.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithLoop_IncludesLoopNote()
    {
        // Arrange
        var model = CreateModelWithLoop();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("note right of Refinement_Critique : Loop: Refinement (max 5)");
    }

    /// <summary>
    /// Verifies that a loop includes the back-transition for continuation.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithLoop_ShowsLoopBackTransition()
    {
        // Arrange
        var model = CreateModelWithLoop();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Refinement_Refine --> Refinement_Critique : continue");
    }

    /// <summary>
    /// Verifies that a loop includes the exit transition.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithLoop_ShowsExitTransition()
    {
        // Arrange
        var model = CreateModelWithLoop();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Refinement_Refine --> Publish : exit");
    }

    // =============================================================================
    // F. Branch Diagram Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a branch includes a choice state.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithBranch_IncludesBranchChoice()
    {
        // Arrange
        var model = CreateModelWithBranch();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("state BranchByStatus <<choice>>");
    }

    /// <summary>
    /// Verifies that a branch includes case transitions with labels.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithBranch_ShowsBranchCaseTransitions()
    {
        // Arrange
        var model = CreateModelWithBranch();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert - each arm enters its case's FIRST step, labelled with the case's path prefix
        await Assert.That(source).Contains("BranchByStatus --> ScoreClaim : Status_ClaimStatus_Auto");
        await Assert.That(source).Contains("BranchByStatus --> AssignAdjuster : Status_ClaimStatus_Manual");
    }

    /// <summary>
    /// Verifies that a multi-step case chains within itself and does not spill into the sibling
    /// case that follows it in the step-name list.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithBranch_ChainsWithinCaseNotAcrossCases()
    {
        // Arrange
        var model = CreateModelWithBranch();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert - the interior of each case
        await Assert.That(source).Contains("ScoreClaim --> SettleClaim");
        await Assert.That(source).Contains("AssignAdjuster --> ReviewClaim");

        // Assert - no edge from one case into the next
        await Assert.That(source).DoesNotContain("SettleClaim --> AssignAdjuster");
    }

    /// <summary>
    /// Verifies that non-terminal branches have rejoin transitions.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithBranch_ShowsRejoinTransition()
    {
        // Arrange
        var model = CreateModelWithBranch();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert - each case rejoins from its own LAST step
        await Assert.That(source).Contains("SettleClaim --> CompleteClaim");
        await Assert.That(source).Contains("ReviewClaim --> CompleteClaim");
    }

    /// <summary>
    /// Verifies that terminal branches have completion transitions.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithTerminalBranch_ShowsTerminalPaths()
    {
        // Arrange
        var model = CreateModelWithTerminalBranch();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ArchiveRecord --> [*]");
        await Assert.That(source).DoesNotContain("ArchiveRecord --> FinalizeRecord");
    }

    // =============================================================================
    // F2. Fork Diagram Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a fork renders as a fork state fanning out to every path and a join state
    /// the paths converge on, rather than as a sequence of steps.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithFork_RendersPathsInParallel()
    {
        // Arrange
        var model = CreateModelWithFork();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert - fan out
        await Assert.That(source).Contains("ValidateOrder --> Fork_ParallelOrder_Fork0");
        await Assert.That(source).Contains("state Fork_ParallelOrder_Fork0 <<fork>>");
        await Assert.That(source).Contains("Fork_ParallelOrder_Fork0 --> ProcessPayment");
        await Assert.That(source).Contains("Fork_ParallelOrder_Fork0 --> ReserveInventory");

        // Assert - fan in
        await Assert.That(source).Contains("state Join_ParallelOrder_Fork0 <<join>>");
        await Assert.That(source).Contains("ProcessPayment --> Join_ParallelOrder_Fork0");
        await Assert.That(source).Contains("ReserveInventory --> Join_ParallelOrder_Fork0");
        await Assert.That(source).Contains("Join_ParallelOrder_Fork0 --> SynthesizeResults");
    }

    /// <summary>
    /// Verifies that fork paths are not drawn as a chain: no path runs into the next one, and the
    /// fork's predecessor does not enter a single path directly.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithFork_DoesNotChainSiblingPaths()
    {
        // Arrange
        var model = CreateModelWithFork();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert
        await Assert.That(source).DoesNotContain("ProcessPayment --> ReserveInventory");
        await Assert.That(source).DoesNotContain("ValidateOrder --> ProcessPayment");
        await Assert.That(source).DoesNotContain("ReserveInventory --> SynthesizeResults");
    }

    // =============================================================================
    // F3. Colliding Loop Boundary Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a nested loop shape whose outer and inner bodies begin and end on the same
    /// step emits a diagram rather than failing the generator on a duplicate lookup key.
    /// </summary>
    [Test]
    public async Task Emit_NestedLoopsSharingBodyBoundaries_EmitsDiagram()
    {
        // Arrange
        var model = CreateModelWithCollidingNestedLoops();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert - both loops are described, neither is dropped
        await Assert.That(source).Contains("note right of Outer_Inner_InspectStep : Loop: Outer (max 5)");
        await Assert.That(source).Contains("note right of Outer_Inner_InspectStep : Loop: Inner (max 3)");
        await Assert.That(source).Contains("Outer_Inner_InspectStep --> Outer_Inner_InspectStep : continue");
        await Assert.That(source).Contains("Outer_Inner_InspectStep --> PublishStep : exit");
    }

    /// <summary>
    /// Verifies that two sibling loops ending on the same body step both contribute their own
    /// continue and exit edges instead of one silently displacing the other.
    /// </summary>
    [Test]
    public async Task Emit_SiblingLoopsSharingLastBodyStep_EmitsBothLoops()
    {
        // Arrange
        var model = CreateModelWithSiblingLoopsSharingLastBodyStep();

        // Act
        var source = MermaidEmitter.Emit(model);

        // Assert - each loop continues back to its own first body step
        await Assert.That(source).Contains("Shared_ReviewStep --> Draft_ComposeStep : continue");
        await Assert.That(source).Contains("Shared_ReviewStep --> Polish_TightenStep : continue");

        // Assert - each loop exits to its own continuation step
        await Assert.That(source).Contains("Shared_ReviewStep --> PublishStep : exit");
        await Assert.That(source).Contains("Shared_ReviewStep --> ArchiveStep : exit");
    }

    // =============================================================================
    // G. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null model throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Emit_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => MermaidEmitter.Emit(null!))
            .Throws<ArgumentNullException>();
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

    private static WorkflowModel CreateModelWithValidation()
    {
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "TestNamespace.ValidateOrder"),
            new("ProcessPayment", "TestNamespace.ProcessPayment", ValidationPredicate: "state.Total > 0", ValidationErrorMessage: "Total must be positive"),
            new("SendConfirmation", "TestNamespace.SendConfirmation"),
        };

        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "SendConfirmation"],
            StateTypeName: "OrderState",
            Steps: steps);
    }

    private static WorkflowModel CreateModelWithLoop()
    {
        var loops = new List<LoopModel>
        {
            new(
                LoopName: "Refinement",
                ConditionId: "iterative-refinement-Refinement",
                MaxIterations: 5,
                BodySteps:
                [
                    StepModel.Create("Refinement_Critique", "TestNamespace.Refinement_Critique"),
                    StepModel.Create("Refinement_Refine", "TestNamespace.Refinement_Refine"),
                ],
                ContinuationStepName: "Publish",
                ParentLoopName: null),
        };

        return new WorkflowModel(
            WorkflowName: "iterative-refinement",
            PascalName: "IterativeRefinement",
            Namespace: "TestNamespace",
            StepNames: ["GenerateDraft", "Refinement_Critique", "Refinement_Refine", "Publish"],
            StateTypeName: "RefinementState",
            Loops: loops);
    }

    /// <summary>
    /// Mirrors what the branch extractor produces: a case's step names are the bare step type
    /// names, and the branch path prefix is a separate field of the form
    /// <c>{discriminatorPath}_{caseValue}</c>. A fixture that folds the prefix into the step names
    /// asserts against a shape the extractor never emits.
    /// </summary>
    private static WorkflowModel CreateModelWithBranch()
    {
        var cases = new List<BranchCaseModel>
        {
            new(
                CaseValueLiteral: "ClaimStatus.Auto",
                BranchPathPrefix: "Status_ClaimStatus_Auto",
                StepNames: ["ScoreClaim", "SettleClaim"],
                IsTerminal: false),
            new(
                CaseValueLiteral: "ClaimStatus.Manual",
                BranchPathPrefix: "Status_ClaimStatus_Manual",
                StepNames: ["AssignAdjuster", "ReviewClaim"],
                IsTerminal: false),
        };

        var branches = new List<BranchModel>
        {
            new(
                BranchId: "process-claim-Branch0-Status",
                PreviousStepName: "ValidateClaim",
                DiscriminatorPropertyPath: "Status",
                DiscriminatorTypeName: "ClaimStatus",
                IsEnumDiscriminator: true,
                IsMethodDiscriminator: false,
                Cases: cases,
                RejoinStepName: "CompleteClaim"),
        };

        return new WorkflowModel(
            WorkflowName: "process-claim",
            PascalName: "ProcessClaim",
            Namespace: "TestNamespace",
            StepNames: ["ValidateClaim", "ScoreClaim", "SettleClaim", "AssignAdjuster", "ReviewClaim", "CompleteClaim"],
            StateTypeName: "ClaimState",
            Branches: branches);
    }

    /// <summary>
    /// A two-case branch whose second case ends the workflow. Case step names are bare, matching
    /// the branch extractor.
    /// </summary>
    private static WorkflowModel CreateModelWithTerminalBranch()
    {
        var cases = new List<BranchCaseModel>
        {
            new(
                CaseValueLiteral: "RecordStatus.Active",
                BranchPathPrefix: "Status_RecordStatus_Active",
                StepNames: ["ProcessRecord"],
                IsTerminal: false),
            new(
                CaseValueLiteral: "RecordStatus.Archived",
                BranchPathPrefix: "Status_RecordStatus_Archived",
                StepNames: ["ArchiveRecord"],
                IsTerminal: true),
        };

        var branches = new List<BranchModel>
        {
            new(
                BranchId: "process-record-Branch0-Status",
                PreviousStepName: "ValidateRecord",
                DiscriminatorPropertyPath: "Status",
                DiscriminatorTypeName: "RecordStatus",
                IsEnumDiscriminator: true,
                IsMethodDiscriminator: false,
                Cases: cases,
                RejoinStepName: "FinalizeRecord"),
        };

        return new WorkflowModel(
            WorkflowName: "process-record",
            PascalName: "ProcessRecord",
            Namespace: "TestNamespace",
            StepNames: ["ValidateRecord", "ProcessRecord", "ArchiveRecord", "FinalizeRecord"],
            StateTypeName: "RecordState",
            Branches: branches);
    }

    /// <summary>
    /// Mirrors what the fork extractor produces: both path steps and the join step are entries of
    /// the step-name list, so consecutive entries chain two paths that run in parallel.
    /// </summary>
    private static WorkflowModel CreateModelWithFork()
    {
        var forks = new List<ForkModel>
        {
            new(
                ForkId: "ParallelOrder-Fork0",
                PreviousStepName: "ValidateOrder",
                Paths:
                [
                    new(
                        PathIndex: 0,
                        Steps: [StepModel.Create("ProcessPayment", "TestNamespace.ProcessPayment")],
                        HasFailureHandler: false,
                        IsTerminalOnFailure: false),
                    new(
                        PathIndex: 1,
                        Steps: [StepModel.Create("ReserveInventory", "TestNamespace.ReserveInventory")],
                        HasFailureHandler: false,
                        IsTerminalOnFailure: false),
                ],
                JoinStepName: "SynthesizeResults"),
        };

        return new WorkflowModel(
            WorkflowName: "parallel-order",
            PascalName: "ParallelOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "ReserveInventory", "SynthesizeResults", "SendConfirmation"],
            StateTypeName: "OrderState",
            Forks: forks);
    }

    /// <summary>
    /// A nested loop whose body is exactly the inner loop's body, so the outer and inner loops
    /// share BOTH their first and their last body step.
    /// </summary>
    private static WorkflowModel CreateModelWithCollidingNestedLoops()
    {
        var sharedBody = new List<StepModel>
        {
            StepModel.Create("Outer_Inner_InspectStep", "TestNamespace.InspectStep"),
        };

        var loops = new List<LoopModel>
        {
            new(
                LoopName: "Outer",
                ConditionId: "nested-audit-Outer",
                MaxIterations: 5,
                BodySteps: sharedBody,
                ContinuationStepName: "PublishStep",
                ParentLoopName: null),
            new(
                LoopName: "Inner",
                ConditionId: "nested-audit-Inner",
                MaxIterations: 3,
                BodySteps: sharedBody,
                ContinuationStepName: "PublishStep",
                ParentLoopName: "Outer"),
        };

        return new WorkflowModel(
            WorkflowName: "nested-audit",
            PascalName: "NestedAudit",
            Namespace: "TestNamespace",
            StepNames: ["StartStep", "Outer_Inner_InspectStep", "PublishStep"],
            StateTypeName: "AuditState",
            Loops: loops);
    }

    /// <summary>
    /// Two sibling loops that end on the same body step but begin on different ones, so only the
    /// last-body-step lookup collides.
    /// </summary>
    private static WorkflowModel CreateModelWithSiblingLoopsSharingLastBodyStep()
    {
        var loops = new List<LoopModel>
        {
            new(
                LoopName: "Draft",
                ConditionId: "authoring-Draft",
                MaxIterations: 5,
                BodySteps:
                [
                    StepModel.Create("Draft_ComposeStep", "TestNamespace.ComposeStep"),
                    StepModel.Create("Shared_ReviewStep", "TestNamespace.ReviewStep"),
                ],
                ContinuationStepName: "PublishStep",
                ParentLoopName: null),
            new(
                LoopName: "Polish",
                ConditionId: "authoring-Polish",
                MaxIterations: 3,
                BodySteps:
                [
                    StepModel.Create("Polish_TightenStep", "TestNamespace.TightenStep"),
                    StepModel.Create("Shared_ReviewStep", "TestNamespace.ReviewStep"),
                ],
                ContinuationStepName: "ArchiveStep",
                ParentLoopName: null),
        };

        return new WorkflowModel(
            WorkflowName: "authoring",
            PascalName: "Authoring",
            Namespace: "TestNamespace",
            StepNames:
            [
                "Draft_ComposeStep",
                "Polish_TightenStep",
                "Shared_ReviewStep",
                "PublishStep",
                "ArchiveStep",
            ],
            StateTypeName: "AuthoringState",
            Loops: loops);
    }
}
