// =============================================================================
// <copyright file="WorkflowBudgetTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Orchestration.Budget;

namespace Strategos.Infrastructure.Tests.Budget;

/// <summary>
/// Unit tests for <see cref="WorkflowBudget"/> verifying budget management
/// and scarcity calculation caching.
/// </summary>
[Property("Category", "Unit")]
public sealed class WorkflowBudgetTests
{
    // =============================================================================
    // A. OverallScarcity Caching Tests
    // =============================================================================

    /// <summary>
    /// Verifies that OverallScarcity is computed once and cached for subsequent accesses.
    /// </summary>
    /// <remarks>
    /// This test verifies the optimization where the OverallScarcity property
    /// is computed lazily and cached rather than iterating over all resources
    /// on every access.
    /// </remarks>
    [Test]
    public async Task OverallScarcity_AccessedMultipleTimes_ComputesOnce()
    {
        // Arrange
        var accessCount = 0;
        var mockBudget = Substitute.For<IResourceBudget>();
        mockBudget.Scarcity.Returns(_ =>
        {
            accessCount++;
            return ScarcityLevel.Normal;
        });

        var budget = new WorkflowBudget
        {
            BudgetId = "test-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = mockBudget
            }
        };

        // Act - Access OverallScarcity multiple times
        var scarcity1 = budget.OverallScarcity;
        var scarcity2 = budget.OverallScarcity;
        var scarcity3 = budget.OverallScarcity;

        // Assert - All accesses should return the same value
        await Assert.That(scarcity1).IsEqualTo(ScarcityLevel.Normal);
        await Assert.That(scarcity2).IsEqualTo(ScarcityLevel.Normal);
        await Assert.That(scarcity3).IsEqualTo(ScarcityLevel.Normal);

        // Assert - Scarcity should only be computed once (mock accessed once per resource)
        await Assert.That(accessCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that OverallScarcity returns the maximum scarcity level from all resources.
    /// </summary>
    [Test]
    public async Task OverallScarcity_WithMultipleResources_ReturnsMaxScarcity()
    {
        // Arrange
        var abundantBudget = Substitute.For<IResourceBudget>();
        abundantBudget.Scarcity.Returns(ScarcityLevel.Abundant);

        var criticalBudget = Substitute.For<IResourceBudget>();
        criticalBudget.Scarcity.Returns(ScarcityLevel.Critical);

        var normalBudget = Substitute.For<IResourceBudget>();
        normalBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var budget = new WorkflowBudget
        {
            BudgetId = "test-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = abundantBudget,
                [ResourceType.Tokens] = criticalBudget,
                [ResourceType.Executions] = normalBudget
            }
        };

        // Act
        var scarcity = budget.OverallScarcity;

        // Assert - Should return Critical (highest ordinal)
        await Assert.That(scarcity).IsEqualTo(ScarcityLevel.Critical);
    }

    /// <summary>
    /// Verifies that OverallScarcity returns Abundant when no resources are defined.
    /// </summary>
    [Test]
    public async Task OverallScarcity_WithNoResources_ReturnsAbundant()
    {
        // Arrange
        var budget = new WorkflowBudget
        {
            BudgetId = "test-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>()
        };

        // Act
        var scarcity = budget.OverallScarcity;

        // Assert
        await Assert.That(scarcity).IsEqualTo(ScarcityLevel.Abundant);
    }

    /// <summary>
    /// Verifies that ScarcityMultiplier is correctly calculated based on cached OverallScarcity.
    /// </summary>
    [Test]
    public async Task ScarcityMultiplier_WithCachedOverallScarcity_ReturnsCorrectValue()
    {
        // Arrange
        var scarceBudget = Substitute.For<IResourceBudget>();
        scarceBudget.Scarcity.Returns(ScarcityLevel.Scarce);

        var budget = new WorkflowBudget
        {
            BudgetId = "test-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = scarceBudget
            }
        };

        // Act
        var multiplier = budget.ScarcityMultiplier;

        // Assert - Scarce level should have multiplier of 3.0
        await Assert.That(multiplier).IsEqualTo(3.0);
    }

    // =============================================================================
    // B. Cache Invalidation Tests (Record Copy Behavior)
    // =============================================================================

    /// <summary>
    /// Verifies that OverallScarcity is recomputed after WithResource creates a copy.
    /// </summary>
    /// <remarks>
    /// This test validates that the Lazy cache is properly reset when a record copy
    /// is created via WithResource. Without proper cache invalidation, the copied
    /// record would return stale scarcity values from the original instance.
    /// </remarks>
    [Test]
    public async Task OverallScarcity_AfterWithResource_RecomputesFromNewResources()
    {
        // Arrange - Create budget with Abundant resource
        var abundantBudget = Substitute.For<IResourceBudget>();
        abundantBudget.Scarcity.Returns(ScarcityLevel.Abundant);

        var originalBudget = new WorkflowBudget
        {
            BudgetId = "test-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = abundantBudget
            }
        };

        // Access scarcity to force caching on original
        var originalScarcity = originalBudget.OverallScarcity;
        await Assert.That(originalScarcity).IsEqualTo(ScarcityLevel.Abundant);

        // Act - Add a Critical resource via WithResource
        var criticalBudget = Substitute.For<IResourceBudget>();
        criticalBudget.Scarcity.Returns(ScarcityLevel.Critical);

        var updatedBudget = (WorkflowBudget)originalBudget.WithResource(ResourceType.Tokens, criticalBudget);

        // Assert - New budget should compute Critical (not stale Abundant)
        var updatedScarcity = updatedBudget.OverallScarcity;
        await Assert.That(updatedScarcity).IsEqualTo(ScarcityLevel.Critical);

        // Original should still be Abundant (immutability preserved)
        await Assert.That(originalBudget.OverallScarcity).IsEqualTo(ScarcityLevel.Abundant);
    }

    /// <summary>
    /// Verifies that OverallScarcity is recomputed after WithConsumption creates a copy.
    /// </summary>
    /// <remarks>
    /// When consumption changes a resource's scarcity level, the copied budget
    /// must reflect the new scarcity, not the cached value from before consumption.
    /// </remarks>
    [Test]
    public async Task OverallScarcity_AfterWithConsumption_RecomputesFromUpdatedResources()
    {
        // Arrange - Create budget with Normal scarcity resource
        var normalBudget = Substitute.For<IResourceBudget>();
        normalBudget.Scarcity.Returns(ScarcityLevel.Normal);
        normalBudget.HasSufficient(Arg.Any<double>()).Returns(true);

        // When consumed, scarcity becomes Critical
        var consumedBudget = Substitute.For<IResourceBudget>();
        consumedBudget.Scarcity.Returns(ScarcityLevel.Critical);
        normalBudget.WithConsumption(Arg.Any<double>()).Returns(consumedBudget);

        var originalBudget = new WorkflowBudget
        {
            BudgetId = "test-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = normalBudget
            }
        };

        // Access scarcity to force caching on original
        var originalScarcity = originalBudget.OverallScarcity;
        await Assert.That(originalScarcity).IsEqualTo(ScarcityLevel.Normal);

        // Act - Consume resources (causing scarcity change)
        var updatedBudget = (WorkflowBudget)originalBudget.WithConsumption(ResourceType.Steps, 100);

        // Assert - New budget should compute Critical (not stale Normal)
        var updatedScarcity = updatedBudget.OverallScarcity;
        await Assert.That(updatedScarcity).IsEqualTo(ScarcityLevel.Critical);

        // Original should still be Normal (immutability preserved)
        await Assert.That(originalBudget.OverallScarcity).IsEqualTo(ScarcityLevel.Normal);
    }
}
