// =============================================================================
// <copyright file="FailureHandlerModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="FailureHandlerModel"/> record.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Create factory method validates inputs</description></item>
///   <item><description>Properties are correctly assigned</description></item>
///   <item><description>Derived properties are calculated correctly</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class FailureHandlerModelTests
{
    // =============================================================================
    // A. Create Factory Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create returns model with correct properties.
    /// </summary>
    [Test]
    public async Task Create_WithValidInputs_ReturnsModelWithCorrectProperties()
    {
        // Arrange
        var handlerId = "workflow-failure-handler";
        var scope = FailureHandlerScope.Workflow;
        var stepNames = new List<string> { "LogFailure", "NotifyAdmin" };

        // Act
        var model = FailureHandlerModel.Create(handlerId, scope, stepNames, isTerminal: true);

        // Assert
        await Assert.That(model.HandlerId).IsEqualTo(handlerId);
        await Assert.That(model.Scope).IsEqualTo(scope);
        await Assert.That(model.StepNames).IsEqualTo(stepNames);
        await Assert.That(model.IsTerminal).IsTrue();
        await Assert.That(model.TriggerStepName).IsNull();
    }

    /// <summary>
    /// Verifies that Create with step scope includes trigger step name.
    /// </summary>
    [Test]
    public async Task Create_WithStepScope_IncludesTriggerStepName()
    {
        // Arrange
        var handlerId = "step-failure-handler";
        var scope = FailureHandlerScope.Step;
        var stepNames = new List<string> { "RollbackPayment" };
        var triggerStepName = "ProcessPayment";

        // Act
        var model = FailureHandlerModel.Create(handlerId, scope, stepNames, isTerminal: false, triggerStepName);

        // Assert
        await Assert.That(model.Scope).IsEqualTo(FailureHandlerScope.Step);
        await Assert.That(model.TriggerStepName).IsEqualTo(triggerStepName);
    }

    /// <summary>
    /// Verifies that Create with null handlerId throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullHandlerId_ThrowsArgumentNullException()
    {
        // Arrange
        var stepNames = new List<string> { "LogFailure" };

        // Act & Assert
        await Assert.That(() => FailureHandlerModel.Create(null!, FailureHandlerScope.Workflow, stepNames, true))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with empty handlerId throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyHandlerId_ThrowsArgumentException()
    {
        // Arrange
        var stepNames = new List<string> { "LogFailure" };

        // Act & Assert
        await Assert.That(() => FailureHandlerModel.Create(string.Empty, FailureHandlerScope.Workflow, stepNames, true))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create with null stepNames throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullStepNames_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => FailureHandlerModel.Create("handler", FailureHandlerScope.Workflow, null!, true))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with empty stepNames throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyStepNames_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => FailureHandlerModel.Create("handler", FailureHandlerScope.Workflow, [], true))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // B. Derived Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that FirstStepName returns the first step.
    /// </summary>
    [Test]
    public async Task FirstStepName_WithSteps_ReturnsFirstStep()
    {
        // Arrange
        var model = FailureHandlerModel.Create(
            "handler",
            FailureHandlerScope.Workflow,
            new List<string> { "LogFailure", "NotifyAdmin" },
            true);

        // Act & Assert
        await Assert.That(model.FirstStepName).IsEqualTo("LogFailure");
    }

    /// <summary>
    /// Verifies that LastStepName returns the last step.
    /// </summary>
    [Test]
    public async Task LastStepName_WithSteps_ReturnsLastStep()
    {
        // Arrange
        var model = FailureHandlerModel.Create(
            "handler",
            FailureHandlerScope.Workflow,
            new List<string> { "LogFailure", "NotifyAdmin" },
            true);

        // Act & Assert
        await Assert.That(model.LastStepName).IsEqualTo("NotifyAdmin");
    }

    /// <summary>
    /// Verifies that IsWorkflowScoped returns true for workflow scope.
    /// </summary>
    [Test]
    public async Task IsWorkflowScoped_WithWorkflowScope_ReturnsTrue()
    {
        // Arrange
        var model = FailureHandlerModel.Create(
            "handler",
            FailureHandlerScope.Workflow,
            new List<string> { "LogFailure" },
            true);

        // Act & Assert
        await Assert.That(model.IsWorkflowScoped).IsTrue();
    }

    /// <summary>
    /// Verifies that IsWorkflowScoped returns false for step scope.
    /// </summary>
    [Test]
    public async Task IsWorkflowScoped_WithStepScope_ReturnsFalse()
    {
        // Arrange
        var model = FailureHandlerModel.Create(
            "handler",
            FailureHandlerScope.Step,
            new List<string> { "RollbackPayment" },
            false,
            "ProcessPayment");

        // Act & Assert
        await Assert.That(model.IsWorkflowScoped).IsFalse();
    }
}
