// -----------------------------------------------------------------------
// <copyright file="SagaStepHandlersEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

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
}
