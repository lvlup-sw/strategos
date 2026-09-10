// =============================================================================
// <copyright file="ChatMessageRecordedTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Events;

/// <summary>
/// Unit tests for <see cref="ChatMessageRecorded"/> event covering construction,
/// interface implementation, and serialization readiness.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
/// <item>Event properly stores workflow ID, specialist type, role, and content</item>
/// <item>Event implements IProgressEvent for Marten event sourcing</item>
/// <item>Record equality semantics work correctly</item>
/// <item>MessageRole enum values are supported</item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ChatMessageRecordedTests
{
    // =============================================================================
    // A. Construction Tests (4 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that ChatMessageRecorded creates an event with all required parameters.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_WithValidParams_CreatesEvent()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var taskId = "task-123";
        var specialistType = SpecialistType.Coder;
        var role = MessageRole.Assistant;
        var content = "def hello(): print('hello')";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var evt = new ChatMessageRecorded(
            workflowId,
            taskId,
            specialistType,
            role,
            content,
            timestamp);

        // Assert
        await Assert.That(evt.WorkflowId).IsEqualTo(workflowId);
        await Assert.That(evt.TaskId).IsEqualTo(taskId);
        await Assert.That(evt.SpecialistType).IsEqualTo(specialistType);
        await Assert.That(evt.Role).IsEqualTo(role);
        await Assert.That(evt.Content).IsEqualTo(content);
        await Assert.That(evt.Timestamp).IsEqualTo(timestamp);
    }

    /// <summary>
    /// Verifies that ChatMessageRecorded can store system messages.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_WithSystemRole_CreatesEvent()
    {
        // Arrange & Act
        var evt = new ChatMessageRecorded(
            Guid.NewGuid(),
            "task-1",
            SpecialistType.Analyst,
            MessageRole.System,
            "You are a data analyst specialist.",
            DateTimeOffset.UtcNow);

        // Assert
        await Assert.That(evt.Role).IsEqualTo(MessageRole.System);
        await Assert.That(evt.Content).StartsWith("You are");
    }

    /// <summary>
    /// Verifies that ChatMessageRecorded can store user messages.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_WithUserRole_CreatesEvent()
    {
        // Arrange & Act
        var evt = new ChatMessageRecorded(
            Guid.NewGuid(),
            "task-2",
            SpecialistType.WebSurfer,
            MessageRole.User,
            "Search for Python tutorials.",
            DateTimeOffset.UtcNow);

        // Assert
        await Assert.That(evt.Role).IsEqualTo(MessageRole.User);
    }

    /// <summary>
    /// Verifies that ChatMessageRecorded can store empty content (for tool calls, etc.).
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_WithEmptyContent_CreatesEvent()
    {
        // Arrange & Act
        var evt = new ChatMessageRecorded(
            Guid.NewGuid(),
            "task-3",
            SpecialistType.Coder,
            MessageRole.Assistant,
            string.Empty,
            DateTimeOffset.UtcNow);

        // Assert
        await Assert.That(evt.Content).IsEmpty();
    }

    // =============================================================================
    // B. Interface Implementation Tests (2 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that ChatMessageRecorded implements IProgressEvent.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_ImplementsIProgressEvent()
    {
        // Arrange & Act
        var evt = new ChatMessageRecorded(
            Guid.NewGuid(),
            "task-1",
            SpecialistType.Coder,
            MessageRole.User,
            "Hello",
            DateTimeOffset.UtcNow);

        // Assert
        await Assert.That(evt).IsAssignableTo<IProgressEvent>();
    }

    /// <summary>
    /// Verifies that IProgressEvent members are accessible through interface.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_IProgressEvent_PropertiesAccessible()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        IProgressEvent evt = new ChatMessageRecorded(
            workflowId,
            "task-1",
            SpecialistType.Coder,
            MessageRole.User,
            "Content",
            timestamp);

        // Assert
        await Assert.That(evt.WorkflowId).IsEqualTo(workflowId);
        await Assert.That(evt.Timestamp).IsEqualTo(timestamp);
    }

    // =============================================================================
    // C. Record Equality Tests (2 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that two events with same values are equal.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_SameValues_AreEqual()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var evt1 = new ChatMessageRecorded(workflowId, "task-1", SpecialistType.Coder, MessageRole.User, "Hello", timestamp);
        var evt2 = new ChatMessageRecorded(workflowId, "task-1", SpecialistType.Coder, MessageRole.User, "Hello", timestamp);

        // Assert
        await Assert.That(evt1).IsEqualTo(evt2);
        await Assert.That(evt1.GetHashCode()).IsEqualTo(evt2.GetHashCode());
    }

    /// <summary>
    /// Verifies that events with different content are not equal.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_DifferentContent_NotEqual()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var evt1 = new ChatMessageRecorded(workflowId, "task-1", SpecialistType.Coder, MessageRole.User, "Hello", timestamp);
        var evt2 = new ChatMessageRecorded(workflowId, "task-1", SpecialistType.Coder, MessageRole.User, "Goodbye", timestamp);

        // Assert
        await Assert.That(evt1).IsNotEqualTo(evt2);
    }

    // =============================================================================
    // D. MessageRole Enum Tests (3 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that all MessageRole values can be used.
    /// </summary>
    [Test]
    public async Task MessageRole_AllValues_CanBeUsed()
    {
        // Arrange & Act
        var systemEvt = new ChatMessageRecorded(
            Guid.NewGuid(), "t1", SpecialistType.Coder, MessageRole.System, "sys", DateTimeOffset.UtcNow);
        var userEvt = new ChatMessageRecorded(
            Guid.NewGuid(), "t2", SpecialistType.Coder, MessageRole.User, "user", DateTimeOffset.UtcNow);
        var assistantEvt = new ChatMessageRecorded(
            Guid.NewGuid(), "t3", SpecialistType.Coder, MessageRole.Assistant, "asst", DateTimeOffset.UtcNow);
        var toolEvt = new ChatMessageRecorded(
            Guid.NewGuid(), "t4", SpecialistType.Coder, MessageRole.Tool, "tool", DateTimeOffset.UtcNow);

        // Assert
        await Assert.That(systemEvt.Role).IsEqualTo(MessageRole.System);
        await Assert.That(userEvt.Role).IsEqualTo(MessageRole.User);
        await Assert.That(assistantEvt.Role).IsEqualTo(MessageRole.Assistant);
        await Assert.That(toolEvt.Role).IsEqualTo(MessageRole.Tool);
    }

    /// <summary>
    /// Verifies that different roles produce different events.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_DifferentRoles_NotEqual()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var userEvt = new ChatMessageRecorded(workflowId, "t1", SpecialistType.Coder, MessageRole.User, "msg", timestamp);
        var assistantEvt = new ChatMessageRecorded(workflowId, "t1", SpecialistType.Coder, MessageRole.Assistant, "msg", timestamp);

        // Assert
        await Assert.That(userEvt).IsNotEqualTo(assistantEvt);
    }

    /// <summary>
    /// Verifies that different specialist types produce different events.
    /// </summary>
    [Test]
    public async Task ChatMessageRecorded_DifferentSpecialist_NotEqual()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var coderEvt = new ChatMessageRecorded(workflowId, "t1", SpecialistType.Coder, MessageRole.User, "msg", timestamp);
        var analystEvt = new ChatMessageRecorded(workflowId, "t1", SpecialistType.Analyst, MessageRole.User, "msg", timestamp);

        // Assert
        await Assert.That(coderEvt).IsNotEqualTo(analystEvt);
    }
}
