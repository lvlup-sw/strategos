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
    /// Verifies that compensation metadata can be set explicitly and immutably.
    /// </summary>
    [Test]
    public async Task CompensationMetadata_CanBeSetExplicitly()
    {
        // Arrange
        var rollbackId = Guid.NewGuid();

        // Act
        var context = CreateValidContext() with
        {
            IsCompensation = true,
            RollbackId = rollbackId,
        };

        // Assert
        await Assert.That(context.IsCompensation).IsTrue();
        await Assert.That(context.RollbackId).IsEqualTo(rollbackId);
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
        WorkflowId = Guid.NewGuid(),
        StepName = "TestStep",
        Timestamp = DateTimeOffset.UtcNow,
        CurrentPhase = "Testing",
    };
}
