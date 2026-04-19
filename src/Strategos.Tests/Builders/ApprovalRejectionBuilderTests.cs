// =============================================================================
// <copyright file="ApprovalRejectionBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ApprovalRejectionBuilder{TState}"/> fluent builder.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Then adds steps to rejection path</description></item>
///   <item><description>Complete marks rejection handler as terminal</description></item>
///   <item><description>Build creates ApprovalRejectionDefinition</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ApprovalRejectionBuilderTests
{
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
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

        // Act
        var result = builder.Then<NotifyAdminStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalRejectionBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Then adds a step to the internal collection.
    /// </summary>
    [Test]
    public async Task Then_WithStepType_AddsStepToCollection()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

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
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

        // Act
        var result = builder
            .Then<LogFailureStep>()
            .Then<NotifyAdminStep>()
            .Then<RefundStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(builder.Steps).Count().IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that steps are added in order.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_PreservesOrder()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

        // Act
        builder
            .Then<LogFailureStep>()
            .Then<NotifyAdminStep>();

        // Assert
        await Assert.That(builder.Steps[0].StepType).IsEqualTo(typeof(LogFailureStep));
        await Assert.That(builder.Steps[1].StepType).IsEqualTo(typeof(NotifyAdminStep));
    }

    // =============================================================================
    // B. Complete Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Complete marks the builder as terminal.
    /// </summary>
    [Test]
    public async Task Complete_MarksAsTerminal()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();
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
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

        // Assert
        await Assert.That(builder.IsTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that Complete can be called after Then chain.
    /// </summary>
    [Test]
    public async Task Complete_AfterThenChain_MarksAsTerminal()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

        // Act
        builder
            .Then<LogFailureStep>()
            .Then<NotifyAdminStep>();
        builder.Complete();

        // Assert
        await Assert.That(builder.IsTerminal).IsTrue();
        await Assert.That(builder.Steps).Count().IsEqualTo(2);
    }

    // =============================================================================
    // C. Build Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Build creates ApprovalRejectionDefinition.
    /// </summary>
    [Test]
    public async Task Build_CreatesRejectionDefinition()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition).IsNotNull();
        await Assert.That(definition.RejectionHandlerId).IsNotNull();
    }

    /// <summary>
    /// Verifies that Build includes steps in definition.
    /// </summary>
    [Test]
    public async Task Build_IncludesSteps()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();
        builder.Then<LogFailureStep>();

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Steps).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Build includes IsTerminal flag in definition.
    /// </summary>
    [Test]
    public async Task Build_IncludesIsTerminalFlag()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();
        builder.Complete();

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Build with non-terminal handler has IsTerminal false.
    /// </summary>
    [Test]
    public async Task Build_WithNonTerminal_HasIsTerminalFalse()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();
        builder.Then<LogFailureStep>();

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.IsTerminal).IsFalse();
    }

    // =============================================================================
    // D. Steps Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Steps collection is empty by default.
    /// </summary>
    [Test]
    public async Task Steps_ByDefault_IsEmpty()
    {
        // Arrange & Act
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();

        // Assert
        await Assert.That(builder.Steps).IsEmpty();
    }

    /// <summary>
    /// Verifies that Steps collection is read-only.
    /// </summary>
    [Test]
    public async Task Steps_ReturnsReadOnlyCollection()
    {
        // Arrange
        var builder = new ApprovalRejectionBuilder<TestWorkflowState>();
        builder.Then<LogFailureStep>();

        // Act
        var steps = builder.Steps;

        // Assert
        await Assert.That(steps).IsTypeOf<IReadOnlyList<StepDefinition>>();
    }
}
