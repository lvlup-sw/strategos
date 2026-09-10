// =============================================================================
// <copyright file="LedgerTypesTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Orchestration;
using Strategos.Orchestration.Ledgers;

namespace Strategos.Tests.Orchestration.Ledgers;

/// <summary>
/// Unit tests for core ledger types: ProgressEntry, TaskEntry, and ExecutorSignal.
/// </summary>
[Property("Category", "Unit")]
public sealed class LedgerTypesTests
{
    #region ProgressEntry Tests

    /// <summary>
    /// Verifies that ProgressEntry.Create sets all properties correctly.
    /// </summary>
    [Test]
    public async Task ProgressEntry_Create_SetsAllPropertiesCorrectly()
    {
        // Arrange
        const string taskId = "task-123";
        const string executorId = "executor-456";
        const string action = "Analyze data";
        const bool progressMade = true;
        const string output = "Analysis complete";

        // Act
        var entry = ProgressEntry.Create(taskId, executorId, action, progressMade, output);

        // Assert
        await Assert.That(entry.TaskId).IsEqualTo(taskId);
        await Assert.That(entry.ExecutorId).IsEqualTo(executorId);
        await Assert.That(entry.Action).IsEqualTo(action);
        await Assert.That(entry.ProgressMade).IsTrue();
        await Assert.That(entry.Output).IsEqualTo(output);
        await Assert.That(entry.EntryId).StartsWith("progress-");
    }

    /// <summary>
    /// Verifies that ProgressEntry has sensible default values.
    /// </summary>
    [Test]
    public async Task ProgressEntry_Create_DefaultValuesAreSensible()
    {
        // Act
        var entry = ProgressEntry.Create("task-1", "exec-1", "action", false);

        // Assert
        await Assert.That(entry.Output).IsNull();
        await Assert.That(entry.Artifacts).IsNotNull();
        await Assert.That(entry.Artifacts).IsEmpty();
        await Assert.That(entry.Duration).IsNull();
        await Assert.That(entry.TokensConsumed).IsEqualTo(0);
        await Assert.That(entry.Signal).IsNull();
        await Assert.That(entry.ExecutorState).IsEqualTo(ExecutorState.Executing);
        await Assert.That(entry.Metadata).IsNull();
    }

    /// <summary>
    /// Verifies that EntryId is unique when generated.
    /// </summary>
    [Test]
    public async Task ProgressEntry_Create_EntryIdIsUniqueWhenGenerated()
    {
        // Act
        var entry1 = ProgressEntry.Create("task-1", "exec-1", "action-1", true);
        var entry2 = ProgressEntry.Create("task-1", "exec-1", "action-2", true);

        // Assert
        await Assert.That(entry1.EntryId).IsNotEqualTo(entry2.EntryId);
    }

    /// <summary>
    /// Verifies that Artifacts collection is not null.
    /// </summary>
    [Test]
    public async Task ProgressEntry_Create_ArtifactsCollectionIsNotNull()
    {
        // Act
        var entry = ProgressEntry.Create("task-1", "exec-1", "action", true);

        // Assert
        await Assert.That(entry.Artifacts).IsNotNull();
        await Assert.That(entry.Artifacts).IsAssignableTo<IReadOnlyList<string>>();
    }

    /// <summary>
    /// Verifies that Signal can be null or set.
    /// </summary>
    [Test]
    public async Task ProgressEntry_Create_SignalCanBeNullOrSet()
    {
        // Arrange
        var signal = new ExecutorSignal
        {
            ExecutorId = "exec-1",
            Type = SignalType.Success
        };

        // Act
        var entryWithoutSignal = ProgressEntry.Create("task-1", "exec-1", "action", true);
        var entryWithSignal = entryWithoutSignal with { Signal = signal };

        // Assert
        await Assert.That(entryWithoutSignal.Signal).IsNull();
        await Assert.That(entryWithSignal.Signal).IsNotNull();
        await Assert.That(entryWithSignal.Signal!.Type).IsEqualTo(SignalType.Success);
    }

    /// <summary>
    /// Verifies that FromSignal creates entry correctly from success signal.
    /// </summary>
    [Test]
    public async Task ProgressEntry_FromSignal_CreatesEntryCorrectlyFromSuccessSignal()
    {
        // Arrange
        var signal = new ExecutorSignal
        {
            ExecutorId = "exec-1",
            Type = SignalType.Success,
            SuccessData = new ExecutorSuccessData
            {
                Result = "Task completed successfully",
                Artifacts = new List<string> { "artifact1.txt", "artifact2.json" }
            }
        };

        // Act
        var entry = ProgressEntry.FromSignal("task-1", signal, "Signaling completion");

        // Assert
        await Assert.That(entry.TaskId).IsEqualTo("task-1");
        await Assert.That(entry.ExecutorId).IsEqualTo("exec-1");
        await Assert.That(entry.Action).IsEqualTo("Signaling completion");
        await Assert.That(entry.ProgressMade).IsTrue();
        await Assert.That(entry.Signal).IsEqualTo(signal);
        await Assert.That(entry.ExecutorState).IsEqualTo(ExecutorState.Signaling);
        await Assert.That(entry.Output).IsEqualTo("Task completed successfully");
        await Assert.That(entry.Artifacts).HasCount(2);
    }

    /// <summary>
    /// Verifies that FromSignal creates entry correctly from failure signal.
    /// </summary>
    [Test]
    public async Task ProgressEntry_FromSignal_CreatesEntryCorrectlyFromFailureSignal()
    {
        // Arrange
        var signal = new ExecutorSignal
        {
            ExecutorId = "exec-1",
            Type = SignalType.Failure,
            FailureData = new ExecutorFailureData
            {
                Reason = "Resource unavailable",
                IsRecoverable = true
            }
        };

        // Act
        var entry = ProgressEntry.FromSignal("task-1", signal, "Signaling failure");

        // Assert
        await Assert.That(entry.ProgressMade).IsFalse();
        await Assert.That(entry.Output).IsEqualTo("Resource unavailable");
        await Assert.That(entry.Artifacts).IsEmpty();
    }

    /// <summary>
    /// Verifies that Create throws ArgumentNullException for null taskId.
    /// </summary>
    [Test]
    public async Task ProgressEntry_Create_ThrowsArgumentNullExceptionForNullTaskId()
    {
        // Act & Assert
        await Assert.That(() => ProgressEntry.Create(null!, "exec-1", "action", true))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region TaskEntry Tests

    /// <summary>
    /// Verifies that CreateWithId creates task with correct id.
    /// </summary>
    [Test]
    public async Task TaskEntry_CreateWithId_CreatesWithCorrectId()
    {
        // Arrange
        const string taskId = "custom-task-id-123";
        const string description = "Test task description";

        // Act
        var task = TaskEntry.CreateWithId(taskId, description);

        // Assert
        await Assert.That(task.TaskId).IsEqualTo(taskId);
        await Assert.That(task.Description).IsEqualTo(description);
    }

    /// <summary>
    /// Verifies that Create generates unique id.
    /// </summary>
    [Test]
    public async Task TaskEntry_Create_GeneratesUniqueId()
    {
        // Act
        var task1 = TaskEntry.Create("Description 1");
        var task2 = TaskEntry.Create("Description 2");

        // Assert
        await Assert.That(task1.TaskId).StartsWith("task-");
        await Assert.That(task2.TaskId).StartsWith("task-");
        await Assert.That(task1.TaskId).IsNotEqualTo(task2.TaskId);
    }

    /// <summary>
    /// Verifies that Priority defaults correctly.
    /// </summary>
    [Test]
    public async Task TaskEntry_Create_PriorityDefaultsCorrectly()
    {
        // Act
        var taskWithDefaultPriority = TaskEntry.Create("Test description");
        var taskWithCustomPriority = TaskEntry.Create("Test description", priority: 10);

        // Assert
        await Assert.That(taskWithDefaultPriority.Priority).IsEqualTo(0);
        await Assert.That(taskWithCustomPriority.Priority).IsEqualTo(10);
    }

    /// <summary>
    /// Verifies that Status can be updated.
    /// </summary>
    [Test]
    public async Task TaskEntry_WithStatus_UpdatesStatus()
    {
        // Arrange
        var task = TaskEntry.Create("Test description");

        // Act
        var updatedTask = task.WithStatus(WorkflowTaskStatus.InProgress);
        var completedTask = updatedTask.WithStatus(WorkflowTaskStatus.Completed);

        // Assert
        await Assert.That(task.Status).IsEqualTo(WorkflowTaskStatus.Pending);
        await Assert.That(updatedTask.Status).IsEqualTo(WorkflowTaskStatus.InProgress);
        await Assert.That(completedTask.Status).IsEqualTo(WorkflowTaskStatus.Completed);
    }

    /// <summary>
    /// Verifies that Description is stored correctly.
    /// </summary>
    [Test]
    public async Task TaskEntry_Create_DescriptionIsStoredCorrectly()
    {
        // Arrange
        const string description = "Perform complex data analysis on user input";

        // Act
        var task = TaskEntry.Create(description);

        // Assert
        await Assert.That(task.Description).IsEqualTo(description);
    }

    /// <summary>
    /// Verifies that WithResult sets result correctly.
    /// </summary>
    [Test]
    public async Task TaskEntry_WithResult_SetsResultCorrectly()
    {
        // Arrange
        var task = TaskEntry.Create("Test description");
        const string result = "Task execution result data";

        // Act
        var taskWithResult = task.WithResult(result);

        // Assert
        await Assert.That(task.Result).IsNull();
        await Assert.That(taskWithResult.Result).IsEqualTo(result);
    }

    /// <summary>
    /// Verifies that IsReadyToExecute returns true when all dependencies are satisfied.
    /// </summary>
    [Test]
    public async Task TaskEntry_IsReadyToExecute_ReturnsTrueWhenDependenciesSatisfied()
    {
        // Arrange
        var task = TaskEntry.Create(
            "Dependent task",
            dependencies: new[] { "dep-1", "dep-2" });
        var completedTasks = new HashSet<string> { "dep-1", "dep-2" };

        // Act
        var isReady = task.IsReadyToExecute(completedTasks);

        // Assert
        await Assert.That(isReady).IsTrue();
    }

    /// <summary>
    /// Verifies that IsReadyToExecute returns false when dependencies are not satisfied.
    /// </summary>
    [Test]
    public async Task TaskEntry_IsReadyToExecute_ReturnsFalseWhenDependenciesNotSatisfied()
    {
        // Arrange
        var task = TaskEntry.Create(
            "Dependent task",
            dependencies: new[] { "dep-1", "dep-2" });
        var completedTasks = new HashSet<string> { "dep-1" };

        // Act
        var isReady = task.IsReadyToExecute(completedTasks);

        // Assert
        await Assert.That(isReady).IsFalse();
    }

    /// <summary>
    /// Verifies that Create throws ArgumentNullException for null description.
    /// </summary>
    [Test]
    public async Task TaskEntry_Create_ThrowsArgumentNullExceptionForNullDescription()
    {
        // Act & Assert
        await Assert.That(() => TaskEntry.Create(null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region ExecutorSignal Tests

    /// <summary>
    /// Verifies that SignalType values are correct.
    /// </summary>
    [Test]
    public async Task ExecutorSignal_SignalTypes_AreCorrect()
    {
        // Assert
        await Assert.That(SignalType.Success).IsTypeOf<SignalType>();
        await Assert.That(SignalType.Failure).IsTypeOf<SignalType>();
        await Assert.That(SignalType.HelpNeeded).IsTypeOf<SignalType>();
        await Assert.That(SignalType.Blocked).IsTypeOf<SignalType>();
        await Assert.That(SignalType.InProgress).IsTypeOf<SignalType>();
    }

    /// <summary>
    /// Verifies that ExecutorSignal creation works.
    /// </summary>
    [Test]
    public async Task ExecutorSignal_Creation_Works()
    {
        // Act
        var signal = new ExecutorSignal
        {
            ExecutorId = "test-executor",
            Type = SignalType.Success
        };

        // Assert
        await Assert.That(signal.ExecutorId).IsEqualTo("test-executor");
        await Assert.That(signal.Type).IsEqualTo(SignalType.Success);
        await Assert.That(signal.SuccessData).IsNull();
        await Assert.That(signal.FailureData).IsNull();
        await Assert.That(signal.Metadata).IsNull();
    }

    /// <summary>
    /// Verifies that ExecutorSuccessData holds values correctly.
    /// </summary>
    [Test]
    public async Task ExecutorSuccessData_HoldsValuesCorrectly()
    {
        // Act
        var successData = new ExecutorSuccessData
        {
            Result = "Completed successfully",
            Confidence = 0.95,
            Artifacts = new List<string> { "file1.txt", "file2.json" }
        };

        // Assert
        await Assert.That(successData.Result).IsEqualTo("Completed successfully");
        await Assert.That(successData.Confidence).IsEqualTo(0.95);
        await Assert.That(successData.Artifacts).HasCount(2);
        await Assert.That(successData.Artifacts).Contains("file1.txt");
    }

    /// <summary>
    /// Verifies that ExecutorFailureData holds values correctly.
    /// </summary>
    [Test]
    public async Task ExecutorFailureData_HoldsValuesCorrectly()
    {
        // Act
        var failureData = new ExecutorFailureData
        {
            Reason = "Network timeout",
            ErrorCode = "ERR_TIMEOUT",
            IsRecoverable = true
        };

        // Assert
        await Assert.That(failureData.Reason).IsEqualTo("Network timeout");
        await Assert.That(failureData.ErrorCode).IsEqualTo("ERR_TIMEOUT");
        await Assert.That(failureData.IsRecoverable).IsTrue();
    }

    #endregion

    #region Record Equality and Immutability Tests

    /// <summary>
    /// Verifies that TaskEntry records with same data are equal.
    /// </summary>
    [Test]
    public async Task TaskEntry_RecordsWithSameData_AreEqual()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var task1 = new TaskEntry
        {
            TaskId = "task-1",
            Description = "Description",
            Priority = 5,
            CreatedAt = timestamp
        };
        var task2 = new TaskEntry
        {
            TaskId = "task-1",
            Description = "Description",
            Priority = 5,
            CreatedAt = timestamp
        };

        // Assert
        await Assert.That(task1).IsEqualTo(task2);
        await Assert.That(task1.GetHashCode()).IsEqualTo(task2.GetHashCode());
    }

    /// <summary>
    /// Verifies that ProgressEntry records with same data are equal.
    /// </summary>
    [Test]
    public async Task ProgressEntry_RecordsWithSameData_AreEqual()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var entry1 = new ProgressEntry
        {
            EntryId = "entry-1",
            TaskId = "task-1",
            ExecutorId = "exec-1",
            Action = "action",
            ProgressMade = true,
            Timestamp = timestamp
        };
        var entry2 = new ProgressEntry
        {
            EntryId = "entry-1",
            TaskId = "task-1",
            ExecutorId = "exec-1",
            Action = "action",
            ProgressMade = true,
            Timestamp = timestamp
        };

        // Assert
        await Assert.That(entry1).IsEqualTo(entry2);
        await Assert.That(entry1.GetHashCode()).IsEqualTo(entry2.GetHashCode());
    }

    /// <summary>
    /// Verifies that ExecutorSignal records with same data are equal.
    /// </summary>
    [Test]
    public async Task ExecutorSignal_RecordsWithSameData_AreEqual()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var signal1 = new ExecutorSignal
        {
            ExecutorId = "exec-1",
            Type = SignalType.Success,
            Timestamp = timestamp
        };
        var signal2 = new ExecutorSignal
        {
            ExecutorId = "exec-1",
            Type = SignalType.Success,
            Timestamp = timestamp
        };

        // Assert
        await Assert.That(signal1).IsEqualTo(signal2);
        await Assert.That(signal1.GetHashCode()).IsEqualTo(signal2.GetHashCode());
    }

    /// <summary>
    /// Verifies that TaskEntry is immutable via with expressions.
    /// </summary>
    [Test]
    public async Task TaskEntry_IsImmutable_ViaWithExpressions()
    {
        // Arrange
        var originalTask = TaskEntry.Create("Original description");

        // Act
        var modifiedTask = originalTask with { Description = "Modified description" };

        // Assert
        await Assert.That(originalTask.Description).IsEqualTo("Original description");
        await Assert.That(modifiedTask.Description).IsEqualTo("Modified description");
        await Assert.That(originalTask).IsNotEqualTo(modifiedTask);
    }

    #endregion
}
