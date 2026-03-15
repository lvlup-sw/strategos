// =============================================================================
// <copyright file="CandidateSelectionBenchmarks.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Benchmarks.Fixtures;
using Strategos.Infrastructure.Selection;
using Strategos.Selection;

using BenchmarkDotNet.Attributes;

namespace Strategos.Benchmarks.Subsystems.LargeScale;

/// <summary>
/// Large-scale benchmarks for candidate selection at production scale.
/// </summary>
/// <remarks>
/// <para>
/// Measures the performance of the Thompson Sampling agent selection algorithm
/// with candidate pools up to 10K+ agents to understand production-scale behavior.
/// </para>
/// <para>
/// Key metrics at scale:
/// <list type="bullet">
///   <item><description>Selection latency with large candidate pools</description></item>
///   <item><description>Memory allocation patterns at scale</description></item>
///   <item><description>Belief store lookup efficiency</description></item>
///   <item><description>Beta distribution sampling overhead</description></item>
/// </list>
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class CandidateSelectionBenchmarks
{
    private static readonly string[] Categories = ["code", "review", "test", "deploy", "monitor"];

    private ThompsonSamplingAgentSelector selector = null!;
    private AgentSelectionContext context = null!;
    private IReadOnlyList<string> agentIds = null!;

    /// <summary>
    /// Gets or sets the number of candidate agents for selection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scales from 100 to 10,000 candidates to measure performance
    /// characteristics across two orders of magnitude.
    /// </para>
    /// </remarks>
    [Params(100, 1000, 10000)]
    public int CandidateCount { get; set; }

    /// <summary>
    /// Sets up the benchmark by creating the selector with a populated belief store.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        // Create a populated belief store with beliefs for all agents
        var beliefStore = TestAgents.CreatePopulatedBeliefStore(this.CandidateCount, categoriesPerAgent: 5);

        // Create selector with a fixed seed for reproducible benchmarks
        this.selector = new ThompsonSamplingAgentSelector(beliefStore, new TaskCategoryClassifier(), randomSeed: 42);

        // Create agent IDs
        this.agentIds = TestAgents.CreateAgentIds(this.CandidateCount);

        // Create the selection context
        this.context = new AgentSelectionContext
        {
            WorkflowId = Guid.NewGuid(),
            StepName = "benchmark-step",
            TaskDescription = "Implement a new feature with unit tests",
            AvailableAgents = this.agentIds,
        };
    }

    /// <summary>
    /// Benchmarks the baseline agent selection operation at scale.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// <para>
    /// Measures the full selection cycle including belief lookups and Beta sampling
    /// for all candidate agents at large scale.
    /// </para>
    /// </remarks>
    [Benchmark(Baseline = true)]
    public async Task<Strategos.Primitives.Result<AgentSelection>> SelectAgent()
    {
        return await this.selector.SelectAgentAsync(this.context);
    }

    /// <summary>
    /// Benchmarks multiple consecutive selections to measure amortized performance.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// <para>
    /// Simulates realistic usage where multiple selections occur in sequence.
    /// This helps identify any caching benefits or state accumulation issues.
    /// </para>
    /// </remarks>
    [Benchmark]
    public async Task<int> SelectMultipleAgents()
    {
        int successCount = 0;
        for (int i = 0; i < 10; i++)
        {
            var result = await this.selector.SelectAgentAsync(this.context);
            if (result.IsSuccess)
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// Benchmarks selection with varying task categories.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// <para>
    /// Tests selection performance when the task category changes,
    /// forcing different belief lookups per selection.
    /// </para>
    /// </remarks>
    [Benchmark]
    public async Task<int> SelectWithVaryingCategories()
    {
        int successCount = 0;

        foreach (var category in Categories)
        {
            var categoryContext = new AgentSelectionContext
            {
                WorkflowId = Guid.NewGuid(),
                StepName = "benchmark-step",
                TaskDescription = $"Perform {category} task",
                AvailableAgents = this.agentIds,
            };

            var result = await this.selector.SelectAgentAsync(categoryContext);
            if (result.IsSuccess)
            {
                successCount++;
            }
        }

        return successCount;
    }
}
