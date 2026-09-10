// =============================================================================
// <copyright file="BudgetGuardAllocationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Infrastructure.Budget;
using Strategos.Orchestration.Budget;

namespace Strategos.Infrastructure.Tests.Budget;

/// <summary>
/// Unit tests for <see cref="BudgetGuard"/> allocation optimizations.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that <see cref="BudgetGuard"/> minimizes heap allocations
/// when checking resource availability. The optimization uses stackalloc for
/// small fixed-size result sets to avoid List&lt;T&gt; allocations on the hot path.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class BudgetGuardAllocationTests
{
    // =============================================================================
    // A. Stackalloc Optimization Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CanAffordReservation succeeds when all resources are sufficient.
    /// </summary>
    /// <remarks>
    /// When all resources are sufficient, CanAffordReservation should return Success.
    /// This test validates the happy-path result.
    /// </remarks>
    [Test]
    public async Task CanAffordReservation_AllSufficient_ReturnsSuccess()
    {
        // Arrange
        var budget = CreateSufficientBudget();
        var reservation = CreateSmallReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert - Success means no insufficient resources were found
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.Reason).IsNull();
    }

    /// <summary>
    /// Verifies that when resources are insufficient, the result correctly identifies them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Even with stackalloc optimization, insufficient resources must be correctly
    /// identified and reported in the blocked result.
    /// </para>
    /// </remarks>
    [Test]
    public async Task CanAffordReservation_SomeInsufficient_ReportsInsufficientResources()
    {
        // Arrange
        var budget = CreateInsufficientBudget();
        var reservation = CreateLargeReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert - Should be blocked with reason mentioning insufficient resources
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Insufficient resources");
    }

    /// <summary>
    /// Verifies that the guard can handle all five resource types being insufficient.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stackalloc buffer must be sized to handle the maximum case where
    /// all five resource types (Steps, Tokens, Executions, ToolCalls, WallTime)
    /// are insufficient.
    /// </para>
    /// </remarks>
    [Test]
    public async Task CanAffordReservation_AllInsufficient_ReportsAllResourceTypes()
    {
        // Arrange - Create budget with minimal resources
        var budget = CreateMinimalBudget();
        var reservation = CreateMaxReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert - Should report multiple insufficient resources
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).Contains("Tokens");
    }

    /// <summary>
    /// Verifies that null budget always allows the reservation.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_NullBudget_ReturnsSuccess()
    {
        // Arrange
        var reservation = CreateSmallReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(null, reservation);

        // Assert
        await Assert.That(result.CanContinue).IsTrue();
    }

    /// <summary>
    /// Verifies that checking a single insufficient resource type works correctly.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_SingleInsufficient_ReportsSingleResource()
    {
        // Arrange - Budget with only Steps being insufficient
        var stepsBudget = Substitute.For<IResourceBudget>();
        stepsBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        stepsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var tokensBudget = Substitute.For<IResourceBudget>();
        tokensBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        tokensBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var budget = new WorkflowBudget
        {
            BudgetId = "test-budget",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = stepsBudget,
                [ResourceType.Tokens] = tokensBudget
            }
        };

        // Only request Steps and Tokens to match the budget's defined resources
        var reservation = new TestReservation(
            Steps: 1,
            Tokens: 100,
            Executions: 0,
            ToolCalls: 0,
            WallTime: TimeSpan.Zero);
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert - Should report only Steps as insufficient
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).DoesNotContain("Tokens");
    }

    // =============================================================================
    // B. All Five Resource Types Insufficient Tests
    // =============================================================================

    /// <summary>
    /// Verifies that when all five resource types are insufficient, all are reported.
    /// </summary>
    /// <remarks>
    /// This test explicitly verifies that all five resource types (Steps, Tokens,
    /// Executions, ToolCalls, WallTime) are included in the error message when
    /// the stackalloc buffer is fully utilized.
    /// </remarks>
    [Test]
    public async Task CanAffordReservation_AllFiveInsufficient_ReportsAllFiveResourceTypes()
    {
        // Arrange - Budget where all resources are insufficient
        var budget = CreateBudgetWithAllResourcesInsufficient();
        var reservation = CreateMaxReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert - All five resource types should be reported
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).Contains("Tokens");
        await Assert.That(result.Reason!).Contains("Executions");
        await Assert.That(result.Reason!).Contains("ToolCalls");
        await Assert.That(result.Reason!).Contains("WallTime");
    }

    /// <summary>
    /// Verifies that WallTime insufficiency is correctly detected using TimeSpan.
    /// </summary>
    /// <remarks>
    /// WallTime uses TimeSpan and is checked via TotalSeconds, requiring special handling.
    /// </remarks>
    [Test]
    public async Task CanAffordReservation_OnlyWallTimeInsufficient_ReportsWallTime()
    {
        // Arrange - Budget with only WallTime insufficient
        var stepsBudget = Substitute.For<IResourceBudget>();
        stepsBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        stepsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var tokensBudget = Substitute.For<IResourceBudget>();
        tokensBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        tokensBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var executionsBudget = Substitute.For<IResourceBudget>();
        executionsBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        executionsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var toolCallsBudget = Substitute.For<IResourceBudget>();
        toolCallsBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        toolCallsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var wallTimeBudget = Substitute.For<IResourceBudget>();
        wallTimeBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        wallTimeBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var budget = new WorkflowBudget
        {
            BudgetId = "walltime-test",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = stepsBudget,
                [ResourceType.Tokens] = tokensBudget,
                [ResourceType.Executions] = executionsBudget,
                [ResourceType.ToolCalls] = toolCallsBudget,
                [ResourceType.WallTime] = wallTimeBudget
            }
        };

        var reservation = CreateSmallReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert - Only WallTime should be reported
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("WallTime");
        await Assert.That(result.Reason!).DoesNotContain("Steps");
        await Assert.That(result.Reason!).DoesNotContain("Tokens");
    }

    // =============================================================================
    // C. Zero Budget and Boundary Tests
    // =============================================================================

    /// <summary>
    /// Verifies that zero reservation values are not checked against budget.
    /// </summary>
    /// <remarks>
    /// When a reservation has zero for a resource type, that resource should not
    /// be checked, allowing success even if the budget would be insufficient.
    /// </remarks>
    [Test]
    public async Task CanAffordReservation_ZeroReservationValues_DoesNotCheckThoseResources()
    {
        // Arrange - Budget with all resources insufficient, but zero reservation
        var budget = CreateBudgetWithAllResourcesInsufficient();
        var zeroReservation = new TestReservation(
            Steps: 0,
            Tokens: 0,
            Executions: 0,
            ToolCalls: 0,
            WallTime: TimeSpan.Zero);
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, zeroReservation);

        // Assert - Zero reservation means nothing to check, should succeed
        await Assert.That(result.CanContinue).IsTrue();
        await Assert.That(result.Reason).IsNull();
    }

    /// <summary>
    /// Verifies that partial zero reservation only checks non-zero resources.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_PartialZeroReservation_ChecksOnlyNonZeroResources()
    {
        // Arrange - Budget where Steps is insufficient, but Steps reservation is zero
        var stepsBudget = Substitute.For<IResourceBudget>();
        stepsBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        stepsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var tokensBudget = Substitute.For<IResourceBudget>();
        tokensBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        tokensBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var budget = new WorkflowBudget
        {
            BudgetId = "partial-zero",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = stepsBudget,
                [ResourceType.Tokens] = tokensBudget
            }
        };

        // Steps is 0, so it shouldn't be checked even though budget is insufficient
        var partialReservation = new TestReservation(
            Steps: 0,
            Tokens: 100,
            Executions: 0,
            ToolCalls: 0,
            WallTime: TimeSpan.Zero);
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, partialReservation);

        // Assert - Steps is not checked (zero), Tokens is sufficient
        await Assert.That(result.CanContinue).IsTrue();
    }

    /// <summary>
    /// Verifies behavior when budget is fully consumed (boundary condition).
    /// </summary>
    [Test]
    public async Task CanAffordReservation_FullyConsumedBudget_ReportsInsufficient()
    {
        // Arrange - Create budget and consume all resources
        var budget = WorkflowBudget.Create(
            workflowId: "consumed-workflow",
            steps: 10,
            tokens: 1000,
            executions: 5,
            toolCalls: 10,
            wallTimeSeconds: 60);

        // Consume all resources
        var consumedBudget = budget
            .WithConsumption(ResourceType.Steps, 10)
            .WithConsumption(ResourceType.Tokens, 1000)
            .WithConsumption(ResourceType.Executions, 5)
            .WithConsumption(ResourceType.ToolCalls, 10)
            .WithConsumption(ResourceType.WallTime, 60);

        var reservation = CreateSmallReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(consumedBudget, reservation);

        // Assert - Any non-zero reservation should fail
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Insufficient resources");
    }

    /// <summary>
    /// Verifies that mixed sufficient/insufficient resources reports only insufficient ones.
    /// </summary>
    [Test]
    public async Task CanAffordReservation_MixedSufficiency_ReportsOnlyInsufficient()
    {
        // Arrange - Steps and Executions insufficient, others sufficient
        var stepsBudget = Substitute.For<IResourceBudget>();
        stepsBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        stepsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var tokensBudget = Substitute.For<IResourceBudget>();
        tokensBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        tokensBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var executionsBudget = Substitute.For<IResourceBudget>();
        executionsBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        executionsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var toolCallsBudget = Substitute.For<IResourceBudget>();
        toolCallsBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        toolCallsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var wallTimeBudget = Substitute.For<IResourceBudget>();
        wallTimeBudget.HasSufficient(Arg.Any<double>()).Returns(true);
        wallTimeBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var budget = new WorkflowBudget
        {
            BudgetId = "mixed-test",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = stepsBudget,
                [ResourceType.Tokens] = tokensBudget,
                [ResourceType.Executions] = executionsBudget,
                [ResourceType.ToolCalls] = toolCallsBudget,
                [ResourceType.WallTime] = wallTimeBudget
            }
        };

        var reservation = CreateSmallReservation();
        var guard = new BudgetGuard();

        // Act
        var result = guard.CanAffordReservation(budget, reservation);

        // Assert - Only Steps and Executions should be reported
        await Assert.That(result.CanContinue).IsFalse();
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!).Contains("Steps");
        await Assert.That(result.Reason!).Contains("Executions");
        await Assert.That(result.Reason!).DoesNotContain("Tokens");
        await Assert.That(result.Reason!).DoesNotContain("ToolCalls");
        await Assert.That(result.Reason!).DoesNotContain("WallTime");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    /// <summary>
    /// Creates a budget where all resources are sufficient for any reservation.
    /// </summary>
    private static WorkflowBudget CreateSufficientBudget()
    {
        return WorkflowBudget.Create(
            workflowId: "sufficient-workflow",
            steps: 1000,
            tokens: 100000,
            executions: 500,
            toolCalls: 1000,
            wallTimeSeconds: 3600);
    }

    /// <summary>
    /// Creates a budget where some resources are insufficient.
    /// </summary>
    private static IWorkflowBudget CreateInsufficientBudget()
    {
        // Create budget and consume most resources
        var budget = WorkflowBudget.Create(
            workflowId: "insufficient-workflow",
            steps: 10,
            tokens: 1000,
            executions: 5,
            toolCalls: 10,
            wallTimeSeconds: 60);

        // Consume most steps to make them insufficient
        return budget.WithConsumption(ResourceType.Steps, 9);
    }

    /// <summary>
    /// Creates a minimal budget where most resources are near exhaustion.
    /// </summary>
    private static IWorkflowBudget CreateMinimalBudget()
    {
        return WorkflowBudget.Create(
            workflowId: "minimal-workflow",
            steps: 1,
            tokens: 10,
            executions: 1,
            toolCalls: 1,
            wallTimeSeconds: 1);
    }

    /// <summary>
    /// Creates a small reservation that should fit within sufficient budgets.
    /// </summary>
    private static IBudgetReservation CreateSmallReservation()
    {
        return new TestReservation(Steps: 1, Tokens: 100, Executions: 1, ToolCalls: 1, WallTime: TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Creates a large reservation that may exceed limited budgets.
    /// </summary>
    private static IBudgetReservation CreateLargeReservation()
    {
        return new TestReservation(Steps: 5, Tokens: 500, Executions: 3, ToolCalls: 5, WallTime: TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Creates a maximum reservation that exceeds minimal budgets.
    /// </summary>
    private static IBudgetReservation CreateMaxReservation()
    {
        return new TestReservation(Steps: 100, Tokens: 10000, Executions: 100, ToolCalls: 100, WallTime: TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// Creates a budget where all five resource types are insufficient.
    /// </summary>
    private static IWorkflowBudget CreateBudgetWithAllResourcesInsufficient()
    {
        var stepsBudget = Substitute.For<IResourceBudget>();
        stepsBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        stepsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var tokensBudget = Substitute.For<IResourceBudget>();
        tokensBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        tokensBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var executionsBudget = Substitute.For<IResourceBudget>();
        executionsBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        executionsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var toolCallsBudget = Substitute.For<IResourceBudget>();
        toolCallsBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        toolCallsBudget.Scarcity.Returns(ScarcityLevel.Normal);

        var wallTimeBudget = Substitute.For<IResourceBudget>();
        wallTimeBudget.HasSufficient(Arg.Any<double>()).Returns(false);
        wallTimeBudget.Scarcity.Returns(ScarcityLevel.Normal);

        return new WorkflowBudget
        {
            BudgetId = "all-insufficient",
            WorkflowId = "test-workflow",
            Resources = new Dictionary<ResourceType, IResourceBudget>
            {
                [ResourceType.Steps] = stepsBudget,
                [ResourceType.Tokens] = tokensBudget,
                [ResourceType.Executions] = executionsBudget,
                [ResourceType.ToolCalls] = toolCallsBudget,
                [ResourceType.WallTime] = wallTimeBudget
            }
        };
    }

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
