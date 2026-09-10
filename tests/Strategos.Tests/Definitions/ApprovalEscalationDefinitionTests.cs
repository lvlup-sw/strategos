// =============================================================================
// <copyright file="ApprovalEscalationDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ApprovalEscalationDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class ApprovalEscalationDefinitionTests
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
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(TestNotifyStep))
        };

        // Act
        var escalation = ApprovalEscalationDefinition.Create(steps, [], isTerminal: true);

        // Assert
        await Assert.That(escalation.Steps.Count).IsEqualTo(1);
        await Assert.That(escalation.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Create generates a unique EscalationId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueEscalationId()
    {
        // Act
        var escalation1 = ApprovalEscalationDefinition.Create([], [], isTerminal: false);
        var escalation2 = ApprovalEscalationDefinition.Create([], [], isTerminal: false);

        // Assert
        await Assert.That(escalation1.EscalationId).IsNotNull();
        await Assert.That(escalation1.EscalationId).IsNotEqualTo(escalation2.EscalationId);
    }

    /// <summary>
    /// Verifies that Create throws for null steps.
    /// </summary>
    [Test]
    public async Task Create_WithNullSteps_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalEscalationDefinition.Create(null!, [], isTerminal: true))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for null nested approvals.
    /// </summary>
    [Test]
    public async Task Create_WithNullNestedApprovals_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalEscalationDefinition.Create([], null!, isTerminal: true))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Steps Tests
    // =============================================================================

    /// <summary>
    /// Verifies that empty steps is allowed.
    /// </summary>
    [Test]
    public async Task Create_WithEmptySteps_Succeeds()
    {
        // Act
        var escalation = ApprovalEscalationDefinition.Create([], [], isTerminal: true);

        // Assert
        await Assert.That(escalation.Steps).IsEmpty();
    }

    /// <summary>
    /// Verifies that step order is preserved.
    /// </summary>
    [Test]
    public async Task Steps_PreservesOrder()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(TestNotifyStep)),
            StepDefinition.Create(typeof(TestEscalateStep)),
        };

        // Act
        var escalation = ApprovalEscalationDefinition.Create(steps, [], isTerminal: true);

        // Assert
        await Assert.That(escalation.Steps[0].StepTypeName).IsEqualTo("TestNotifyStep");
        await Assert.That(escalation.Steps[1].StepTypeName).IsEqualTo("TestEscalateStep");
    }

    // =============================================================================
    // C. Nested Approvals Tests
    // =============================================================================

    /// <summary>
    /// Verifies that nested approvals can be added.
    /// </summary>
    [Test]
    public async Task Create_WithNestedApprovals_StoresApprovals()
    {
        // Arrange
        var nestedApproval = ApprovalDefinition.Create(
            typeof(SupervisorApprover),
            ApprovalConfiguration.Default,
            "nested-step");

        // Act
        var escalation = ApprovalEscalationDefinition.Create(
            [],
            [nestedApproval],
            isTerminal: false);

        // Assert
        await Assert.That(escalation.NestedApprovals.Count).IsEqualTo(1);
        await Assert.That(escalation.NestedApprovals[0].ApproverType).IsEqualTo(typeof(SupervisorApprover));
    }

    // =============================================================================
    // D. IsTerminal Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsTerminal true terminates workflow on timeout.
    /// </summary>
    [Test]
    public async Task IsTerminal_True_IndicatesWorkflowTermination()
    {
        // Act
        var escalation = ApprovalEscalationDefinition.Create([], [], isTerminal: true);

        // Assert
        await Assert.That(escalation.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that IsTerminal false rejoins main flow.
    /// </summary>
    [Test]
    public async Task IsTerminal_False_IndicatesRejoin()
    {
        // Act
        var escalation = ApprovalEscalationDefinition.Create([], [], isTerminal: false);

        // Assert
        await Assert.That(escalation.IsTerminal).IsFalse();
    }

    // =============================================================================
    // E. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ApprovalEscalationDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task ApprovalEscalationDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = ApprovalEscalationDefinition.Create([], [], isTerminal: false);

        // Act - Use record with syntax
        var modified = original with { IsTerminal = true };

        // Assert
        await Assert.That(original.IsTerminal).IsFalse();
        await Assert.That(modified.IsTerminal).IsTrue();
        await Assert.That(original).IsNotEqualTo(modified);
    }
}

/// <summary>
/// Test step class for escalation testing.
/// </summary>
internal sealed class TestNotifyStep
{
}

/// <summary>
/// Test step class for escalation testing.
/// </summary>
internal sealed class TestEscalateStep
{
}
