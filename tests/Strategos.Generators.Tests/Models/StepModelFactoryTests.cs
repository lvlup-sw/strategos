// -----------------------------------------------------------------------
// <copyright file="StepModelFactoryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="StepModel.Create"/> factory method validation.
/// </summary>
public sealed class StepModelFactoryTests
{
    [Test]
    public async Task Create_WithValidParameters_ReturnsModel()
    {
        // Arrange
        var stepName = "ValidateOrder";
        var stepTypeName = "MyCompany.Steps.ValidateOrderStep";

        // Act
        var model = StepModel.Create(
            stepName: stepName,
            stepTypeName: stepTypeName);

        // Assert
        await Assert.That(model.StepName).IsEqualTo(stepName);
        await Assert.That(model.StepTypeName).IsEqualTo(stepTypeName);
        await Assert.That(model.LoopName).IsNull();
        await Assert.That(model.ValidationPredicate).IsNull();
        await Assert.That(model.ValidationErrorMessage).IsNull();
        await Assert.That(model.HasValidation).IsFalse();
    }

    [Test]
    public async Task Create_WithLoopName_ReturnsModelWithPhaseName()
    {
        // Arrange
        var stepName = "ProcessItem";
        var stepTypeName = "MyCompany.Steps.ProcessItemStep";
        var loopName = "ItemLoop";

        // Act
        var model = StepModel.Create(
            stepName: stepName,
            stepTypeName: stepTypeName,
            loopName: loopName);

        // Assert
        await Assert.That(model.LoopName).IsEqualTo(loopName);
        await Assert.That(model.PhaseName).IsEqualTo("ItemLoop_ProcessItem");
    }

    [Test]
    public async Task Create_WithValidation_ReturnsModelWithValidation()
    {
        // Arrange
        var stepName = "ValidateOrder";
        var stepTypeName = "MyCompany.Steps.ValidateOrderStep";
        var validationPredicate = "state.TotalAmount > 0";
        var validationErrorMessage = "Order amount must be positive";

        // Act
        var model = StepModel.Create(
            stepName: stepName,
            stepTypeName: stepTypeName,
            validationPredicate: validationPredicate,
            validationErrorMessage: validationErrorMessage);

        // Assert
        await Assert.That(model.ValidationPredicate).IsEqualTo(validationPredicate);
        await Assert.That(model.ValidationErrorMessage).IsEqualTo(validationErrorMessage);
        await Assert.That(model.HasValidation).IsTrue();
    }

    [Test]
    public async Task Create_WithNullStepName_ThrowsArgumentNullException()
    {
        // Arrange
        string? stepName = null;
        var stepTypeName = "MyCompany.Steps.ValidateOrderStep";

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            StepModel.Create(
                stepName: stepName!,
                stepTypeName: stepTypeName);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithInvalidStepName_ThrowsArgumentException()
    {
        // Arrange
        var stepName = "Invalid-Step"; // Hyphen is not valid in C# identifier
        var stepTypeName = "MyCompany.Steps.ValidateOrderStep";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StepModel.Create(
                stepName: stepName,
                stepTypeName: stepTypeName);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithEmptyStepTypeName_ThrowsArgumentException()
    {
        // Arrange
        var stepName = "ValidateOrder";
        var stepTypeName = "";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StepModel.Create(
                stepName: stepName,
                stepTypeName: stepTypeName);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithInvalidLoopName_ThrowsArgumentException()
    {
        // Arrange
        var stepName = "ValidateOrder";
        var stepTypeName = "MyCompany.Steps.ValidateOrderStep";
        var loopName = "Invalid-Loop"; // Hyphen is not valid

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StepModel.Create(
                stepName: stepName,
                stepTypeName: stepTypeName,
                loopName: loopName);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithPredicateButNoMessage_ThrowsArgumentException()
    {
        // Arrange
        var stepName = "ValidateOrder";
        var stepTypeName = "MyCompany.Steps.ValidateOrderStep";
        var validationPredicate = "state.TotalAmount > 0";
        string? validationErrorMessage = null;

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StepModel.Create(
                stepName: stepName,
                stepTypeName: stepTypeName,
                validationPredicate: validationPredicate,
                validationErrorMessage: validationErrorMessage);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Create_WithMessageButNoPredicate_ThrowsArgumentException()
    {
        // Arrange
        var stepName = "ValidateOrder";
        var stepTypeName = "MyCompany.Steps.ValidateOrderStep";
        string? validationPredicate = null;
        var validationErrorMessage = "Order amount must be positive";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StepModel.Create(
                stepName: stepName,
                stepTypeName: stepTypeName,
                validationPredicate: validationPredicate,
                validationErrorMessage: validationErrorMessage);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }
}
