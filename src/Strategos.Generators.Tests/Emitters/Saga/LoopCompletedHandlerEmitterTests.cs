// -----------------------------------------------------------------------
// <copyright file="LoopCompletedHandlerEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="LoopCompletedHandlerEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class LoopCompletedHandlerEmitterTests
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
        var emitter = new LoopCompletedHandlerEmitter();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(null!, model, "ProcessStep", context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null model.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, null!, "ProcessStep", context))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that EmitHandler throws for null stepName.
    /// </summary>
    [Test]
    public async Task EmitHandler_NullStepName_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

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
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act & Assert
        await Assert.That(() => emitter.EmitHandler(sb, model, "ProcessStep", null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Handler Signature Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates handler returning object.
    /// </summary>
    [Test]
    public async Task EmitHandler_ValidInput_GeneratesObjectReturnType()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("public object Handle(");
    }

    /// <summary>
    /// Verifies that EmitHandler generates handler accepting completed event and ILogger.
    /// </summary>
    [Test]
    public async Task EmitHandler_ValidInput_AcceptsCompletedEvent()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert - Handler now uses method injection for ILogger (multiline signature)
        await Assert.That(result).Contains("ProcessStepCompleted evt,");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    // =============================================================================
    // C. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates guard clauses for event and logger.
    /// </summary>
    [Test]
    public async Task EmitHandler_ValidInput_GeneratesGuardClause()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert - Guard clauses for both event and logger
        await Assert.That(result).Contains("ArgumentNullException.ThrowIfNull(evt, nameof(evt))");
        await Assert.That(result).Contains("ArgumentNullException.ThrowIfNull(logger, nameof(logger))");
    }

    // =============================================================================
    // D. Reducer Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler applies reducer when state type exists.
    /// </summary>
    [Test]
    public async Task EmitHandler_WithStateType_AppliesReducer()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("State = TestStateReducer.Reduce(State, evt.UpdatedState)");
    }

    // =============================================================================
    // E. Max Iterations Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates max iteration guard.
    /// </summary>
    [Test]
    public async Task EmitHandler_SingleLoop_GeneratesMaxIterationGuard()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("if (RefinementIterationCount >= 5)");
    }

    // =============================================================================
    // F. Condition Check Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates condition check.
    /// </summary>
    [Test]
    public async Task EmitHandler_SingleLoop_GeneratesConditionCheck()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("if (ShouldExitRefinementLoop())");
    }

    // =============================================================================
    // G. Continue Loop Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler increments iteration count.
    /// </summary>
    [Test]
    public async Task EmitHandler_SingleLoop_IncrementsIterationCount()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("RefinementIterationCount++");
    }

    /// <summary>
    /// Verifies that EmitHandler returns first loop step command.
    /// </summary>
    [Test]
    public async Task EmitHandler_SingleLoop_ReturnsFirstLoopStepCommand()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("return new StartRefine_StartCommand(WorkflowId)");
    }

    // =============================================================================
    // H. Exit with Continuation Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler returns continuation step when loop exits.
    /// </summary>
    [Test]
    public async Task EmitHandler_LoopWithContinuation_ReturnsContinuationCommand()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loop = LoopModel.Create(
            loopName: "Refinement",
            conditionId: "Test-Refinement",
            maxIterations: 5,
            bodySteps:
            [
                StepModel.Create("Refine_Start", "TestNamespace.Refine_Start"),
                StepModel.Create("Refine_End", "TestNamespace.Refine_End"),
            ],
            continuationStepName: "FinalizeStep");
        var context = CreateContext([loop]);

        // Act
        emitter.EmitHandler(sb, model, "Refine_End", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("return new StartFinalizeStepCommand(WorkflowId)");
    }

    // =============================================================================
    // I. Exit without Continuation (Workflow Complete) Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler marks completed when loop exits without continuation.
    /// </summary>
    [Test]
    public async Task EmitHandler_LoopNoContinuation_MarksCompleted()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop(); // No continuation
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.Completed");
        await Assert.That(result).Contains("MarkCompleted()");
    }

    // =============================================================================
    // J. XML Documentation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EmitHandler generates XML documentation including logger param.
    /// </summary>
    [Test]
    public async Task EmitHandler_ValidInput_GeneratesXmlDocumentation()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var loops = CreateSingleLoop();
        var context = CreateContext(loops);

        // Act
        emitter.EmitHandler(sb, model, "ProcessStep", context);
        var result = sb.ToString();

        // Assert - XML docs include logger param
        await Assert.That(result).Contains("/// <summary>");
        await Assert.That(result).Contains("/// </summary>");
        await Assert.That(result).Contains("/// <param name=\"logger\">");
        await Assert.That(result).Contains("/// <returns>");
    }

    // =============================================================================
    // K. Nested Loop Tests
    // =============================================================================

    /// <summary>
    /// Verifies that nested loops use hierarchical iteration property names.
    /// For a loop with ParentLoopName="Outer" and LoopName="Inner",
    /// the property should be "OuterInnerIterationCount", not "InnerIterationCount".
    /// </summary>
    [Test]
    public async Task EmitHandler_NestedLoop_UsesHierarchicalIterationProperty()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var nestedLoop = LoopModel.Create(
            loopName: "Inner",
            conditionId: "TestWorkflow-Outer-Inner",
            maxIterations: 3,
            bodySteps: [StepModel.Create("Outer_Inner_InnerStep", "TestNamespace.Outer_Inner_InnerStep")],
            continuationStepName: "NextStep",
            parentLoopName: "Outer");
        var context = CreateContext([nestedLoop]);

        // Act
        emitter.EmitHandler(sb, model, "Outer_Inner_InnerStep", context);
        var result = sb.ToString();

        // Assert - Should use hierarchical property name (OuterInnerIterationCount)
        await Assert.That(result).Contains("OuterInnerIterationCount");
        // Should NOT use just the inner loop name (InnerIterationCount)
        await Assert.That(result).DoesNotContain(" InnerIterationCount");
    }

    /// <summary>
    /// Verifies that nested loops use hierarchical condition method names.
    /// For a loop with ParentLoopName="Outer" and LoopName="Inner",
    /// the method should be "ShouldExitOuterInnerLoop", not "ShouldExitInnerLoop".
    /// </summary>
    [Test]
    public async Task EmitHandler_NestedLoop_UsesHierarchicalConditionMethod()
    {
        // Arrange
        var emitter = new LoopCompletedHandlerEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();
        var nestedLoop = LoopModel.Create(
            loopName: "Inner",
            conditionId: "TestWorkflow-Outer-Inner",
            maxIterations: 3,
            bodySteps: [StepModel.Create("Outer_Inner_InnerStep", "TestNamespace.Outer_Inner_InnerStep")],
            continuationStepName: "NextStep",
            parentLoopName: "Outer");
        var context = CreateContext([nestedLoop]);

        // Act
        emitter.EmitHandler(sb, model, "Outer_Inner_InnerStep", context);
        var result = sb.ToString();

        // Assert - Should use hierarchical method name (ShouldExitOuterInnerLoop)
        await Assert.That(result).Contains("ShouldExitOuterInnerLoop()");
        // Should NOT use just the inner loop name (ShouldExitInnerLoop)
        await Assert.That(result).DoesNotContain("ShouldExitInnerLoop()");
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
            StepNames: ["ValidateStep", "ProcessStep", "FinalizeStep"],
            StateTypeName: "TestState",
            Loops: null);
    }

    private static List<LoopModel> CreateSingleLoop()
    {
        return
        [
            LoopModel.Create(
                loopName: "Refinement",
                conditionId: "TestWorkflow-Refinement",
                maxIterations: 5,
                bodySteps:
                [
                    StepModel.Create("Refine_Start", "TestNamespace.Refine_Start"),
                    StepModel.Create("Refine_End", "TestNamespace.Refine_End"),
                ]),
        ];
    }

    private static HandlerContext CreateContext(List<LoopModel> loops)
    {
        return new HandlerContext(
            StepIndex: 1,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "FinalizeStep",
            StepModel: null,
            LoopsAtStep: loops,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);
    }
}
