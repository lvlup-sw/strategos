// -----------------------------------------------------------------------
// <copyright file="LoopModelFactoryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="LoopModel.Create"/> factory method validation.
/// </summary>
public sealed class LoopModelFactoryTests
{
    /// <summary>
    /// Builds a bare configured step whose <see cref="StepModel.PhaseName"/> equals the supplied
    /// (already loop-prefixed) name.
    /// </summary>
    private static StepModel Step(string phaseName) => StepModel.Create(phaseName, $"TestNamespace.{phaseName}");

    [Test]
    public async Task Create_WithValidParameters_ReturnsModel()
    {
        // Arrange
        var loopName = "Refinement";
        var conditionId = "ProcessClaim-Refinement";
        var maxIterations = 10;
        var firstBodyStepName = "Refinement_Analyze";
        var lastBodyStepName = "Refinement_Validate";

        // Act
        var model = LoopModel.Create(
            loopName: loopName,
            conditionId: conditionId,
            maxIterations: maxIterations,
            bodySteps: [Step(firstBodyStepName), Step(lastBodyStepName)]);

        // Assert
        await Assert.That(model.LoopName).IsEqualTo(loopName);
        await Assert.That(model.ConditionId).IsEqualTo(conditionId);
        await Assert.That(model.MaxIterations).IsEqualTo(maxIterations);
        await Assert.That(model.FirstBodyStepName).IsEqualTo(firstBodyStepName);
        await Assert.That(model.LastBodyStepName).IsEqualTo(lastBodyStepName);
        await Assert.That(model.ContinuationStepName).IsNull();
        await Assert.That(model.ParentLoopName).IsNull();
    }

    [Test]
    public async Task Create_WithNestedLoopParameters_ReturnsModel()
    {
        // Arrange
        var loopName = "Inner";
        var conditionId = "ProcessClaim-Inner";
        var maxIterations = 5;
        var firstBodyStepName = "Outer_Inner_Process";
        var lastBodyStepName = "Outer_Inner_Validate";
        var continuationStepName = "Outer_Continue";
        var parentLoopName = "Outer";

        // Act
        var model = LoopModel.Create(
            loopName: loopName,
            conditionId: conditionId,
            maxIterations: maxIterations,
            bodySteps: [Step(firstBodyStepName), Step(lastBodyStepName)],
            continuationStepName: continuationStepName,
            parentLoopName: parentLoopName);

        // Assert
        await Assert.That(model.ContinuationStepName).IsEqualTo(continuationStepName);
        await Assert.That(model.ParentLoopName).IsEqualTo(parentLoopName);
        await Assert.That(model.FullPrefix).IsEqualTo("Outer_Inner");
    }

    [Test]
    public async Task Create_WithNullLoopName_ThrowsArgumentNullException()
    {
        // Arrange
        string? loopName = null;
        var conditionId = "ProcessClaim-Refinement";

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            LoopModel.Create(
                loopName: loopName!,
                conditionId: conditionId,
                maxIterations: 10,
                bodySteps: [Step("Refinement_Analyze"), Step("Refinement_Validate")]);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithInvalidLoopName_ThrowsArgumentException()
    {
        // Arrange
        var loopName = "Invalid-Loop"; // Hyphen is not valid
        var conditionId = "ProcessClaim-Refinement";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            LoopModel.Create(
                loopName: loopName,
                conditionId: conditionId,
                maxIterations: 10,
                bodySteps: [Step("Refinement_Analyze"), Step("Refinement_Validate")]);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithEmptyConditionId_ThrowsArgumentException()
    {
        // Arrange
        var loopName = "Refinement";
        var conditionId = "";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            LoopModel.Create(
                loopName: loopName,
                conditionId: conditionId,
                maxIterations: 10,
                bodySteps: [Step("Refinement_Analyze"), Step("Refinement_Validate")]);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithMaxIterationsZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var loopName = "Refinement";
        var conditionId = "ProcessClaim-Refinement";

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            LoopModel.Create(
                loopName: loopName,
                conditionId: conditionId,
                maxIterations: 0,
                bodySteps: [Step("Refinement_Analyze"), Step("Refinement_Validate")]);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithEmptyBodySteps_ThrowsArgumentException()
    {
        // Arrange
        var loopName = "Refinement";
        var conditionId = "ProcessClaim-Refinement";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            LoopModel.Create(
                loopName: loopName,
                conditionId: conditionId,
                maxIterations: 10,
                bodySteps: []);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithInvalidParentLoopName_ThrowsArgumentException()
    {
        // Arrange
        var loopName = "Inner";
        var conditionId = "ProcessClaim-Inner";
        var parentLoopName = "Invalid-Parent"; // Hyphen is not valid

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            LoopModel.Create(
                loopName: loopName,
                conditionId: conditionId,
                maxIterations: 5,
                bodySteps: [Step("Outer_Inner_Process"), Step("Outer_Inner_Validate")],
                parentLoopName: parentLoopName);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }
}
