// =============================================================================
// <copyright file="ProgressLedgerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Infrastructure.Tests.Ledgers;

/// <summary>
/// Unit tests for <see cref="ProgressLedger"/> verifying entry appending
/// and pre-allocation optimizations.
/// </summary>
[Property("Category", "Unit")]
public sealed class ProgressLedgerTests
{
    // =============================================================================
    // A. WithEntry Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithEntry correctly appends an entry and preserves all existing entries.
    /// </summary>
    [Test]
    public async Task WithEntry_AppendsSingleEntry_PreservesAllEntries()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var entry1 = CreateTestEntry("task-1", "executor-1", "action-1");
        var entry2 = CreateTestEntry("task-1", "executor-1", "action-2");

        // Act
        var ledgerAfterFirst = (ProgressLedger)ledger.WithEntry(entry1);
        var ledgerAfterSecond = (ProgressLedger)ledgerAfterFirst.WithEntry(entry2);

        // Assert
        await Assert.That(ledgerAfterSecond.Entries.Count).IsEqualTo(2);
        await Assert.That(ledgerAfterSecond.Entries[0].Action).IsEqualTo("action-1");
        await Assert.That(ledgerAfterSecond.Entries[1].Action).IsEqualTo("action-2");
    }

    /// <summary>
    /// Verifies that WithEntry correctly handles large entry counts.
    /// </summary>
    /// <remarks>
    /// This test validates correctness at scale with large entry counts.
    /// Pre-allocation optimizations (capacity set to Entries.Count + 1) are
    /// verified implicitly through correct behavior; allocation impact is
    /// measured via benchmarks, not unit tests.
    /// </remarks>
    [Test]
    public async Task WithEntry_LargeEntryCount_PreservesAllEntries()
    {
        // Arrange - Create ledger with many entries
        var ledger = ProgressLedger.Create("task-ledger-1");
        const int initialCount = 1000;

        // Add many entries to build up the ledger
        var currentLedger = ledger;
        for (var i = 0; i < initialCount; i++)
        {
            var entry = CreateTestEntry("task-1", "executor-1", $"action-{i}");
            currentLedger = (ProgressLedger)currentLedger.WithEntry(entry);
        }

        // Act - Add one more entry
        var finalEntry = CreateTestEntry("task-1", "executor-1", "final-action");
        var finalLedger = (ProgressLedger)currentLedger.WithEntry(finalEntry);

        // Assert - All entries should be present and in order
        await Assert.That(finalLedger.Entries.Count).IsEqualTo(initialCount + 1);
        await Assert.That(finalLedger.Entries[0].Action).IsEqualTo("action-0");
        await Assert.That(finalLedger.Entries[initialCount - 1].Action).IsEqualTo($"action-{initialCount - 1}");
        await Assert.That(finalLedger.Entries[initialCount].Action).IsEqualTo("final-action");
    }

    /// <summary>
    /// Verifies that WithEntry creates an immutable copy and does not modify the original ledger.
    /// </summary>
    [Test]
    public async Task WithEntry_CreatesImmutableCopy_OriginalUnchanged()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var entry1 = CreateTestEntry("task-1", "executor-1", "action-1");
        var ledgerWithOne = (ProgressLedger)ledger.WithEntry(entry1);

        // Act
        var entry2 = CreateTestEntry("task-1", "executor-1", "action-2");
        var ledgerWithTwo = (ProgressLedger)ledgerWithOne.WithEntry(entry2);

        // Assert - Original ledgers should be unchanged
        await Assert.That(ledger.Entries.Count).IsEqualTo(0);
        await Assert.That(ledgerWithOne.Entries.Count).IsEqualTo(1);
        await Assert.That(ledgerWithTwo.Entries.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that WithEntry throws ArgumentNullException when entry is null.
    /// </summary>
    [Test]
    public async Task WithEntry_WithNullEntry_ThrowsArgumentNullException()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act & Assert
        await Assert.That(() => ledger.WithEntry(null!))
            .Throws<ArgumentNullException>()
            .WithParameterName("entry");
    }

    // =============================================================================
    // B. WithEntries Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithEntries correctly appends multiple entries.
    /// </summary>
    [Test]
    public async Task WithEntries_AppendsMultipleEntries_PreservesOrder()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var entries = new[]
        {
            CreateTestEntry("task-1", "executor-1", "action-1"),
            CreateTestEntry("task-1", "executor-1", "action-2"),
            CreateTestEntry("task-1", "executor-1", "action-3"),
        };

        // Act
        var updatedLedger = (ProgressLedger)ledger.WithEntries(entries);

        // Assert
        await Assert.That(updatedLedger.Entries.Count).IsEqualTo(3);
        await Assert.That(updatedLedger.Entries[0].Action).IsEqualTo("action-1");
        await Assert.That(updatedLedger.Entries[1].Action).IsEqualTo("action-2");
        await Assert.That(updatedLedger.Entries[2].Action).IsEqualTo("action-3");
    }

    // =============================================================================
    // C. GetRecentEntries Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetRecentEntries returns the last N entries.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WithWindowSize_ReturnsLastNEntries()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var currentLedger = ledger;
        for (var i = 0; i < 10; i++)
        {
            var entry = CreateTestEntry("task-1", "executor-1", $"action-{i}");
            currentLedger = (ProgressLedger)currentLedger.WithEntry(entry);
        }

        // Act
        var recentEntries = currentLedger.GetRecentEntries(3);

        // Assert
        await Assert.That(recentEntries.Count).IsEqualTo(3);
        await Assert.That(recentEntries[0].Action).IsEqualTo("action-7");
        await Assert.That(recentEntries[1].Action).IsEqualTo("action-8");
        await Assert.That(recentEntries[2].Action).IsEqualTo("action-9");
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns the same reference when window is greater than count.
    /// This tests the Phase 2 optimization that avoids allocation when window covers all entries.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowGreaterThanCount_ReturnsSameReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var currentLedger = ledger;
        for (var i = 0; i < 5; i++)
        {
            var entry = CreateTestEntry("task-1", "executor-1", $"action-{i}");
            currentLedger = (ProgressLedger)currentLedger.WithEntry(entry);
        }

        // Act
        var recentEntries = currentLedger.GetRecentEntries(10);

        // Assert - Should return the same reference (no allocation)
        await Assert.That(ReferenceEquals(recentEntries, currentLedger.Entries)).IsTrue();
        await Assert.That(recentEntries.Count).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns the same reference when window equals count.
    /// This tests the boundary condition of the Phase 2 optimization.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowEqualsCount_ReturnsSameReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var currentLedger = ledger;
        for (var i = 0; i < 5; i++)
        {
            var entry = CreateTestEntry("task-1", "executor-1", $"action-{i}");
            currentLedger = (ProgressLedger)currentLedger.WithEntry(entry);
        }

        // Act
        var recentEntries = currentLedger.GetRecentEntries(5);

        // Assert - Should return the same reference when window == count
        await Assert.That(ReferenceEquals(recentEntries, currentLedger.Entries)).IsTrue();
        await Assert.That(recentEntries.Count).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns a new list when window is less than count.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowLessThanCount_ReturnsNewList()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var currentLedger = ledger;
        for (var i = 0; i < 10; i++)
        {
            var entry = CreateTestEntry("task-1", "executor-1", $"action-{i}");
            currentLedger = (ProgressLedger)currentLedger.WithEntry(entry);
        }

        // Act
        var recentEntries = currentLedger.GetRecentEntries(5);

        // Assert - Should return a new list (not the same reference)
        await Assert.That(ReferenceEquals(recentEntries, currentLedger.Entries)).IsFalse();
        await Assert.That(recentEntries.Count).IsEqualTo(5);
        await Assert.That(recentEntries[0].Action).IsEqualTo("action-5");
        await Assert.That(recentEntries[4].Action).IsEqualTo("action-9");
    }

    /// <summary>
    /// Verifies that GetRecentEntries with window of 1 returns single entry.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowOfOne_ReturnsSingleEntry()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var currentLedger = ledger;
        for (var i = 0; i < 10; i++)
        {
            var entry = CreateTestEntry("task-1", "executor-1", $"action-{i}");
            currentLedger = (ProgressLedger)currentLedger.WithEntry(entry);
        }

        // Act
        var recentEntries = currentLedger.GetRecentEntries(1);

        // Assert
        await Assert.That(recentEntries.Count).IsEqualTo(1);
        await Assert.That(recentEntries[0].Action).IsEqualTo("action-9");
    }

    /// <summary>
    /// Verifies that GetRecentEntries handles very large window sizes gracefully.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_VeryLargeWindowSize_ReturnsAllEntriesWithSameReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var currentLedger = ledger;
        for (var i = 0; i < 5; i++)
        {
            var entry = CreateTestEntry("task-1", "executor-1", $"action-{i}");
            currentLedger = (ProgressLedger)currentLedger.WithEntry(entry);
        }

        // Act - Use a very large window size
        var recentEntries = currentLedger.GetRecentEntries(int.MaxValue / 2);

        // Assert - Should return all entries with same reference
        await Assert.That(ReferenceEquals(recentEntries, currentLedger.Entries)).IsTrue();
        await Assert.That(recentEntries.Count).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that GetRecentEntries throws for zero window size.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_ZeroWindowSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act & Assert
        await Assert.That(() => ledger.GetRecentEntries(0))
            .Throws<ArgumentOutOfRangeException>()
            .WithParameterName("windowSize");
    }

    /// <summary>
    /// Verifies that GetRecentEntries throws for negative window size.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_NegativeWindowSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act & Assert
        await Assert.That(() => ledger.GetRecentEntries(-1))
            .Throws<ArgumentOutOfRangeException>()
            .WithParameterName("windowSize");
    }

    // =============================================================================
    // D. WithEntry Additional Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithEntry adds entry to the end of the list.
    /// </summary>
    [Test]
    public async Task WithEntry_AddsToEndOfList_MaintainsOrder()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var entry1 = CreateTestEntry("task-1", "executor-1", "first");
        var entry2 = CreateTestEntry("task-1", "executor-1", "second");
        var entry3 = CreateTestEntry("task-1", "executor-1", "third");

        // Act
        var ledgerAfterFirst = (ProgressLedger)ledger.WithEntry(entry1);
        var ledgerAfterSecond = (ProgressLedger)ledgerAfterFirst.WithEntry(entry2);
        var ledgerAfterThird = (ProgressLedger)ledgerAfterSecond.WithEntry(entry3);

        // Assert - Entries should be in order of addition
        await Assert.That(ledgerAfterThird.Entries[0].Action).IsEqualTo("first");
        await Assert.That(ledgerAfterThird.Entries[1].Action).IsEqualTo("second");
        await Assert.That(ledgerAfterThird.Entries[2].Action).IsEqualTo("third");
    }

    // =============================================================================
    // E. WithEntries Additional Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithEntries adds multiple entries and preserves original immutability.
    /// </summary>
    [Test]
    public async Task WithEntries_PreservesImmutability_OriginalUnchanged()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var initialEntry = CreateTestEntry("task-1", "executor-1", "initial");
        var ledgerWithOne = (ProgressLedger)ledger.WithEntry(initialEntry);

        var newEntries = new[]
        {
            CreateTestEntry("task-1", "executor-1", "batch-1"),
            CreateTestEntry("task-1", "executor-1", "batch-2"),
        };

        // Act
        var ledgerWithBatch = (ProgressLedger)ledgerWithOne.WithEntries(newEntries);

        // Assert - Original ledger should be unchanged
        await Assert.That(ledgerWithOne.Entries.Count).IsEqualTo(1);
        await Assert.That(ledgerWithBatch.Entries.Count).IsEqualTo(3);
        await Assert.That(ledgerWithBatch.Entries[0].Action).IsEqualTo("initial");
        await Assert.That(ledgerWithBatch.Entries[1].Action).IsEqualTo("batch-1");
        await Assert.That(ledgerWithBatch.Entries[2].Action).IsEqualTo("batch-2");
    }

    /// <summary>
    /// Verifies that WithEntries throws ArgumentNullException when entries is null.
    /// </summary>
    [Test]
    public async Task WithEntries_WithNullEntries_ThrowsArgumentNullException()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act & Assert
        await Assert.That(() => ledger.WithEntries(null!))
            .Throws<ArgumentNullException>()
            .WithParameterName("entries");
    }

    // =============================================================================
    // F. GetMetrics Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetMetrics returns accurate counts for a populated ledger.
    /// </summary>
    [Test]
    public async Task GetMetrics_PopulatedLedger_ReturnsAccurateCounts()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        var successSignal = new ExecutorSignal
        {
            ExecutorId = "executor-1",
            Type = SignalType.Success
        };
        var failureSignal = new ExecutorSignal
        {
            ExecutorId = "executor-1",
            Type = SignalType.Failure
        };

        var entries = new[]
        {
            CreateTestEntryWithDetails("task-1", "executor-1", "action-1", tokensConsumed: 100, duration: TimeSpan.FromSeconds(1), artifacts: ["artifact-1"]),
            CreateTestEntryWithDetails("task-1", "executor-1", "action-2", tokensConsumed: 200, duration: TimeSpan.FromSeconds(2), artifacts: ["artifact-2"]),
            CreateTestEntryWithSignal("task-1", successSignal, tokensConsumed: 50),
            CreateTestEntryWithSignal("task-1", failureSignal, tokensConsumed: 25),
            CreateTestEntryWithDetails("task-1", "executor-1", "action-3", tokensConsumed: 75, duration: null, artifacts: ["artifact-1"]), // Duplicate artifact
        };

        var currentLedger = (ProgressLedger)ledger.WithEntries(entries);

        // Act
        var metrics = currentLedger.GetMetrics();

        // Assert
        await Assert.That(metrics.TotalEntries).IsEqualTo(5);
        await Assert.That(metrics.TotalTokensConsumed).IsEqualTo(450); // 100 + 200 + 50 + 25 + 75
        await Assert.That(metrics.TotalDuration).IsEqualTo(TimeSpan.FromSeconds(3)); // 1 + 2 (null excluded)
        await Assert.That(metrics.UniqueArtifactCount).IsEqualTo(2); // artifact-1, artifact-2 (duplicates removed)
        await Assert.That(metrics.SuccessfulSignalCount).IsEqualTo(1);
        await Assert.That(metrics.FailedSignalCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that GetMetrics returns zeroes for an empty ledger.
    /// </summary>
    [Test]
    public async Task GetMetrics_EmptyLedger_ReturnsZeroes()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act
        var metrics = ledger.GetMetrics();

        // Assert
        await Assert.That(metrics.TotalEntries).IsEqualTo(0);
        await Assert.That(metrics.TotalTokensConsumed).IsEqualTo(0);
        await Assert.That(metrics.TotalDuration).IsEqualTo(TimeSpan.Zero);
        await Assert.That(metrics.UniqueArtifactCount).IsEqualTo(0);
        await Assert.That(metrics.SuccessfulSignalCount).IsEqualTo(0);
        await Assert.That(metrics.FailedSignalCount).IsEqualTo(0);
    }

    // =============================================================================
    // G. Edge Cases and Large Scale Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the ledger handles a very large number of entries (10K+).
    /// </summary>
    [Test]
    public async Task LargeLedger_TenThousandEntries_OperationsSucceed()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        const int entryCount = 10_000;

        // Build entries in batches for efficiency
        var entries = new List<ProgressEntry>(entryCount);
        for (var i = 0; i < entryCount; i++)
        {
            entries.Add(CreateTestEntry("task-1", "executor-1", $"action-{i}"));
        }

        // Act
        var currentLedger = (ProgressLedger)ledger.WithEntries(entries);
        var recentEntries = currentLedger.GetRecentEntries(100);
        var metrics = currentLedger.GetMetrics();

        // Assert
        await Assert.That(currentLedger.Entries.Count).IsEqualTo(entryCount);
        await Assert.That(recentEntries.Count).IsEqualTo(100);
        await Assert.That(recentEntries[0].Action).IsEqualTo($"action-{entryCount - 100}");
        await Assert.That(recentEntries[99].Action).IsEqualTo($"action-{entryCount - 1}");
        await Assert.That(metrics.TotalEntries).IsEqualTo(entryCount);
    }

    /// <summary>
    /// Verifies that concurrent reads of entries are safe (immutable ledger).
    /// </summary>
    [Test]
    public async Task ConcurrentReads_EntriesCollection_ThreadSafe()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        var entries = Enumerable.Range(0, 1000)
            .Select(i => CreateTestEntry("task-1", "executor-1", $"action-{i}"))
            .ToList();

        var currentLedger = (ProgressLedger)ledger.WithEntries(entries);

        // Act - Perform concurrent reads
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() =>
            {
                var recent = currentLedger.GetRecentEntries(100);
                var count = currentLedger.Entries.Count;
                var metrics = currentLedger.GetMetrics();
                return (recent.Count, count, metrics.TotalEntries);
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - All concurrent reads should return consistent results
        foreach (var (recentCount, entriesCount, totalEntries) in results)
        {
            await Assert.That(recentCount).IsEqualTo(100);
            await Assert.That(entriesCount).IsEqualTo(1000);
            await Assert.That(totalEntries).IsEqualTo(1000);
        }
    }

    /// <summary>
    /// Verifies that GetRecentEntries on an empty ledger returns empty list with same reference.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_EmptyLedger_ReturnsSameEmptyReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act
        var recentEntries = ledger.GetRecentEntries(5);

        // Assert - Should return same reference for empty list
        await Assert.That(ReferenceEquals(recentEntries, ledger.Entries)).IsTrue();
        await Assert.That(recentEntries.Count).IsEqualTo(0);
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static ProgressEntry CreateTestEntry(string taskId, string executorId, string action)
    {
        return ProgressEntry.Create(taskId, executorId, action, progressMade: true);
    }

    private static ProgressEntry CreateTestEntryWithDetails(
        string taskId,
        string executorId,
        string action,
        int tokensConsumed = 0,
        TimeSpan? duration = null,
        IReadOnlyList<string>? artifacts = null)
    {
        return new ProgressEntry
        {
            EntryId = $"progress-{Guid.NewGuid():N}",
            TaskId = taskId,
            ExecutorId = executorId,
            Action = action,
            ProgressMade = true,
            TokensConsumed = tokensConsumed,
            Duration = duration,
            Artifacts = artifacts ?? []
        };
    }

    private static ProgressEntry CreateTestEntryWithSignal(
        string taskId,
        ExecutorSignal signal,
        int tokensConsumed = 0)
    {
        return new ProgressEntry
        {
            EntryId = $"progress-{Guid.NewGuid():N}",
            TaskId = taskId,
            ExecutorId = signal.ExecutorId,
            Action = $"Signal: {signal.Type}",
            ProgressMade = signal.Type == SignalType.Success,
            Signal = signal,
            TokensConsumed = tokensConsumed
        };
    }
}
