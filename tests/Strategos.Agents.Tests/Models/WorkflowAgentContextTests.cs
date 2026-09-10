// =============================================================================
// <copyright file="WorkflowAgentContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for <see cref="WorkflowAgentContext"/> covering creation and properties.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowAgentContextTests
{
    /// <summary>
    /// Verifies that WorkflowAgentContext constructor sets properties correctly.
    /// </summary>
    [Test]
    public async Task Create_WithValidValues_SetsProperties()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
        };

        // Act
        var context = new WorkflowAgentContext(chatClient, messages);

        // Assert
        await Assert.That(context.ChatClient).IsSameReferenceAs(chatClient);
        await Assert.That(context.Messages).IsSameReferenceAs(messages);
    }

    /// <summary>
    /// Verifies that WorkflowAgentContext messages list is mutable.
    /// </summary>
    [Test]
    public async Task Messages_IsMutable_AllowsModification()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var messages = new List<ChatMessage>();
        var context = new WorkflowAgentContext(chatClient, messages);

        // Act
        context.Messages.Add(new ChatMessage(ChatRole.User, "Hello"));

        // Assert
        await Assert.That(context.Messages).HasCount(1);
        await Assert.That(messages).HasCount(1); // Original list also updated
    }

    /// <summary>
    /// Verifies that WorkflowAgentContext can be created with empty messages.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyMessages_Succeeds()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var messages = new List<ChatMessage>();

        // Act
        var context = new WorkflowAgentContext(chatClient, messages);

        // Assert
        await Assert.That(context.Messages).IsEmpty();
    }

    /// <summary>
    /// Verifies that two WorkflowAgentContexts with same references are equal.
    /// </summary>
    [Test]
    public async Task Equality_WithSameReferences_ReturnsTrue()
    {
        // Arrange
        var chatClient = Substitute.For<IChatClient>();
        var messages = new List<ChatMessage>();

        var context1 = new WorkflowAgentContext(chatClient, messages);
        var context2 = new WorkflowAgentContext(chatClient, messages);

        // Assert
        await Assert.That(context1).IsEqualTo(context2);
    }
}

