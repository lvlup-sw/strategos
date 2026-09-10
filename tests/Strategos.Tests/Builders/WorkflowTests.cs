// =============================================================================
// <copyright file="WorkflowTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Builders;

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="Workflow{TState}"/> static entry point.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Create returns a workflow builder</description></item>
///   <item><description>Guard clauses throw appropriate exceptions</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class WorkflowTests
{
    // =============================================================================
    // A. Create Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with a valid name returns a workflow builder.
    /// </summary>
    [Test]
    public async Task Create_WithValidName_ReturnsWorkflowBuilder()
    {
        // Arrange
        const string workflowName = "test-workflow";

        // Act
        var builder = Workflow<TestWorkflowState>.Create(workflowName);

        // Assert
        await Assert.That(builder).IsNotNull();
        await Assert.That(builder).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that Create throws ArgumentNullException for null name.
    /// </summary>
    [Test]
    public async Task Create_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => Workflow<TestWorkflowState>.Create(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws ArgumentException for empty name.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => Workflow<TestWorkflowState>.Create(string.Empty))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws ArgumentException for whitespace-only name.
    /// </summary>
    [Test]
    public async Task Create_WithWhitespaceName_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => Workflow<TestWorkflowState>.Create("   "))
            .Throws<ArgumentException>();
    }
}
