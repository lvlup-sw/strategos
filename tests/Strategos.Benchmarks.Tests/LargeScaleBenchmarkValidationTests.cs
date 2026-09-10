// =============================================================================
// <copyright file="LargeScaleBenchmarkValidationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

using Strategos.Benchmarks.Subsystems.LargeScale;

using BenchmarkDotNet.Attributes;

namespace Strategos.Benchmarks.Tests;

/// <summary>
/// Validation tests for large-scale benchmark classes.
/// </summary>
/// <remarks>
/// <para>
/// These tests validate that benchmark classes exist and have the correct
/// BenchmarkDotNet attributes for proper execution. They ensure:
/// <list type="bullet">
///   <item><description>Benchmark classes are properly decorated</description></item>
///   <item><description>Params include 10K+ scale scenarios</description></item>
///   <item><description>Methods have correct benchmark attributes</description></item>
/// </list>
/// </para>
/// </remarks>
[Property("Category", "Benchmark")]
public sealed class LargeScaleBenchmarkValidationTests
{
    // =============================================================================
    // DocumentSearchBenchmarks Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that DocumentSearchBenchmarks class exists and has MemoryDiagnoser.
    /// </summary>
    [Test]
    public async Task DocumentSearchBenchmarks_HasMemoryDiagnoserAttribute()
    {
        // Arrange
        var benchmarkType = typeof(DocumentSearchBenchmarks);

        // Act
        var attribute = benchmarkType.GetCustomAttribute<MemoryDiagnoserAttribute>();

        // Assert
        await Assert.That(attribute).IsNotNull();
    }

    /// <summary>
    /// Verifies that DocumentSearchBenchmarks has DocumentCount parameter with 10K scale.
    /// </summary>
    [Test]
    public async Task DocumentSearchBenchmarks_HasDocumentCountParamsIncluding10K()
    {
        // Arrange
        var benchmarkType = typeof(DocumentSearchBenchmarks);
        var property = benchmarkType.GetProperty("DocumentCount");

        // Act
        var paramsAttribute = property?.GetCustomAttribute<ParamsAttribute>();

        // Assert
        await Assert.That(paramsAttribute).IsNotNull();
        await Assert.That(paramsAttribute!.Values).Contains(100);
        await Assert.That(paramsAttribute.Values).Contains(1000);
        await Assert.That(paramsAttribute.Values).Contains(10000);
    }

    /// <summary>
    /// Verifies that DocumentSearchBenchmarks has at least one benchmark method.
    /// </summary>
    [Test]
    public async Task DocumentSearchBenchmarks_HasBenchmarkMethods()
    {
        // Arrange
        var benchmarkType = typeof(DocumentSearchBenchmarks);

        // Act
        var benchmarkMethods = benchmarkType.GetMethods()
            .Where(m => m.GetCustomAttribute<BenchmarkAttribute>() != null)
            .ToList();

        // Assert
        await Assert.That(benchmarkMethods.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that DocumentSearchBenchmarks has GlobalSetup method.
    /// </summary>
    [Test]
    public async Task DocumentSearchBenchmarks_HasGlobalSetup()
    {
        // Arrange
        var benchmarkType = typeof(DocumentSearchBenchmarks);

        // Act
        var setupMethod = benchmarkType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<GlobalSetupAttribute>() != null);

        // Assert
        await Assert.That(setupMethod).IsNotNull();
    }

    // =============================================================================
    // CandidateSelectionBenchmarks Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CandidateSelectionBenchmarks class exists and has MemoryDiagnoser.
    /// </summary>
    [Test]
    public async Task CandidateSelectionBenchmarks_HasMemoryDiagnoserAttribute()
    {
        // Arrange
        var benchmarkType = typeof(CandidateSelectionBenchmarks);

        // Act
        var attribute = benchmarkType.GetCustomAttribute<MemoryDiagnoserAttribute>();

        // Assert
        await Assert.That(attribute).IsNotNull();
    }

    /// <summary>
    /// Verifies that CandidateSelectionBenchmarks has CandidateCount parameter with 10K scale.
    /// </summary>
    [Test]
    public async Task CandidateSelectionBenchmarks_HasCandidateCountParamsIncluding10K()
    {
        // Arrange
        var benchmarkType = typeof(CandidateSelectionBenchmarks);
        var property = benchmarkType.GetProperty("CandidateCount");

        // Act
        var paramsAttribute = property?.GetCustomAttribute<ParamsAttribute>();

        // Assert
        await Assert.That(paramsAttribute).IsNotNull();
        await Assert.That(paramsAttribute!.Values).Contains(100);
        await Assert.That(paramsAttribute.Values).Contains(1000);
        await Assert.That(paramsAttribute.Values).Contains(10000);
    }

    /// <summary>
    /// Verifies that CandidateSelectionBenchmarks has at least one benchmark method.
    /// </summary>
    [Test]
    public async Task CandidateSelectionBenchmarks_HasBenchmarkMethods()
    {
        // Arrange
        var benchmarkType = typeof(CandidateSelectionBenchmarks);

        // Act
        var benchmarkMethods = benchmarkType.GetMethods()
            .Where(m => m.GetCustomAttribute<BenchmarkAttribute>() != null)
            .ToList();

        // Assert
        await Assert.That(benchmarkMethods.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that CandidateSelectionBenchmarks has GlobalSetup method.
    /// </summary>
    [Test]
    public async Task CandidateSelectionBenchmarks_HasGlobalSetup()
    {
        // Arrange
        var benchmarkType = typeof(CandidateSelectionBenchmarks);

        // Act
        var setupMethod = benchmarkType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<GlobalSetupAttribute>() != null);

        // Assert
        await Assert.That(setupMethod).IsNotNull();
    }
}
