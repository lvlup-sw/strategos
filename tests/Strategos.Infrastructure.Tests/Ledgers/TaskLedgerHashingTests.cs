// =============================================================================
// <copyright file="TaskLedgerHashingTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Security.Cryptography;

using MemoryPack;

namespace Strategos.Infrastructure.Tests.Ledgers;

/// <summary>
/// Unit tests for TaskLedger content hashing using MemoryPack serialization.
/// </summary>
[Property("Category", "Unit")]
public sealed class TaskLedgerHashingTests
{
    /// <summary>
    /// Verifies that ComputeContentHash produces consistent hashes using MemoryPack serialization.
    /// </summary>
    /// <remarks>
    /// This test validates that the content hash is computed using MemoryPack binary serialization
    /// rather than JSON serialization, which provides better performance.
    /// </remarks>
    [Test]
    public async Task ComputeContentHash_MemoryPack_ProducesConsistentHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var tasks = new List<TaskEntry> { task1, task2 };
        var originalRequest = "Test request for hashing";

        // Create a ledger to get its hash
        var ledger = TaskLedger.Create(originalRequest, tasks);

        // Compute expected hash using MemoryPack serialization
        var hashContent = new TaskLedgerHashContent
        {
            OriginalRequest = originalRequest,
            TaskIds = tasks.Select(t => t.TaskId).ToList(),
            TaskDescriptions = tasks.Select(t => t.Description).ToList(),
        };
        var bytes = MemoryPackSerializer.Serialize(hashContent);
        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // Act
        var actualHash = ledger.ContentHash;

        // Assert - If using MemoryPack, hashes should match
        await Assert.That(actualHash).IsEqualTo(expectedHash);
    }

    /// <summary>
    /// Verifies that the same content always produces the same hash.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_SameContent_ProducesSameHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var tasks = new List<TaskEntry> { task1, task2 };
        var originalRequest = "Test request for hashing";

        // Act
        var ledger1 = TaskLedger.Create(originalRequest, tasks);
        var ledger2 = TaskLedger.Create(originalRequest, tasks);

        // Assert - Same content should produce same hash
        await Assert.That(ledger1.ContentHash).IsEqualTo(ledger2.ContentHash);
    }

    /// <summary>
    /// Verifies that different content produces different hashes.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_DifferentContent_ProducesDifferentHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var task3 = TaskEntry.CreateWithId("task-003", "Third task", priority: 3);

        var tasks1 = new List<TaskEntry> { task1, task2 };
        var tasks2 = new List<TaskEntry> { task1, task2, task3 };

        // Act
        var ledger1 = TaskLedger.Create("Test request", tasks1);
        var ledger2 = TaskLedger.Create("Test request", tasks2);

        // Assert - Different content should produce different hash
        await Assert.That(ledger1.ContentHash).IsNotEqualTo(ledger2.ContentHash);
    }

    /// <summary>
    /// Verifies that WithTask updates the content hash.
    /// </summary>
    [Test]
    public async Task WithTask_UpdatesContentHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var ledger = TaskLedger.Create("Test request", new List<TaskEntry> { task1 });

        // Act
        var updatedLedger = ledger.WithTask(task2);

        // Assert - Hash should be different after adding a task
        await Assert.That(updatedLedger.ContentHash).IsNotEqualTo(ledger.ContentHash);
    }

    /// <summary>
    /// Verifies that hash remains stable across multiple calls with identical content.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_MultipleCallsSameContent_ReturnsStableHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var tasks = new List<TaskEntry> { task1, task2 };
        var originalRequest = "Test request for stability";

        // Act - Create multiple ledgers and compute hashes multiple times
        var hashes = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var ledger = TaskLedger.Create(originalRequest, tasks);
            hashes.Add(ledger.ContentHash);
        }

        // Assert - All hashes should be identical
        var firstHash = hashes[0];
        await Assert.That(hashes).HasCount().EqualTo(10);
        foreach (var hash in hashes)
        {
            await Assert.That(hash).IsEqualTo(firstHash);
        }
    }

    /// <summary>
    /// Verifies that hashing performs efficiently with a large number of tasks.
    /// </summary>
    [Test]
    [Property("Category", "Unit")]
    public async Task ComputeContentHash_LargeTaskCount_CompletesSuccessfully()
    {
        // Arrange - Create 1000 tasks
        var tasks = new List<TaskEntry>();
        for (int i = 0; i < 1000; i++)
        {
            tasks.Add(TaskEntry.CreateWithId(
                $"task-{i:D4}",
                $"Task description {i} with some additional content to simulate realistic task descriptions",
                priority: i % 10));
        }

        var originalRequest = "Large scale test request with many tasks";

        // Act
        var ledger = TaskLedger.Create(originalRequest, tasks);

        // Assert - Hash should be computed and valid
        await Assert.That(ledger.ContentHash).IsNotNull();
        await Assert.That(ledger.ContentHash.Length).IsEqualTo(64); // SHA-256 produces 64 hex characters
        await Assert.That(ledger.Tasks).HasCount().EqualTo(1000);
        await Assert.That(ledger.VerifyIntegrity()).IsTrue();
    }

    /// <summary>
    /// Verifies that an empty task list produces a valid hash.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_EmptyTaskList_ProducesValidHash()
    {
        // Arrange
        var emptyTasks = new List<TaskEntry>();
        var originalRequest = "Request with no tasks";

        // Act
        var ledger = TaskLedger.Create(originalRequest, emptyTasks);

        // Assert - Hash should be valid even with empty task list
        await Assert.That(ledger.ContentHash).IsNotNull();
        await Assert.That(ledger.ContentHash).IsNotEmpty();
        await Assert.That(ledger.ContentHash.Length).IsEqualTo(64); // SHA-256 produces 64 hex characters
        await Assert.That(ledger.Tasks).IsEmpty();
    }

    /// <summary>
    /// Verifies that VerifyIntegrity returns true immediately after creation.
    /// </summary>
    [Test]
    public async Task VerifyIntegrity_ImmediatelyAfterCreation_ReturnsTrue()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var tasks = new List<TaskEntry> { task1, task2 };

        // Act
        var ledger = TaskLedger.Create("Test request", tasks);
        var isValid = ledger.VerifyIntegrity();

        // Assert
        await Assert.That(isValid).IsTrue();
    }

    /// <summary>
    /// Verifies that VerifyIntegrity returns true after WithTask operation.
    /// </summary>
    [Test]
    public async Task VerifyIntegrity_AfterWithTask_ReturnsTrue()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var ledger = TaskLedger.Create("Test request", new List<TaskEntry> { task1 });

        // Act
        var updatedLedger = ledger.WithTask(task2);
        var isValid = updatedLedger.VerifyIntegrity();

        // Assert
        await Assert.That(isValid).IsTrue();
    }

    /// <summary>
    /// Verifies that WithTask produces consistent hash when adding the same task.
    /// </summary>
    [Test]
    public async Task WithTask_SameTaskAddedMultipleTimes_ProducesConsistentHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);
        var ledger1 = TaskLedger.Create("Test request", new List<TaskEntry> { task1 });
        var ledger2 = TaskLedger.Create("Test request", new List<TaskEntry> { task1 });

        // Act
        var updatedLedger1 = ledger1.WithTask(task2);
        var updatedLedger2 = ledger2.WithTask(task2);

        // Assert - Adding same task to same base should produce same hash
        await Assert.That(updatedLedger1.ContentHash).IsEqualTo(updatedLedger2.ContentHash);
    }

    /// <summary>
    /// Verifies that hash changes when task order differs.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_DifferentTaskOrder_ProducesDifferentHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var task2 = TaskEntry.CreateWithId("task-002", "Second task", priority: 2);

        var tasksOrder1 = new List<TaskEntry> { task1, task2 };
        var tasksOrder2 = new List<TaskEntry> { task2, task1 };

        // Act
        var ledger1 = TaskLedger.Create("Test request", tasksOrder1);
        var ledger2 = TaskLedger.Create("Test request", tasksOrder2);

        // Assert - Different order should produce different hash (order matters)
        await Assert.That(ledger1.ContentHash).IsNotEqualTo(ledger2.ContentHash);
    }

    /// <summary>
    /// Verifies that hash changes when original request differs.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_DifferentOriginalRequest_ProducesDifferentHash()
    {
        // Arrange
        var task1 = TaskEntry.CreateWithId("task-001", "First task", priority: 1);
        var tasks = new List<TaskEntry> { task1 };

        // Act
        var ledger1 = TaskLedger.Create("Request A", tasks);
        var ledger2 = TaskLedger.Create("Request B", tasks);

        // Assert
        await Assert.That(ledger1.ContentHash).IsNotEqualTo(ledger2.ContentHash);
    }

    /// <summary>
    /// Verifies that MemoryPack serialization handles special characters correctly.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_SpecialCharacters_ProducesValidHash()
    {
        // Arrange
        var taskWithSpecialChars = TaskEntry.CreateWithId(
            "task-001",
            "Task with special chars: \u00e9\u00e8\u00ea \u4e2d\u6587 \u0420\u0443\u0441\u0441\u043a\u0438\u0439 <>&\"'",
            priority: 1);
        var tasks = new List<TaskEntry> { taskWithSpecialChars };
        var originalRequest = "Request with unicode: \u2764 \ud83d\ude00 \u2728";

        // Act
        var ledger = TaskLedger.Create(originalRequest, tasks);

        // Assert
        await Assert.That(ledger.ContentHash).IsNotNull();
        await Assert.That(ledger.ContentHash.Length).IsEqualTo(64);
        await Assert.That(ledger.VerifyIntegrity()).IsTrue();
    }

    /// <summary>
    /// Verifies that hash is computed correctly for TaskLedgerHashContent with empty strings.
    /// </summary>
    [Test]
    public async Task ComputeContentHash_EmptyStrings_ProducesValidHash()
    {
        // Arrange
        var taskWithEmptyDesc = TaskEntry.CreateWithId("task-001", string.Empty, priority: 1);
        var tasks = new List<TaskEntry> { taskWithEmptyDesc };
        var emptyRequest = string.Empty;

        // Act
        var ledger = TaskLedger.Create(emptyRequest, tasks);

        // Assert
        await Assert.That(ledger.ContentHash).IsNotNull();
        await Assert.That(ledger.ContentHash.Length).IsEqualTo(64);
        await Assert.That(ledger.VerifyIntegrity()).IsTrue();
    }
}

