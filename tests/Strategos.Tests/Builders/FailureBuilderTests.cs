// =============================================================================
// <copyright file="FailureBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="FailureBuilder{TState}"/> fluent builder.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Then adds steps to failure path</description></item>
///   <item><description>Complete marks the path as terminal</description></item>
///   <item><description>Steps collection is properly maintained</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class FailureBuilderTests
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
        var builder = new FailureBuilder<TestWorkflowState>();

        // Act
        var result = builder.Then<LogFailureStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IFailureBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Then adds a step to the internal collection.
    /// </summary>
    [Test]
    public async Task Then_WithStepType_AddsStepToCollection()
    {
        // Arrange
        var builder = new FailureBuilder<TestWorkflowState>();

        // Act
        builder.Then<LogFailureStep>();

        // Assert
        await Assert.That(builder.Steps).HasCount().EqualTo(1);
        await Assert.That(builder.Steps[0].StepType).IsEqualTo(typeof(LogFailureStep));
    }

    /// <summary>
    /// Verifies that multiple Then calls can be chained.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_CanBeChained()
    {
        // Arrange
        var builder = new FailureBuilder<TestWorkflowState>();

        // Act
        var result = builder
            .Then<LogFailureStep>()
            .Then<NotifyAdminStep>();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(builder.Steps).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that steps are added in order.
    /// </summary>
    [Test]
    public async Task Then_MultipleCalls_PreservesOrder()
    {
        // Arrange
        var builder = new FailureBuilder<TestWorkflowState>();

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
        var builder = new FailureBuilder<TestWorkflowState>();
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
        var builder = new FailureBuilder<TestWorkflowState>();

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
        var builder = new FailureBuilder<TestWorkflowState>();

        // Act
        builder
            .Then<LogFailureStep>()
            .Then<NotifyAdminStep>();
        builder.Complete();

        // Assert
        await Assert.That(builder.IsTerminal).IsTrue();
        await Assert.That(builder.Steps).HasCount().EqualTo(2);
    }

    // =============================================================================
    // C. Steps Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Steps collection is empty by default.
    /// </summary>
    [Test]
    public async Task Steps_ByDefault_IsEmpty()
    {
        // Arrange & Act
        var builder = new FailureBuilder<TestWorkflowState>();

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
        var builder = new FailureBuilder<TestWorkflowState>();
        builder.Then<LogFailureStep>();

        // Act
        var steps = builder.Steps;

        // Assert
        await Assert.That(steps).IsTypeOf<IReadOnlyList<StepDefinition>>();
    }
}
