// =============================================================================
// <copyright file="StepExecutionLedgerOptionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Infrastructure.ExecutionLedgers;

namespace Strategos.Infrastructure.Tests.ExecutionLedgers;

/// <summary>
/// Tests for the <see cref="StepExecutionLedgerOptions"/> class.
/// </summary>
/// <remarks>
/// Tests verify the configuration options for the step execution ledger,
/// including default values and property validation.
/// </remarks>
[Property("Category", "Unit")]
public sealed class StepExecutionLedgerOptionsTests
{
    // =========================================================================
    // A. Default Value Tests
    // =========================================================================

    /// <summary>
    /// Verifies that UseBitFasterCache defaults to false.
    /// </summary>
    [Test]
    public async Task UseBitFasterCache_DefaultValue_IsFalse()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act & Assert
        await Assert.That(options.UseBitFasterCache).IsFalse();
    }

    /// <summary>
    /// Verifies that CacheCapacity defaults to 10000.
    /// </summary>
    [Test]
    public async Task CacheCapacity_DefaultValue_Is10000()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act & Assert
        await Assert.That(options.CacheCapacity).IsEqualTo(10000);
    }

    // =========================================================================
    // B. Property Setter Tests
    // =========================================================================

    /// <summary>
    /// Verifies that UseBitFasterCache can be set to true.
    /// </summary>
    [Test]
    public async Task UseBitFasterCache_SetToTrue_ReturnsTrue()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act
        options.UseBitFasterCache = true;

        // Assert
        await Assert.That(options.UseBitFasterCache).IsTrue();
    }

    /// <summary>
    /// Verifies that CacheCapacity can be set to a positive value.
    /// </summary>
    [Test]
    [Arguments(1)]
    [Arguments(100)]
    [Arguments(50000)]
    [Arguments(int.MaxValue)]
    public async Task CacheCapacity_SetToPositiveValue_ReturnsValue(int capacity)
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act
        options.CacheCapacity = capacity;

        // Assert
        await Assert.That(options.CacheCapacity).IsEqualTo(capacity);
    }

    // =========================================================================
    // C. Validation Tests
    // =========================================================================

    /// <summary>
    /// Verifies that CacheCapacity throws ArgumentOutOfRangeException for zero.
    /// </summary>
    [Test]
    public async Task CacheCapacity_SetToZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act & Assert
        await Assert.That(() => options.CacheCapacity = 0)
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that CacheCapacity throws ArgumentOutOfRangeException for negative values.
    /// </summary>
    [Test]
    [Arguments(-1)]
    [Arguments(-100)]
    [Arguments(int.MinValue)]
    public async Task CacheCapacity_SetToNegativeValue_ThrowsArgumentOutOfRangeException(int capacity)
    {
        // Arrange
        var options = new StepExecutionLedgerOptions();

        // Act & Assert
        await Assert.That(() => options.CacheCapacity = capacity)
            .Throws<ArgumentOutOfRangeException>();
    }

    // =========================================================================
    // D. Object Initializer Tests
    // =========================================================================

    /// <summary>
    /// Verifies that options can be created using object initializer syntax.
    /// </summary>
    [Test]
    public async Task ObjectInitializer_WithAllProperties_SetsAllValues()
    {
        // Arrange & Act
        var options = new StepExecutionLedgerOptions
        {
            UseBitFasterCache = true,
            CacheCapacity = 5000,
        };

        // Assert
        await Assert.That(options.UseBitFasterCache).IsTrue();
        await Assert.That(options.CacheCapacity).IsEqualTo(5000);
    }
}
