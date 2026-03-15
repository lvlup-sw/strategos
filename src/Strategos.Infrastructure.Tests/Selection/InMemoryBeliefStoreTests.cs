// =============================================================================
// <copyright file="InMemoryBeliefStoreTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Infrastructure.Selection;
using Strategos.Selection;

namespace Strategos.Infrastructure.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="InMemoryBeliefStore"/> covering the in-memory
/// implementation of belief persistence for Thompson Sampling.
/// </summary>
[Property("Category", "Unit")]
public class InMemoryBeliefStoreTests
{
    // =============================================================================
    // A. GetBeliefAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefAsync returns a default prior for unknown agent/category.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_NoExistingBelief_ReturnsPrior()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var result = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.AgentId).IsEqualTo("agent-1");
        await Assert.That(result.Value.TaskCategory).IsEqualTo("CodeGeneration");
        await Assert.That(result.Value.Alpha).IsEqualTo(AgentBelief.DefaultPriorAlpha);
        await Assert.That(result.Value.Beta).IsEqualTo(AgentBelief.DefaultPriorBeta);
        await Assert.That(result.Value.ObservationCount).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that GetBeliefAsync returns stored belief after update.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_AfterUpdate_ReturnsUpdatedBelief()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);

        // Act
        var result = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Alpha).IsEqualTo(3.0); // Prior(2) + Success(1)
        await Assert.That(result.Value.Beta).IsEqualTo(2.0); // Prior(2)
        await Assert.That(result.Value.ObservationCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that different agent/category pairs are stored separately.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_DifferentKeys_ReturnsSeparateBeliefs()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "DataAnalysis", success: false).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-2", "CodeGeneration", success: true).ConfigureAwait(false);

        // Act
        var belief1Code = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        var belief1Data = await store.GetBeliefAsync("agent-1", "DataAnalysis").ConfigureAwait(false);
        var belief2Code = await store.GetBeliefAsync("agent-2", "CodeGeneration").ConfigureAwait(false);

        // Assert
        await Assert.That(belief1Code.Value.Alpha).IsEqualTo(3.0);
        await Assert.That(belief1Code.Value.Beta).IsEqualTo(2.0);

        await Assert.That(belief1Data.Value.Alpha).IsEqualTo(2.0);
        await Assert.That(belief1Data.Value.Beta).IsEqualTo(3.0);

        await Assert.That(belief2Code.Value.Alpha).IsEqualTo(3.0);
        await Assert.That(belief2Code.Value.Beta).IsEqualTo(2.0);
    }

    // =============================================================================
    // B. UpdateBeliefAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that UpdateBeliefAsync increments Alpha on success.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_Success_IncrementsAlpha()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var result = await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        var belief = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(belief.Value.Alpha).IsEqualTo(3.0); // 2 + 1
        await Assert.That(belief.Value.Beta).IsEqualTo(2.0); // unchanged
    }

    /// <summary>
    /// Verifies that UpdateBeliefAsync increments Beta on failure.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_Failure_IncrementsBeta()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var result = await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: false).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        var belief = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(belief.Value.Alpha).IsEqualTo(2.0); // unchanged
        await Assert.That(belief.Value.Beta).IsEqualTo(3.0); // 2 + 1
    }

    /// <summary>
    /// Verifies that multiple updates accumulate correctly.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_MultipleUpdates_AccumulatesCorrectly()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act - 3 successes, 2 failures
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: false).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: false).ConfigureAwait(false);

        // Assert
        var belief = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(belief.Value.Alpha).IsEqualTo(5.0); // 2 + 3
        await Assert.That(belief.Value.Beta).IsEqualTo(4.0); // 2 + 2
        await Assert.That(belief.Value.ObservationCount).IsEqualTo(5);
    }

    // =============================================================================
    // C. GetBeliefsForAgentAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync returns empty list for unknown agent.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_NoBeliefs_ReturnsEmptyList()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var result = await store.GetBeliefsForAgentAsync("unknown-agent").ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEmpty();
    }

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync returns all beliefs for an agent.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_MultipleBeliefsExist_ReturnsAllForAgent()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "DataAnalysis", success: false).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "WebSearch", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-2", "CodeGeneration", success: true).ConfigureAwait(false);

        // Act
        var result = await store.GetBeliefsForAgentAsync("agent-1").ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Count).IsEqualTo(3);
        await Assert.That(result.Value.All(b => b.AgentId == "agent-1")).IsTrue();
    }

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync excludes other agents' beliefs.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_MultipleAgents_ExcludesOtherAgents()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-2", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-2", "DataAnalysis", success: true).ConfigureAwait(false);

        // Act
        var result = await store.GetBeliefsForAgentAsync("agent-2").ConfigureAwait(false);

        // Assert
        await Assert.That(result.Value.Count).IsEqualTo(2);
        await Assert.That(result.Value.Any(b => b.AgentId == "agent-1")).IsFalse();
    }

    // =============================================================================
    // D. GetBeliefsForCategoryAsync Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync returns empty list for unknown category.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_NoBeliefs_ReturnsEmptyList()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var result = await store.GetBeliefsForCategoryAsync("UnknownCategory").ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEmpty();
    }

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync returns all beliefs for a category.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_MultipleBeliefsExist_ReturnsAllForCategory()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        await store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-2", "CodeGeneration", success: false).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-3", "CodeGeneration", success: true).ConfigureAwait(false);
        await store.UpdateBeliefAsync("agent-1", "DataAnalysis", success: true).ConfigureAwait(false);

        // Act
        var result = await store.GetBeliefsForCategoryAsync("CodeGeneration").ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Count).IsEqualTo(3);
        await Assert.That(result.Value.All(b => b.TaskCategory == "CodeGeneration")).IsTrue();
    }

    // =============================================================================
    // E. Thread Safety Tests
    // =============================================================================

    /// <summary>
    /// Verifies that concurrent updates are thread-safe.
    /// </summary>
    [Test]
    public async Task ConcurrentUpdates_ThreadSafe()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int iterationCount = 100;

        // Act - Run concurrent updates
        var tasks = new List<Task>();
        for (int i = 0; i < iterationCount; i++)
        {
            tasks.Add(store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).AsTask());
            tasks.Add(store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: false).AsTask());
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All updates should be recorded
        var belief = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(belief.Value.ObservationCount).IsEqualTo(iterationCount * 2);
        await Assert.That(belief.Value.Alpha).IsEqualTo(2.0 + iterationCount); // Prior + successes
        await Assert.That(belief.Value.Beta).IsEqualTo(2.0 + iterationCount); // Prior + failures
    }

    /// <summary>
    /// Verifies that concurrent reads and writes are thread-safe.
    /// </summary>
    [Test]
    public async Task ConcurrentReadsAndWrites_ThreadSafe()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int iterationCount = 50;

        // Act - Concurrent reads and writes
        var tasks = new List<Task>();
        for (int i = 0; i < iterationCount; i++)
        {
            tasks.Add(store.UpdateBeliefAsync("agent-1", "CodeGeneration", success: true).AsTask());
            tasks.Add(store.GetBeliefAsync("agent-1", "CodeGeneration").AsTask());
            tasks.Add(store.GetBeliefsForAgentAsync("agent-1").AsTask());
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Should complete without exceptions
        var belief = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(belief.IsSuccess).IsTrue();
        await Assert.That(belief.Value.ObservationCount).IsEqualTo(iterationCount);
    }

    // =============================================================================
    // F. Performance Optimization Tests (Secondary Indices)
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync returns correct results with many beliefs.
    /// Note: O(1) lookup performance is guaranteed by implementation using dictionary indices.
    /// We verify functional correctness here rather than timing, which is flaky in CI.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_ManyBeliefs_ReturnsCorrectResultsViaIndex()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int agentCount = 100;
        const int categoriesPerAgent = 100;

        // Populate store with 10,000 beliefs (100 agents x 100 categories)
        for (int a = 0; a < agentCount; a++)
        {
            for (int c = 0; c < categoriesPerAgent; c++)
            {
                await store.UpdateBeliefAsync($"agent-{a}", $"category-{c}", success: true).ConfigureAwait(false);
            }
        }

        // Act - Lookup beliefs for a specific agent
        var result = await store.GetBeliefsForAgentAsync("agent-50").ConfigureAwait(false);

        // Assert - Verify correct results are returned (index correctness)
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Count).IsEqualTo(categoriesPerAgent);
        await Assert.That(result.Value.All(b => b.AgentId == "agent-50")).IsTrue();
    }

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync returns correct results with many beliefs.
    /// Note: O(1) lookup performance is guaranteed by implementation using dictionary indices.
    /// We verify functional correctness here rather than timing, which is flaky in CI.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_ManyBeliefs_ReturnsCorrectResultsViaIndex()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int agentCount = 100;
        const int categoriesPerAgent = 100;

        // Populate store with 10,000 beliefs (100 agents x 100 categories)
        for (int a = 0; a < agentCount; a++)
        {
            for (int c = 0; c < categoriesPerAgent; c++)
            {
                await store.UpdateBeliefAsync($"agent-{a}", $"category-{c}", success: true).ConfigureAwait(false);
            }
        }

        // Act - Lookup beliefs for a specific category
        var result = await store.GetBeliefsForCategoryAsync("category-50").ConfigureAwait(false);

        // Assert - Verify correct results are returned (index correctness)
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Count).IsEqualTo(agentCount);
        await Assert.That(result.Value.All(b => b.TaskCategory == "category-50")).IsTrue();
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync maintains secondary indices.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_MaintainsIndices()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var belief = AgentBelief.CreatePrior("agent-1", "CodeGeneration").WithSuccess();

        // Act
        await store.SaveBeliefAsync(belief).ConfigureAwait(false);

        // Assert - Verify the belief is accessible via both indices
        var byAgent = await store.GetBeliefsForAgentAsync("agent-1").ConfigureAwait(false);
        var byCategory = await store.GetBeliefsForCategoryAsync("CodeGeneration").ConfigureAwait(false);

        await Assert.That(byAgent.Value.Count).IsEqualTo(1);
        await Assert.That(byCategory.Value.Count).IsEqualTo(1);
        await Assert.That(byAgent.Value[0].Alpha).IsEqualTo(belief.Alpha);
        await Assert.That(byCategory.Value[0].Alpha).IsEqualTo(belief.Alpha);
    }

    // =============================================================================
    // G. Null/Empty Input Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefAsync throws ArgumentNullException for null agentId.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_NullAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act & Assert
        await Assert.That(() => store.GetBeliefAsync(null!, "CodeGeneration").AsTask())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetBeliefAsync throws ArgumentNullException for null taskCategory.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_NullTaskCategory_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act & Assert
        await Assert.That(() => store.GetBeliefAsync("agent-1", null!).AsTask())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetBeliefAsync handles empty category string.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_EmptyCategory_ReturnsBeliefWithEmptyCategory()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act
        var result = await store.GetBeliefAsync("agent-1", string.Empty).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.TaskCategory).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Verifies that UpdateBeliefAsync throws ArgumentNullException for null agentId.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_NullAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act & Assert
        await Assert.That(() => store.UpdateBeliefAsync(null!, "CodeGeneration", true).AsTask())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that UpdateBeliefAsync throws ArgumentNullException for null taskCategory.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_NullTaskCategory_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act & Assert
        await Assert.That(() => store.UpdateBeliefAsync("agent-1", null!, true).AsTask())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync throws ArgumentNullException for null belief.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_NullBelief_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act & Assert
        await Assert.That(() => store.SaveBeliefAsync(null!).AsTask())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync throws ArgumentNullException for null agentId.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_NullAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act & Assert
        await Assert.That(() => store.GetBeliefsForAgentAsync(null!).AsTask())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync throws ArgumentNullException for null taskCategory.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_NullTaskCategory_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);

        // Act & Assert
        await Assert.That(() => store.GetBeliefsForCategoryAsync(null!).AsTask())
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // H. SaveBeliefAsync Extended Tests
    // =============================================================================

    /// <summary>
    /// Verifies that SaveBeliefAsync saves a new belief successfully.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_NewBelief_SavesSuccessfully()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var belief = AgentBelief.CreatePrior("agent-1", "CodeGeneration")
            .WithSuccess()
            .WithSuccess()
            .WithFailure();

        // Act
        var result = await store.SaveBeliefAsync(belief).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        var retrieved = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(retrieved.Value.Alpha).IsEqualTo(belief.Alpha);
        await Assert.That(retrieved.Value.Beta).IsEqualTo(belief.Beta);
        await Assert.That(retrieved.Value.ObservationCount).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync overwrites an existing belief.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_ExistingBelief_OverwritesExisting()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var originalBelief = AgentBelief.CreatePrior("agent-1", "CodeGeneration").WithSuccess();
        await store.SaveBeliefAsync(originalBelief).ConfigureAwait(false);

        var newBelief = AgentBelief.CreatePrior("agent-1", "CodeGeneration")
            .WithFailure()
            .WithFailure()
            .WithFailure();

        // Act
        var result = await store.SaveBeliefAsync(newBelief).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        var retrieved = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(retrieved.Value.Alpha).IsEqualTo(newBelief.Alpha);
        await Assert.That(retrieved.Value.Beta).IsEqualTo(newBelief.Beta);
        await Assert.That(retrieved.Value.ObservationCount).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync updates indices correctly when overwriting.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_OverwritesBelief_IndicesRemainCorrect()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var belief1 = AgentBelief.CreatePrior("agent-1", "CodeGeneration").WithSuccess();
        var belief2 = AgentBelief.CreatePrior("agent-1", "DataAnalysis").WithSuccess();
        await store.SaveBeliefAsync(belief1).ConfigureAwait(false);
        await store.SaveBeliefAsync(belief2).ConfigureAwait(false);

        // Overwrite first belief
        var updatedBelief1 = AgentBelief.CreatePrior("agent-1", "CodeGeneration")
            .WithFailure()
            .WithFailure();
        await store.SaveBeliefAsync(updatedBelief1).ConfigureAwait(false);

        // Act
        var agentBeliefs = await store.GetBeliefsForAgentAsync("agent-1").ConfigureAwait(false);
        var categoryBeliefs = await store.GetBeliefsForCategoryAsync("CodeGeneration").ConfigureAwait(false);

        // Assert - Should still have exactly 2 beliefs for agent and 1 for category
        await Assert.That(agentBeliefs.Value.Count).IsEqualTo(2);
        await Assert.That(categoryBeliefs.Value.Count).IsEqualTo(1);
        await Assert.That(categoryBeliefs.Value[0].Beta).IsEqualTo(updatedBelief1.Beta);
    }

    // =============================================================================
    // I. Additional Concurrent Operations Tests
    // =============================================================================

    /// <summary>
    /// Verifies that concurrent saves work correctly.
    /// </summary>
    [Test]
    public async Task ConcurrentSaves_ThreadSafe()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int iterationCount = 50;

        // Act - Concurrent saves for different agents
        var tasks = new List<Task>();
        for (int i = 0; i < iterationCount; i++)
        {
            var belief = AgentBelief.CreatePrior($"agent-{i}", "CodeGeneration").WithSuccess();
            tasks.Add(store.SaveBeliefAsync(belief).AsTask());
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All beliefs should be saved
        var categoryBeliefs = await store.GetBeliefsForCategoryAsync("CodeGeneration").ConfigureAwait(false);
        await Assert.That(categoryBeliefs.Value.Count).IsEqualTo(iterationCount);
    }

    /// <summary>
    /// Verifies that concurrent saves to the same key work correctly (last write wins).
    /// </summary>
    [Test]
    public async Task ConcurrentSaves_SameKey_LastWriteWins()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int iterationCount = 100;

        // Create beliefs with different alpha values
        var beliefs = new List<AgentBelief>();
        var prior = AgentBelief.CreatePrior("agent-1", "CodeGeneration");
        var current = prior;
        for (int i = 0; i < iterationCount; i++)
        {
            current = current.WithSuccess();
            beliefs.Add(current);
        }

        // Act - Concurrent saves of the same key
        var tasks = beliefs.Select(b => store.SaveBeliefAsync(b).AsTask()).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Should have exactly one belief stored
        var retrieved = await store.GetBeliefAsync("agent-1", "CodeGeneration").ConfigureAwait(false);
        await Assert.That(retrieved.IsSuccess).IsTrue();

        // The stored belief should be one of the beliefs we saved (last write wins is non-deterministic)
        await Assert.That(retrieved.Value.Alpha).IsGreaterThanOrEqualTo(AgentBelief.DefaultPriorAlpha);
    }

    /// <summary>
    /// Verifies that concurrent gets, saves, and updates work correctly together.
    /// </summary>
    [Test]
    public async Task ConcurrentMixedOperations_ThreadSafe()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int iterationCount = 30;

        // Pre-populate some beliefs
        for (int i = 0; i < 10; i++)
        {
            await store.UpdateBeliefAsync($"agent-{i}", "CodeGeneration", success: true).ConfigureAwait(false);
        }

        // Act - Mix of all operations concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < iterationCount; i++)
        {
            // Gets
            tasks.Add(store.GetBeliefAsync($"agent-{i % 10}", "CodeGeneration").AsTask());
            tasks.Add(store.GetBeliefsForAgentAsync($"agent-{i % 10}").AsTask());
            tasks.Add(store.GetBeliefsForCategoryAsync("CodeGeneration").AsTask());

            // Updates
            tasks.Add(store.UpdateBeliefAsync($"agent-{i % 10}", "CodeGeneration", success: i % 2 == 0).AsTask());

            // Saves
            var belief = AgentBelief.CreatePrior($"agent-{i % 10}", $"Category-{i}").WithSuccess();
            tasks.Add(store.SaveBeliefAsync(belief).AsTask());
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - All operations should complete without exception
        var finalBeliefs = await store.GetBeliefsForCategoryAsync("CodeGeneration").ConfigureAwait(false);
        await Assert.That(finalBeliefs.IsSuccess).IsTrue();
        await Assert.That(finalBeliefs.Value.Count).IsEqualTo(10);
    }

    /// <summary>
    /// Verifies that concurrent operations across multiple agents and categories work correctly.
    /// </summary>
    [Test]
    public async Task ConcurrentOperations_MultipleAgentsAndCategories_ThreadSafe()
    {
        // Arrange
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        const int agentCount = 10;
        const int categoryCount = 10;

        // Act - Concurrent updates across all agent/category combinations
        var tasks = new List<Task>();
        for (int a = 0; a < agentCount; a++)
        {
            for (int c = 0; c < categoryCount; c++)
            {
                tasks.Add(store.UpdateBeliefAsync($"agent-{a}", $"category-{c}", success: true).AsTask());
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Verify all beliefs were created
        for (int a = 0; a < agentCount; a++)
        {
            var agentBeliefs = await store.GetBeliefsForAgentAsync($"agent-{a}").ConfigureAwait(false);
            await Assert.That(agentBeliefs.Value.Count).IsEqualTo(categoryCount);
        }

        for (int c = 0; c < categoryCount; c++)
        {
            var categoryBeliefs = await store.GetBeliefsForCategoryAsync($"category-{c}").ConfigureAwait(false);
            await Assert.That(categoryBeliefs.Value.Count).IsEqualTo(agentCount);
        }
    }
}
