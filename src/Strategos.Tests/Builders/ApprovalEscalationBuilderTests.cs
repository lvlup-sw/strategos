// =============================================================================
// <copyright file="ApprovalEscalationBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ApprovalEscalationBuilder{TState}"/> fluent builder.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Then adds steps to escalation path</description></item>
///   <item><description>EscalateTo configures nested approval</description></item>
///   <item><description>Complete marks escalation as terminal</description></item>
///   <item><description>Build creates ApprovalEscalationDefinition</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ApprovalEscalationBuilderTests
{
    // =============================================================================
    // Test Marker Types
    // =============================================================================

    /// <summary>
    /// Marker type for supervisor approver role.
    /// </summary>
    private sealed class SupervisorApprover;

    /// <summary>
    /// Marker type for manager approver role.
    /// </summary>
    private sealed class ManagerApprover;

    // =============================================================================
    // A. Then Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Then returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task Then_WithStepType_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        var result = builder.Then<NotifyAdminStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalEscalationBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Then adds a step to the internal collection.
    /// </summary>
    [Test]
    public async Task Then_WithStepType_AddsStepToCollection()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        builder.Then<NotifyAdminStep>();

        // Assert
        await Assert.That(builder.Steps).Count().IsEqualTo(1);
        await Assert.That(builder.Steps[0].StepType).IsEqualTo(typeof(NotifyAdminStep));
    }

    /// <summary>
    /// Verifies that multiple Then calls can be chained.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_CanBeChained()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        var result = builder
            .Then<LogFailureStep>()
            .Then<NotifyAdminStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(builder.Steps).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that steps are added in order.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_PreservesOrder()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        builder
            .Then<LogFailureStep>()
            .Then<NotifyAdminStep>();

        // Assert
        await Assert.That(builder.Steps[0].StepType).IsEqualTo(typeof(LogFailureStep));
        await Assert.That(builder.Steps[1].StepType).IsEqualTo(typeof(NotifyAdminStep));
    }

    // =============================================================================
    // B. EscalateTo Tests
    // =============================================================================

    /// <summary>
    /// Verifies that EscalateTo returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task EscalateTo_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        var result = builder.EscalateTo<SupervisorApprover>(approval => { });

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalEscalationBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that EscalateTo adds nested approval to collection.
    /// </summary>
    [Test]
    public async Task EscalateTo_AddsNestedApproval()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        builder.EscalateTo<SupervisorApprover>(approval =>
            approval.WithContext("Escalated for supervisor review"));

        // Assert
        await Assert.That(builder.NestedApprovals).Count().IsEqualTo(1);
        await Assert.That(builder.NestedApprovals[0].ApproverType).IsEqualTo(typeof(SupervisorApprover));
    }

    /// <summary>
    /// Verifies that EscalateTo can chain multiple nested approvals.
    /// </summary>
    [Test]
    public async Task EscalateTo_MultipleCalls_AddsMultipleNestedApprovals()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        builder
            .EscalateTo<SupervisorApprover>(approval =>
                approval.WithContext("For supervisor"))
            .EscalateTo<ManagerApprover>(approval =>
                approval.WithContext("For manager"));

        // Assert
        await Assert.That(builder.NestedApprovals).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that EscalateTo with null configure action throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task EscalateTo_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act & Assert
        await Assert.That(() => builder.EscalateTo<SupervisorApprover>(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // C. Complete Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Complete marks the builder as terminal.
    /// </summary>
    [Test]
    public async Task Complete_MarksAsTerminal()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();
        builder.Then<LogFailureStep>();

        // Act
        builder.Complete();

        // Assert
        await Assert.That(builder.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that IsTerminal is false by default.
    /// </summary>
    [Test]
    public async Task IsTerminal_ByDefault_IsFalse()
    {
        // Arrange & Act
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Assert
        await Assert.That(builder.IsTerminal).IsFalse();
    }

    // =============================================================================
    // D. Build Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Build creates ApprovalEscalationDefinition.
    /// </summary>
    [Test]
    public async Task Build_CreatesEscalationDefinition()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition).IsNotNull();
        await Assert.That(definition.EscalationId).IsNotNull();
    }

    /// <summary>
    /// Verifies that Build includes steps in definition.
    /// </summary>
    [Test]
    public async Task Build_IncludesSteps()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();
        builder.Then<LogFailureStep>();

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Steps).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Build includes nested approvals in definition.
    /// </summary>
    [Test]
    public async Task Build_IncludesNestedApprovals()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();
        builder.EscalateTo<SupervisorApprover>(approval =>
            approval.WithContext("For supervisor"));

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.NestedApprovals).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Build includes IsTerminal flag in definition.
    /// </summary>
    [Test]
    public async Task Build_IncludesIsTerminalFlag()
    {
        // Arrange
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();
        builder.Complete();

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.IsTerminal).IsTrue();
    }

    // =============================================================================
    // E. Steps Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Steps collection is empty by default.
    /// </summary>
    [Test]
    public async Task Steps_ByDefault_IsEmpty()
    {
        // Arrange & Act
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Assert
        await Assert.That(builder.Steps).IsEmpty();
    }

    /// <summary>
    /// Verifies that NestedApprovals collection is empty by default.
    /// </summary>
    [Test]
    public async Task NestedApprovals_ByDefault_IsEmpty()
    {
        // Arrange & Act
        var builder = new ApprovalEscalationBuilder<TestWorkflowState>();

        // Assert
        await Assert.That(builder.NestedApprovals).IsEmpty();
    }
}
