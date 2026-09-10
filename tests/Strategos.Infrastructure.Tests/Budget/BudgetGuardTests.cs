// =============================================================================
// <copyright file="BudgetGuardTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Infrastructure.Budget;
using Strategos.Orchestration.Budget;

namespace Strategos.Infrastructure.Tests.Budget;

/// <summary>
/// Unit tests for <see cref="BudgetGuard"/> verifying resource sufficiency checks
/// and budget guard result scenarios.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that <see cref="BudgetGuard"/> correctly identifies insufficient
/// resources for each resource type (Steps, Tokens, Executions, ToolCalls, WallTime)
/// and returns appropriate guard results based on scarcity levels.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class BudgetGuardTests
{
    // =============================================================================
    // A. Resource Sufficiency Tests - Individual Resources
    // =============================================================================

    /// <summary>
    /// Verifies that when all resources are sufficient, CanAffordReservation returns success.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_AllResourcesSufficient_ReturnsSuccess()
    {
        // Arrange
        var budget = WorkflowBudget.Create(
            workflowId: "sufficient-workflow",
            steps: 100,
            tokens: 10000,
            executions: 50,
            toolCalls: 100,
            wallTimeSeconds: 3600);
        var reservation = new TestReservation(
            Steps: 5,
            Tokens: 500,
            Executions: 3,
            ToolCalls: 5,
            WallTime: TimeSpan.FromSeconds(60));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.HasWarning).IsFalse();
        await Assert.That(result.Reason).IsNull();
    }

    /// <summary>
    /// Verifies that when Steps are insufficient, the result reports Steps.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_StepsInsufficient_ReportsSteps()
    {
        // Arrange - Budget with very limited steps
        var budget = WorkflowBudget.Create(
            workflowId: "steps-limited",
            steps: 5,
            tokens: 10000,
            executions: 50,
            toolCalls: 100,
            wallTimeSeconds: 3600);

        // Consume most steps to make them insufficient
        var consumedBudget = budget.WithConsumption(ResourceType.Steps, 4);

        var reservation = new TestReservation(
            Steps: 10,
            Tokens: 100,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).DoesNotContain("Tokens");
        await Assert.That(result.Reason!).DoesNotContain("Executions");
        await Assert.That(result.Reason!).DoesNotContain("ToolCalls");
        await Assert.That(result.Reason!).DoesNotContain("WallTime");
    }

    /// <summary>
    /// Verifies that when Tokens are insufficient, the result reports Tokens.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_TokensInsufficient_ReportsTokens()
    {
        // Arrange - Budget with very limited tokens
        var budget = WorkflowBudget.Create(
            workflowId: "tokens-limited",
            steps: 100,
            tokens: 100,
            executions: 50,
            toolCalls: 100,
            wallTimeSeconds: 3600);

        // Consume most tokens
        var consumedBudget = budget.WithConsumption(ResourceType.Tokens, 90);

        var reservation = new TestReservation(
            Steps: 1,
            Tokens: 500,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Tokens");
        await Assert.That(result.Reason!).DoesNotContain("Steps");
        await Assert.That(result.Reason!).DoesNotContain("Executions");
        await Assert.That(result.Reason!).DoesNotContain("ToolCalls");
        await Assert.That(result.Reason!).DoesNotContain("WallTime");
    }

    /// <summary>
    /// Verifies that when Executions are insufficient, the result reports Executions.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_ExecutionsInsufficient_ReportsExecutions()
    {
        // Arrange - Budget with very limited executions
        var budget = WorkflowBudget.Create(
            workflowId: "executions-limited",
            steps: 100,
            tokens: 10000,
            executions: 5,
            toolCalls: 100,
            wallTimeSeconds: 3600);

        // Consume most executions
        var consumedBudget = budget.WithConsumption(ResourceType.Executions, 4);

        var reservation = new TestReservation(
            Steps: 1,
            Tokens: 100,
            Executions: 10,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Executions");
        await Assert.That(result.Reason!).DoesNotContain("Steps");
        await Assert.That(result.Reason!).DoesNotContain("Tokens");
        await Assert.That(result.Reason!).DoesNotContain("ToolCalls");
        await Assert.That(result.Reason!).DoesNotContain("WallTime");
    }

    /// <summary>
    /// Verifies that when ToolCalls are insufficient, the result reports ToolCalls.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_ToolCallsInsufficient_ReportsToolCalls()
    {
        // Arrange - Budget with very limited tool calls
        var budget = WorkflowBudget.Create(
            workflowId: "toolcalls-limited",
            steps: 100,
            tokens: 10000,
            executions: 50,
            toolCalls: 5,
            wallTimeSeconds: 3600);

        // Consume most tool calls
        var consumedBudget = budget.WithConsumption(ResourceType.ToolCalls, 4);

        var reservation = new TestReservation(
            Steps: 1,
            Tokens: 100,
            Executions: 1,
            ToolCalls: 10,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("ToolCalls");
        await Assert.That(result.Reason!).DoesNotContain("Steps");
        await Assert.That(result.Reason!).DoesNotContain("Tokens");
        await Assert.That(result.Reason!).DoesNotContain("Executions");
        await Assert.That(result.Reason!).DoesNotContain("WallTime");
    }

    // =============================================================================
    // B. Multiple Insufficiency Tests
    // =============================================================================

    /// <summary>
    /// Verifies that when two resources are insufficient, both are reported.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_TwoResourcesInsufficient_ReportsBoth()
    {
        // Arrange - Budget with limited steps and tokens
        var budget = WorkflowBudget.Create(
            workflowId: "two-limited",
            steps: 10,
            tokens: 100,
            executions: 100,
            toolCalls: 100,
            wallTimeSeconds: 3600);

        // Consume most of both
        var consumedBudget = budget
            .WithConsumption(ResourceType.Steps, 9)
            .WithConsumption(ResourceType.Tokens, 90);

        var reservation = new TestReservation(
            Steps: 10,
            Tokens: 500,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).Contains("Tokens");
        await Assert.That(result.Reason!).DoesNotContain("Executions");
        await Assert.That(result.Reason!).DoesNotContain("ToolCalls");
        await Assert.That(result.Reason!).DoesNotContain("WallTime");
    }

    /// <summary>
    /// Verifies that when three resources are insufficient, all three are reported.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_ThreeResourcesInsufficient_ReportsAllThree()
    {
        // Arrange - Budget with limited steps, tokens, and executions
        var budget = WorkflowBudget.Create(
            workflowId: "three-limited",
            steps: 10,
            tokens: 100,
            executions: 10,
            toolCalls: 100,
            wallTimeSeconds: 3600);

        // Consume most of all three
        var consumedBudget = budget
            .WithConsumption(ResourceType.Steps, 9)
            .WithConsumption(ResourceType.Tokens, 90)
            .WithConsumption(ResourceType.Executions, 9);

        var reservation = new TestReservation(
            Steps: 10,
            Tokens: 500,
            Executions: 10,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).Contains("Tokens");
        await Assert.That(result.Reason!).Contains("Executions");
        await Assert.That(result.Reason!).DoesNotContain("ToolCalls");
        await Assert.That(result.Reason!).DoesNotContain("WallTime");
    }

    /// <summary>
    /// Verifies that when all five resources are insufficient, all five are reported.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_AllFiveResourcesInsufficient_ReportsAllFive()
    {
        // Arrange - Budget with all limited resources
        var budget = WorkflowBudget.Create(
            workflowId: "all-limited",
            steps: 10,
            tokens: 100,
            executions: 10,
            toolCalls: 10,
            wallTimeSeconds: 60);

        // Consume most of all resources
        var consumedBudget = budget
            .WithConsumption(ResourceType.Steps, 9)
            .WithConsumption(ResourceType.Tokens, 90)
            .WithConsumption(ResourceType.Executions, 9)
            .WithConsumption(ResourceType.ToolCalls, 9)
            .WithConsumption(ResourceType.WallTime, 55);

        var reservation = new TestReservation(
            Steps: 10,
            Tokens: 500,
            Executions: 10,
            ToolCalls: 10,
            WallTime: TimeSpan.FromSeconds(60));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).Contains("Tokens");
        await Assert.That(result.Reason!).Contains("Executions");
        await Assert.That(result.Reason!).Contains("ToolCalls");
        await Assert.That(result.Reason!).Contains("WallTime");
    }

    // =============================================================================
    // C. Edge Cases
    // =============================================================================

    /// <summary>
    /// Verifies that zero reservation always succeeds regardless of budget state.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_ZeroReservation_AlwaysSucceeds()
    {
        // Arrange - Fully consumed budget
        var budget = WorkflowBudget.Create(
            workflowId: "consumed-workflow",
            steps: 10,
            tokens: 100,
            executions: 10,
            toolCalls: 10,
            wallTimeSeconds: 60);

        var consumedBudget = budget
            .WithConsumption(ResourceType.Steps, 10)
            .WithConsumption(ResourceType.Tokens, 100)
            .WithConsumption(ResourceType.Executions, 10)
            .WithConsumption(ResourceType.ToolCalls, 10)
            .WithConsumption(ResourceType.WallTime, 60);

        var zeroReservation = new TestReservation(
            Steps: 0,
            Tokens: 0,
            Executions: 0,
            ToolCalls: 0,
            WallTime: TimeSpan.Zero);
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, zeroReservation);

        // Assert - Zero reservation should always succeed
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.Reason).IsNull();
    }

    /// <summary>
    /// Verifies that exactly at limit succeeds when checking for that amount.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_ExactlyAtLimit_Succeeds()
    {
        // Arrange - Budget with 10 steps remaining
        var budget = WorkflowBudget.Create(
            workflowId: "exact-limit",
            steps: 10,
            tokens: 1000,
            executions: 10,
            toolCalls: 10,
            wallTimeSeconds: 60);

        // Reservation for exactly 10 steps (remaining amount)
        var exactReservation = new TestReservation(
            Steps: 10,
            Tokens: 100,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, exactReservation);

        // Assert - Exactly at limit should succeed
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.Reason).IsNull();
    }

    /// <summary>
    /// Verifies that just over limit fails.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_JustOverLimit_Fails()
    {
        // Arrange - Budget with 10 steps
        var budget = WorkflowBudget.Create(
            workflowId: "over-limit",
            steps: 10,
            tokens: 1000,
            executions: 10,
            toolCalls: 10,
            wallTimeSeconds: 60);

        // Reservation for 11 steps (one over limit)
        var overReservation = new TestReservation(
            Steps: 11,
            Tokens: 100,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(10));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, overReservation);

        // Assert - Just over limit should fail
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
    }

    // =============================================================================
    // D. WallTime Specific Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WallTime insufficiency is correctly detected.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_WallTimeInsufficient_ReportsWallTime()
    {
        // Arrange - Budget with limited wall time
        var budget = WorkflowBudget.Create(
            workflowId: "walltime-limited",
            steps: 100,
            tokens: 10000,
            executions: 50,
            toolCalls: 100,
            wallTimeSeconds: 60);

        // Consume most wall time
        var consumedBudget = budget.WithConsumption(ResourceType.WallTime, 55);

        var reservation = new TestReservation(
            Steps: 1,
            Tokens: 100,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(30));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("WallTime");
        await Assert.That(result.Reason!).DoesNotContain("Steps");
        await Assert.That(result.Reason!).DoesNotContain("Tokens");
        await Assert.That(result.Reason!).DoesNotContain("Executions");
        await Assert.That(result.Reason!).DoesNotContain("ToolCalls");
    }

    /// <summary>
    /// Verifies that WallTime TimeSpan conversion is accurate (uses TotalSeconds).
    /// </summary>
    [Test]
    public async Task CanAffordReservation_WallTimeTimeSpanConversion_IsAccurate()
    {
        // Arrange - Budget with exactly 120 seconds (2 minutes) wall time
        var budget = WorkflowBudget.Create(
            workflowId: "walltime-conversion",
            steps: 100,
            tokens: 10000,
            executions: 50,
            toolCalls: 100,
            wallTimeSeconds: 120);

        // Reservation for 2 minutes (should succeed - exactly at limit)
        var exactReservation = new TestReservation(
            Steps: 1,
            Tokens: 100,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromMinutes(2));
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, exactReservation);

        // Assert - 2 minutes = 120 seconds, should succeed
        await Assert.That(result.CanContinue).IsTrue();

        // Now test over limit - 2 minutes and 1 second
        var overReservation = new TestReservation(
            Steps: 1,
            Tokens: 100,
            Executions: 1,
            ToolCalls: 1,
            WallTime: TimeSpan.FromSeconds(121));

        // Act
        var overResult = guard.CanAffordReservation(budget, overReservation);

        // Assert - 121 seconds should fail
        await Assert.That(overResult.CanContinue).IsFalse();
        await Assert.That(overResult.Reason!).Contains("WallTime");
    }

    // =============================================================================
    // E. CanProceed Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CanProceed returns success when budget is null.
    /// </summary>
    [Test]
    public async Task CanProceed_NullBudget_ReturnsSuccess()
    {
        // Arrange
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanProceed(null);

        // Assert
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.HasWarning).IsFalse();
        await Assert.That(result.Reason).IsNull();
    }

    /// <summary>
    /// Verifies that CanProceed returns success when resources are abundant.
    /// </summary>
    [Test]
    public async Task CanProceed_AbundantResources_ReturnsSuccess()
    {
        // Arrange
        var abundantBudget = Substitute.For<IResourceBudget>();
        abundantBudget.Scarcity.Returns(ScarcityLevel.Abundant);

        var budget = new WorkflowBudget
        {
            BudgetId = "abundant-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = abundantBudget
            }
        };
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanProceed(budget);

        // Assert
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.HasWarning).IsFalse();
        await Assert.That(result.Reason).IsNull();
    }

    /// <summary>
    /// Verifies that CanProceed returns warning when resources are scarce.
    /// </summary>
    [Test]
    public async Task CanProceed_ScarceResources_ReturnsWarning()
    {
        // Arrange
        var scarceBudget = Substitute.For<IResourceBudget>();
        scarceBudget.Scarcity.Returns(ScarcityLevel.Scarce);

        var budget = new WorkflowBudget
        {
            BudgetId = "scarce-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = scarceBudget
            }
        };
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanProceed(budget);

        // Assert
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.HasWarning).IsTrue();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("running low");
    }

    /// <summary>
    /// Verifies that CanProceed returns blocked when resources are critical.
    /// </summary>
    [Test]
    public async Task CanProceed_CriticalResources_ReturnsBlocked()
    {
        // Arrange
        var criticalBudget = Substitute.For<IResourceBudget>();
        criticalBudget.Scarcity.Returns(ScarcityLevel.Critical);

        var budget = new WorkflowBudget
        {
            BudgetId = "critical-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = criticalBudget
            }
        };
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanProceed(budget);

        // Assert
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.HasWarning).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Critical");
    }

    /// <summary>
    /// Verifies that CanProceed returns success when budget has empty resources.
    /// </summary>
    [Test]
    public async Task CanProceed_EmptyResources_ReturnsSuccess()
    {
        // Arrange
        var budget = new WorkflowBudget
        {
            BudgetId = "empty-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>()
        };
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanProceed(budget);

        // Assert
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.HasWarning).IsFalse();
        await Assert.That(result.Reason).IsNull();
    }

    // =============================================================================
    // Helper Types
    // =============================================================================

    /// <summary>
    /// Test implementation of budget reservation for testing purposes.
    /// </summary>
    private sealed record TestReservation(
        int Steps,
        int Tokens,
        int Executions,
        int ToolCalls,
        TimeSpan WallTime) : IBudgetReservation;
}
