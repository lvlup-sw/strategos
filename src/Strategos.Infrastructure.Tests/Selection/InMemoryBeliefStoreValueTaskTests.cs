// =============================================================================
// <copyright file="InMemoryBeliefStoreValueTaskTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Infrastructure.Selection;
using Strategos.Selection;

namespace Strategos.Infrastructure.Tests.Selection;

/// <summary>
/// Tests verifying that <see cref="InMemoryBeliefStore"/> returns ValueTask that completes
/// synchronously, avoiding Task allocations on the hot path.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that the ValueTask returned by each method has
/// <see cref="ValueTask{TResult}.IsCompletedSuccessfully"/> set to true immediately,
/// which indicates the operation completed synchronously without allocating a Task.
/// </para>
/// <para>
/// This is critical for performance in high-throughput scenarios where belief lookups
/// occur frequently during agent selection.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class InMemoryBeliefStoreValueTaskTests
{
    // =============================================================================
    // A. GetBeliefAsync Synchronous Completion Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefAsync completes synchronously for a new belief (cache miss).
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_NewBelief_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var valueTask = store.GetBeliefAsync("agent-1", "CodeGeneration");

        // Assert - ValueTask should be completed synchronously (no Task allocation)
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        // Verify the result is valid
        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that GetBeliefAsync completes synchronously for an existing belief (cache hit).
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_ExistingBelief_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);

        // Act
        var valueTask = store.GetBeliefAsync("agent-1", "CodeGeneration");

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Alpha).IsEqualTo(3.0); // Verify correct belief returned
    }

    // =============================================================================
    // B. UpdateBeliefAsync Synchronous Completion Tests
    // =============================================================================

    /// <summary>
    /// Verifies that UpdateBeliefAsync completes synchronously for a new belief.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_NewBelief_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var valueTask = store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true);

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that UpdateBeliefAsync completes synchronously for an existing belief.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_ExistingBelief_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);

        // Act
        var valueTask = store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: false);

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
    }

    // =============================================================================
    // C. GetBeliefsForAgentAsync Synchronous Completion Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync completes synchronously when agent has no beliefs.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_NoBeliefs_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var valueTask = store.GetBeliefsForAgentAsync("unknown-agent");

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEmpty();
    }

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync completes synchronously with existing beliefs.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_WithBeliefs_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "DataAnalysis", success: false).ConfigureAwait(false);

        // Act
        var valueTask = store.GetBeliefsForAgentAsync("agent-1");

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Count).IsEqualTo(2);
    }

    // =============================================================================
    // D. GetBeliefsForCategoryAsync Synchronous Completion Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync completes synchronously when category has no beliefs.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_NoBeliefs_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var valueTask = store.GetBeliefsForCategoryAsync("UnknownCategory");

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEmpty();
    }

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync completes synchronously with existing beliefs.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_WithBeliefs_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-2", "CodeGeneration", success: false).ConfigureAwait(false);

        // Act
        var valueTask = store.GetBeliefsForCategoryAsync("CodeGeneration");

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Count).IsEqualTo(2);
    }

    // =============================================================================
    // E. SaveBeliefAsync Synchronous Completion Tests
    // =============================================================================

    /// <summary>
    /// Verifies that SaveBeliefAsync completes synchronously for a new belief.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_NewBelief_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var belief = AgentBelief.CreatePrior("agent-1", "CodeGeneration");

        // Act
        var valueTask = store.SaveBeliefAsync(belief);

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync completes synchronously when overwriting an existing belief.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_ExistingBelief_CompletesSynchronously()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var initialBelief = AgentBelief.CreatePrior("agent-1", "CodeGeneration");
        await store.SaveBeliefAsync(initialBelief).ConfigureAwait(false);

        var updatedBelief = initialBelief.WithSuccess().WithSuccess();

        // Act
        var valueTask = store.SaveBeliefAsync(updatedBelief);

        // Assert - ValueTask should be completed synchronously
        await Assert.That(valueTask.IsCompletedSuccessfully).IsTrue();

        var result = await valueTask.ConfigureAwait(false);
        await Assert.That(result.IsSuccess).IsTrue();
    }

    // =============================================================================
    // F. Comprehensive Synchronous Completion Tests
    // =============================================================================

    /// <summary>
    /// Verifies that all belief store methods complete synchronously across
    /// multiple sequential operations, ensuring no Task allocations occur.
    /// </summary>
    [Test]
    public async Task AllMethods_MultipleOperations_CompletesSynchronouslyConsistently()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int operationCount = 100;

        // Act & Assert - Perform many operations and verify each completes synchronously
        for (int i = 0; i < operationCount; i++)
        {
            var agentId = $"agent-{i % 10}";
            var category = $"category-{i % 5}";

            // GetBeliefAsync
            var getTask = store.GetBeliefAsync(agentId, category);
            await Assert.That(getTask.IsCompletedSuccessfully).IsTrue();
            _ = await getTask.ConfigureAwait(false);

            // UpdateBeliefAsync
            var updateTask = store.UpdateBeliefAsync(agentId, category, success: i % 2 == 0);
            await Assert.That(updateTask.IsCompletedSuccessfully).IsTrue();
            _ = await updateTask.ConfigureAwait(false);

            // GetBeliefsForAgentAsync
            var byAgentTask = store.GetBeliefsForAgentAsync(agentId);
            await Assert.That(byAgentTask.IsCompletedSuccessfully).IsTrue();
            _ = await byAgentTask.ConfigureAwait(false);

            // GetBeliefsForCategoryAsync
            var byCategoryTask = store.GetBeliefsForCategoryAsync(category);
            await Assert.That(byCategoryTask.IsCompletedSuccessfully).IsTrue();
            _ = await byCategoryTask.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync maintains synchronous completion across
    /// multiple save operations with different beliefs.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_MultipleSaves_CompletesSynchronouslyConsistently()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int saveCount = 50;

        // Act & Assert
        for (int i = 0; i < saveCount; i++)
        {
            var belief = AgentBelief.CreatePrior($"agent-{i}", $"category-{i % 5}");
            var saveTask = store.SaveBeliefAsync(belief);

            await Assert.That(saveTask.IsCompletedSuccessfully).IsTrue();
            _ = await saveTask.ConfigureAwait(false);
        }

        // Verify all beliefs were saved
        for (int i = 0; i < saveCount; i++)
        {
            var getTask = store.GetBeliefAsync($"agent-{i}", $"category-{i % 5}");
            await Assert.That(getTask.IsCompletedSuccessfully).IsTrue();

            var result = await getTask.ConfigureAwait(false);
            await Assert.That(result.IsSuccess).IsTrue();
        }
    }
}

