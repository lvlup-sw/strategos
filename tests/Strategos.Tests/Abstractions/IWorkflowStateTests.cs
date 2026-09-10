// =============================================================================
// <copyright file="IWorkflowStateTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="IWorkflowState"/>.
/// </summary>
[Property("Category", "Unit")]
public class IWorkflowStateTests
{
    /// <summary>
    /// Verifies that IWorkflowState requires WorkflowId property.
    /// </summary>
    [Test]
    public async Task IWorkflowState_RequiresWorkflowIdProperty()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        IWorkflowState state = new TestWorkflowState { WorkflowId = workflowId };

        // Assert
        await Assert.That(state.WorkflowId).IsEqualTo(workflowId);
    }

    /// <summary>
    /// Verifies that TestWorkflowState implements IWorkflowState.
    /// </summary>
    [Test]
    public async Task TestWorkflowState_ImplementsIWorkflowState()
    {
        // Arrange & Act
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid() };

        // Assert
        await Assert.That(state).IsTypeOf<IWorkflowState>();
    }

    /// <summary>
    /// Verifies that a custom state implementation works correctly.
    /// </summary>
    [Test]
    public async Task CustomState_ImplementsIWorkflowState()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        IWorkflowState state = new CustomWorkflowState
        {
            WorkflowId = workflowId,
            CustomProperty = "test-value",
        };

        // Assert
        await Assert.That(state.WorkflowId).IsEqualTo(workflowId);
    }
}

/// <summary>
/// Custom workflow state for testing interface implementation.
/// </summary>
internal sealed record CustomWorkflowState : IWorkflowState
{
    /// <inheritdoc/>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets custom test property.
    /// </summary>
    public string CustomProperty { get; init; } = string.Empty;
}
