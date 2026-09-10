// -----------------------------------------------------------------------
// <copyright file="BranchModelFactoryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="BranchModel.Create"/> and <see cref="BranchCaseModel.Create"/> factory methods.
/// </summary>
public sealed class BranchModelFactoryTests
{
    [Test]
    public async Task BranchCaseModel_Create_WithValidParameters_ReturnsModel()
    {
        // Arrange
        var caseValueLiteral = "OrderStatus.Approved";
        var branchPathPrefix = "Approved";
        var stepNames = new[] { "Approved_Process", "Approved_Complete" };
        var isTerminal = false;

        // Act
        var model = BranchCaseModel.Create(
            caseValueLiteral: caseValueLiteral,
            branchPathPrefix: branchPathPrefix,
            stepNames: stepNames,
            isTerminal: isTerminal);

        // Assert
        await Assert.That(model.CaseValueLiteral).IsEqualTo(caseValueLiteral);
        await Assert.That(model.BranchPathPrefix).IsEqualTo(branchPathPrefix);
        await Assert.That(model.StepNames).IsEquivalentTo(stepNames);
        await Assert.That(model.IsTerminal).IsFalse();
        await Assert.That(model.FirstStepName).IsEqualTo("Approved_Process");
        await Assert.That(model.LastStepName).IsEqualTo("Approved_Complete");
    }

    [Test]
    public async Task BranchCaseModel_Create_WithEmptyStepNames_ThrowsArgumentException()
    {
        // Arrange
        var caseValueLiteral = "OrderStatus.Approved";
        var branchPathPrefix = "Approved";
        var stepNames = Array.Empty<string>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            BranchCaseModel.Create(
                caseValueLiteral: caseValueLiteral,
                branchPathPrefix: branchPathPrefix,
                stepNames: stepNames,
                isTerminal: false);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task BranchCaseModel_Create_WithEmptyCaseValue_ThrowsArgumentException()
    {
        // Arrange
        var caseValueLiteral = "";
        var branchPathPrefix = "Approved";
        var stepNames = new[] { "Approved_Process" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            BranchCaseModel.Create(
                caseValueLiteral: caseValueLiteral,
                branchPathPrefix: branchPathPrefix,
                stepNames: stepNames,
                isTerminal: false);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task BranchModel_Create_WithValidParameters_ReturnsModel()
    {
        // Arrange
        var branchId = "ProcessOrder-OrderStatus";
        var previousStepName = "ValidateOrder";
        var discriminatorPropertyPath = "Status";
        var discriminatorTypeName = "OrderStatus";
        var isEnumDiscriminator = true;
        var cases = new List<BranchCaseModel>
        {
            BranchCaseModel.Create("OrderStatus.Approved", "Approved", new[] { "Approved_Ship" }, false),
            BranchCaseModel.Create("OrderStatus.Rejected", "Rejected", new[] { "Rejected_Notify" }, true)
        };
        var rejoinStepName = "FinalStep";

        // Act
        var model = BranchModel.Create(
            branchId: branchId,
            previousStepName: previousStepName,
            discriminatorPropertyPath: discriminatorPropertyPath,
            discriminatorTypeName: discriminatorTypeName,
            isEnumDiscriminator: isEnumDiscriminator,
            isMethodDiscriminator: false,
            cases: cases,
            rejoinStepName: rejoinStepName);

        // Assert
        await Assert.That(model.BranchId).IsEqualTo(branchId);
        await Assert.That(model.PreviousStepName).IsEqualTo(previousStepName);
        await Assert.That(model.DiscriminatorPropertyPath).IsEqualTo(discriminatorPropertyPath);
        await Assert.That(model.DiscriminatorTypeName).IsEqualTo(discriminatorTypeName);
        await Assert.That(model.IsEnumDiscriminator).IsTrue();
        await Assert.That(model.Cases.Count).IsEqualTo(2);
        await Assert.That(model.RejoinStepName).IsEqualTo(rejoinStepName);
        await Assert.That(model.HasRejoinPoint).IsTrue();
    }

    [Test]
    public async Task BranchModel_Create_WithInvalidPropertyPath_ThrowsArgumentException()
    {
        // Arrange
        var branchId = "ProcessOrder-OrderStatus";
        var previousStepName = "ValidateOrder";
        var discriminatorPropertyPath = "Invalid.123.Path"; // Has invalid segment
        var discriminatorTypeName = "OrderStatus";
        var cases = new List<BranchCaseModel>
        {
            BranchCaseModel.Create("OrderStatus.Approved", "Approved", new[] { "Approved_Ship" }, false)
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            BranchModel.Create(
                branchId: branchId,
                previousStepName: previousStepName,
                discriminatorPropertyPath: discriminatorPropertyPath,
                discriminatorTypeName: discriminatorTypeName,
                isEnumDiscriminator: true,
                isMethodDiscriminator: false,
                cases: cases);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task BranchModel_Create_WithEmptyCases_ThrowsArgumentException()
    {
        // Arrange
        var branchId = "ProcessOrder-OrderStatus";
        var previousStepName = "ValidateOrder";
        var discriminatorPropertyPath = "Status";
        var discriminatorTypeName = "OrderStatus";
        var cases = new List<BranchCaseModel>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            BranchModel.Create(
                branchId: branchId,
                previousStepName: previousStepName,
                discriminatorPropertyPath: discriminatorPropertyPath,
                discriminatorTypeName: discriminatorTypeName,
                isEnumDiscriminator: true,
                isMethodDiscriminator: false,
                cases: cases);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task BranchModel_Create_WithEmptyBranchId_ThrowsArgumentException()
    {
        // Arrange
        var branchId = "";
        var previousStepName = "ValidateOrder";
        var discriminatorPropertyPath = "Status";
        var discriminatorTypeName = "OrderStatus";
        var cases = new List<BranchCaseModel>
        {
            BranchCaseModel.Create("OrderStatus.Approved", "Approved", new[] { "Approved_Ship" }, false)
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            BranchModel.Create(
                branchId: branchId,
                previousStepName: previousStepName,
                discriminatorPropertyPath: discriminatorPropertyPath,
                discriminatorTypeName: discriminatorTypeName,
                isEnumDiscriminator: true,
                isMethodDiscriminator: false,
                cases: cases);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }
}
