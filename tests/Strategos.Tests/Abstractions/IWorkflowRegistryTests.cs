// =============================================================================
// <copyright file="IWorkflowRegistryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Linq.Expressions;

using Strategos.Tests.Fixtures;

namespace Strategos.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="IWorkflowRegistry"/>.
/// </summary>
/// <remarks>
/// Tests verify the Registry Pattern for lambda condition handling as described
/// in the workflow design documentation. The registry enables:
/// <list type="bullet">
///   <item><description>Runtime condition lookup by deterministic ID</description></item>
///   <item><description>Auditability via condition string representation</description></item>
///   <item><description>Type-safe condition execution</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class IWorkflowRegistryTests
{
    // =============================================================================
    // A. Registration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a condition can be registered with the registry.
    /// </summary>
    [Test]
    public async Task RegisterCondition_WithValidExpression_Succeeds()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;

        // Act & Assert - should not throw, verify condition is retrievable
        registry.RegisterCondition("ProcessClaim-Refinement", condition);
        var retrieved = registry.GetCondition<TestWorkflowState>("ProcessClaim-Refinement");
        await Assert.That(retrieved).IsNotNull();
    }

    /// <summary>
    /// Verifies that registering a condition with null ID throws.
    /// </summary>
    [Test]
    public async Task RegisterCondition_WithNullId_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;

        // Act & Assert
        await Assert.That(() => registry.RegisterCondition(null!, condition))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that registering a condition with null expression throws.
    /// </summary>
    [Test]
    public async Task RegisterCondition_WithNullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new WorkflowRegistry();

        // Act & Assert
        await Assert.That(() => registry.RegisterCondition<TestWorkflowState>("Test", null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that registering duplicate condition ID throws.
    /// </summary>
    [Test]
    public async Task RegisterCondition_WithDuplicateId_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition1 = s => s.QualityScore >= 0.9m;
        Expression<Func<TestWorkflowState, bool>> condition2 = s => s.QualityScore >= 0.8m;

        registry.RegisterCondition("ProcessClaim-Refinement", condition1);

        // Act & Assert
        await Assert.That(() => registry.RegisterCondition("ProcessClaim-Refinement", condition2))
            .Throws<InvalidOperationException>();
    }

    // =============================================================================
    // B. Retrieval Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a registered condition can be retrieved.
    /// </summary>
    [Test]
    public async Task GetCondition_WhenRegistered_ReturnsCondition()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;
        registry.RegisterCondition("ProcessClaim-Refinement", condition);

        // Act
        var retrieved = registry.GetCondition<TestWorkflowState>("ProcessClaim-Refinement");

        // Assert
        await Assert.That(retrieved).IsNotNull();
    }

    /// <summary>
    /// Verifies that retrieving unregistered condition throws.
    /// </summary>
    [Test]
    public async Task GetCondition_WhenNotRegistered_ThrowsKeyNotFoundException()
    {
        // Arrange
        var registry = new WorkflowRegistry();

        // Act & Assert
        await Assert.That(() => registry.GetCondition<TestWorkflowState>("Unknown-Condition"))
            .Throws<KeyNotFoundException>();
    }

    /// <summary>
    /// Verifies that TryGetCondition returns true when condition exists.
    /// </summary>
    [Test]
    public async Task TryGetCondition_WhenRegistered_ReturnsTrueAndCondition()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;
        registry.RegisterCondition("ProcessClaim-Refinement", condition);

        // Act
        var found = registry.TryGetCondition<TestWorkflowState>("ProcessClaim-Refinement", out var retrieved);

        // Assert
        await Assert.That(found).IsTrue();
        await Assert.That(retrieved).IsNotNull();
    }

    /// <summary>
    /// Verifies that TryGetCondition returns false when condition doesn't exist.
    /// </summary>
    [Test]
    public async Task TryGetCondition_WhenNotRegistered_ReturnsFalseAndNull()
    {
        // Arrange
        var registry = new WorkflowRegistry();

        // Act
        var found = registry.TryGetCondition<TestWorkflowState>("Unknown-Condition", out var retrieved);

        // Assert
        await Assert.That(found).IsFalse();
        await Assert.That(retrieved).IsNull();
    }

    // =============================================================================
    // C. Execution Tests
    // =============================================================================

    /// <summary>
    /// Verifies that condition can be executed with true result.
    /// </summary>
    [Test]
    public async Task WorkflowCondition_Execute_WhenConditionMet_ReturnsTrue()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;
        registry.RegisterCondition("ProcessClaim-Refinement", condition);

        var state = new TestWorkflowState
        {
            WorkflowId = Guid.NewGuid(),
            QualityScore = 0.95m,
        };

        // Act
        var retrieved = registry.GetCondition<TestWorkflowState>("ProcessClaim-Refinement");
        var result = retrieved.Execute(state);

        // Assert
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that condition can be executed with false result.
    /// </summary>
    [Test]
    public async Task WorkflowCondition_Execute_WhenConditionNotMet_ReturnsFalse()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;
        registry.RegisterCondition("ProcessClaim-Refinement", condition);

        var state = new TestWorkflowState
        {
            WorkflowId = Guid.NewGuid(),
            QualityScore = 0.7m,
        };

        // Act
        var retrieved = registry.GetCondition<TestWorkflowState>("ProcessClaim-Refinement");
        var result = retrieved.Execute(state);

        // Assert
        await Assert.That(result).IsFalse();
    }

    // =============================================================================
    // D. Auditability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that condition ToString returns the expression text.
    /// </summary>
    [Test]
    public async Task WorkflowCondition_ToString_ReturnsExpressionText()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;
        registry.RegisterCondition("ProcessClaim-Refinement", condition);

        // Act
        var retrieved = registry.GetCondition<TestWorkflowState>("ProcessClaim-Refinement");
        var text = retrieved.ToString();

        // Assert - the exact string varies by compiler, but should contain key elements
        await Assert.That(text).Contains("QualityScore");
        await Assert.That(text).Contains("0.9");
    }

    /// <summary>
    /// Verifies that ConditionId property is set correctly.
    /// </summary>
    [Test]
    public async Task WorkflowCondition_ConditionId_ReturnsRegisteredId()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= 0.9m;
        registry.RegisterCondition("ProcessClaim-Refinement", condition);

        // Act
        var retrieved = registry.GetCondition<TestWorkflowState>("ProcessClaim-Refinement");

        // Assert
        await Assert.That(retrieved.ConditionId).IsEqualTo("ProcessClaim-Refinement");
    }

    // =============================================================================
    // E. Thread Safety Tests
    // =============================================================================

    /// <summary>
    /// Verifies that registry is thread-safe for concurrent registrations.
    /// </summary>
    [Test]
    public async Task Registry_ConcurrentRegistration_DoesNotCorrupt()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var id = $"Condition-{i}";
            var threshold = i / 100m;
            Expression<Func<TestWorkflowState, bool>> condition = s => s.QualityScore >= threshold;
            tasks.Add(Task.Run(() => registry.RegisterCondition(id, condition)));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - all conditions should be retrievable
        for (int i = 0; i < 100; i++)
        {
            var id = $"Condition-{i}";
            var found = registry.TryGetCondition<TestWorkflowState>(id, out var _);
            await Assert.That(found).IsTrue();
        }
    }

    // =============================================================================
    // F. Complex Condition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that complex conditions with multiple clauses work correctly.
    /// </summary>
    [Test]
    public async Task WorkflowCondition_ComplexExpression_ExecutesCorrectly()
    {
        // Arrange
        var registry = new WorkflowRegistry();
        Expression<Func<TestWorkflowState, bool>> condition =
            s => s.QualityScore >= 0.8m && s.IterationCount < 5;
        registry.RegisterCondition("ProcessClaim-Complex", condition);

        var passingState = new TestWorkflowState
        {
            WorkflowId = Guid.NewGuid(),
            QualityScore = 0.85m,
            IterationCount = 3,
        };

        var failingState = new TestWorkflowState
        {
            WorkflowId = Guid.NewGuid(),
            QualityScore = 0.85m,
            IterationCount = 6,
        };

        // Act
        var retrieved = registry.GetCondition<TestWorkflowState>("ProcessClaim-Complex");
        var passResult = retrieved.Execute(passingState);
        var failResult = retrieved.Execute(failingState);

        // Assert
        await Assert.That(passResult).IsTrue();
        await Assert.That(failResult).IsFalse();
    }
}
