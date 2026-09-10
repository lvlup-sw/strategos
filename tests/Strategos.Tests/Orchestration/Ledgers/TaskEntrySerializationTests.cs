// =============================================================================
// <copyright file="TaskEntrySerializationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Orchestration;
using Strategos.Orchestration.Ledgers;

using MemoryPack;

namespace Strategos.Tests.Orchestration.Ledgers;

/// <summary>
/// Tests for MemoryPack serialization of TaskEntry.
/// </summary>
public sealed class TaskEntrySerializationTests
{
    /// <summary>
    /// Verifies that TaskEntry can be serialized and deserialized with MemoryPack,
    /// preserving all property values through the round-trip.
    /// </summary>
    [Test]
    public async Task TaskEntry_MemoryPackSerialize_RoundTrips()
    {
        // Arrange
        var original = TaskEntry.Create(
            description: "Test task description",
            priority: 5,
            dependencies: new[] { "dep-1", "dep-2" }) with
        {
            Status = WorkflowTaskStatus.InProgress,
            PreferredExecutorId = "preferred-exec",
            Result = "Task result",
            Deadline = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero),
            RequiredCapabilities = Capability.CodeGeneration | Capability.CodeExecution,
            Metadata = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" }
        };

        // Act
        var bytes = MemoryPackSerializer.Serialize(original);
        var deserialized = MemoryPackSerializer.Deserialize<TaskEntry>(bytes);

        // Assert
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.TaskId).IsEqualTo(original.TaskId);
        await Assert.That(deserialized.Description).IsEqualTo(original.Description);
        await Assert.That(deserialized.Priority).IsEqualTo(original.Priority);
        await Assert.That(deserialized.Status).IsEqualTo(original.Status);
        await Assert.That(deserialized.PreferredExecutorId).IsEqualTo(original.PreferredExecutorId);
        await Assert.That(deserialized.Result).IsEqualTo(original.Result);
        await Assert.That(deserialized.Deadline).IsEqualTo(original.Deadline);
        await Assert.That(deserialized.RequiredCapabilities).IsEqualTo(original.RequiredCapabilities);
        await Assert.That(deserialized.Dependencies).IsEquivalentTo(original.Dependencies);
        await Assert.That(deserialized.Metadata).IsNotNull();
        await Assert.That(deserialized.Metadata!["key1"]).IsEqualTo("value1");
        await Assert.That(deserialized.Metadata["key2"]).IsEqualTo("value2");
    }

    /// <summary>
    /// Verifies that TaskEntry with minimal properties can be serialized and deserialized.
    /// </summary>
    [Test]
    public async Task TaskEntry_MemoryPackSerialize_MinimalProperties_RoundTrips()
    {
        // Arrange
        var original = TaskEntry.Create(description: "Minimal task");

        // Act
        var bytes = MemoryPackSerializer.Serialize(original);
        var deserialized = MemoryPackSerializer.Deserialize<TaskEntry>(bytes);

        // Assert
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.TaskId).IsEqualTo(original.TaskId);
        await Assert.That(deserialized.Description).IsEqualTo(original.Description);
        await Assert.That(deserialized.Status).IsEqualTo(WorkflowTaskStatus.Pending);
        await Assert.That(deserialized.Priority).IsEqualTo(0);
        await Assert.That(deserialized.Dependencies).IsEmpty();
        await Assert.That(deserialized.PreferredExecutorId).IsNull();
        await Assert.That(deserialized.Result).IsNull();
        await Assert.That(deserialized.Deadline).IsNull();
        await Assert.That(deserialized.Metadata).IsNull();
    }

    /// <summary>
    /// Verifies that TaskEntry created with CreateWithId can be serialized and deserialized.
    /// </summary>
    [Test]
    public async Task TaskEntry_MemoryPackSerialize_WithExplicitId_RoundTrips()
    {
        // Arrange
        var original = TaskEntry.CreateWithId(
            taskId: "explicit-task-id",
            description: "Task with explicit ID",
            priority: 10,
            dependencies: new[] { "dep-a" });

        // Act
        var bytes = MemoryPackSerializer.Serialize(original);
        var deserialized = MemoryPackSerializer.Deserialize<TaskEntry>(bytes);

        // Assert
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.TaskId).IsEqualTo("explicit-task-id");
        await Assert.That(deserialized.Description).IsEqualTo("Task with explicit ID");
        await Assert.That(deserialized.Priority).IsEqualTo(10);
        await Assert.That(deserialized.Dependencies).Contains("dep-a");
    }
}

