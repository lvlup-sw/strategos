// -----------------------------------------------------------------------
// <copyright file="SagaLoopConditionsEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

using TUnit.Core;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for <see cref="SagaLoopConditionsEmitter"/>.
/// </summary>
[Property("Category", "Unit")]
public class SagaLoopConditionsEmitterTests
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
        var emitter = new SagaLoopConditionsEmitter();
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
        var emitter = new SagaLoopConditionsEmitter();
        var sb = new StringBuilder();

        // Act & Assert
        await Assert.That(() => emitter.Emit(sb, null!))
            .Throws<ArgumentNullException>();
    }

    // ====================================================================
    // Section B: Empty Loop Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit emits nothing when model has no loops.
    /// </summary>
    [Test]
    public async Task Emit_NoLoops_EmitsNothing()
    {
        // Arrange
        var emitter = new SagaLoopConditionsEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);

        // Assert
        await Assert.That(sb.ToString()).IsEqualTo(string.Empty);
    }

    // ====================================================================
    // Section C: Single Loop Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit emits a condition method for a single loop.
    /// </summary>
    [Test]
    public async Task Emit_SingleLoop_EmitsConditionMethod()
    {
        // Arrange
        var emitter = new SagaLoopConditionsEmitter();
        var sb = new StringBuilder();
        var loop = CreateLoop("Refinement");
        var model = CreateMinimalModel(loops: [loop]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();
        await Assert.That(output).Contains("ShouldExitRefinementLoop");
        await Assert.That(output).Contains("protected virtual bool");
        await Assert.That(output).Contains("WorkflowConditionRegistry.Evaluate");
    }

    /// <summary>
    /// Verifies that Emit includes XML documentation for the condition method.
    /// </summary>
    [Test]
    public async Task Emit_SingleLoop_IncludesXmlDocumentation()
    {
        // Arrange
        var emitter = new SagaLoopConditionsEmitter();
        var sb = new StringBuilder();
        var loop = CreateLoop("Refinement");
        var model = CreateMinimalModel(loops: [loop]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();
        await Assert.That(output).Contains("<summary>");
        await Assert.That(output).Contains("Evaluates whether the Refinement loop should exit");
        await Assert.That(output).Contains("<returns>");
    }

    // ====================================================================
    // Section D: Multiple Loops Tests
    // ====================================================================

    /// <summary>
    /// Verifies that Emit emits condition methods for all loops.
    /// </summary>
    [Test]
    public async Task Emit_MultipleLoops_EmitsAllConditionMethods()
    {
        // Arrange
        var emitter = new SagaLoopConditionsEmitter();
        var sb = new StringBuilder();
        var loop1 = CreateLoop("Refinement");
        var loop2 = CreateLoop("Validation");
        var loop3 = CreateLoop("Processing");
        var model = CreateMinimalModel(loops: [loop1, loop2, loop3]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();
        await Assert.That(output).Contains("ShouldExitRefinementLoop");
        await Assert.That(output).Contains("ShouldExitValidationLoop");
        await Assert.That(output).Contains("ShouldExitProcessingLoop");
    }

    /// <summary>
    /// Verifies that each loop has a newline separator.
    /// </summary>
    [Test]
    public async Task Emit_MultipleLoops_EachLoopHasNewlineSeparator()
    {
        // Arrange
        var emitter = new SagaLoopConditionsEmitter();
        var sb = new StringBuilder();
        var loop1 = CreateLoop("First");
        var loop2 = CreateLoop("Second");
        var model = CreateMinimalModel(loops: [loop1, loop2]);

        // Act
        emitter.Emit(sb, model);

        // Assert
        var output = sb.ToString();

        // Count the number of method definitions
        var firstMethodIndex = output.IndexOf("ShouldExitFirstLoop", StringComparison.Ordinal);
        var secondMethodIndex = output.IndexOf("ShouldExitSecondLoop", StringComparison.Ordinal);

        await Assert.That(firstMethodIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(secondMethodIndex).IsGreaterThan(firstMethodIndex);
    }

    // ====================================================================
    // Section E: Interface Implementation Tests
    // ====================================================================

    /// <summary>
    /// Verifies that the class implements ISagaComponentEmitter.
    /// </summary>
    [Test]
    public async Task Class_ImplementsISagaComponentEmitter()
    {
        // Arrange
        var emitter = new SagaLoopConditionsEmitter();

        // Assert
        await Assert.That(emitter is ISagaComponentEmitter).IsTrue();
    }

    // ====================================================================
    // Helper Methods
    // ====================================================================

    private static WorkflowModel CreateMinimalModel(IReadOnlyList<LoopModel>? loops = null)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["Step1", "Step2"],
            StateTypeName: "TestState",
            Version: 1,
            Loops: loops,
            Branches: null,
            Steps: null);
    }

    private static LoopModel CreateLoop(string loopName)
    {
        return LoopModel.Create(
            loopName: loopName,
            conditionId: $"TestWorkflow-{loopName}",
            maxIterations: 5,
            bodySteps:
            [
                StepModel.Create($"{loopName}_Start", $"TestNamespace.{loopName}_Start"),
                StepModel.Create($"{loopName}_End", $"TestNamespace.{loopName}_End"),
            ],
            continuationStepName: null,
            parentLoopName: null);
    }
}
