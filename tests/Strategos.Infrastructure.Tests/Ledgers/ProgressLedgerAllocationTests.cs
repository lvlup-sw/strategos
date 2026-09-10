// =============================================================================
// <copyright file="ProgressLedgerAllocationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Infrastructure.Ledgers;
using Strategos.Orchestration.Ledgers;

namespace Strategos.Infrastructure.Tests.Ledgers;

/// <summary>
/// Unit tests for <see cref="ProgressLedger.GetRecentEntries"/> verifying
/// optimized window query behavior and correct subset retrieval.
/// </summary>
[Property("Category", "Unit")]
public sealed class ProgressLedgerAllocationTests
{
    // =============================================================================
    // A. GetRecentEntries Window Optimization Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetRecentEntries returns the correct subset when window is smaller than entries.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_LargeWindow_ReturnsCorrectSubset()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        for (var i = 0; i < 100; i++)
        {
            ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
                taskId: "task-1",
                executorId: "exec-1",
                action: $"Action{i}",
                progressMade: true));
        }

        // Act
        var recent = ledger.GetRecentEntries(10);

        // Assert
        await Assert.That(recent.Count).IsEqualTo(10);
        await Assert.That(recent[0].Action).IsEqualTo("Action90"); // Last 10, starting at 90
        await Assert.That(recent[9].Action).IsEqualTo("Action99");
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns all entries when window is larger than entry count.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowLargerThanEntries_ReturnsAllEntries()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        for (var i = 0; i < 5; i++)
        {
            ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
                taskId: "task-1",
                executorId: "exec-1",
                action: $"Action{i}",
                progressMade: true));
        }

        // Act
        var recent = ledger.GetRecentEntries(10);

        // Assert
        await Assert.That(recent.Count).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns empty list when ledger has no entries.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_EmptyLedger_ReturnsEmptyList()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act
        var recent = ledger.GetRecentEntries(5);

        // Assert
        await Assert.That(recent.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns the same instance when window covers all entries.
    /// This optimization avoids unnecessary allocations.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowExactlyEqualsCount_ReturnsAllEntries()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        for (var i = 0; i < 5; i++)
        {
            ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
                taskId: "task-1",
                executorId: "exec-1",
                action: $"Action{i}",
                progressMade: true));
        }

        // Act
        var recent = ledger.GetRecentEntries(5);

        // Assert - Should return all entries since window == count
        await Assert.That(recent.Count).IsEqualTo(5);
        await Assert.That(recent[0].Action).IsEqualTo("Action0");
        await Assert.That(recent[4].Action).IsEqualTo("Action4");
    }

    /// <summary>
    /// Verifies that the optimized implementation returns the original Entries
    /// when window size is greater than or equal to entry count (no allocation needed).
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowGreaterThanOrEqualToCount_ReturnsSameReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        for (var i = 0; i < 5; i++)
        {
            ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
                taskId: "task-1",
                executorId: "exec-1",
                action: $"Action{i}",
                progressMade: true));
        }

        // Act
        var recent = ledger.GetRecentEntries(10);

        // Assert - Should be the same reference as Entries (no allocation)
        await Assert.That(ReferenceEquals(recent, ledger.Entries)).IsTrue();
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns correct entries for boundary cases.
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowOfOne_ReturnsOnlyLastEntry()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        for (var i = 0; i < 10; i++)
        {
            ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
                taskId: "task-1",
                executorId: "exec-1",
                action: $"Action{i}",
                progressMade: true));
        }

        // Act
        var recent = ledger.GetRecentEntries(1);

        // Assert
        await Assert.That(recent.Count).IsEqualTo(1);
        await Assert.That(recent[0].Action).IsEqualTo("Action9");
    }

    // =============================================================================
    // B. Optimized Return Path Tests (Reference Identity)
    // =============================================================================

    /// <summary>
    /// Verifies that GetRecentEntries returns the same reference when windowSize
    /// exactly equals entry count (optimization: no allocation).
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowExactlyEqualsCount_ReturnsSameReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        for (var i = 0; i < 5; i++)
        {
            ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
                taskId: "task-1",
                executorId: "exec-1",
                action: $"Action{i}",
                progressMade: true));
        }

        // Act - Window size exactly matches entry count
        var recent = ledger.GetRecentEntries(5);

        // Assert - Should be the same reference as Entries (no allocation)
        await Assert.That(ReferenceEquals(recent, ledger.Entries)).IsTrue();
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns the same reference for empty ledger
    /// (optimization: no allocation for empty collections).
    /// </summary>
    [Test]
    public async Task GetRecentEntries_EmptyLedger_ReturnsSameReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");

        // Act
        var recent = ledger.GetRecentEntries(5);

        // Assert - Should be the same reference as Entries (empty list, no allocation)
        await Assert.That(ReferenceEquals(recent, ledger.Entries)).IsTrue();
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns the same reference for single entry
    /// when windowSize >= 1 (optimization: no allocation).
    /// </summary>
    [Test]
    public async Task GetRecentEntries_SingleEntry_WindowGreaterOrEqual_ReturnsSameReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
            taskId: "task-1",
            executorId: "exec-1",
            action: "SingleAction",
            progressMade: true));

        // Act - Window size is greater than entry count
        var recent = ledger.GetRecentEntries(5);

        // Assert - Should be the same reference (no allocation needed)
        await Assert.That(ReferenceEquals(recent, ledger.Entries)).IsTrue();
        await Assert.That(recent.Count).IsEqualTo(1);
        await Assert.That(recent[0].Action).IsEqualTo("SingleAction");
    }

    /// <summary>
    /// Verifies that GetRecentEntries returns a new list (not the same reference)
    /// when windowSize is less than entry count (allocation required for slicing).
    /// </summary>
    [Test]
    public async Task GetRecentEntries_WindowLessThanCount_ReturnsDifferentReference()
    {
        // Arrange
        var ledger = ProgressLedger.Create("task-ledger-1");
        for (var i = 0; i < 10; i++)
        {
            ledger = (ProgressLedger)ledger.WithEntry(ProgressEntry.Create(
                taskId: "task-1",
                executorId: "exec-1",
                action: $"Action{i}",
                progressMade: true));
        }

        // Act - Window size is less than entry count
        var recent = ledger.GetRecentEntries(5);

        // Assert - Should NOT be the same reference (new list allocated for slice)
        await Assert.That(ReferenceEquals(recent, ledger.Entries)).IsFalse();
        await Assert.That(recent.Count).IsEqualTo(5);
        await Assert.That(recent[0].Action).IsEqualTo("Action5");
        await Assert.That(recent[4].Action).IsEqualTo("Action9");
    }

    /// <summary>
    /// Verifies that GetRecentEntries throws ArgumentOutOfRangeException for zero windowSize.
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
    /// Verifies that GetRecentEntries throws ArgumentOutOfRangeException for negative windowSize.
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
}

