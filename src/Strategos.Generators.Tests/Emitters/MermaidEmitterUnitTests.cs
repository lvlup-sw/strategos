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

        // Assert
        await Assert.That(source).Contains("BranchByStatus --> Auto_ProcessStep : Auto");
        await Assert.That(source).Contains("BranchByStatus --> Manual_ProcessStep : Manual");
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

        // Assert
        await Assert.That(source).Contains("Auto_CompleteStep --> CompleteClaim");
        await Assert.That(source).Contains("Manual_CompleteStep --> CompleteClaim");
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
        await Assert.That(source).Contains("Archive_Step --> [*]");
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

    private static WorkflowModel CreateModelWithBranch()
    {
        var cases = new List<BranchCaseModel>
        {
            new(
                CaseValueLiteral: "Status.Auto",
                BranchPathPrefix: "Auto",
                StepNames: ["Auto_ProcessStep", "Auto_CompleteStep"],
                IsTerminal: false),
            new(
                CaseValueLiteral: "Status.Manual",
                BranchPathPrefix: "Manual",
                StepNames: ["Manual_ProcessStep", "Manual_CompleteStep"],
                IsTerminal: false),
        };

        var branches = new List<BranchModel>
        {
            new(
                BranchId: "process-claim-Status",
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
            StepNames: ["ValidateClaim", "Auto_ProcessStep", "Auto_CompleteStep", "Manual_ProcessStep", "Manual_CompleteStep", "CompleteClaim"],
            StateTypeName: "ClaimState",
            Branches: branches);
    }

    private static WorkflowModel CreateModelWithTerminalBranch()
    {
        var cases = new List<BranchCaseModel>
        {
            new(
                CaseValueLiteral: "Status.Active",
                BranchPathPrefix: "Active",
                StepNames: ["Active_ProcessStep"],
                IsTerminal: false),
            new(
                CaseValueLiteral: "Status.Archived",
                BranchPathPrefix: "Archive",
                StepNames: ["Archive_Step"],
                IsTerminal: true),
        };

        var branches = new List<BranchModel>
        {
            new(
                BranchId: "process-record-Status",
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
            StepNames: ["ValidateRecord", "Active_ProcessStep", "Archive_Step", "FinalizeRecord"],
            StateTypeName: "RecordState",
            Branches: branches);
    }
}
