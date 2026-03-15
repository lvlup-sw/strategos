// =============================================================================
// <copyright file="BeliefStoreAllocationBenchmarks.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Abstractions;
using Strategos.Infrastructure.Selection;
using Strategos.Primitives;
using Strategos.Selection;

using BenchmarkDotNet.Attributes;

namespace Strategos.Benchmarks.Subsystems.ThompsonSampling;

/// <summary>
/// Benchmarks focusing on allocation behavior for <see cref="IBeliefStore"/> hot paths.
/// </summary>
/// <remarks>
/// <para>
/// Validates that critical belief store operations minimize or avoid heap allocations
/// to reduce GC pressure in high-throughput scenarios.
/// </para>
/// <para>
/// Key metrics observed:
/// <list type="bullet">
///   <item><description>Gen0 collections (per operation)</description></item>
///   <item><description>Bytes allocated per operation</description></item>
///   <item><description>Allocation patterns in cache-hit scenarios</description></item>
/// </list>
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BeliefStoreAllocationBenchmarks
{
    private InMemoryBeliefStore _store = null!;
    private string _cachedAgentId = null!;
    private string _cachedCategory = null!;
    private string _newAgentId = null!;
    private string _newCategory = null!;
    private int _updateCounter;

    /// <summary>
    /// Sets up the benchmark by pre-populating beliefs for cache-hit scenarios.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Pre-populate a belief for cache-hit testing
        _cachedAgentId = "agent-cached";
        _cachedCategory = "code";
        var belief = AgentBelief.CreatePrior(_cachedAgentId, _cachedCategory);
        _store.SaveBeliefAsync(belief, CancellationToken.None).GetAwaiter().GetResult();

        // Agent IDs for update testing
        _newAgentId = "agent-update";
        _newCategory = "test";
        _updateCounter = 0;
    }

    /// <summary>
    /// Resets state before each iteration for update benchmarks.
    /// </summary>
    [IterationSetup(Target = nameof(UpdateBeliefAsync_Allocations))]
    public void IterationSetup()
    {
        // Create a fresh agent/category for each iteration to test AddOrUpdate path
        _updateCounter++;
        _newAgentId = $"agent-update-{_updateCounter:D6}";
    }

    /// <summary>
    /// Benchmarks allocation behavior when retrieving a cached belief.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// <para>
    /// Cache hits should have minimal allocations - primarily the Result wrapper.
    /// This is the hot path during agent selection where each candidate's belief
    /// is retrieved.
    /// </para>
    /// <para>
    /// Target: minimal allocations per lookup.
    /// </para>
    /// </remarks>
    [Benchmark(Baseline = true)]
    public async Task<Result<AgentBelief>> GetBeliefAsync_CacheHit_Allocations()
    {
        return await _store.GetBeliefAsync(_cachedAgentId, _cachedCategory);
    }

    /// <summary>
    /// Benchmarks allocation behavior when updating a belief.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// <para>
    /// Update operations create new AgentBelief records due to immutability.
    /// This benchmark measures the allocation overhead of the update path.
    /// </para>
    /// <para>
    /// Expected allocations: Result wrapper + new AgentBelief record.
    /// </para>
    /// </remarks>
    [Benchmark]
    public async Task<Result<Unit>> UpdateBeliefAsync_Allocations()
    {
        return await _store.UpdateBeliefAsync(_newAgentId, _newCategory, success: true);
    }
}
