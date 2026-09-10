// =============================================================================
// <copyright file="ApprovalDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ApprovalDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class ApprovalDefinitionTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid params returns a definition.
    /// </summary>
    [Test]
    public async Task Create_WithValidParams_ReturnsDefinition()
    {
        // Act
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-123");

        // Assert
        await Assert.That(approval.ApproverType).IsEqualTo(typeof(TestApprover));
        await Assert.That(approval.Configuration).IsNotNull();
        await Assert.That(approval.PrecedingStepId).IsEqualTo("step-123");
    }

    /// <summary>
    /// Verifies that Create generates a unique ApprovalPointId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueApprovalPointId()
    {
        // Act
        var approval1 = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");
        var approval2 = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-2");

        // Assert
        await Assert.That(approval1.ApprovalPointId).IsNotNull();
        await Assert.That(approval1.ApprovalPointId).IsNotEqualTo(approval2.ApprovalPointId);
    }

    /// <summary>
    /// Verifies that Create throws for null approver type.
    /// </summary>
    [Test]
    public async Task Create_WithNullApproverType_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalDefinition.Create(
            null!,
            ApprovalConfiguration.Default,
            "step-1"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for null configuration.
    /// </summary>
    [Test]
    public async Task Create_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalDefinition.Create(
            typeof(TestApprover),
            null!,
            "step-1"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for null preceding step ID.
    /// </summary>
    [Test]
    public async Task Create_WithNullPrecedingStepId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Default Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EscalationHandler defaults to null.
    /// </summary>
    [Test]
    public async Task Create_EscalationHandler_DefaultsToNull()
    {
        // Act
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");

        // Assert
        await Assert.That(approval.EscalationHandler).IsNull();
    }

    /// <summary>
    /// Verifies that RejectionHandler defaults to null.
    /// </summary>
    [Test]
    public async Task Create_RejectionHandler_DefaultsToNull()
    {
        // Act
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");

        // Assert
        await Assert.That(approval.RejectionHandler).IsNull();
    }

    // =============================================================================
    // C. WithEscalation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithEscalation sets the escalation handler.
    /// </summary>
    [Test]
    public async Task WithEscalation_SetsEscalationHandler()
    {
        // Arrange
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");
        var escalation = ApprovalEscalationDefinition.Create([], [], isTerminal: true);

        // Act
        var updated = approval.WithEscalation(escalation);

        // Assert
        await Assert.That(updated.EscalationHandler).IsNotNull();
        await Assert.That(updated.EscalationHandler!.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that WithEscalation preserves original instance.
    /// </summary>
    [Test]
    public async Task WithEscalation_PreservesOriginal()
    {
        // Arrange
        var original = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");
        var escalation = ApprovalEscalationDefinition.Create([], [], isTerminal: true);

        // Act
        var updated = original.WithEscalation(escalation);

        // Assert
        await Assert.That(original.EscalationHandler).IsNull();
        await Assert.That(updated.EscalationHandler).IsNotNull();
    }

    /// <summary>
    /// Verifies that WithEscalation throws for null escalation.
    /// </summary>
    [Test]
    public async Task WithEscalation_WithNullEscalation_ThrowsArgumentNullException()
    {
        // Arrange
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");

        // Act & Assert
        await Assert.That(() => approval.WithEscalation(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // D. WithRejection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithRejection sets the rejection handler.
    /// </summary>
    [Test]
    public async Task WithRejection_SetsRejectionHandler()
    {
        // Arrange
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");
        var rejection = ApprovalRejectionDefinition.Create([], isTerminal: true);

        // Act
        var updated = approval.WithRejection(rejection);

        // Assert
        await Assert.That(updated.RejectionHandler).IsNotNull();
        await Assert.That(updated.RejectionHandler!.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that WithRejection preserves original instance.
    /// </summary>
    [Test]
    public async Task WithRejection_PreservesOriginal()
    {
        // Arrange
        var original = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");
        var rejection = ApprovalRejectionDefinition.Create([], isTerminal: true);

        // Act
        var updated = original.WithRejection(rejection);

        // Assert
        await Assert.That(original.RejectionHandler).IsNull();
        await Assert.That(updated.RejectionHandler).IsNotNull();
    }

    /// <summary>
    /// Verifies that WithRejection throws for null rejection.
    /// </summary>
    [Test]
    public async Task WithRejection_WithNullRejection_ThrowsArgumentNullException()
    {
        // Arrange
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");

        // Act & Assert
        await Assert.That(() => approval.WithRejection(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // E. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ApprovalDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task ApprovalDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = ApprovalDefinition.Create(
            typeof(TestApprover),
            ApprovalConfiguration.Default,
            "step-1");

        // Act - Use record with syntax
        var modified = original with { PrecedingStepId = "step-2" };

        // Assert
        await Assert.That(original.PrecedingStepId).IsEqualTo("step-1");
        await Assert.That(modified.PrecedingStepId).IsEqualTo("step-2");
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that Configuration is preserved in immutable copy.
    /// </summary>
    [Test]
    public async Task Configuration_PreservedInCopy()
    {
        // Arrange
        var config = new ApprovalConfiguration { Timeout = TimeSpan.FromHours(4) };
        var approval = ApprovalDefinition.Create(
            typeof(TestApprover),
            config,
            "step-1");

        // Assert
        await Assert.That(approval.Configuration.Timeout).IsEqualTo(TimeSpan.FromHours(4));
    }
}

/// <summary>
/// Test approver marker class for approval definition testing.
/// </summary>
internal sealed class TestApprover
{
}

/// <summary>
/// Test supervisor approver marker class for nested approval testing.
/// </summary>
internal sealed class SupervisorApprover
{
}
