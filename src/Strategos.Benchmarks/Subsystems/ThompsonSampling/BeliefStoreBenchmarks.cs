// =============================================================================
// <copyright file="BeliefStoreBenchmarks.cs" company="Levelup Software">
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
/// Benchmarks for <see cref="IBeliefStore"/> operations measuring lookup performance
/// across varying store sizes.
/// </summary>
/// <remarks>
/// <para>
/// Focuses on the three primary query patterns for belief retrieval:
/// <list type="bullet">
///   <item><description>Single belief lookup by (agentId, category)</description></item>
///   <item><description>Agent-indexed secondary lookup</description></item>
///   <item><description>Category-indexed secondary lookup</description></item>
/// </list>
/// </para>
/// <para>
/// These benchmarks help identify scaling characteristics as the belief store grows
/// and validate that index operations remain efficient.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BeliefStoreBenchmarks
{
    private InMemoryBeliefStore _store = null!;
    private string _testAgentId = null!;
    private string _testCategory = null!;

    /// <summary>
    /// Gets or sets the total number of beliefs in the store.
    /// </summary>
    [Params(100, 1000, 10000)]
    public int BeliefCount { get; set; }

    /// <summary>
    /// Sets up the benchmark by populating the belief store.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Distribute beliefs across agents and categories
        // Aim for ~5 categories per agent
        var agentCount = Math.Max(1, BeliefCount / 5);
        var categoriesPerAgent = Math.Max(1, BeliefCount / agentCount);

        // Categories to cycle through
        var categories = new[] { "code", "review", "test", "deploy", "monitor", "debug", "refactor", "analyze", "optimize", "document" };

        var beliefIndex = 0;
        for (int a = 0; a < agentCount && beliefIndex < BeliefCount; a++)
        {
            var agentId = $"agent-{a:D4}";
            for (int c = 0; c < categoriesPerAgent && beliefIndex < BeliefCount; c++)
            {
                var category = categories[c % categories.Length];
                var belief = AgentBelief.CreatePrior(agentId, category);

                // Apply some history for realistic data
                for (int i = 0; i < (a % 5) + 1; i++)
                {
                    belief = belief.WithSuccess();
                }

                _store.SaveBeliefAsync(belief, CancellationToken.None).GetAwaiter().GetResult();
                beliefIndex++;
            }
        }

        // Pick a test agent and category that exist in the store
        _testAgentId = "agent-0000";
        _testCategory = "code";
    }

    /// <summary>
    /// Benchmarks a single belief lookup operation (baseline).
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Measures the performance of the primary lookup path by (agentId, category) key.
    /// </remarks>
    [Benchmark(Baseline = true)]
    public async Task<Result<AgentBelief>> GetBeliefAsync_SingleLookup()
    {
        return await _store.GetBeliefAsync(_testAgentId, _testCategory);
    }

    /// <summary>
    /// Benchmarks retrieving all beliefs for a specific agent.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Measures the secondary index performance for agent-based queries,
    /// which requires scanning all beliefs filtered by agent ID.
    /// </remarks>
    [Benchmark]
    public async Task<Result<IReadOnlyList<AgentBelief>>> GetBeliefsForAgentAsync_SecondaryIndex()
    {
        return await _store.GetBeliefsForAgentAsync(_testAgentId);
    }

    /// <summary>
    /// Benchmarks retrieving all beliefs for a specific category.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Measures the secondary index performance for category-based queries,
    /// which requires scanning all beliefs filtered by category.
    /// </remarks>
    [Benchmark]
    public async Task<Result<IReadOnlyList<AgentBelief>>> GetBeliefsForCategoryAsync_SecondaryIndex()
    {
        return await _store.GetBeliefsForCategoryAsync(_testCategory);
    }
}
