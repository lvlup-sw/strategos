// -----------------------------------------------------------------------
// <copyright file="StepModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="StepModel"/> record.
/// </summary>
[Property("Category", "Unit")]
public class StepModelTests
{
    // =============================================================================
    // A. Constructor Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepModel constructor captures the step name.
    /// </summary>
    [Test]
    public async Task Constructor_CapturesStepName()
    {
        // Arrange & Act
        var model = new StepModel("ValidateOrder", "TestNamespace.ValidateOrder");

        // Assert
        await Assert.That(model.StepName).IsEqualTo("ValidateOrder");
    }

    /// <summary>
    /// Verifies that StepModel constructor captures the step type name.
    /// </summary>
    [Test]
    public async Task Constructor_CapturesStepTypeName()
    {
        // Arrange & Act
        var model = new StepModel("ValidateOrder", "TestNamespace.ValidateOrder");

        // Assert
        await Assert.That(model.StepTypeName).IsEqualTo("TestNamespace.ValidateOrder");
    }

    /// <summary>
    /// Verifies that StepModel correctly stores fully qualified type name.
    /// </summary>
    [Test]
    public async Task Constructor_PreservesFullyQualifiedTypeName()
    {
        // Arrange & Act
        var model = new StepModel("ProcessPayment", "MyApp.Workflows.Steps.ProcessPayment");

        // Assert
        await Assert.That(model.StepTypeName).IsEqualTo("MyApp.Workflows.Steps.ProcessPayment");
    }

    // =============================================================================
    // B. Phase Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that PhaseName defaults to StepName when no loop context.
    /// </summary>
    [Test]
    public async Task PhaseName_WhenNoLoopName_ReturnsStepName()
    {
        // Arrange
        var model = new StepModel("ValidateOrder", "TestNamespace.ValidateOrder");

        // Act & Assert
        await Assert.That(model.PhaseName).IsEqualTo("ValidateOrder");
    }

    /// <summary>
    /// Verifies that PhaseName includes loop prefix when inside a loop.
    /// </summary>
    [Test]
    public async Task PhaseName_WhenLoopNameProvided_ReturnsPrefixedName()
    {
        // Arrange - use named parameter for LoopName (now 4th param, after InstanceName)
        var model = new StepModel("ProcessItem", "TestNamespace.ProcessItem", InstanceName: null, LoopName: "Refinement");

        // Act & Assert
        await Assert.That(model.PhaseName).IsEqualTo("Refinement_ProcessItem");
    }

    // =============================================================================
    // C. Validation Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that HasValidation returns true when ValidationPredicate is not null.
    /// </summary>
    [Test]
    public async Task HasValidation_WithValidationPredicate_ReturnsTrue()
    {
        // Arrange
        var model = new StepModel(
            "ProcessPayment",
            "TestNamespace.ProcessPayment",
            ValidationPredicate: "state.Order.Total > 0",
            ValidationErrorMessage: "Order total must be positive");

        // Act & Assert
        await Assert.That(model.HasValidation).IsTrue();
    }

    /// <summary>
    /// Verifies that HasValidation returns false when ValidationPredicate is null.
    /// </summary>
    [Test]
    public async Task HasValidation_WithoutValidation_ReturnsFalse()
    {
        // Arrange
        var model = new StepModel("ProcessPayment", "TestNamespace.ProcessPayment");

        // Act & Assert
        await Assert.That(model.HasValidation).IsFalse();
    }

    /// <summary>
    /// Verifies that validation properties store predicate and message correctly.
    /// </summary>
    [Test]
    public async Task Validation_StoresPredicateAndMessage()
    {
        // Arrange
        const string predicate = "state.Order.Items.Any()";
        const string message = "Order must have items";

        var model = new StepModel(
            "ProcessPayment",
            "TestNamespace.ProcessPayment",
            ValidationPredicate: predicate,
            ValidationErrorMessage: message);

        // Act & Assert
        await Assert.That(model.ValidationPredicate).IsEqualTo(predicate);
        await Assert.That(model.ValidationErrorMessage).IsEqualTo(message);
    }
}
