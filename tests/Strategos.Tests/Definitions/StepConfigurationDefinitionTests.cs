// =============================================================================
// <copyright file="StepConfigurationDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="StepConfigurationDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class StepConfigurationDefinitionTests
{
    // =============================================================================
    // A. Empty Configuration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Empty returns an empty configuration.
    /// </summary>
    [Test]
    public async Task Empty_ReturnsEmptyConfiguration()
    {
        // Act
        var config = StepConfigurationDefinition.Empty;

        // Assert
        await Assert.That(config.ConfidenceThreshold).IsNull();
        await Assert.That(config.OnLowConfidence).IsNull();
        await Assert.That(config.Compensation).IsNull();
        await Assert.That(config.Retry).IsNull();
        await Assert.That(config.Timeout).IsNull();
    }

    /// <summary>
    /// Verifies that Empty is a singleton instance.
    /// </summary>
    [Test]
    public async Task Empty_IsSingletonInstance()
    {
        // Act
        var empty1 = StepConfigurationDefinition.Empty;
        var empty2 = StepConfigurationDefinition.Empty;

        // Assert
        await Assert.That(ReferenceEquals(empty1, empty2)).IsTrue();
    }

    // =============================================================================
    // B. WithConfidence Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithConfidence sets the threshold.
    /// </summary>
    [Test]
    public async Task WithConfidence_SetsThreshold()
    {
        // Act
        var config = StepConfigurationDefinition.WithConfidence(0.85);

        // Assert
        await Assert.That(config.ConfidenceThreshold).IsEqualTo(0.85);
    }

    /// <summary>
    /// Verifies that WithConfidence throws for value below 0.
    /// </summary>
    [Test]
    public async Task WithConfidence_BelowZero_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => StepConfigurationDefinition.WithConfidence(-0.1))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that WithConfidence throws for value above 1.
    /// </summary>
    [Test]
    public async Task WithConfidence_AboveOne_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => StepConfigurationDefinition.WithConfidence(1.1))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that WithConfidence accepts boundary value 0.
    /// </summary>
    [Test]
    public async Task WithConfidence_WithZero_Succeeds()
    {
        // Act
        var config = StepConfigurationDefinition.WithConfidence(0.0);

        // Assert
        await Assert.That(config.ConfidenceThreshold).IsEqualTo(0.0);
    }

    /// <summary>
    /// Verifies that WithConfidence accepts boundary value 1.
    /// </summary>
    [Test]
    public async Task WithConfidence_WithOne_Succeeds()
    {
        // Act
        var config = StepConfigurationDefinition.WithConfidence(1.0);

        // Assert
        await Assert.That(config.ConfidenceThreshold).IsEqualTo(1.0);
    }

    // =============================================================================
    // C. WithRetry Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithRetry sets the retry configuration.
    /// </summary>
    [Test]
    public async Task WithRetry_SetsRetryConfig()
    {
        // Arrange
        var retry = RetryConfiguration.Create(3);
        var config = StepConfigurationDefinition.Empty;

        // Act
        var updated = config.WithRetry(retry);

        // Assert
        await Assert.That(updated.Retry).IsNotNull();
        await Assert.That(updated.Retry!.MaxAttempts).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that WithRetry throws for null retry configuration.
    /// </summary>
    [Test]
    public async Task WithRetry_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var config = StepConfigurationDefinition.Empty;

        // Act & Assert
        await Assert.That(() => config.WithRetry(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // D. WithCompensation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithCompensation sets the compensation configuration.
    /// </summary>
    [Test]
    public async Task WithCompensation_SetsCompensationConfig()
    {
        // Arrange
        var compensation = CompensationConfiguration.Create<TestStepConfigStep>();
        var config = StepConfigurationDefinition.Empty;

        // Act
        var updated = config.WithCompensation(compensation);

        // Assert
        await Assert.That(updated.Compensation).IsNotNull();
        await Assert.That(updated.Compensation!.CompensationStepType).IsEqualTo(typeof(TestStepConfigStep));
    }

    /// <summary>
    /// Verifies that WithCompensation throws for null compensation.
    /// </summary>
    [Test]
    public async Task WithCompensation_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var config = StepConfigurationDefinition.Empty;

        // Act & Assert
        await Assert.That(() => config.WithCompensation(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // E. WithLowConfidenceHandler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithLowConfidenceHandler sets the handler.
    /// </summary>
    [Test]
    public async Task WithLowConfidenceHandler_SetsHandler()
    {
        // Arrange
        var handler = LowConfidenceHandlerDefinition.Create([]);
        var config = StepConfigurationDefinition.Empty;

        // Act
        var updated = config.WithLowConfidenceHandler(handler);

        // Assert
        await Assert.That(updated.OnLowConfidence).IsNotNull();
    }

    /// <summary>
    /// Verifies that WithLowConfidenceHandler throws for null handler.
    /// </summary>
    [Test]
    public async Task WithLowConfidenceHandler_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var config = StepConfigurationDefinition.Empty;

        // Act & Assert
        await Assert.That(() => config.WithLowConfidenceHandler(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // F. WithTimeout Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithTimeout sets the timeout value.
    /// </summary>
    [Test]
    public async Task WithTimeout_SetsTimeout()
    {
        // Arrange
        var config = StepConfigurationDefinition.Empty;
        var timeout = TimeSpan.FromMinutes(5);

        // Act
        var updated = config.WithTimeout(timeout);

        // Assert
        await Assert.That(updated.Timeout).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    // =============================================================================
    // G. Fluent Chaining Tests
    // =============================================================================

    /// <summary>
    /// Verifies that fluent methods can be chained.
    /// </summary>
    [Test]
    public async Task FluentChaining_CombinesAllConfigurations()
    {
        // Arrange
        var retry = RetryConfiguration.Create(3);
        var compensation = CompensationConfiguration.Create<TestStepConfigStep>();
        var handler = LowConfidenceHandlerDefinition.Create([]);

        // Act
        var config = StepConfigurationDefinition
            .WithConfidence(0.85)
            .WithRetry(retry)
            .WithCompensation(compensation)
            .WithLowConfidenceHandler(handler)
            .WithTimeout(TimeSpan.FromMinutes(2));

        // Assert
        await Assert.That(config.ConfidenceThreshold).IsEqualTo(0.85);
        await Assert.That(config.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(config.Compensation!.CompensationStepType).IsEqualTo(typeof(TestStepConfigStep));
        await Assert.That(config.OnLowConfidence).IsNotNull();
        await Assert.That(config.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    // =============================================================================
    // H. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepConfigurationDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task StepConfigurationDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = StepConfigurationDefinition.WithConfidence(0.85);

        // Act - Use record with syntax
        var modified = original with { ConfidenceThreshold = 0.90 };

        // Assert
        await Assert.That(original.ConfidenceThreshold).IsEqualTo(0.85);
        await Assert.That(modified.ConfidenceThreshold).IsEqualTo(0.90);
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that builder methods preserve original instance.
    /// </summary>
    [Test]
    public async Task BuilderMethods_PreserveOriginalInstance()
    {
        // Arrange
        var original = StepConfigurationDefinition.Empty;
        var retry = RetryConfiguration.Create(3);

        // Act
        var updated = original.WithRetry(retry);

        // Assert
        await Assert.That(original.Retry).IsNull();
        await Assert.That(updated.Retry).IsNotNull();
    }

    // =============================================================================
    // I. WithValidation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Empty has null Validation.
    /// </summary>
    [Test]
    public async Task Empty_HasNullValidation()
    {
        // Act
        var config = StepConfigurationDefinition.Empty;

        // Assert
        await Assert.That(config.Validation).IsNull();
    }

    /// <summary>
    /// Verifies that WithValidation sets the validation configuration.
    /// </summary>
    [Test]
    public async Task WithValidation_SetsValidation()
    {
        // Arrange
        var validation = ValidationDefinition.Create("state.Items.Any()", "Must have items");
        var config = StepConfigurationDefinition.Empty;

        // Act
        var updated = config.WithValidation(validation);

        // Assert
        await Assert.That(updated.Validation).IsNotNull();
        await Assert.That(updated.Validation!.PredicateExpression).IsEqualTo("state.Items.Any()");
        await Assert.That(updated.Validation!.ErrorMessage).IsEqualTo("Must have items");
    }

    /// <summary>
    /// Verifies that WithValidation throws for null validation.
    /// </summary>
    [Test]
    public async Task WithValidation_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var config = StepConfigurationDefinition.Empty;

        // Act & Assert
        await Assert.That(() => config.WithValidation(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that WithValidation preserves other configurations.
    /// </summary>
    [Test]
    public async Task WithValidation_PreservesOtherConfigurations()
    {
        // Arrange
        var retry = RetryConfiguration.Create(3);
        var validation = ValidationDefinition.Create("state.Value > 0", "Value must be positive");
        var config = StepConfigurationDefinition
            .WithConfidence(0.85)
            .WithRetry(retry);

        // Act
        var updated = config.WithValidation(validation);

        // Assert
        await Assert.That(updated.ConfidenceThreshold).IsEqualTo(0.85);
        await Assert.That(updated.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(updated.Validation).IsNotNull();
    }
}

/// <summary>
/// Test step class for unit testing.
/// </summary>
internal sealed class TestStepConfigStep
{
}
