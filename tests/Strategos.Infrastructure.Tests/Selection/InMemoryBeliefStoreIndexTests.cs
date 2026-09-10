// =============================================================================
// <copyright file="InMemoryBeliefStoreIndexTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Infrastructure.Selection;

namespace Strategos.Infrastructure.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="InMemoryBeliefStore"/> index implementation,
/// specifically verifying the use of HashSet for memory efficiency.
/// </summary>
[Property("Category", "Unit")]
public sealed class InMemoryBeliefStoreIndexTests
{
    /// <summary>
    /// Verifies that the secondary indices use HashSet instead of ConcurrentDictionary
    /// to eliminate the byte sentinel overhead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Using ConcurrentDictionary&lt;string, byte&gt; as a set wastes memory because:
    /// - Each entry has a byte value (1 byte + padding = 8 bytes on 64-bit)
    /// - The byte is always 0 (sentinel) and never used.
    /// </para>
    /// <para>
    /// HashSet&lt;string&gt; eliminates this overhead by storing only the keys.
    /// This test uses reflection to verify the internal implementation choice.
    /// </para>
    /// </remarks>
    [Test]
    public async Task AddToIndices_HashSet_EliminatesByteSentinel()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act - Add a belief to trigger index population
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);

        // Assert - Use reflection to verify internal index types
        var storeType = store.GetType();

        // Check _byAgent field
        var byAgentField = storeType.GetField("_byAgent", BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(byAgentField).IsNotNull();

        var byAgentValue = byAgentField!.GetValue(store);
        await Assert.That(byAgentValue).IsNotNull();

        // The outer dictionary should map string -> HashSet<string>
        var byAgentType = byAgentValue!.GetType();
        var genericArgs = byAgentType.GetGenericArguments();

        // Verify the value type is HashSet<string> (wrapped in a thread-safe container)
        // We check that it's NOT ConcurrentDictionary<string, byte>
        await Assert.That(genericArgs.Length).IsGreaterThanOrEqualTo(2);

        var valueType = genericArgs[1];
        var isConcurrentDictionaryWithByte = valueType.IsGenericType
            && valueType.GetGenericTypeDefinition() == typeof(System.Collections.Concurrent.ConcurrentDictionary<,>)
            && valueType.GetGenericArguments().Length == 2
            && valueType.GetGenericArguments()[1] == typeof(byte);

        await Assert.That(isConcurrentDictionaryWithByte)
            .IsFalse()
            .Because("Index should use HashSet<string>, not ConcurrentDictionary<string, byte>");

        // Check _byCategory field
        var byCategoryField = storeType.GetField("_byCategory", BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(byCategoryField).IsNotNull();

        var byCategoryValue = byCategoryField!.GetValue(store);
        await Assert.That(byCategoryValue).IsNotNull();

        var byCategoryType = byCategoryValue!.GetType();
        var categoryGenericArgs = byCategoryType.GetGenericArguments();

        await Assert.That(categoryGenericArgs.Length).IsGreaterThanOrEqualTo(2);

        var categoryValueType = categoryGenericArgs[1];
        var isCategoryDictionaryWithByte = categoryValueType.IsGenericType
            && categoryValueType.GetGenericTypeDefinition() == typeof(System.Collections.Concurrent.ConcurrentDictionary<,>)
            && categoryValueType.GetGenericArguments().Length == 2
            && categoryValueType.GetGenericArguments()[1] == typeof(byte);

        await Assert.That(isCategoryDictionaryWithByte)
            .IsFalse()
            .Because("Index should use HashSet<string>, not ConcurrentDictionary<string, byte>");
    }

    /// <summary>
    /// Verifies that the HashSet-based indices still maintain thread safety
    /// under concurrent access.
    /// </summary>
    [Test]
    public async Task HashSetIndices_ConcurrentAccess_MaintainsThreadSafety()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int taskCount = 100;
        const int categoriesPerTask = 10;

        // Act - Perform concurrent updates from multiple tasks
        var tasks = new List<Task>();
        for (int t = 0; t < taskCount; t++)
        {
            var taskId = t;
            tasks.Add(Task.Run(async () =>
            {
                for (int c = 0; c < categoriesPerTask; c++)
                {
                    await store.UpdateBeliefAsync($"agent-{taskId}", $"category-{c}", success: true).ConfigureAwait(false);
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Verify all beliefs were indexed correctly
        for (int t = 0; t < taskCount; t++)
        {
            var agentBeliefs = await store.GetBeliefsForAgentAsync($"agent-{t}").ConfigureAwait(false);
            await Assert.That(agentBeliefs.IsSuccess).IsTrue();
            await Assert.That(agentBeliefs.Value.Count).IsEqualTo(categoriesPerTask);
        }

        for (int c = 0; c < categoriesPerTask; c++)
        {
            var categoryBeliefs = await store.GetBeliefsForCategoryAsync($"category-{c}").ConfigureAwait(false);
            await Assert.That(categoryBeliefs.IsSuccess).IsTrue();
            await Assert.That(categoryBeliefs.Value.Count).IsEqualTo(taskCount);
        }
    }

    /// <summary>
    /// Verifies that simultaneous reads and writes to the HashSet indices
    /// do not cause exceptions or data corruption.
    /// </summary>
    [Test]
    public async Task HashSetIndices_SimultaneousReadsWrites_NoExceptions()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int iterationCount = 50;
        var exceptions = new List<Exception>();

        // Pre-populate with some data
        for (int i = 0; i < 10; i++)
        {
            await store.UpdateBeliefAsync($"agent-{i}", "initial-category", success: true).ConfigureAwait(false);
        }

        // Act - Concurrent reads and writes
        var tasks = new List<Task>();
        for (int i = 0; i < iterationCount; i++)
        {
            var index = i;

            // Writers
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await store.UpdateBeliefAsync($"agent-{index % 10}", $"category-{index}", success: true).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));

            // Readers (agent index)
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await store.GetBeliefsForAgentAsync($"agent-{index % 10}").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));

            // Readers (category index)
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await store.GetBeliefsForCategoryAsync($"category-{index % 5}").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - No exceptions should have occurred
        await Assert.That(exceptions).IsEmpty();
    }

    /// <summary>
    /// Verifies that indices remain consistent after multiple updates to the same belief.
    /// The belief should only appear once in each index regardless of update count.
    /// </summary>
    [Test]
    public async Task IndexConsistency_MultipleUpdatesToSameBelief_NoIndexDuplicates()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int updateCount = 50;

        // Act - Update the same belief many times
        for (int i = 0; i < updateCount; i++)
        {
            await store.UpdateBeliefAsync("agent-1", "category-1", success: i % 2 == 0).ConfigureAwait(false);
        }

        // Assert - The belief should appear exactly once in each index
        var byAgent = await store.GetBeliefsForAgentAsync("agent-1").ConfigureAwait(false);
        var byCategory = await store.GetBeliefsForCategoryAsync("category-1").ConfigureAwait(false);

        await Assert.That(byAgent.IsSuccess).IsTrue();
        await Assert.That(byAgent.Value.Count).IsEqualTo(1)
            .Because("repeated updates should not create duplicate index entries");

        await Assert.That(byCategory.IsSuccess).IsTrue();
        await Assert.That(byCategory.Value.Count).IsEqualTo(1)
            .Because("repeated updates should not create duplicate index entries");

        // Verify the belief values are correct (accumulated from all updates)
        var belief = byAgent.Value[0];
        await Assert.That(belief.ObservationCount).IsEqualTo(updateCount);
    }

    /// <summary>
    /// Verifies that concurrent updates to different agents maintain index isolation.
    /// Each agent's index should only contain that agent's beliefs.
    /// </summary>
    [Test]
    public async Task ConcurrentUpdates_DifferentAgents_IndicesRemainIsolated()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int agentCount = 50;
        const int updatesPerAgent = 20;

        // Act - Concurrent updates to many different agents
        var tasks = new List<Task>();
        for (int a = 0; a < agentCount; a++)
        {
            var agentId = $"agent-{a}";
            tasks.Add(Task.Run(async () =>
            {
                for (int u = 0; u < updatesPerAgent; u++)
                {
                    await store.UpdateBeliefAsync(agentId, $"category-{u % 5}", success: true).ConfigureAwait(false);
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Each agent's index contains exactly 5 distinct categories
        for (int a = 0; a < agentCount; a++)
        {
            var beliefs = await store.GetBeliefsForAgentAsync($"agent-{a}").ConfigureAwait(false);
            await Assert.That(beliefs.IsSuccess).IsTrue();
            await Assert.That(beliefs.Value.Count).IsEqualTo(5)
                .Because("each agent has exactly 5 unique categories");
            await Assert.That(beliefs.Value.All(b => b.AgentId == $"agent-{a}")).IsTrue()
                .Because("agent index should only contain beliefs for that agent");
        }
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync correctly updates both indices when replacing an existing belief.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_ReplacesExisting_IndicesRemainConsistent()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var initialBelief = Strategos.Selection.AgentBelief.CreatePrior("agent-1", "category-1");
        await store.SaveBeliefAsync(initialBelief).ConfigureAwait(false);

        // Act - Save an updated belief with the same key
        var updatedBelief = initialBelief.WithSuccess().WithSuccess().WithFailure();
        await store.SaveBeliefAsync(updatedBelief).ConfigureAwait(false);

        // Assert - Indices should have exactly one entry with updated values
        var byAgent = await store.GetBeliefsForAgentAsync("agent-1").ConfigureAwait(false);
        var byCategory = await store.GetBeliefsForCategoryAsync("category-1").ConfigureAwait(false);

        await Assert.That(byAgent.Value.Count).IsEqualTo(1);
        await Assert.That(byCategory.Value.Count).IsEqualTo(1);
        await Assert.That(byAgent.Value[0].ObservationCount).IsEqualTo(3);
        await Assert.That(byAgent.Value[0].Alpha).IsEqualTo(updatedBelief.Alpha);
        await Assert.That(byAgent.Value[0].Beta).IsEqualTo(updatedBelief.Beta);
    }

    /// <summary>
    /// Verifies large-scale index operations with many agents and categories.
    /// Ensures indices correctly track a high volume of beliefs.
    /// </summary>
    [Test]
    public async Task LargeScaleIndex_ManyAgentsAndCategories_IndicesCorrect()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int agentCount = 200;
        const int categoryCount = 50;

        // Act - Create beliefs for all combinations
        for (int a = 0; a < agentCount; a++)
        {
            for (int c = 0; c < categoryCount; c++)
            {
                await store.UpdateBeliefAsync($"agent-{a}", $"category-{c}", success: true).ConfigureAwait(false);
            }
        }

        // Assert - Verify agent indices (sample every 40th agent)
        for (int a = 0; a < agentCount; a += 40)
        {
            var beliefs = await store.GetBeliefsForAgentAsync($"agent-{a}").ConfigureAwait(false);
            await Assert.That(beliefs.IsSuccess).IsTrue();
            await Assert.That(beliefs.Value.Count).IsEqualTo(categoryCount);
        }

        // Assert - Verify category indices (sample every 10th category)
        for (int c = 0; c < categoryCount; c += 10)
        {
            var beliefs = await store.GetBeliefsForCategoryAsync($"category-{c}").ConfigureAwait(false);
            await Assert.That(beliefs.IsSuccess).IsTrue();
            await Assert.That(beliefs.Value.Count).IsEqualTo(agentCount);
        }
    }

    /// <summary>
    /// Verifies that GetBeliefAsync correctly adds new beliefs to indices
    /// when creating a prior for an unknown agent/category.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_CreatesNewPrior_AddsToIndices()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act - GetBeliefAsync creates a prior when belief doesn't exist
        var result = await store.GetBeliefAsync("new-agent", "new-category").ConfigureAwait(false);

        // Assert - The prior should be indexed correctly
        await Assert.That(result.IsSuccess).IsTrue();

        var byAgent = await store.GetBeliefsForAgentAsync("new-agent").ConfigureAwait(false);
        var byCategory = await store.GetBeliefsForCategoryAsync("new-category").ConfigureAwait(false);

        await Assert.That(byAgent.IsSuccess).IsTrue();
        await Assert.That(byAgent.Value.Count).IsEqualTo(1);
        await Assert.That(byAgent.Value[0].AgentId).IsEqualTo("new-agent");

        await Assert.That(byCategory.IsSuccess).IsTrue();
        await Assert.That(byCategory.Value.Count).IsEqualTo(1);
        await Assert.That(byCategory.Value[0].TaskCategory).IsEqualTo("new-category");
    }

    /// <summary>
    /// Verifies thread safety when concurrent reads and writes occur on overlapping keys.
    /// This tests the lock mechanism under contention.
    /// </summary>
    [Test]
    public async Task ConcurrentReadsWrites_OverlappingKeys_NoDataCorruption()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int taskCount = 100;
        var exceptions = new List<Exception>();
        var successCounts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        var failureCounts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        // Act - Many concurrent operations on overlapping agent/category combinations
        var tasks = new List<Task>();
        for (int i = 0; i < taskCount; i++)
        {
            var index = i;
            var agentId = $"agent-{index % 3}"; // Only 3 agents for high contention
            var category = $"category-{index % 2}"; // Only 2 categories for high contention

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    bool success = index % 2 == 0;
                    await store.UpdateBeliefAsync(agentId, category, success: success).ConfigureAwait(false);

                    var key = $"{agentId}_{category}";
                    if (success)
                    {
                        successCounts.AddOrUpdate(key, 1, (_, c) => c + 1);
                    }
                    else
                    {
                        failureCounts.AddOrUpdate(key, 1, (_, c) => c + 1);
                    }

                    // Concurrent read
                    await store.GetBeliefsForAgentAsync(agentId).ConfigureAwait(false);
                    await store.GetBeliefsForCategoryAsync(category).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - No exceptions and data consistency
        await Assert.That(exceptions).IsEmpty();

        // Verify all beliefs are accessible and have correct counts
        for (int a = 0; a < 3; a++)
        {
            var beliefs = await store.GetBeliefsForAgentAsync($"agent-{a}").ConfigureAwait(false);
            await Assert.That(beliefs.IsSuccess).IsTrue();
            await Assert.That(beliefs.Value.Count).IsLessThanOrEqualTo(2)
                .Because("each agent should have at most 2 categories");
        }
    }

    /// <summary>
    /// Verifies that indices correctly track beliefs when the same belief is accessed
    /// through both GetBeliefAsync and UpdateBeliefAsync in concurrent operations.
    /// </summary>
    [Test]
    public async Task MixedGetAndUpdate_SameBelief_IndexRemainsSingleEntry()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int operationCount = 100;

        // Act - Interleave Get and Update operations
        var tasks = new List<Task>();
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            if (index % 2 == 0)
            {
                tasks.Add(store.GetBeliefAsync("agent-x", "category-y").AsTask());
            }
            else
            {
                tasks.Add(store.UpdateBeliefAsync("agent-x", "category-y", success: true).AsTask());
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Exactly one entry in each index
        var byAgent = await store.GetBeliefsForAgentAsync("agent-x").ConfigureAwait(false);
        var byCategory = await store.GetBeliefsForCategoryAsync("category-y").ConfigureAwait(false);

        await Assert.That(byAgent.Value.Count).IsEqualTo(1);
        await Assert.That(byCategory.Value.Count).IsEqualTo(1);
    }
}

