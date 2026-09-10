// =============================================================================
// <copyright file="UsageMetricsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for <see cref="UsageMetrics"/> covering creation, default values,
/// and integration with <see cref="SpecialistSignal"/>.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
/// <item>UsageMetrics record creation with valid values</item>
/// <item>Zero factory returns empty metrics</item>
/// <item>SpecialistSignal can include UsageMetrics</item>
/// <item>Addition operator combines metrics correctly</item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class UsageMetricsTests
{
    // =============================================================================
    // A. Factory and Creation Tests (4 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that UsageMetrics created with constructor sets all properties correctly.
    /// </summary>
    [Test]
    public async Task Create_WithValidValues_SetsAllProperties()
    {
        // Arrange
        const long tokens = 1500;
        const long executions = 2;
        const long toolCalls = 5;
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var metrics = new UsageMetrics(tokens, executions, toolCalls, duration);

        // Assert
        await Assert.That(metrics.TokensConsumed).IsEqualTo(tokens);
        await Assert.That(metrics.ExecutionsPerformed).IsEqualTo(executions);
        await Assert.That(metrics.ToolCallsMade).IsEqualTo(toolCalls);
        await Assert.That(metrics.Duration).IsEqualTo(duration);
    }

    /// <summary>
    /// Verifies that UsageMetrics.Zero returns a metrics instance with all zeros.
    /// </summary>
    [Test]
    public async Task Zero_ReturnsEmptyMetrics()
    {
        // Act
        var metrics = UsageMetrics.Zero;

        // Assert
        await Assert.That(metrics.TokensConsumed).IsEqualTo(0);
        await Assert.That(metrics.ExecutionsPerformed).IsEqualTo(0);
        await Assert.That(metrics.ToolCallsMade).IsEqualTo(0);
        await Assert.That(metrics.Duration).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>
    /// Verifies that UsageMetrics.Zero returns the same instance (singleton).
    /// </summary>
    [Test]
    public async Task Zero_ReturnsSameInstance()
    {
        // Act
        var metrics1 = UsageMetrics.Zero;
        var metrics2 = UsageMetrics.Zero;

        // Assert
        await Assert.That(metrics1).IsEqualTo(metrics2);
    }

    /// <summary>
    /// Verifies that UsageMetrics supports with-expression for immutable updates.
    /// </summary>
    [Test]
    public async Task WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new UsageMetrics(100, 1, 2, TimeSpan.FromSeconds(5));

        // Act
        var updated = original with { TokensConsumed = 200 };

        // Assert
        await Assert.That(updated.TokensConsumed).IsEqualTo(200);
        await Assert.That(original.TokensConsumed).IsEqualTo(100);
    }

    // =============================================================================
    // B. Addition Operator Tests (3 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that addition operator combines two UsageMetrics correctly.
    /// </summary>
    [Test]
    public async Task Addition_CombinesMetrics()
    {
        // Arrange
        var metrics1 = new UsageMetrics(100, 1, 2, TimeSpan.FromSeconds(5));
        var metrics2 = new UsageMetrics(200, 2, 3, TimeSpan.FromSeconds(10));

        // Act
        var combined = metrics1 + metrics2;

        // Assert
        await Assert.That(combined.TokensConsumed).IsEqualTo(300);
        await Assert.That(combined.ExecutionsPerformed).IsEqualTo(3);
        await Assert.That(combined.ToolCallsMade).IsEqualTo(5);
        await Assert.That(combined.Duration).IsEqualTo(TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Verifies that adding Zero to metrics returns equivalent metrics.
    /// </summary>
    [Test]
    public async Task Addition_WithZero_ReturnsEquivalent()
    {
        // Arrange
        var metrics = new UsageMetrics(100, 1, 2, TimeSpan.FromSeconds(5));

        // Act
        var result = metrics + UsageMetrics.Zero;

        // Assert
        await Assert.That(result.TokensConsumed).IsEqualTo(metrics.TokensConsumed);
        await Assert.That(result.ExecutionsPerformed).IsEqualTo(metrics.ExecutionsPerformed);
        await Assert.That(result.ToolCallsMade).IsEqualTo(metrics.ToolCallsMade);
        await Assert.That(result.Duration).IsEqualTo(metrics.Duration);
    }

    /// <summary>
    /// Verifies addition is commutative.
    /// </summary>
    [Test]
    public async Task Addition_IsCommutative()
    {
        // Arrange
        var metrics1 = new UsageMetrics(100, 1, 2, TimeSpan.FromSeconds(5));
        var metrics2 = new UsageMetrics(200, 2, 3, TimeSpan.FromSeconds(10));

        // Act
        var result1 = metrics1 + metrics2;
        var result2 = metrics2 + metrics1;

        // Assert
        await Assert.That(result1).IsEqualTo(result2);
    }

    // =============================================================================
    // C. SpecialistSignal Integration Tests (3 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that SpecialistSignal.Success can include UsageMetrics.
    /// </summary>
    [Test]
    public async Task SpecialistSignal_Success_IncludesUsageMetrics()
    {
        // Arrange
        var usage = new UsageMetrics(1500, 1, 3, TimeSpan.FromSeconds(8));

        // Act
        var signal = SpecialistSignal.Success(
            SpecialistType.Coder,
            result: "Task completed",
            confidence: 0.95,
            usage: usage);

        // Assert
        await Assert.That(signal.Usage).IsNotNull();
        await Assert.That(signal.Usage!.TokensConsumed).IsEqualTo(1500);
        await Assert.That(signal.Usage!.ExecutionsPerformed).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that SpecialistSignal.Failure can include partial UsageMetrics.
    /// </summary>
    [Test]
    public async Task SpecialistSignal_Failure_IncludesPartialUsageMetrics()
    {
        // Arrange
        var usage = new UsageMetrics(500, 1, 0, TimeSpan.FromSeconds(3));

        // Act
        var signal = SpecialistSignal.Failure(
            SpecialistType.Analyst,
            reason: "Timeout occurred",
            usage: usage);

        // Assert
        await Assert.That(signal.Usage).IsNotNull();
        await Assert.That(signal.Usage!.TokensConsumed).IsEqualTo(500);
    }

    /// <summary>
    /// Verifies that SpecialistSignal.Success without usage returns null Usage.
    /// </summary>
    [Test]
    public async Task SpecialistSignal_Success_WithoutUsage_ReturnsNullUsage()
    {
        // Act
        var signal = SpecialistSignal.Success(
            SpecialistType.Coder,
            result: "Task completed",
            confidence: 0.95);

        // Assert
        await Assert.That(signal.Usage).IsNull();
    }
}
