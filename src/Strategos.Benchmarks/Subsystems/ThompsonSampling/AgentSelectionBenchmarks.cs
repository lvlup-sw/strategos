// =============================================================================
// <copyright file="AgentSelectionBenchmarks.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Benchmarks.Fixtures;
using Strategos.Infrastructure.Selection;
using Strategos.Selection;

using BenchmarkDotNet.Attributes;

namespace Strategos.Benchmarks.Subsystems.ThompsonSampling;

/// <summary>
/// Benchmarks for <see cref="ThompsonSamplingAgentSelector"/> agent selection performance.
/// </summary>
/// <remarks>
/// <para>
/// Measures the performance of the Thompson Sampling agent selection algorithm
/// across varying candidate pool sizes to understand selection overhead.
/// </para>
/// <para>
/// Key metrics:
/// <list type="bullet">
///   <item><description>Selection latency per candidate count</description></item>
///   <item><description>Memory allocations during selection</description></item>
///   <item><description>Scalability with increasing agent pools</description></item>
/// </list>
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class AgentSelectionBenchmarks
{
    private ThompsonSamplingAgentSelector _selector = null!;
    private AgentSelectionContext _context = null!;
    private IReadOnlyList<string> _agentIds = null!;

    /// <summary>
    /// Gets or sets the number of candidate agents for selection.
    /// </summary>
    [Params(5, 25, 100)]
    public int CandidateCount { get; set; }

    /// <summary>
    /// Sets up the benchmark by creating the selector with a populated belief store.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Create a populated belief store with beliefs for all agents
        var beliefStore = TestAgents.CreatePopulatedBeliefStore(CandidateCount, categoriesPerAgent: 5);

        // Create selector with a fixed seed for reproducible benchmarks
        _selector = new ThompsonSamplingAgentSelector(beliefStore, NullLogger<ThompsonSamplingAgentSelector>.Instance, randomSeed: 42);

        // Create agent IDs
        _agentIds = TestAgents.CreateAgentIds(CandidateCount);

        // Create the selection context
        _context = new AgentSelectionContext
        {
            WorkflowId = Guid.NewGuid(),
            StepName = "benchmark-step",
            TaskDescription = "Implement a new feature with unit tests",
            AvailableAgents = _agentIds,
        };
    }

    /// <summary>
    /// Benchmarks the baseline agent selection operation.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Measures the full selection cycle including belief lookups and Beta sampling
    /// for all candidate agents.
    /// </remarks>
    [Benchmark(Baseline = true)]
    public async Task<Strategos.Primitives.Result<AgentSelection>> SelectAgent()
    {
        return await _selector.SelectAgentAsync(_context);
    }
}
