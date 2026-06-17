// -----------------------------------------------------------------------
// <copyright file="ForkPathModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="ForkPathModel"/>.
/// </summary>
[Property("Category", "Unit")]
public class ForkPathModelTests
{
    private static StepModel Step(string name, string? validationPredicate = null, string? validationErrorMessage = null)
        => StepModel.Create(
            name,
            $"TestNamespace.{name}",
            validationPredicate: validationPredicate,
            validationErrorMessage: validationErrorMessage);

    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid params returns a model.
    /// </summary>
    [Test]
    public async Task Create_WithValidParams_ReturnsModel()
    {
        // Arrange
        var steps = new List<StepModel> { Step("ProcessPayment"), Step("ChargeCard") };

        // Act
        var model = ForkPathModel.Create(
            pathIndex: 0,
            steps: steps,
            hasFailureHandler: false,
            isTerminalOnFailure: false);

        // Assert
        await Assert.That(model.PathIndex).IsEqualTo(0);
        await Assert.That(model.Steps.Count).IsEqualTo(2);
        await Assert.That(model.StepNames.Count).IsEqualTo(2);
        await Assert.That(model.HasFailureHandler).IsFalse();
        await Assert.That(model.IsTerminalOnFailure).IsFalse();
    }

    /// <summary>
    /// Verifies that Create throws for null steps.
    /// </summary>
    [Test]
    public async Task Create_WithNullStepNames_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ForkPathModel.Create(
            pathIndex: 0,
            steps: null!,
            hasFailureHandler: false,
            isTerminalOnFailure: false))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for empty steps.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyStepNames_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => ForkPathModel.Create(
            pathIndex: 0,
            steps: [],
            hasFailureHandler: false,
            isTerminalOnFailure: false))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws for negative path index.
    /// </summary>
    [Test]
    public async Task Create_WithNegativePathIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var steps = new List<StepModel> { Step("Step1") };

        // Act & Assert
        await Assert.That(() => ForkPathModel.Create(
            pathIndex: -1,
            steps: steps,
            hasFailureHandler: false,
            isTerminalOnFailure: false))
            .Throws<ArgumentOutOfRangeException>();
    }

    // =============================================================================
    // B. Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that FirstStepName returns the first step.
    /// </summary>
    [Test]
    public async Task FirstStepName_ReturnsFirstStep()
    {
        // Arrange
        var steps = new List<StepModel> { Step("First"), Step("Second"), Step("Third") };
        var model = ForkPathModel.Create(0, steps, false, false);

        // Assert
        await Assert.That(model.FirstStepName).IsEqualTo("First");
    }

    /// <summary>
    /// Verifies that LastStepName returns the last step.
    /// </summary>
    [Test]
    public async Task LastStepName_ReturnsLastStep()
    {
        // Arrange
        var steps = new List<StepModel> { Step("First"), Step("Second"), Step("Third") };
        var model = ForkPathModel.Create(0, steps, false, false);

        // Assert
        await Assert.That(model.LastStepName).IsEqualTo("Third");
    }

    /// <summary>
    /// Verifies that HasFailureHandler returns true when present.
    /// </summary>
    [Test]
    public async Task HasFailureHandler_WhenPresent_ReturnsTrue()
    {
        // Arrange
        var model = ForkPathModel.Create(
            pathIndex: 0,
            steps: new List<StepModel> { Step("Step1") },
            hasFailureHandler: true,
            isTerminalOnFailure: false);

        // Assert
        await Assert.That(model.HasFailureHandler).IsTrue();
    }

    /// <summary>
    /// Verifies that StatusPropertyName returns correct name.
    /// </summary>
    [Test]
    public async Task StatusPropertyName_ReturnsCorrectName()
    {
        // Arrange
        var model = ForkPathModel.Create(0, new List<StepModel> { Step("Step1") }, false, false);

        // Assert
        await Assert.That(model.StatusPropertyName).IsEqualTo("Path0Status");
    }

    /// <summary>
    /// Verifies that StatePropertyName returns correct name.
    /// </summary>
    [Test]
    public async Task StatePropertyName_ReturnsCorrectName()
    {
        // Arrange
        var model = ForkPathModel.Create(1, new List<StepModel> { Step("Step1") }, false, false);

        // Assert
        await Assert.That(model.StatePropertyName).IsEqualTo("Path1State");
    }

    /// <summary>
    /// Verifies that failure handler step names are included when present.
    /// </summary>
    [Test]
    public async Task Create_WithFailureHandlerSteps_IncludesSteps()
    {
        // Arrange
        var steps = new List<StepModel> { Step("Process") };
        var failureSteps = new List<string> { "Recover", "Cleanup" };

        // Act
        var model = ForkPathModel.Create(
            pathIndex: 0,
            steps: steps,
            hasFailureHandler: true,
            isTerminalOnFailure: false,
            failureHandlerStepNames: failureSteps);

        // Assert
        await Assert.That(model.FailureHandlerStepNames).IsNotNull();
        await Assert.That(model.FailureHandlerStepNames!.Count).IsEqualTo(2);
    }

    // =============================================================================
    // C. Configured-Step Shape (DR-17 / Task 25, F5)
    // =============================================================================

    /// <summary>
    /// Verifies that <see cref="ForkPathModel"/> carries per-step configuration as
    /// <see cref="StepModel"/> records mirroring the top-level/loop emitters' step model,
    /// rather than exposing only step names. The model must surface each step's configured
    /// <c>ValidateState</c> guard (predicate + error message), not just its name.
    /// </summary>
    [Test]
    public async Task ForkPathModel_CarriesConfiguredSteps_NotJustNames()
    {
        // Arrange - a fork-path step configured with a ValidateState guard
        var validatedStep = Step(
            "ProcessPayment",
            validationPredicate: "state.ItemCount > 0",
            validationErrorMessage: "Order must have items");
        var plainStep = Step("ReserveInventory");

        // Act
        var model = ForkPathModel.Create(
            pathIndex: 0,
            steps: new List<StepModel> { validatedStep, plainStep },
            hasFailureHandler: false,
            isTerminalOnFailure: false);

        // Assert - the path carries configured StepModel records, not just names
        await Assert.That(model.Steps.Count).IsEqualTo(2);

        var configured = model.Steps[0];
        await Assert.That(configured.StepName).IsEqualTo("ProcessPayment");
        await Assert.That(configured.HasValidation).IsTrue();
        await Assert.That(configured.ValidationPredicate).IsEqualTo("state.ItemCount > 0");
        await Assert.That(configured.ValidationErrorMessage).IsEqualTo("Order must have items");

        // Assert - per-step config is preserved alongside (not collapsed into) the name projection
        await Assert.That(model.StepNames[0]).IsEqualTo("ProcessPayment");
        await Assert.That(model.Steps[1].HasValidation).IsFalse();
    }
}
