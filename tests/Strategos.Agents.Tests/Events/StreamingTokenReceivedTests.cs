// =============================================================================
// <copyright file="StreamingTokenReceivedTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Events;

/// <summary>
/// Unit tests for <see cref="StreamingTokenReceived"/> event record.
/// </summary>
[Property("Category", "Unit")]
public class StreamingTokenReceivedTests
{
    // =============================================================================
    // A. Record Construction Tests (2 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that StreamingTokenReceived can be constructed with all required properties.
    /// </summary>
    [Test]
    public async Task Constructor_WithValidParameters_CreatesRecord()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var taskId = "task-123";
        var specialistType = SpecialistType.Coder;
        var token = "def";
        var tokenIndex = 5;
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var evt = new StreamingTokenReceived(
            workflowId,
            taskId,
            specialistType,
            token,
            tokenIndex,
            timestamp);

        // Assert
        await Assert.That(evt.WorkflowId).IsEqualTo(workflowId);
        await Assert.That(evt.TaskId).IsEqualTo(taskId);
        await Assert.That(evt.SpecialistType).IsEqualTo(specialistType);
        await Assert.That(evt.Token).IsEqualTo(token);
        await Assert.That(evt.TokenIndex).IsEqualTo(tokenIndex);
        await Assert.That(evt.Timestamp).IsEqualTo(timestamp);
    }

    /// <summary>
    /// Verifies that StreamingTokenReceived implements IProgressEvent.
    /// </summary>
    [Test]
    public async Task StreamingTokenReceived_ImplementsIProgressEvent()
    {
        // Arrange
        var evt = new StreamingTokenReceived(
            Guid.NewGuid(),
            "task-1",
            SpecialistType.Analyst,
            "token",
            0,
            DateTimeOffset.UtcNow);

        // Act & Assert
        await Assert.That(evt).IsAssignableTo<IProgressEvent>();
    }

    // =============================================================================
    // B. Record Equality Tests (2 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that two records with same values are equal.
    /// </summary>
    [Test]
    public async Task Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var evt1 = new StreamingTokenReceived(
            workflowId,
            "task-1",
            SpecialistType.Coder,
            "token",
            0,
            timestamp);

        var evt2 = new StreamingTokenReceived(
            workflowId,
            "task-1",
            SpecialistType.Coder,
            "token",
            0,
            timestamp);

        // Act & Assert
        await Assert.That(evt1).IsEqualTo(evt2);
        await Assert.That(evt1.GetHashCode()).IsEqualTo(evt2.GetHashCode());
    }

    /// <summary>
    /// Verifies that two records with different values are not equal.
    /// </summary>
    [Test]
    public async Task Equals_WithDifferentTokenIndex_ReturnsFalse()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var evt1 = new StreamingTokenReceived(
            workflowId,
            "task-1",
            SpecialistType.Coder,
            "token",
            0,
            timestamp);

        var evt2 = new StreamingTokenReceived(
            workflowId,
            "task-1",
            SpecialistType.Coder,
            "token",
            1, // Different index
            timestamp);

        // Act & Assert
        await Assert.That(evt1).IsNotEqualTo(evt2);
    }

    // =============================================================================
    // C. With Expression Tests (1 test)
    // =============================================================================

    /// <summary>
    /// Verifies that with expression creates modified copy.
    /// </summary>
    [Test]
    public async Task With_ModifyingToken_CreatesModifiedCopy()
    {
        // Arrange
        var original = new StreamingTokenReceived(
            Guid.NewGuid(),
            "task-1",
            SpecialistType.Coder,
            "original",
            0,
            DateTimeOffset.UtcNow);

        // Act
        var modified = original with { Token = "modified" };

        // Assert
        await Assert.That(original.Token).IsEqualTo("original");
        await Assert.That(modified.Token).IsEqualTo("modified");
        await Assert.That(modified.WorkflowId).IsEqualTo(original.WorkflowId);
    }
}
