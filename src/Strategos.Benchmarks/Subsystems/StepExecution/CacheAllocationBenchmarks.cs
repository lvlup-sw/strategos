// =============================================================================
// <copyright file="CacheAllocationBenchmarks.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Abstractions;
using Strategos.Infrastructure.ExecutionLedgers;

using BenchmarkDotNet.Attributes;

namespace Strategos.Benchmarks.Subsystems.StepExecution;

/// <summary>
/// Benchmarks for verifying zero-allocation cache hits in <see cref="IStepExecutionLedger"/>.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks focus on memory allocation patterns:
/// <list type="bullet">
///   <item><description>Target: 0 B allocation on cache hit in steady state</description></item>
///   <item><description>Validates Gen0 = 0 for cache hit operations</description></item>
/// </list>
/// </para>
/// <para>
/// Zero-allocation cache hits are critical for high-throughput scenarios
/// where GC pressure can significantly impact latency.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class CacheAllocationBenchmarks
{
    private InMemoryStepExecutionLedger _ledger = null!;
    private string _stepName = null!;
    private string _inputHash = null!;

    /// <summary>
    /// Sets up the benchmark with a pre-populated cache for steady-state testing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cache is pre-populated with a test result to ensure we measure
    /// steady-state behavior rather than first-access initialization costs.
    /// </para>
    /// </remarks>
    [GlobalSetup]
    public void Setup()
    {
        _ledger = new InMemoryStepExecutionLedger(TimeProvider.System, NullLogger<InMemoryStepExecutionLedger>.Instance);
        _stepName = "SteadyStateStep";
        _inputHash = "steadystate123";

        // Pre-populate the cache
        var cachedResult = new AllocationTestResult
        {
            Id = 42,
            Name = "Pre-cached Result",
            Timestamp = DateTimeOffset.UtcNow,
        };

        _ledger.CacheResultAsync(_stepName, _inputHash, cachedResult, null, CancellationToken.None)
            .GetAwaiter().GetResult();

        // Warmup - perform several reads to ensure JIT optimization
        for (var i = 0; i < 100; i++)
        {
            _ = _ledger.TryGetCachedResultAsync<AllocationTestResult>(_stepName, _inputHash, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Benchmarks cache hit allocation in steady state.
    /// </summary>
    /// <returns>The cached result.</returns>
    /// <remarks>
    /// <para>
    /// Target metrics:
    /// <list type="bullet">
    ///   <item><description>Gen0 = 0 (no generation 0 collections triggered)</description></item>
    ///   <item><description>Allocated = 0 B (no heap allocations)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Note: The current implementation uses JSON deserialization which allocates.
    /// This benchmark documents the baseline for future optimization.
    /// </para>
    /// </remarks>
    [Benchmark(Description = "Cache Hit - Steady State (target: 0B)")]
    public async Task<AllocationTestResult?> CacheHit_SteadyState_ZeroAlloc()
    {
        return await _ledger.TryGetCachedResultAsync<AllocationTestResult>(
            _stepName,
            _inputHash,
            CancellationToken.None);
    }
}

/// <summary>
/// Test result type for allocation benchmarks.
/// </summary>
/// <remarks>
/// A slightly more complex record type to measure realistic serialization overhead.
/// </remarks>
public sealed record AllocationTestResult
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
