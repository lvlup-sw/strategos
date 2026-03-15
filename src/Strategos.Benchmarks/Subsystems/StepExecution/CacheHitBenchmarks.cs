// =============================================================================
// <copyright file="CacheHitBenchmarks.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Abstractions;
using Strategos.Infrastructure.ExecutionLedgers;

using BenchmarkDotNet.Attributes;

namespace Strategos.Benchmarks.Subsystems.StepExecution;

/// <summary>
/// Benchmarks for <see cref="IStepExecutionLedger"/> cache hit and miss latency.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks focus on:
/// <list type="bullet">
///   <item><description>Cache hit latency (target: less than 1 microsecond)</description></item>
///   <item><description>Cache miss latency (baseline)</description></item>
///   <item><description>Cache storage cost</description></item>
/// </list>
/// </para>
/// <para>
/// The benchmarks validate that the ValueTask optimization in the in-memory
/// implementation provides near-instant cache hits.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class CacheHitBenchmarks
{
    private InMemoryStepExecutionLedger _ledger = null!;
    private string _hitStepName = null!;
    private string _hitInputHash = null!;
    private string _missStepName = null!;
    private string _missInputHash = null!;
    private TestCacheResult _resultToStore = null!;
    private string _storeStepName = null!;
    private string _storeInputHash = null!;
    private int _storeCounter;

    /// <summary>
    /// Sets up the benchmark with pre-populated cache entries.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _ledger = new InMemoryStepExecutionLedger(TimeProvider.System, NullLogger<InMemoryStepExecutionLedger>.Instance);

        // Setup for cache hit scenario
        _hitStepName = "CachedStep";
        _hitInputHash = "abc123def456";
        var cachedResult = new TestCacheResult { Id = 1, Value = "Cached Value" };
        _ledger.CacheResultAsync(_hitStepName, _hitInputHash, cachedResult, null, CancellationToken.None)
            .GetAwaiter().GetResult();

        // Setup for cache miss scenario (no entry stored)
        _missStepName = "UncachedStep";
        _missInputHash = "xyz789missing";

        // Setup for cache store scenario
        _resultToStore = new TestCacheResult { Id = 2, Value = "New Value" };
        _storeStepName = "StoreStep";
        _storeInputHash = "store000";
        _storeCounter = 0;
    }

    /// <summary>
    /// Benchmarks cache hit latency for retrieving a cached result.
    /// </summary>
    /// <returns>The cached result if found.</returns>
    /// <remarks>
    /// Target: less than 1 microsecond latency for cache hits.
    /// </remarks>
    [Benchmark(Description = "TryGetCachedResult - Cache Hit")]
    public async Task<TestCacheResult?> TryGetCachedResultAsync_CacheHit()
    {
        return await _ledger.TryGetCachedResultAsync<TestCacheResult>(
            _hitStepName,
            _hitInputHash,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks cache miss latency for a non-existent entry.
    /// </summary>
    /// <returns>Null indicating cache miss.</returns>
    /// <remarks>
    /// This provides a baseline for comparison with cache hit latency.
    /// </remarks>
    [Benchmark(Description = "TryGetCachedResult - Cache Miss")]
    public async Task<TestCacheResult?> TryGetCachedResultAsync_CacheMiss()
    {
        return await _ledger.TryGetCachedResultAsync<TestCacheResult>(
            _missStepName,
            _missInputHash,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks the cost of storing a result in the cache.
    /// </summary>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// Measures serialization and dictionary insertion overhead.
    /// </remarks>
    [Benchmark(Description = "CacheResult - Store")]
    public async Task CacheResultAsync_Store()
    {
        // Use unique key each iteration to avoid overwrite optimization
        var uniqueHash = $"store{Interlocked.Increment(ref _storeCounter)}";
        await _ledger.CacheResultAsync(
            _storeStepName,
            uniqueHash,
            _resultToStore,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
    }
}

/// <summary>
/// Test result type for cache benchmarks.
/// </summary>
/// <remarks>
/// Simple record type used to measure serialization overhead in cache operations.
/// </remarks>
public sealed record TestCacheResult
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
