// =============================================================================
// <copyright file="RetryConfigurationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="RetryConfiguration"/>.
/// </summary>
[Property("Category", "Unit")]
public class RetryConfigurationTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create returns a configuration with the specified max attempts.
    /// </summary>
    [Test]
    public async Task Create_WithMaxAttempts_ReturnsConfiguration()
    {
        // Arrange
        const int maxAttempts = 3;

        // Act
        var config = RetryConfiguration.Create(maxAttempts);

        // Assert
        await Assert.That(config.MaxAttempts).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that Create throws for zero max attempts.
    /// </summary>
    [Test]
    public async Task Create_WithZeroAttempts_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => RetryConfiguration.Create(0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that Create throws for negative max attempts.
    /// </summary>
    [Test]
    public async Task Create_WithNegativeAttempts_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => RetryConfiguration.Create(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    // =============================================================================
    // B. Default Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that InitialDelay defaults to 1 second.
    /// </summary>
    [Test]
    public async Task Create_InitialDelay_DefaultsToOneSecond()
    {
        // Act
        var config = RetryConfiguration.Create(3);

        // Assert
        await Assert.That(config.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Verifies that BackoffMultiplier defaults to 2.0.
    /// </summary>
    [Test]
    public async Task Create_BackoffMultiplier_DefaultsToTwo()
    {
        // Act
        var config = RetryConfiguration.Create(3);

        // Assert
        await Assert.That(config.BackoffMultiplier).IsEqualTo(2.0);
    }

    /// <summary>
    /// Verifies that MaxDelay defaults to 1 minute.
    /// </summary>
    [Test]
    public async Task Create_MaxDelay_DefaultsToOneMinute()
    {
        // Act
        var config = RetryConfiguration.Create(3);

        // Assert
        await Assert.That(config.MaxDelay).IsEqualTo(TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Verifies that UseJitter defaults to true.
    /// </summary>
    [Test]
    public async Task Create_UseJitter_DefaultsToTrue()
    {
        // Act
        var config = RetryConfiguration.Create(3);

        // Assert
        await Assert.That(config.UseJitter).IsTrue();
    }

    // =============================================================================
    // C. WithExponentialBackoff Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithExponentialBackoff sets the backoff properties.
    /// </summary>
    [Test]
    public async Task WithExponentialBackoff_SetsBackoffProperties()
    {
        // Arrange
        const int maxAttempts = 5;
        var initialDelay = TimeSpan.FromMilliseconds(100);
        const double multiplier = 3.0;

        // Act
        var config = RetryConfiguration.WithExponentialBackoff(maxAttempts, initialDelay, multiplier);

        // Assert
        await Assert.That(config.MaxAttempts).IsEqualTo(5);
        await Assert.That(config.InitialDelay).IsEqualTo(TimeSpan.FromMilliseconds(100));
        await Assert.That(config.BackoffMultiplier).IsEqualTo(3.0);
    }

    /// <summary>
    /// Verifies that WithExponentialBackoff throws for invalid multiplier.
    /// </summary>
    [Test]
    public async Task WithExponentialBackoff_WithMultiplierLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => RetryConfiguration.WithExponentialBackoff(3, TimeSpan.FromSeconds(1), 0.5))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that WithExponentialBackoff throws for multiplier equal to one.
    /// </summary>
    [Test]
    public async Task WithExponentialBackoff_WithMultiplierEqualToOne_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => RetryConfiguration.WithExponentialBackoff(3, TimeSpan.FromSeconds(1), 1.0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that WithExponentialBackoff uses default multiplier of 2.0.
    /// </summary>
    [Test]
    public async Task WithExponentialBackoff_WithoutMultiplier_UsesDefaultOfTwo()
    {
        // Act
        var config = RetryConfiguration.WithExponentialBackoff(3, TimeSpan.FromSeconds(1));

        // Assert
        await Assert.That(config.BackoffMultiplier).IsEqualTo(2.0);
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that RetryConfiguration is an immutable record.
    /// </summary>
    [Test]
    public async Task RetryConfiguration_IsImmutableRecord()
    {
        // Arrange
        var original = RetryConfiguration.Create(3);

        // Act - Use record with syntax
        var modified = original with { MaxAttempts = 5 };

        // Assert
        await Assert.That(original.MaxAttempts).IsEqualTo(3);
        await Assert.That(modified.MaxAttempts).IsEqualTo(5);
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that equal configurations are considered equal.
    /// </summary>
    [Test]
    public async Task Equality_SameValues_AreEqual()
    {
        // Arrange
        var config1 = RetryConfiguration.Create(3);
        var config2 = RetryConfiguration.Create(3);

        // Assert - Different instances with same values should be equal (record semantics)
        await Assert.That(config1.MaxAttempts).IsEqualTo(config2.MaxAttempts);
        await Assert.That(config1.InitialDelay).IsEqualTo(config2.InitialDelay);
        await Assert.That(config1.BackoffMultiplier).IsEqualTo(config2.BackoffMultiplier);
    }
}
