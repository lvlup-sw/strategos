// =============================================================================
// <copyright file="BudgetOptionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.ComponentModel.DataAnnotations;

using Strategos.Configuration;

namespace Strategos.Tests.Configuration;

/// <summary>
/// Tests for <see cref="BudgetOptions"/> validation logic.
/// </summary>
public sealed class BudgetOptionsTests
{
    [Test]
    public async Task Validate_DefaultOptions_ReturnsNoErrors()
    {
        // Arrange
        var options = new BudgetOptions();
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Validate_StepsPerComplexityUnitZero_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { StepsPerComplexityUnit = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.StepsPerComplexityUnit));
    }

    [Test]
    public async Task Validate_StepsPerComplexityUnitNegative_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { StepsPerComplexityUnit = -5 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("StepsPerComplexityUnit");
    }

    [Test]
    public async Task Validate_AverageTokensPerStepZero_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { AverageTokensPerStep = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.AverageTokensPerStep));
    }

    [Test]
    public async Task Validate_AverageTokensPerStepNegative_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { AverageTokensPerStep = -100 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("AverageTokensPerStep");
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    [Arguments(2.0)]
    public async Task Validate_ExecutionRatioOutOfRange_ReturnsError(double ratio)
    {
        // Arrange
        var options = new BudgetOptions { ExecutionRatio = ratio };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.ExecutionRatio));
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.5)]
    [Arguments(1.0)]
    public async Task Validate_ExecutionRatioValid_ReturnsNoErrors(double ratio)
    {
        // Arrange
        var options = new BudgetOptions { ExecutionRatio = ratio };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Validate_ToolCallRatioNegative_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { ToolCallRatio = -0.5 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.ToolCallRatio));
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(1.0)]
    [Arguments(5.0)]
    public async Task Validate_ToolCallRatioValid_ReturnsNoErrors(double ratio)
    {
        // Arrange
        var options = new BudgetOptions { ToolCallRatio = ratio };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(0.6)]
    [Arguments(1.0)]
    public async Task Validate_RetryMarginOutOfRange_ReturnsError(double margin)
    {
        // Arrange
        var options = new BudgetOptions { RetryMargin = margin };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.RetryMargin));
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.25)]
    [Arguments(0.5)]
    public async Task Validate_RetryMarginValid_ReturnsNoErrors(double margin)
    {
        // Arrange
        var options = new BudgetOptions { RetryMargin = margin };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Validate_DefaultStepBudgetZero_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { DefaultStepBudget = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.DefaultStepBudget));
    }

    [Test]
    public async Task Validate_DefaultTokenBudgetZero_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { DefaultTokenBudget = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.DefaultTokenBudget));
    }

    [Test]
    public async Task Validate_DefaultWallTimeSecondsZero_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions { DefaultWallTimeSeconds = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.DefaultWallTimeSeconds));
    }

    [Test]
    public async Task Validate_MultipliersNotInOrder_ReturnsError()
    {
        // Arrange - Normal > Scarce (invalid order)
        var options = new BudgetOptions
        {
            AbundantMultiplier = 1.0,
            NormalMultiplier = 5.0,
            ScarceMultiplier = 3.0,
            CriticalMultiplier = 10.0,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("increasing order");
    }

    [Test]
    public async Task Validate_AbundantGreaterThanNormal_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions
        {
            AbundantMultiplier = 2.0,
            NormalMultiplier = 1.5,
            ScarceMultiplier = 3.0,
            CriticalMultiplier = 10.0,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.AbundantMultiplier));
    }

    [Test]
    public async Task Validate_ScarceGreaterThanCritical_ReturnsError()
    {
        // Arrange
        var options = new BudgetOptions
        {
            AbundantMultiplier = 1.0,
            NormalMultiplier = 1.5,
            ScarceMultiplier = 15.0,
            CriticalMultiplier = 10.0,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(BudgetOptions.ScarceMultiplier));
    }

    [Test]
    public async Task Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var options = new BudgetOptions
        {
            StepsPerComplexityUnit = 0,
            AverageTokensPerStep = -1,
            ExecutionRatio = 2.0,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results.Count).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task Key_ReturnsExpectedValue()
    {
        // Assert
        await Assert.That(BudgetOptions.Key).IsEqualTo("Workflow:Budget");
    }

    [Test]
    public async Task CreateDevelopmentDefaults_ReturnsConservativeValues()
    {
        // Act
        var options = BudgetOptions.CreateDevelopmentDefaults();

        // Assert
        await Assert.That(options.DefaultStepBudget).IsEqualTo(15);
        await Assert.That(options.DefaultTokenBudget).IsEqualTo(25000);
        await Assert.That(options.DefaultExecutionBudget).IsEqualTo(10);
        await Assert.That(options.DefaultToolCallBudget).IsEqualTo(25);
        await Assert.That(options.DefaultWallTimeSeconds).IsEqualTo(180);
    }

    [Test]
    public async Task CreateProductionDefaults_ReturnsProductionValues()
    {
        // Act
        var options = BudgetOptions.CreateProductionDefaults();

        // Assert
        await Assert.That(options.DefaultStepBudget).IsEqualTo(25);
        await Assert.That(options.DefaultTokenBudget).IsEqualTo(50000);
        await Assert.That(options.DefaultExecutionBudget).IsEqualTo(15);
        await Assert.That(options.DefaultToolCallBudget).IsEqualTo(40);
        await Assert.That(options.DefaultWallTimeSeconds).IsEqualTo(300);
    }

    [Test]
    public async Task CreateDevelopmentDefaults_PassesValidation()
    {
        // Arrange
        var options = BudgetOptions.CreateDevelopmentDefaults();
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task CreateProductionDefaults_PassesValidation()
    {
        // Arrange
        var options = BudgetOptions.CreateProductionDefaults();
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }
}
