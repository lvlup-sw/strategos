// =============================================================================
// <copyright file="TestAgents.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Infrastructure.Selection;
using Strategos.Selection;

namespace Strategos.Benchmarks.Fixtures;

/// <summary>
/// Provides test data generators for agent-related benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// Generates agent identifiers and populated belief stores for
/// Thompson Sampling and agent selection benchmarks.
/// </para>
/// </remarks>
public static class TestAgents
{
    private static readonly string[] Categories = ["code", "review", "test", "deploy", "monitor"];

    /// <summary>
    /// Creates a list of agent identifiers with the specified count.
    /// </summary>
    /// <param name="count">The number of agent IDs to create.</param>
    /// <returns>A read-only list of agent identifiers.</returns>
    public static IReadOnlyList<string> CreateAgentIds(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => $"agent-{i:D4}")
            .ToList();
    }

    /// <summary>
    /// Creates an <see cref="InMemoryBeliefStore"/> populated with beliefs
    /// for a specified number of agents and categories.
    /// </summary>
    /// <param name="agentCount">The number of agents to create beliefs for.</param>
    /// <param name="categoriesPerAgent">The number of categories per agent.</param>
    /// <returns>A populated belief store suitable for benchmarks.</returns>
    /// <remarks>
    /// <para>
    /// Beliefs are initialized with varying success and failure counts
    /// to simulate realistic historical data. The pattern ensures reproducible
    /// benchmark conditions.
    /// </para>
    /// </remarks>
    public static InMemoryBeliefStore CreatePopulatedBeliefStore(int agentCount, int categoriesPerAgent)
    {
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        for (int a = 0; a < agentCount; a++)
        {
            var agentId = $"agent-{a:D4}";
            for (int c = 0; c < Math.Min(categoriesPerAgent, Categories.Length); c++)
            {
                // Create belief with some successes and failures applied
                var belief = CreateBeliefWithHistory(agentId, Categories[c], successCount: 10 + (a % 5), failureCount: 2 + (c % 3));
                store.SaveBeliefAsync(belief, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        return store;
    }

    /// <summary>
    /// Creates an <see cref="AgentBelief"/> with simulated history.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="taskCategory">The task category.</param>
    /// <param name="successCount">The number of successes to apply.</param>
    /// <param name="failureCount">The number of failures to apply.</param>
    /// <returns>An agent belief with the specified history applied.</returns>
    private static AgentBelief CreateBeliefWithHistory(string agentId, string taskCategory, int successCount, int failureCount)
    {
        var belief = AgentBelief.CreatePrior(agentId, taskCategory);

        // Apply successes
        for (int i = 0; i < successCount; i++)
        {
            belief = belief.WithSuccess();
        }

        // Apply failures
        for (int i = 0; i < failureCount; i++)
        {
            belief = belief.WithFailure();
        }

        return belief;
    }
}
