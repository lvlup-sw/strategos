// -----------------------------------------------------------------------
// <copyright file="TransitionsEmitterUnitTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="TransitionsEmitter"/> class.
/// </summary>
/// <remarks>
/// These tests verify transition table generation in isolation, independent of the source generator.
/// </remarks>
[Property("Category", "Unit")]
public class TransitionsEmitterUnitTests
{
    // =============================================================================
    // A. Transition Class Structure Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates a transitions class.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_GeneratesTransitionsClass()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public static partial class ProcessOrderTransitions");
    }

    /// <summary>
    /// Verifies that the transitions class has a ValidTransitions dictionary.
    /// </summary>
    [Test]
    public async Task Emit_TransitionsClass_HasValidTransitionsDictionary()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("IReadOnlyDictionary<ProcessOrderPhase, ProcessOrderPhase[]>");
        await Assert.That(source).Contains("ValidTransitions");
    }

    // =============================================================================
    // B. Transition Entries Tests
    // =============================================================================

    /// <summary>
    /// Verifies that NotStarted transitions to the first step.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_NotStartedToFirstStep()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.NotStarted, [ProcessOrderPhase.ValidateOrder]");
    }

    /// <summary>
    /// Verifies that steps transition sequentially.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_StepsTransitionSequentially()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.ValidateOrder, [ProcessOrderPhase.ProcessPayment, ProcessOrderPhase.Failed]");
        await Assert.That(source).Contains("ProcessOrderPhase.ProcessPayment, [ProcessOrderPhase.SendConfirmation, ProcessOrderPhase.Failed]");
    }

    /// <summary>
    /// Verifies that the last step transitions to Completed.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_LastStepToCompleted()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.SendConfirmation, [ProcessOrderPhase.Completed, ProcessOrderPhase.Failed]");
    }

    /// <summary>
    /// Verifies that terminal phases have no transitions.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_TerminalPhasesHaveNoTransitions()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderPhase.Completed, []");
        await Assert.That(source).Contains("ProcessOrderPhase.Failed, []");
    }

    /// <summary>
    /// Verifies that IsValidTransition helper method is generated.
    /// </summary>
    [Test]
    public async Task Emit_Transitions_GeneratesIsValidTransitionMethod()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public static bool IsValidTransition(ProcessOrderPhase from, ProcessOrderPhase to)");
    }

    /// <summary>
    /// Verifies IsValidTransition uses ValidTransitions dictionary.
    /// </summary>
    [Test]
    public async Task Emit_IsValidTransition_UsesValidTransitionsDictionary()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidTransitions.TryGetValue(from, out var validTargets)");
    }

    // =============================================================================
    // C. Header and Namespace Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the auto-generated header is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesAutoGeneratedHeader()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("// <auto-generated/>");
    }

    /// <summary>
    /// Verifies that nullable enable directive is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesNullableEnable()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that the emitter uses the correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_Transitions_UsesCorrectNamespace()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = TransitionsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("namespace TestNamespace;");
    }

    // =============================================================================
    // C. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null model throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Emit_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => TransitionsEmitter.Emit(null!))
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
}
