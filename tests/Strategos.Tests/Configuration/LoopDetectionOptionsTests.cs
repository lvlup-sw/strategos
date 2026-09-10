// =============================================================================
// <copyright file="LoopDetectionOptionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.ComponentModel.DataAnnotations;

using Strategos.Configuration;

namespace Strategos.Tests.Configuration;

/// <summary>
/// Tests for <see cref="LoopDetectionOptions"/> validation logic.
/// </summary>
public sealed class LoopDetectionOptionsTests
{
    [Test]
    public async Task Validate_DefaultOptions_ReturnsNoErrors()
    {
        // Arrange
        var options = new LoopDetectionOptions();
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Validate_WindowSizeZero_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions { WindowSize = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(LoopDetectionOptions.WindowSize));
    }

    [Test]
    public async Task Validate_WindowSizeNegative_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions { WindowSize = -5 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("WindowSize");
    }

    [Test]
    public async Task Validate_WindowSizeExceedsMaximum_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions { WindowSize = 25 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("exceed 20");
    }

    [Test]
    [Arguments(1)]
    [Arguments(10)]
    [Arguments(20)]
    public async Task Validate_WindowSizeValid_ReturnsNoErrors(int size)
    {
        // Arrange
        var options = new LoopDetectionOptions { WindowSize = size };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(1.0)]
    [Arguments(-0.1)]
    [Arguments(1.5)]
    public async Task Validate_SimilarityThresholdOutOfRange_ReturnsError(double threshold)
    {
        // Arrange
        var options = new LoopDetectionOptions { SimilarityThreshold = threshold };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(LoopDetectionOptions.SimilarityThreshold));
    }

    [Test]
    [Arguments(0.1)]
    [Arguments(0.5)]
    [Arguments(0.85)]
    [Arguments(0.99)]
    public async Task Validate_SimilarityThresholdValid_ReturnsNoErrors(double threshold)
    {
        // Arrange
        var options = new LoopDetectionOptions { SimilarityThreshold = threshold };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Validate_MaxResetsZero_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions { MaxResets = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(LoopDetectionOptions.MaxResets));
    }

    [Test]
    public async Task Validate_MaxResetsNegative_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions { MaxResets = -1 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("MaxResets");
    }

    [Test]
    public async Task Validate_MaxSpecialistRetriesZero_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions { MaxSpecialistRetries = 0 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(LoopDetectionOptions.MaxSpecialistRetries));
    }

    [Test]
    public async Task Validate_MaxSpecialistRetriesNegative_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions { MaxSpecialistRetries = -3 };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("MaxSpecialistRetries");
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(1.0)]
    [Arguments(-0.1)]
    [Arguments(1.5)]
    public async Task Validate_RecoveryThresholdOutOfRange_ReturnsError(double threshold)
    {
        // Arrange
        var options = new LoopDetectionOptions { RecoveryThreshold = threshold };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].MemberNames).Contains(nameof(LoopDetectionOptions.RecoveryThreshold));
    }

    [Test]
    [Arguments(0.1)]
    [Arguments(0.5)]
    [Arguments(0.7)]
    [Arguments(0.99)]
    public async Task Validate_RecoveryThresholdValid_ReturnsNoErrors(double threshold)
    {
        // Arrange
        var options = new LoopDetectionOptions { RecoveryThreshold = threshold };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Validate_WeightsDoNotSumToOne_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions
        {
            RepetitionScoreWeight = 0.3,
            SemanticScoreWeight = 0.3,
            TimeScoreWeight = 0.3,
            FrustrationScoreWeight = 0.3,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].ErrorMessage).Contains("sum to 1.0");
    }

    [Test]
    public async Task Validate_WeightsSumToOneWithinTolerance_ReturnsNoErrors()
    {
        // Arrange
        var options = new LoopDetectionOptions
        {
            RepetitionScoreWeight = 0.25,
            SemanticScoreWeight = 0.25,
            TimeScoreWeight = 0.25,
            FrustrationScoreWeight = 0.25,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Validate_NegativeRepetitionScoreWeight_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions
        {
            RepetitionScoreWeight = -0.1,
            SemanticScoreWeight = 0.4,
            TimeScoreWeight = 0.4,
            FrustrationScoreWeight = 0.3,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results.Any(r => r.ErrorMessage!.Contains("negative"))).IsTrue();
    }

    [Test]
    public async Task Validate_NegativeSemanticScoreWeight_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions
        {
            RepetitionScoreWeight = 0.4,
            SemanticScoreWeight = -0.1,
            TimeScoreWeight = 0.4,
            FrustrationScoreWeight = 0.3,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results.Any(r => r.ErrorMessage!.Contains("negative"))).IsTrue();
    }

    [Test]
    public async Task Validate_NegativeTimeScoreWeight_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions
        {
            RepetitionScoreWeight = 0.4,
            SemanticScoreWeight = 0.4,
            TimeScoreWeight = -0.1,
            FrustrationScoreWeight = 0.3,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results.Any(r => r.ErrorMessage!.Contains("negative"))).IsTrue();
    }

    [Test]
    public async Task Validate_NegativeFrustrationScoreWeight_ReturnsError()
    {
        // Arrange
        var options = new LoopDetectionOptions
        {
            RepetitionScoreWeight = 0.4,
            SemanticScoreWeight = 0.4,
            TimeScoreWeight = 0.3,
            FrustrationScoreWeight = -0.1,
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results.Any(r => r.ErrorMessage!.Contains("negative"))).IsTrue();
    }

    [Test]
    public async Task Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var options = new LoopDetectionOptions
        {
            WindowSize = 0,
            MaxResets = -1,
            SimilarityThreshold = 2.0,
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
        await Assert.That(LoopDetectionOptions.Key).IsEqualTo("Workflow:LoopDetection");
    }

    [Test]
    public async Task CreateDevelopmentDefaults_ReturnsAggressiveValues()
    {
        // Act
        var options = LoopDetectionOptions.CreateDevelopmentDefaults();

        // Assert
        await Assert.That(options.WindowSize).IsEqualTo(3);
        await Assert.That(options.SimilarityThreshold).IsEqualTo(0.80);
        await Assert.That(options.MaxResets).IsEqualTo(2);
        await Assert.That(options.MaxSpecialistRetries).IsEqualTo(2);
        await Assert.That(options.RecoveryThreshold).IsEqualTo(0.6);
    }

    [Test]
    public async Task CreateProductionDefaults_ReturnsBalancedValues()
    {
        // Act
        var options = LoopDetectionOptions.CreateProductionDefaults();

        // Assert
        await Assert.That(options.WindowSize).IsEqualTo(5);
        await Assert.That(options.SimilarityThreshold).IsEqualTo(0.85);
        await Assert.That(options.MaxResets).IsEqualTo(3);
        await Assert.That(options.MaxSpecialistRetries).IsEqualTo(3);
        await Assert.That(options.RecoveryThreshold).IsEqualTo(0.7);
    }

    [Test]
    public async Task CreateDevelopmentDefaults_PassesValidation()
    {
        // Arrange
        var options = LoopDetectionOptions.CreateDevelopmentDefaults();
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
        var options = LoopDetectionOptions.CreateProductionDefaults();
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task Enabled_DefaultIsTrue()
    {
        // Arrange
        var options = new LoopDetectionOptions();

        // Assert
        await Assert.That(options.Enabled).IsTrue();
    }
}
