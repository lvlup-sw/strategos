// -----------------------------------------------------------------------
// <copyright file="SagaEmitterValidationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Tests for <see cref="SagaEmitter"/> validation step handling.
/// </summary>
[Property("Category", "Unit")]
public class SagaEmitterValidationTests
{
    // =============================================================================
    // A. Step Validation Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates validation guard code for steps with validation.
    /// </summary>
    [Test]
    public async Task Emit_StepWithValidation_GeneratesValidationGuard()
    {
        // Arrange
        var stepModel = new StepModel(
            StepName: "ValidateInput",
            StepTypeName: "TestNamespace.ValidateInput",
            ValidationPredicate: "state.Input != null",
            ValidationErrorMessage: "Input cannot be null");

        var model = CreateModelWithSteps([stepModel]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert
        await Assert.That(result).Contains("// Validation guard");
        await Assert.That(result).Contains("State.Input != null");
    }

    /// <summary>
    /// Verifies that Emit generates yield-based handler for validated steps.
    /// </summary>
    [Test]
    public async Task Emit_StepWithValidation_GeneratesYieldBasedHandler()
    {
        // Arrange
        var stepModel = new StepModel(
            StepName: "ValidateInput",
            StepTypeName: "TestNamespace.ValidateInput",
            ValidationPredicate: "state.HasData",
            ValidationErrorMessage: "Missing data");

        var model = CreateModelWithSteps([stepModel]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert - Handler now includes ILogger method injection
        await Assert.That(result).Contains("IEnumerable<object> Handle(");
        await Assert.That(result).Contains("StartValidateInputCommand command,");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
        await Assert.That(result).Contains("yield return");
    }

    /// <summary>
    /// Verifies that Emit generates ValidationFailed event with correct workflow name.
    /// </summary>
    [Test]
    public async Task Emit_StepWithValidation_GeneratesValidationFailedEvent()
    {
        // Arrange
        var stepModel = new StepModel(
            StepName: "ValidateInput",
            StepTypeName: "TestNamespace.ValidateInput",
            ValidationPredicate: "state.IsValid",
            ValidationErrorMessage: "State is invalid");

        var model = CreateModelWithSteps([stepModel]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert
        await Assert.That(result).Contains("TestWorkflowValidationFailed");
        await Assert.That(result).Contains("\"ValidateInput\"");
        await Assert.That(result).Contains("\"State is invalid\"");
    }

    /// <summary>
    /// Verifies that Emit sets ValidationFailed phase when guard fails.
    /// </summary>
    [Test]
    public async Task Emit_StepWithValidation_SetsValidationFailedPhase()
    {
        // Arrange
        var stepModel = new StepModel(
            StepName: "ValidateInput",
            StepTypeName: "TestNamespace.ValidateInput",
            ValidationPredicate: "state.IsValid",
            ValidationErrorMessage: "Invalid");

        var model = CreateModelWithSteps([stepModel]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert
        await Assert.That(result).Contains("Phase = TestWorkflowPhase.ValidationFailed;");
    }

    // =============================================================================
    // B. Steps Without Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates standard handler for steps without validation.
    /// </summary>
    [Test]
    public async Task Emit_StepWithoutValidation_GeneratesStandardHandler()
    {
        // Arrange
        var stepModel = new StepModel(
            StepName: "ProcessData",
            StepTypeName: "TestNamespace.ProcessData");

        var model = CreateModelWithSteps([stepModel]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert - Standard handler now includes ILogger method injection
        await Assert.That(result).Contains("ExecuteProcessDataWorkerCommand Handle(");
        await Assert.That(result).Contains("StartProcessDataCommand command,");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
        await Assert.That(result).DoesNotContain("// Validation guard");
    }

    /// <summary>
    /// Verifies that Emit handles null Steps collection gracefully.
    /// </summary>
    [Test]
    public async Task Emit_NullStepsCollection_GeneratesStandardHandlers()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ProcessStep"],
            StateTypeName: "TestState",
            Steps: null);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert - Should not crash and should generate standard handler with ILogger
        await Assert.That(result).Contains("ExecuteProcessStepWorkerCommand Handle(");
        await Assert.That(result).Contains("StartProcessStepCommand command,");
        await Assert.That(result).Contains("ILogger<TestWorkflowSaga> logger)");
    }

    // =============================================================================
    // C. State Parameter Replacement Tests
    // =============================================================================

    /// <summary>
    /// Verifies that validation predicate replaces state parameter with State property.
    /// </summary>
    [Test]
    public async Task Emit_ValidationPredicate_ReplacesStateParameter()
    {
        // Arrange
        var stepModel = new StepModel(
            StepName: "CheckInput",
            StepTypeName: "TestNamespace.CheckInput",
            ValidationPredicate: "state.Items.Count > 0",
            ValidationErrorMessage: "No items");

        var model = CreateModelWithSteps([stepModel]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert - "state." should be replaced with "State."
        await Assert.That(result).Contains("State.Items.Count > 0");
        await Assert.That(result).DoesNotContain("state.Items");
    }

    /// <summary>
    /// Verifies that complex predicates with multiple state references are handled.
    /// </summary>
    [Test]
    public async Task Emit_ComplexPredicate_ReplacesAllStateReferences()
    {
        // Arrange
        var stepModel = new StepModel(
            StepName: "Validate",
            StepTypeName: "TestNamespace.Validate",
            ValidationPredicate: "state.A != null && state.B > 0",
            ValidationErrorMessage: "Invalid state");

        var model = CreateModelWithSteps([stepModel]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert
        await Assert.That(result).Contains("State.A != null && State.B > 0");
    }

    // =============================================================================
    // D. Mixed Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Emit handles mix of validated and non-validated steps.
    /// </summary>
    [Test]
    public async Task Emit_MixedSteps_GeneratesCorrectHandlersForEach()
    {
        // Arrange
        var validatedStep = new StepModel(
            StepName: "ValidateInput",
            StepTypeName: "TestNamespace.ValidateInput",
            ValidationPredicate: "state.Valid",
            ValidationErrorMessage: "Invalid");

        var normalStep = new StepModel(
            StepName: "Process",
            StepTypeName: "TestNamespace.Process");

        var model = new WorkflowModel(
            WorkflowName: "mixed-workflow",
            PascalName: "MixedWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateInput", "Process"],
            StateTypeName: "TestState",
            Steps: [validatedStep, normalStep]);

        // Act
        var result = SagaEmitter.Emit(model);

        // Assert - ValidateInput uses yield-based handler with ILogger injection
        await Assert.That(result).Contains("IEnumerable<object> Handle(");
        await Assert.That(result).Contains("StartValidateInputCommand command,");
        await Assert.That(result).Contains("ILogger<MixedWorkflowSaga> logger)");

        // Assert - Process uses standard handler with ILogger injection
        await Assert.That(result).Contains("ExecuteProcessWorkerCommand Handle(");
        await Assert.That(result).Contains("StartProcessCommand command,");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateModelWithSteps(List<StepModel> steps)
    {
        var stepNames = steps.Select(s => s.StepName).ToList();

        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: stepNames,
            StateTypeName: "TestState",
            Steps: steps);
    }
}
