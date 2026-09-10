// =============================================================================
// <copyright file="TaskLedgerAllocationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Infrastructure.Ledgers;
using Strategos.Orchestration.Ledgers;

namespace Strategos.Infrastructure.Tests.Ledgers;

/// <summary>
/// Unit tests for <see cref="TaskLedger"/> verifying task management
/// and pre-allocation optimizations.
/// </summary>
[Property("Category", "Unit")]
public sealed class TaskLedgerAllocationTests
{
    // =============================================================================
    // A. WithTask Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithTask correctly appends a task and preserves all existing tasks.
    /// </summary>
    [Test]
    public async Task WithTask_ExistingTasks_PreservesAllTasksAndAddsNew()
    {
        // Arrange
        var initialTasks = new List<TaskEntry>
        {
            TaskEntry.Create("Task 1"),
            TaskEntry.Create("Task 2")
        };
        var ledger = TaskLedger.Create("Original request", initialTasks);
        var newTask = TaskEntry.Create("Task 3");

        // Act
        var updated = (TaskLedger)ledger.WithTask(newTask);

        // Assert
        await Assert.That(updated.Tasks.Count).IsEqualTo(3);
        await Assert.That(updated.Tasks[0].Description).IsEqualTo("Task 1");
        await Assert.That(updated.Tasks[1].Description).IsEqualTo("Task 2");
        await Assert.That(updated.Tasks[2].Description).IsEqualTo("Task 3");
    }

    /// <summary>
    /// Verifies that WithTask correctly handles large task counts.
    /// </summary>
    /// <remarks>
    /// This test validates correctness at scale with large task counts.
    /// Pre-allocation optimizations (capacity set to Tasks.Count + 1) are
    /// verified implicitly through correct behavior; allocation impact is
    /// measured via benchmarks, not unit tests.
    /// </remarks>
    [Test]
    public async Task WithTask_LargeTaskCount_PreservesAllTasks()
    {
        // Arrange - Create ledger with many tasks
        var initialTasks = new List<TaskEntry>();
        const int initialCount = 100;

        for (var i = 0; i < initialCount; i++)
        {
            initialTasks.Add(TaskEntry.Create($"Task {i}"));
        }

        var ledger = TaskLedger.Create("Original request", initialTasks);

        // Act - Add one more task
        var finalTask = TaskEntry.Create("Final Task");
        var updatedLedger = (TaskLedger)ledger.WithTask(finalTask);

        // Assert - All tasks should be present and in order
        await Assert.That(updatedLedger.Tasks.Count).IsEqualTo(initialCount + 1);
        await Assert.That(updatedLedger.Tasks[0].Description).IsEqualTo("Task 0");
        await Assert.That(updatedLedger.Tasks[initialCount - 1].Description).IsEqualTo($"Task {initialCount - 1}");
        await Assert.That(updatedLedger.Tasks[initialCount].Description).IsEqualTo("Final Task");
    }

    /// <summary>
    /// Verifies that WithTask creates an immutable copy and does not modify the original ledger.
    /// </summary>
    [Test]
    public async Task WithTask_CreatesImmutableCopy_OriginalUnchanged()
    {
        // Arrange
        var initialTasks = new List<TaskEntry>
        {
            TaskEntry.Create("Task 1")
        };
        var ledger = TaskLedger.Create("Original request", initialTasks);

        // Act
        var newTask = TaskEntry.Create("Task 2");
        var updatedLedger = (TaskLedger)ledger.WithTask(newTask);

        // Assert - Original ledger should be unchanged
        await Assert.That(ledger.Tasks.Count).IsEqualTo(1);
        await Assert.That(updatedLedger.Tasks.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that WithTask updates the content hash correctly.
    /// </summary>
    [Test]
    public async Task WithTask_UpdatesContentHash()
    {
        // Arrange
        var initialTasks = new List<TaskEntry>
        {
            TaskEntry.Create("Task 1")
        };
        var ledger = TaskLedger.Create("Original request", initialTasks);
        var originalHash = ledger.ContentHash;

        // Act
        var newTask = TaskEntry.Create("Task 2");
        var updatedLedger = (TaskLedger)ledger.WithTask(newTask);

        // Assert - Hash should be different after adding a task
        await Assert.That(updatedLedger.ContentHash).IsNotEqualTo(originalHash);
        await Assert.That(updatedLedger.VerifyIntegrity()).IsTrue();
    }

    // =============================================================================
    // B. WithUpdatedTask Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithUpdatedTask correctly updates a task by ID.
    /// </summary>
    [Test]
    public async Task WithUpdatedTask_ExistingTask_UpdatesCorrectTask()
    {
        // Arrange
        var task1 = TaskEntry.Create("Task 1");
        var task2 = TaskEntry.Create("Task 2");
        var initialTasks = new List<TaskEntry> { task1, task2 };
        var ledger = TaskLedger.Create("Original request", initialTasks);
        var updatedTask = task1 with { Status = WorkflowTaskStatus.Completed, Result = "Done" };

        // Act
        var updated = (TaskLedger)ledger.WithUpdatedTask(task1.TaskId, updatedTask);

        // Assert
        await Assert.That(updated.Tasks.Count).IsEqualTo(2);
        await Assert.That(updated.Tasks[0].Status).IsEqualTo(WorkflowTaskStatus.Completed);
        await Assert.That(updated.Tasks[0].Result).IsEqualTo("Done");
        await Assert.That(updated.Tasks[1].Description).IsEqualTo("Task 2");
    }

    /// <summary>
    /// Verifies that WithUpdatedTask throws KeyNotFoundException when task ID is not found.
    /// </summary>
    [Test]
    public async Task WithUpdatedTask_NonExistentTask_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ledger = TaskLedger.Create("Original request", new List<TaskEntry>
        {
            TaskEntry.Create("Task 1")
        });
        var fakeTask = TaskEntry.Create("Fake");

        // Act & Assert
        await Assert.That(() => ledger.WithUpdatedTask("non-existent-id", fakeTask))
            .Throws<KeyNotFoundException>();
    }

    /// <summary>
    /// Verifies that WithUpdatedTask preserves other tasks when updating one.
    /// </summary>
    [Test]
    public async Task WithUpdatedTask_PreservesOtherTasks()
    {
        // Arrange
        var task1 = TaskEntry.Create("Task 1");
        var task2 = TaskEntry.Create("Task 2");
        var task3 = TaskEntry.Create("Task 3");
        var initialTasks = new List<TaskEntry> { task1, task2, task3 };
        var ledger = TaskLedger.Create("Original request", initialTasks);
        var updatedTask2 = task2 with { Status = WorkflowTaskStatus.InProgress };

        // Act
        var updated = (TaskLedger)ledger.WithUpdatedTask(task2.TaskId, updatedTask2);

        // Assert
        await Assert.That(updated.Tasks.Count).IsEqualTo(3);
        await Assert.That(updated.Tasks[0].Description).IsEqualTo("Task 1");
        await Assert.That(updated.Tasks[0].Status).IsEqualTo(WorkflowTaskStatus.Pending);
        await Assert.That(updated.Tasks[1].Status).IsEqualTo(WorkflowTaskStatus.InProgress);
        await Assert.That(updated.Tasks[2].Description).IsEqualTo("Task 3");
        await Assert.That(updated.Tasks[2].Status).IsEqualTo(WorkflowTaskStatus.Pending);
    }

    /// <summary>
    /// Verifies that WithUpdatedTask updates the content hash correctly.
    /// </summary>
    [Test]
    public async Task WithUpdatedTask_UpdatesContentHash()
    {
        // Arrange
        var task1 = TaskEntry.Create("Task 1");
        var initialTasks = new List<TaskEntry> { task1 };
        var ledger = TaskLedger.Create("Original request", initialTasks);
        var originalHash = ledger.ContentHash;

        // Note: Hash is computed from TaskId and Description, not Status
        // So changing status should NOT change the hash
        var updatedTask = task1 with { Status = WorkflowTaskStatus.Completed };

        // Act
        var updated = (TaskLedger)ledger.WithUpdatedTask(task1.TaskId, updatedTask);

        // Assert - Hash should be the same since only status changed (not id or description)
        await Assert.That(updated.ContentHash).IsEqualTo(originalHash);
        await Assert.That(updated.VerifyIntegrity()).IsTrue();
    }

    /// <summary>
    /// Verifies that WithUpdatedTask handles large task counts correctly.
    /// </summary>
    [Test]
    public async Task WithUpdatedTask_LargeTaskCount_UpdatesCorrectTask()
    {
        // Arrange - Create ledger with many tasks
        var tasks = new List<TaskEntry>();
        const int taskCount = 100;

        for (var i = 0; i < taskCount; i++)
        {
            tasks.Add(TaskEntry.Create($"Task {i}"));
        }

        var ledger = TaskLedger.Create("Original request", tasks);
        var middleTask = tasks[50];
        var updatedTask = middleTask with { Status = WorkflowTaskStatus.Completed, Result = "Middle done" };

        // Act
        var updated = (TaskLedger)ledger.WithUpdatedTask(middleTask.TaskId, updatedTask);

        // Assert
        await Assert.That(updated.Tasks.Count).IsEqualTo(taskCount);
        await Assert.That(updated.Tasks[50].Status).IsEqualTo(WorkflowTaskStatus.Completed);
        await Assert.That(updated.Tasks[50].Result).IsEqualTo("Middle done");
        await Assert.That(updated.Tasks[0].Status).IsEqualTo(WorkflowTaskStatus.Pending);
        await Assert.That(updated.Tasks[99].Status).IsEqualTo(WorkflowTaskStatus.Pending);
    }

    /// <summary>
    /// Verifies that WithUpdatedTask creates an immutable copy and does not modify the original ledger.
    /// </summary>
    [Test]
    public async Task WithUpdatedTask_CreatesImmutableCopy_OriginalUnchanged()
    {
        // Arrange
        var task1 = TaskEntry.Create("Task 1");
        var initialTasks = new List<TaskEntry> { task1 };
        var ledger = TaskLedger.Create("Original request", initialTasks);

        // Act
        var updatedTask = task1 with { Status = WorkflowTaskStatus.Completed };
        var updatedLedger = (TaskLedger)ledger.WithUpdatedTask(task1.TaskId, updatedTask);

        // Assert - Original ledger should be unchanged
        await Assert.That(ledger.Tasks[0].Status).IsEqualTo(WorkflowTaskStatus.Pending);
        await Assert.That(updatedLedger.Tasks[0].Status).IsEqualTo(WorkflowTaskStatus.Completed);
    }
}
