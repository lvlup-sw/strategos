// =============================================================================
// <copyright file="ScarcityMultipliersTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Infrastructure.Budget;
using Strategos.Orchestration.Budget;

namespace Strategos.Infrastructure.Tests.Budget;

/// <summary>
/// Unit tests for <see cref="ScarcityMultipliers"/> verifying that scarcity levels
/// map to correct multiplier values for budget-aware action scoring.
/// </summary>
/// <remarks>
/// Scarcity multipliers are used by the task scorer to penalize expensive actions
/// when resources are becoming scarce. Lower multipliers make costly actions less attractive.
/// </remarks>
[Property("Category", "Unit")]
public sealed class ScarcityMultipliersTests
{
    // =============================================================================
    // A. For() Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Abundant scarcity level returns multiplier of 1.0 (no penalty).
    /// </summary>
    [Test]
    public async Task For_WithAbundant_ReturnsOnePointZero()
    {
        // Arrange & Act
        var result = ScarcityMultipliers.For(ScarcityLevel.Abundant);

        // Assert
        await Assert.That(result).IsEqualTo(1.0m);
    }

    /// <summary>
    /// Verifies that Normal scarcity level returns multiplier of 0.8 (slight penalty).
    /// </summary>
    [Test]
    public async Task For_WithNormal_ReturnsZeroPointEight()
    {
        // Arrange & Act
        var result = ScarcityMultipliers.For(ScarcityLevel.Normal);

        // Assert
        await Assert.That(result).IsEqualTo(0.8m);
    }

    /// <summary>
    /// Verifies that Scarce scarcity level returns multiplier of 0.5 (significant penalty).
    /// </summary>
    [Test]
    public async Task For_WithScarce_ReturnsZeroPointFive()
    {
        // Arrange & Act
        var result = ScarcityMultipliers.For(ScarcityLevel.Scarce);

        // Assert
        await Assert.That(result).IsEqualTo(0.5m);
    }

    /// <summary>
    /// Verifies that Critical scarcity level returns multiplier of 0.2 (severe penalty).
    /// </summary>
    [Test]
    public async Task For_WithCritical_ReturnsZeroPointTwo()
    {
        // Arrange & Act
        var result = ScarcityMultipliers.For(ScarcityLevel.Critical);

        // Assert
        await Assert.That(result).IsEqualTo(0.2m);
    }

    /// <summary>
    /// Verifies that invalid scarcity level throws ArgumentOutOfRangeException.
    /// </summary>
    [Test]
    public async Task For_WithInvalidLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidLevel = (ScarcityLevel)99;

        // Act & Assert
        await Assert.That(() => ScarcityMultipliers.For(invalidLevel))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentOutOfRangeException))
            .ConfigureAwait(false);
    }

    // =============================================================================
    // B. Constant Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the Abundant constant is 1.0.
    /// </summary>
    [Test]
    public async Task Abundant_Constant_IsOnePointZero()
    {
        await Assert.That(ScarcityMultipliers.Abundant).IsEqualTo(1.0m);
    }

    /// <summary>
    /// Verifies that the Normal constant is 0.8.
    /// </summary>
    [Test]
    public async Task Normal_Constant_IsZeroPointEight()
    {
        await Assert.That(ScarcityMultipliers.Normal).IsEqualTo(0.8m);
    }

    /// <summary>
    /// Verifies that the Scarce constant is 0.5.
    /// </summary>
    [Test]
    public async Task Scarce_Constant_IsZeroPointFive()
    {
        await Assert.That(ScarcityMultipliers.Scarce).IsEqualTo(0.5m);
    }

    /// <summary>
    /// Verifies that the Critical constant is 0.2.
    /// </summary>
    [Test]
    public async Task Critical_Constant_IsZeroPointTwo()
    {
        await Assert.That(ScarcityMultipliers.Critical).IsEqualTo(0.2m);
    }
}
