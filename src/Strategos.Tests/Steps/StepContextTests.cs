// =============================================================================
// <copyright file="StepContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Steps;

namespace Strategos.Tests.Steps;

/// <summary>
/// Unit tests for <see cref="StepContext"/>.
/// </summary>
[Property("Category", "Unit")]
public class StepContextTests
{
    // =============================================================================
    // A. Required Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepContext requires CorrelationId.
    /// </summary>
    [Test]
    public async Task StepContext_RequiresCorrelationId()
    {
        // Arrange
        var correlationId = "corr-12345";
        var context = CreateValidContext() with { CorrelationId = correlationId };

        // Assert
        await Assert.That(context.CorrelationId).IsEqualTo(correlationId);
    }

    /// <summary>
    /// Verifies that StepContext requires WorkflowId.
    /// </summary>
    [Test]
    public async Task StepContext_RequiresWorkflowId()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var context = CreateValidContext() with { WorkflowId = workflowId };

        // Assert
        await Assert.That(context.WorkflowId).IsEqualTo(workflowId);
    }

    /// <summary>
    /// Verifies that StepContext requires StepName.
    /// </summary>
    [Test]
    public async Task StepContext_RequiresStepName()
    {
        // Arrange
        var stepName = "ValidateOrder";
        var context = CreateValidContext() with { StepName = stepName };

        // Assert
        await Assert.That(context.StepName).IsEqualTo(stepName);
    }

    /// <summary>
    /// Verifies that StepContext requires Timestamp.
    /// </summary>
    [Test]
    public async Task StepContext_RequiresTimestamp()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var context = CreateValidContext() with { Timestamp = timestamp };

        // Assert
        await Assert.That(context.Timestamp).IsEqualTo(timestamp);
    }

    /// <summary>
    /// Verifies that StepContext requires CurrentPhase.
    /// </summary>
    [Test]
    public async Task StepContext_RequiresCurrentPhase()
    {
        // Arrange
        var phase = "Executing";
        var context = CreateValidContext() with { CurrentPhase = phase };

        // Assert
        await Assert.That(context.CurrentPhase).IsEqualTo(phase);
    }

    // =============================================================================
    // B. Default Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that RetryCount defaults to zero.
    /// </summary>
    [Test]
    public async Task RetryCount_DefaultsToZero()
    {
        // Arrange
        var context = CreateValidContext();

        // Assert
        await Assert.That(context.RetryCount).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that RetryCount can be set explicitly.
    /// </summary>
    [Test]
    public async Task RetryCount_CanBeSetExplicitly()
    {
        // Arrange
        var context = CreateValidContext() with { RetryCount = 3 };

        // Assert
        await Assert.That(context.RetryCount).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that compensation metadata defaults to ordinary forward execution.
    /// </summary>
    [Test]
    public async Task CompensationMetadata_DefaultsToForwardExecution()
    {
        // Arrange
        var context = CreateValidContext();

        // Assert
        await Assert.That(context.IsCompensation).IsFalse();
        await Assert.That(context.RollbackId).IsNull();
    }

    /// <summary>
    /// Verifies that a set rollback identifier derives compensation execution.
    /// </summary>
    [Test]
    public async Task IsCompensation_WithRollbackId_IsTrue()
    {
        // Arrange
        var rollbackId = Guid.NewGuid();

        // Act
        var context = CreateValidContext() with { RollbackId = rollbackId };

        // Assert
        await Assert.That(context.RollbackId).IsEqualTo(rollbackId);
        await Assert.That(context.IsCompensation).IsTrue();
    }

    /// <summary>
    /// Verifies that clearing the rollback identifier derives forward execution, so the
    /// two halves of the compensation identity pair cannot disagree.
    /// </summary>
    [Test]
    public async Task IsCompensation_WithoutRollbackId_IsFalse()
    {
        // Arrange
        var compensating = CreateValidContext() with { RollbackId = Guid.NewGuid() };

        // Act
        var forward = compensating with { RollbackId = null };

        // Assert
        await Assert.That(forward.RollbackId).IsNull();
        await Assert.That(forward.IsCompensation).IsFalse();
    }

    /// <summary>
    /// Verifies that the durable execution identity round-trips through with syntax.
    /// </summary>
    [Test]
    public async Task ExecutionId_RoundTripsThroughWith()
    {
        // Arrange
        var original = CreateValidContext();
        var executionId = Guid.NewGuid();

        // Act
        var modified = original with { ExecutionId = executionId };

        // Assert
        await Assert.That(modified.ExecutionId).IsEqualTo(executionId);
        await Assert.That(original.ExecutionId).IsNotEqualTo(executionId);
        await Assert.That((modified with { RetryCount = 9 }).ExecutionId).IsEqualTo(executionId);
    }

    // =============================================================================
    // C. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create factory method creates a valid context.
    /// </summary>
    [Test]
    public async Task Create_WithValidParameters_CreatesContext()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var stepName = "ProcessOrder";
        var phase = "Processing";

        // Act
        var context = StepContext.Create(workflowId, stepName, phase);

        // Assert
        await Assert.That(context.WorkflowId).IsEqualTo(workflowId);
        await Assert.That(context.StepName).IsEqualTo(stepName);
        await Assert.That(context.CurrentPhase).IsEqualTo(phase);
        await Assert.That(context.CorrelationId).IsNotEmpty();
        await Assert.That(context.Timestamp).IsGreaterThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(-1));
        await Assert.That(context.Timestamp).IsLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
        await Assert.That(context.IsCompensation).IsFalse();
        await Assert.That(context.RollbackId).IsNull();
    }

    /// <summary>
    /// Verifies that Create mints a non-empty execution identity on the legacy
    /// non-derived path, where no dispatched command supplies one.
    /// </summary>
    [Test]
    public async Task Create_MintsNonEmptyExecutionId()
    {
        // Act
        var first = StepContext.Create(Guid.NewGuid(), "Step1", "Phase1");
        var second = StepContext.Create(Guid.NewGuid(), "Step1", "Phase1");

        // Assert
        await Assert.That(first.ExecutionId).IsNotEqualTo(Guid.Empty);
        await Assert.That(second.ExecutionId).IsNotEqualTo(Guid.Empty);
        await Assert.That(first.ExecutionId).IsNotEqualTo(second.ExecutionId);
    }

    /// <summary>
    /// Verifies that Create generates unique CorrelationIds.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueCorrelationId()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        // Act
        var context1 = StepContext.Create(workflowId, "Step1", "Phase1");
        var context2 = StepContext.Create(workflowId, "Step1", "Phase1");

        // Assert
        await Assert.That(context1.CorrelationId).IsNotEqualTo(context2.CorrelationId);
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepContext is an immutable record.
    /// </summary>
    [Test]
    public async Task StepContext_IsImmutableRecord()
    {
        // Arrange
        var original = CreateValidContext();

        // Act - Use record with syntax
        var modified = original with { RetryCount = 5 };

        // Assert
        await Assert.That(original.RetryCount).IsEqualTo(0);
        await Assert.That(modified.RetryCount).IsEqualTo(5);
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that all required properties are preserved through with syntax.
    /// </summary>
    [Test]
    public async Task WithSyntax_PreservesAllProperties()
    {
        // Arrange
        var original = CreateValidContext();

        // Act
        var modified = original with { RetryCount = 1 };

        // Assert
        await Assert.That(modified.CorrelationId).IsEqualTo(original.CorrelationId);
        await Assert.That(modified.WorkflowId).IsEqualTo(original.WorkflowId);
        await Assert.That(modified.StepName).IsEqualTo(original.StepName);
        await Assert.That(modified.Timestamp).IsEqualTo(original.Timestamp);
        await Assert.That(modified.CurrentPhase).IsEqualTo(original.CurrentPhase);
        await Assert.That(modified.ExecutionId).IsEqualTo(original.ExecutionId);
        await Assert.That(modified.IsCompensation).IsEqualTo(original.IsCompensation);
        await Assert.That(modified.RollbackId).IsEqualTo(original.RollbackId);
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    /// <summary>
    /// Creates a valid StepContext for testing.
    /// </summary>
    private static StepContext CreateValidContext() => new StepContext
    {
        CorrelationId = Guid.NewGuid().ToString("N"),
        ExecutionId = Guid.NewGuid(),
        WorkflowId = Guid.NewGuid(),
        StepName = "TestStep",
        Timestamp = DateTimeOffset.UtcNow,
        CurrentPhase = "Testing",
    };
}
