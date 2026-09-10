// -----------------------------------------------------------------------
// <copyright file="SagaEmitterOrchestrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

using TUnit.Core;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Tests verifying the orchestration of saga component emitters.
/// </summary>
/// <remarks>
/// These tests ensure that when SagaEmitter composes all component emitters,
/// the output sections appear in the correct order.
/// </remarks>
[Property("Category", "Integration")]
public class SagaEmitterOrchestrationTests
{
    // ====================================================================
    // Section A: Component Order Tests
    // ====================================================================

    /// <summary>
    /// Verifies that properties are emitted first in the saga class body.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_PropertiesEmittedFirst()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var output = SagaEmitter.Emit(model);

        // Assert
        // Properties section should contain WorkflowId and State
        var classBodyStart = output.IndexOf("{", output.IndexOf("class", StringComparison.Ordinal));
        var workflowIdIndex = output.IndexOf("WorkflowId", classBodyStart);
        var stateIndex = output.IndexOf("State", classBodyStart);

        await Assert.That(workflowIdIndex).IsGreaterThan(classBodyStart);
        await Assert.That(stateIndex).IsGreaterThan(classBodyStart);
    }

    /// <summary>
    /// Verifies that loop conditions are emitted after properties but before handlers.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithLoop_LoopConditionsEmittedAfterProperties()
    {
        // Arrange
        var loop = CreateLoop("Refinement");
        var model = CreateMinimalModel(loops: [loop]);

        // Act
        var output = SagaEmitter.Emit(model);

        // Assert
        var phasePropertyIndex = output.IndexOf("Phase", StringComparison.Ordinal);
        var loopConditionIndex = output.IndexOf("ShouldExitRefinementLoop", StringComparison.Ordinal);

        // The Start method is a static factory method with tuple return type
        var startMethodIndex = output.IndexOf("public static (", StringComparison.Ordinal);

        await Assert.That(loopConditionIndex).IsGreaterThan(phasePropertyIndex);
        await Assert.That(loopConditionIndex).IsLessThan(startMethodIndex);
    }

    /// <summary>
    /// Verifies that Start method is emitted before step handlers.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_StartMethodEmittedBeforeStepHandlers()
    {
        // Arrange
        var model = CreateMinimalModel(stepNames: ["Analyze", "Process"]);

        // Act
        var output = SagaEmitter.Emit(model);

        // Assert
        // The Start method is a static factory method with tuple return type
        var startMethodIndex = output.IndexOf("public static (", StringComparison.Ordinal);

        // Find the StartAnalyzeCommand that appears INSIDE a handler (Handle method)
        // Skip the first one that appears in the Start method return type
        // Handler signature now includes ILogger method injection with line breaks
        var handleMethodIndex = output.IndexOf("public ExecuteAnalyzeWorkerCommand Handle(", StringComparison.Ordinal);

        await Assert.That(startMethodIndex).IsGreaterThan(0);
        await Assert.That(handleMethodIndex).IsGreaterThan(startMethodIndex);
    }

    /// <summary>
    /// Verifies that step handlers are emitted before NotFound handlers.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_StepHandlersEmittedBeforeNotFound()
    {
        // Arrange
        var model = CreateMinimalModel(stepNames: ["First", "Last"]);

        // Act
        var output = SagaEmitter.Emit(model);

        // Assert
        var lastStepHandlerIndex = output.IndexOf("LastCompleted", StringComparison.Ordinal);
        var notFoundIndex = output.IndexOf("NotFound", StringComparison.Ordinal);

        await Assert.That(lastStepHandlerIndex).IsLessThan(notFoundIndex);
    }

    /// <summary>
    /// Verifies that NotFound handlers are emitted last.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_NotFoundHandlersEmittedLast()
    {
        // Arrange
        var model = CreateMinimalModel();

        // Act
        var output = SagaEmitter.Emit(model);

        // Assert
        var notFoundIndex = output.IndexOf("NotFound", StringComparison.Ordinal);
        var closingBraceIndex = output.LastIndexOf("}", StringComparison.Ordinal);

        await Assert.That(notFoundIndex).IsGreaterThan(0);
        await Assert.That(notFoundIndex).IsLessThan(closingBraceIndex);
    }

    // ====================================================================
    // Section B: Complex Workflow Tests
    // ====================================================================

    /// <summary>
    /// Verifies that all components are emitted for workflows with loops.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithLoops_AllComponentsEmitted()
    {
        // Arrange
        var loop = CreateLoop("Refinement", lastBodyStepName: "Refine");
        var model = CreateMinimalModel(
            stepNames: ["Analyze", "Refine", "Complete"],
            loops: [loop]);

        // Act
        var output = SagaEmitter.Emit(model);

        // Assert
        // Properties
        await Assert.That(output).Contains("WorkflowId");
        await Assert.That(output).Contains("State");
        await Assert.That(output).Contains("Phase");

        // Loop condition
        await Assert.That(output).Contains("ShouldExitRefinementLoop");
        await Assert.That(output).Contains("RefinementIterationCount");

        // Start method (static factory with tuple return)
        await Assert.That(output).Contains("public static (");

        // Step handlers
        await Assert.That(output).Contains("StartAnalyzeCommand");
        await Assert.That(output).Contains("StartRefineCommand");
        await Assert.That(output).Contains("StartCompleteCommand");

        // NotFound handlers
        await Assert.That(output).Contains("NotFound");
    }

    /// <summary>
    /// Verifies that all components are emitted for workflows with branches.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithBranches_AllComponentsEmitted()
    {
        // Arrange
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
        var output = SagaEmitter.Emit(model);

        // Assert
        // Properties
        await Assert.That(output).Contains("WorkflowId");
        await Assert.That(output).Contains("State");

        // Start method (static factory with tuple return)
        await Assert.That(output).Contains("public static (");

        // Branch routing
        await Assert.That(output).Contains("State.Status switch");
        await Assert.That(output).Contains("OrderStatus.Approved");

        // NotFound handlers
        await Assert.That(output).Contains("NotFound");
    }

    // ====================================================================
    // Section C: Interface Verification Tests
    // ====================================================================

    /// <summary>
    /// Verifies that SagaPropertiesEmitter implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task SagaPropertiesEmitter_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaPropertiesEmitter();

        // Assert
        await Assert.That(emitter is ISagaComponentEmitter).IsTrue();
    }

    /// <summary>
    /// Verifies that SagaLoopConditionsEmitter implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task SagaLoopConditionsEmitter_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaLoopConditionsEmitter();

        // Assert
        await Assert.That(emitter is ISagaComponentEmitter).IsTrue();
    }

    /// <summary>
    /// Verifies that SagaStartMethodEmitter implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task SagaStartMethodEmitter_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaStartMethodEmitter();

        // Assert
        await Assert.That(emitter is ISagaComponentEmitter).IsTrue();
    }

    /// <summary>
    /// Verifies that SagaStepHandlersEmitter implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task SagaStepHandlersEmitter_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaStepHandlersEmitter();

        // Assert
        await Assert.That(emitter is ISagaComponentEmitter).IsTrue();
    }

    /// <summary>
    /// Verifies that SagaNotFoundHandlersEmitter implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task SagaNotFoundHandlersEmitter_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaNotFoundHandlersEmitter();

        // Assert
        await Assert.That(emitter is ISagaComponentEmitter).IsTrue();
    }

    // ====================================================================
    // Helper Methods
    // ====================================================================

    private static WorkflowModel CreateMinimalModel(
        IReadOnlyList<string>? stepNames = null,
        IReadOnlyList<LoopModel>? loops = null,
        IReadOnlyList<BranchModel>? branches = null)
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
            Steps: null);
    }

    private static LoopModel CreateLoop(
        string loopName,
        string? lastBodyStepName = null)
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
            continuationStepName: "Complete",
            parentLoopName: null);
    }
}
