// =============================================================================
// <copyright file="ResourceConsumptionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Infrastructure.Tests.Budget;

/// <summary>
/// Unit tests for <see cref="ResourceConsumption"/> verifying resource tracking
/// and aggregation capabilities for workflow operations.
/// </summary>
/// <remarks>
/// ResourceConsumption is used to track resources consumed during workflow steps,
/// including tokens, steps, and wall time. These values are aggregated and compared
/// against budget limits by BudgetGuard.
/// </remarks>
[Property("Category", "Unit")]
public sealed class ResourceConsumptionTests
{
    // =============================================================================
    // A. Static Factory Tests
    // =============================================================================

    /// <summary>
    /// Verifies that None returns a ResourceConsumption with all values at zero.
    /// </summary>
    [Test]
    public async Task None_ReturnsZeroValues()
    {
        // Arrange & Act
        var consumption = ResourceConsumption.None;

        // Assert
        await Assert.That(consumption.Tokens).IsEqualTo(0);
        await Assert.That(consumption.Steps).IsEqualTo(0);
        await Assert.That(consumption.WallTime).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>
    /// Verifies that FromTokens creates a consumption with only tokens set.
    /// </summary>
    [Test]
    public async Task FromTokens_WithValue_SetsTokensOnly()
    {
        // Arrange & Act
        var consumption = ResourceConsumption.FromTokens(100);

        // Assert
        await Assert.That(consumption.Tokens).IsEqualTo(100);
        await Assert.That(consumption.Steps).IsEqualTo(0);
        await Assert.That(consumption.WallTime).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>
    /// Verifies that FromStep creates a consumption with steps set to 1.
    /// </summary>
    [Test]
    public async Task FromStep_SetsStepsToOne()
    {
        // Arrange & Act
        var consumption = ResourceConsumption.FromStep();

        // Assert
        await Assert.That(consumption.Steps).IsEqualTo(1);
        await Assert.That(consumption.Tokens).IsEqualTo(0);
        await Assert.That(consumption.WallTime).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>
    /// Verifies that FromWallTime creates a consumption with wall time set.
    /// </summary>
    [Test]
    public async Task FromWallTime_WithValue_SetsWallTimeOnly()
    {
        // Arrange
        var wallTime = TimeSpan.FromSeconds(30);

        // Act
        var consumption = ResourceConsumption.FromWallTime(wallTime);

        // Assert
        await Assert.That(consumption.WallTime).IsEqualTo(wallTime);
        await Assert.That(consumption.Tokens).IsEqualTo(0);
        await Assert.That(consumption.Steps).IsEqualTo(0);
    }

    // =============================================================================
    // B. Add Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Add combines two consumptions correctly.
    /// </summary>
    [Test]
    public async Task Add_CombinesConsumptions()
    {
        // Arrange
        var a = new ResourceConsumption { Tokens = 50, Steps = 1, WallTime = TimeSpan.FromSeconds(10) };
        var b = new ResourceConsumption { Tokens = 30, Steps = 2, WallTime = TimeSpan.FromSeconds(5) };

        // Act
        var result = a.Add(b);

        // Assert
        await Assert.That(result.Tokens).IsEqualTo(80);
        await Assert.That(result.Steps).IsEqualTo(3);
        await Assert.That(result.WallTime).IsEqualTo(TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Verifies that Add with None returns the original consumption.
    /// </summary>
    [Test]
    public async Task Add_WithNone_ReturnsOriginal()
    {
        // Arrange
        var original = new ResourceConsumption { Tokens = 100, Steps = 5, WallTime = TimeSpan.FromMinutes(1) };

        // Act
        var result = original.Add(ResourceConsumption.None);

        // Assert
        await Assert.That(result.Tokens).IsEqualTo(100);
        await Assert.That(result.Steps).IsEqualTo(5);
        await Assert.That(result.WallTime).IsEqualTo(TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Verifies that Add creates a new instance rather than modifying in place.
    /// </summary>
    [Test]
    public async Task Add_CreatesNewInstance()
    {
        // Arrange
        var original = new ResourceConsumption { Tokens = 50 };
        var toAdd = new ResourceConsumption { Tokens = 30 };

        // Act
        var result = original.Add(toAdd);

        // Assert - original should be unchanged
        await Assert.That(original.Tokens).IsEqualTo(50);
        await Assert.That(result.Tokens).IsEqualTo(80);
    }

    // =============================================================================
    // C. Record Equality Tests
    // =============================================================================

    /// <summary>
    /// Verifies that two ResourceConsumptions with same values are equal.
    /// </summary>
    [Test]
    public async Task Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var a = new ResourceConsumption { Tokens = 100, Steps = 5, WallTime = TimeSpan.FromSeconds(30) };
        var b = new ResourceConsumption { Tokens = 100, Steps = 5, WallTime = TimeSpan.FromSeconds(30) };

        // Act & Assert
        await Assert.That(a).IsEqualTo(b);
    }

    /// <summary>
    /// Verifies that two ResourceConsumptions with different values are not equal.
    /// </summary>
    [Test]
    public async Task Equality_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var a = new ResourceConsumption { Tokens = 100 };
        var b = new ResourceConsumption { Tokens = 200 };

        // Act & Assert
        await Assert.That(a).IsNotEqualTo(b);
    }

    /// <summary>
    /// Verifies that None instances are equal.
    /// </summary>
    [Test]
    public async Task None_Instances_AreEqual()
    {
        // Arrange & Act
        var a = ResourceConsumption.None;
        var b = ResourceConsumption.None;

        // Assert
        await Assert.That(a).IsEqualTo(b);
    }
}
