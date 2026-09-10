// =============================================================================
// <copyright file="AgentStepContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for <see cref="AgentStepContext"/> covering creation and properties.
/// </summary>
[Property("Category", "Unit")]
public class AgentStepContextTests
{
    /// <summary>
    /// Verifies that AgentStepContext constructor sets required properties correctly.
    /// </summary>
    [Test]
    public async Task Create_WithRequiredValues_SetsProperties()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var workflowId = Guid.NewGuid();
        var stepName = "TestStep";
        var stepExecutionId = Guid.NewGuid();

        // Act
        var context = new AgentStepContext(
            chatClient,
            workflowId,
            stepName,
            stepExecutionId);

        // Assert
        await Assert.That(context.ChatClient).IsSameReferenceAs(chatClient);
        await Assert.That(context.WorkflowId).IsEqualTo(workflowId);
        await Assert.That(context.StepName).IsEqualTo(stepName);
        await Assert.That(context.StepExecutionId).IsEqualTo(stepExecutionId);
    }

    /// <summary>
    /// Verifies that optional parameters default to null.
    /// </summary>
    [Test]
    public async Task Create_WithOptionalDefaults_SetsNullValues()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();

        // Act
        var context = new AgentStepContext(
            chatClient,
            Guid.NewGuid(),
            "TestStep",
            Guid.NewGuid());

        // Assert
        await Assert.That(context.StreamingCallback).IsNull();
        await Assert.That(context.ConversationThreadManager).IsNull();
    }

    /// <summary>
    /// Verifies that AgentStepContext can include StreamingCallback.
    /// </summary>
    [Test]
    public async Task Create_WithStreamingCallback_SetsProperty()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var callback = Substitute.For<IStreamingCallback>();

        // Act
        var context = new AgentStepContext(
            chatClient,
            Guid.NewGuid(),
            "TestStep",
            Guid.NewGuid(),
            StreamingCallback: callback);

        // Assert
        await Assert.That(context.StreamingCallback).IsSameReferenceAs(callback);
    }

    /// <summary>
    /// Verifies that AgentStepContext can include ConversationThreadManager.
    /// </summary>
    [Test]
    public async Task Create_WithConversationThreadManager_SetsProperty()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var threadManager = Substitute.For<IConversationThreadManager>();

        // Act
        var context = new AgentStepContext(
            chatClient,
            Guid.NewGuid(),
            "TestStep",
            Guid.NewGuid(),
            ConversationThreadManager: threadManager);

        // Assert
        await Assert.That(context.ConversationThreadManager).IsSameReferenceAs(threadManager);
    }

    /// <summary>
    /// Verifies that two AgentStepContexts with same values are equal.
    /// </summary>
    [Test]
    public async Task Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var workflowId = Guid.NewGuid();
        var stepExecutionId = Guid.NewGuid();

        var context1 = new AgentStepContext(chatClient, workflowId, "TestStep", stepExecutionId);
        var context2 = new AgentStepContext(chatClient, workflowId, "TestStep", stepExecutionId);

        // Assert
        await Assert.That(context1).IsEqualTo(context2);
    }
}

