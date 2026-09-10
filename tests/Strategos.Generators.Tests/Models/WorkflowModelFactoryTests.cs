// -----------------------------------------------------------------------
// <copyright file="WorkflowModelFactoryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="WorkflowModel.Create"/> factory method validation.
/// </summary>
public sealed class WorkflowModelFactoryTests
{
    [Test]
    public async Task Create_WithValidParameters_ReturnsModel()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "ProcessOrder";
        var @namespace = "MyCompany.Workflows";
        var stepNames = new[] { "ValidateOrder", "ProcessPayment", "ShipOrder" };

        // Act
        var model = WorkflowModel.Create(
            workflowName: workflowName,
            pascalName: pascalName,
            @namespace: @namespace,
            stepNames: stepNames);

        // Assert
        await Assert.That(model.WorkflowName).IsEqualTo(workflowName);
        await Assert.That(model.PascalName).IsEqualTo(pascalName);
        await Assert.That(model.Namespace).IsEqualTo(@namespace);
        await Assert.That(model.StepNames).IsEquivalentTo(stepNames);
        await Assert.That(model.Version).IsEqualTo(1);
        await Assert.That(model.StateTypeName).IsNull();
    }

    [Test]
    public async Task Create_WithAllOptionalParameters_ReturnsModel()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "ProcessOrder";
        var @namespace = "MyCompany.Workflows";
        var stepNames = new[] { "ValidateOrder" };
        var stateTypeName = "OrderState";
        var version = 2;
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "MyCompany.Steps.ValidateOrderStep")
        };
        var loops = new List<LoopModel>();
        var branches = new List<BranchModel>();

        // Act
        var model = WorkflowModel.Create(
            workflowName: workflowName,
            pascalName: pascalName,
            @namespace: @namespace,
            stepNames: stepNames,
            stateTypeName: stateTypeName,
            version: version,
            steps: steps,
            loops: loops,
            branches: branches);

        // Assert
        await Assert.That(model.StateTypeName).IsEqualTo(stateTypeName);
        await Assert.That(model.Version).IsEqualTo(version);
        await Assert.That(model.Steps).IsNotNull();
        await Assert.That(model.Loops).IsNotNull();
        await Assert.That(model.Branches).IsNotNull();
    }

    [Test]
    public async Task Create_WithNullPascalName_ThrowsArgumentNullException()
    {
        // Arrange
        var workflowName = "process-order";
        string? pascalName = null;
        var @namespace = "MyCompany.Workflows";
        var stepNames = new[] { "ValidateOrder" };

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            WorkflowModel.Create(
                workflowName: workflowName,
                pascalName: pascalName!,
                @namespace: @namespace,
                stepNames: stepNames);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithInvalidPascalName_ThrowsArgumentException()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "123Invalid"; // Starts with number - invalid
        var @namespace = "MyCompany.Workflows";
        var stepNames = new[] { "ValidateOrder" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            WorkflowModel.Create(
                workflowName: workflowName,
                pascalName: pascalName,
                @namespace: @namespace,
                stepNames: stepNames);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithEmptyNamespace_ThrowsArgumentException()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "ProcessOrder";
        var @namespace = "";
        var stepNames = new[] { "ValidateOrder" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            WorkflowModel.Create(
                workflowName: workflowName,
                pascalName: pascalName,
                @namespace: @namespace,
                stepNames: stepNames);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithEmptyStepNames_ThrowsArgumentException()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "ProcessOrder";
        var @namespace = "MyCompany.Workflows";
        var stepNames = Array.Empty<string>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            WorkflowModel.Create(
                workflowName: workflowName,
                pascalName: pascalName,
                @namespace: @namespace,
                stepNames: stepNames);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithDuplicateStepNames_ThrowsArgumentException()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "ProcessOrder";
        var @namespace = "MyCompany.Workflows";
        var stepNames = new[] { "ValidateOrder", "ProcessPayment", "ValidateOrder" }; // Duplicate

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            WorkflowModel.Create(
                workflowName: workflowName,
                pascalName: pascalName,
                @namespace: @namespace,
                stepNames: stepNames);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithInvalidStepName_ThrowsArgumentException()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "ProcessOrder";
        var @namespace = "MyCompany.Workflows";
        var stepNames = new[] { "ValidateOrder", "Invalid-Step" }; // Hyphen is invalid

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            WorkflowModel.Create(
                workflowName: workflowName,
                pascalName: pascalName,
                @namespace: @namespace,
                stepNames: stepNames);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithVersionZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var workflowName = "process-order";
        var pascalName = "ProcessOrder";
        var @namespace = "MyCompany.Workflows";
        var stepNames = new[] { "ValidateOrder" };

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            WorkflowModel.Create(
                workflowName: workflowName,
                pascalName: pascalName,
                @namespace: @namespace,
                stepNames: stepNames,
                version: 0);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    /// <summary>
    /// F4 (CodeRabbit / epic #135): the <see cref="WorkflowModel.Create"/> factory
    /// must thread <c>confidenceHandlerStepNames</c> (DR-5) so a model built through
    /// the factory carries the same handler identity as one built via the primary
    /// constructor. Before the fix the parameter was silently dropped, leaving
    /// <see cref="WorkflowModel.HasConfidenceHandlers"/> false and
    /// <see cref="WorkflowModel.IsConfidenceHandlerStep"/> wrong for every
    /// <c>Create</c>-built model.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Create_WithConfidenceHandlerStepNames_PreservesHandlerMetadata()
    {
        // Arrange
        var stepNames = new[] { "ValidateOrder", "ClassifyIntent", "HumanReview", "SendConfirmation" };
        var confidenceHandlerStepNames = new[] { "HumanReview" };

        // Act
        var model = WorkflowModel.Create(
            workflowName: "process-order",
            pascalName: "ProcessOrder",
            @namespace: "MyCompany.Workflows",
            stepNames: stepNames,
            confidenceHandlerStepNames: confidenceHandlerStepNames);

        // Assert — the DR-5 handler identity survives the factory.
        await Assert.That(model.ConfidenceHandlerStepNames).IsNotNull();
        await Assert.That(model.HasConfidenceHandlers).IsTrue();
        await Assert.That(model.IsConfidenceHandlerStep("HumanReview")).IsTrue();
        await Assert.That(model.IsConfidenceHandlerStep("ClassifyIntent")).IsFalse();
    }

    /// <summary>
    /// F4 regression guard: with no confidence handler step names supplied a
    /// <c>Create</c>-built model reports no confidence handlers (the optional
    /// parameter defaults to null, matching the constructor default).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Create_WithoutConfidenceHandlerStepNames_ReportsNoConfidenceHandlers()
    {
        // Arrange & Act
        var model = WorkflowModel.Create(
            workflowName: "process-order",
            pascalName: "ProcessOrder",
            @namespace: "MyCompany.Workflows",
            stepNames: new[] { "ValidateOrder", "SendConfirmation" });

        // Assert
        await Assert.That(model.ConfidenceHandlerStepNames).IsNull();
        await Assert.That(model.HasConfidenceHandlers).IsFalse();
        await Assert.That(model.IsConfidenceHandlerStep("ValidateOrder")).IsFalse();
    }
}
