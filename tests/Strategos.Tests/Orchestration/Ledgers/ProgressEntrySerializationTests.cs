// =============================================================================
// <copyright file="ProgressEntrySerializationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Orchestration.Ledgers;

using MemoryPack;

namespace Strategos.Tests.Orchestration.Ledgers;

/// <summary>
/// Unit tests for ProgressEntry MemoryPack serialization.
/// </summary>
[Property("Category", "Unit")]
public sealed class ProgressEntrySerializationTests
{
    /// <summary>
    /// Verifies that ProgressEntry can be serialized and deserialized with MemoryPack,
    /// preserving all property values through the round-trip.
    /// </summary>
    [Test]
    public async Task ProgressEntry_MemoryPackSerialize_RoundTrips()
    {
        // Arrange
        var original = ProgressEntry.Create(
            taskId: "task-123",
            executorId: "executor-456",
            action: "TestAction",
            progressMade: true,
            output: "Test output") with
        {
            TokensConsumed = 100,
            Duration = TimeSpan.FromSeconds(5),
            Artifacts = new[] { "artifact1", "artifact2" }
        };

        // Act
        var bytes = MemoryPackSerializer.Serialize(original);
        var deserialized = MemoryPackSerializer.Deserialize<ProgressEntry>(bytes);

        // Assert
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.EntryId).IsEqualTo(original.EntryId);
        await Assert.That(deserialized.TaskId).IsEqualTo(original.TaskId);
        await Assert.That(deserialized.ExecutorId).IsEqualTo(original.ExecutorId);
        await Assert.That(deserialized.Action).IsEqualTo(original.Action);
        await Assert.That(deserialized.Output).IsEqualTo(original.Output);
        await Assert.That(deserialized.ProgressMade).IsEqualTo(original.ProgressMade);
        await Assert.That(deserialized.TokensConsumed).IsEqualTo(original.TokensConsumed);
        await Assert.That(deserialized.Duration).IsEqualTo(original.Duration);
        await Assert.That(deserialized.Artifacts).IsEquivalentTo(original.Artifacts);
    }

    /// <summary>
    /// Verifies that ProgressEntry with null optional properties can be serialized and deserialized.
    /// </summary>
    [Test]
    public async Task ProgressEntry_MemoryPackSerialize_WithNullProperties_RoundTrips()
    {
        // Arrange
        var original = ProgressEntry.Create(
            taskId: "task-456",
            executorId: "executor-789",
            action: "MinimalAction",
            progressMade: false);

        // Act
        var bytes = MemoryPackSerializer.Serialize(original);
        var deserialized = MemoryPackSerializer.Deserialize<ProgressEntry>(bytes);

        // Assert
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.TaskId).IsEqualTo(original.TaskId);
        await Assert.That(deserialized.ExecutorId).IsEqualTo(original.ExecutorId);
        await Assert.That(deserialized.Action).IsEqualTo(original.Action);
        await Assert.That(deserialized.ProgressMade).IsEqualTo(original.ProgressMade);
        await Assert.That(deserialized.Output).IsNull();
        await Assert.That(deserialized.Duration).IsNull();
    }
}
