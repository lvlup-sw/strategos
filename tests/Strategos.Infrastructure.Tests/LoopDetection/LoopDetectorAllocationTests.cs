// =============================================================================
// <copyright file="LoopDetectorAllocationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Configuration;
using Strategos.Infrastructure.LoopDetection;
using Strategos.Orchestration.Ledgers;
using Strategos.Orchestration.LoopDetection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Strategos.Infrastructure.Tests.LoopDetection;

/// <summary>
/// Unit tests for <see cref="LoopDetector"/> verifying functional loop-detection behavior
/// including oscillation pattern detection and window-based analysis.
/// </summary>
[Property("Category", "Unit")]
public sealed class LoopDetectorAllocationTests
{
    /// <summary>
    /// Test semantic similarity calculator that returns zero similarity.
    /// </summary>
    private sealed class ZeroSimilarityCalculator : ISemanticSimilarityCalculator
    {
        private static readonly Task<double> ZeroSimilarityTask = Task.FromResult(0.0);

        public Task<double> CalculateMaxSimilarityAsync(
            IEnumerable<string?> outputs,
            CancellationToken cancellationToken = default)
        {
            return ZeroSimilarityTask;
        }
    }

    /// <summary>
    /// Creates a test instance of LoopDetector with configurable dependencies.
    /// </summary>
    private static LoopDetector CreateLoopDetector(LoopDetectionOptions? options = null)
    {
        var logger = Substitute.For<ILogger<LoopDetector>>();
        var opts = Options.Create(options ?? LoopDetectionOptions.CreateProductionDefaults());
        var similarity = new ZeroSimilarityCalculator();
        return new LoopDetector(logger, opts, similarity);
    }

    /// <summary>
    /// Creates a progress entry with configurable properties.
    /// </summary>
    private static ProgressEntry CreateEntry(string action, bool progressMade = true)
    {
        return new ProgressEntry
        {
            EntryId = $"entry-{Guid.NewGuid():N}",
            TaskId = "test-task",
            ExecutorId = "Coder",
            Action = action,
            Output = $"Output for {action}",
            ProgressMade = progressMade,
            Artifacts = [],
            Signal = null
        };
    }

    /// <summary>
    /// Creates a progress ledger with entries.
    /// </summary>
    private static IProgressLedger CreateLedgerWithEntries(params ProgressEntry[] entries)
    {
        var ledger = ProgressLedger.Create("test-task-ledger");
        return ledger.WithEntries(entries);
    }

    /// <summary>
    /// Verifies that CalculateOscillationScore works correctly with large window sizes.
    /// This exercises the SpanOwner-based implementation path.
    /// </summary>
    [Test]
    public async Task CalculateOscillationScore_LargeWindow_DetectsPattern()
    {
        // Arrange - Create oscillating pattern A-B-A-B-A-B-A-B-A-B (10 entries)
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 10;
        var detector = CreateLoopDetector(options);

        var entries = new List<ProgressEntry>();
        for (var i = 0; i < 10; i++)
        {
            entries.Add(CreateEntry(i % 2 == 0 ? "ActionA" : "ActionB"));
        }

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act - Detect loops (this exercises CalculateOscillationScore internally)
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - Should detect oscillation pattern
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
    }

    /// <summary>
    /// Verifies that oscillation detection with large windows correctly identifies period-3 patterns.
    /// The SpanOwner implementation must correctly populate the span for pattern analysis.
    /// </summary>
    [Test]
    public async Task CalculateOscillationScore_LargeWindowPeriod3_DetectsPattern()
    {
        // Arrange - Create repeating A-B-C-A-B-C-A-B-C pattern (9 entries)
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 9;
        var detector = CreateLoopDetector(options);

        var actions = new[] { "ActionA", "ActionB", "ActionC" };
        var entries = new List<ProgressEntry>();
        for (var i = 0; i < 9; i++)
        {
            entries.Add(CreateEntry(actions[i % 3]));
        }

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - Should detect oscillation pattern
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
    }

    /// <summary>
    /// Verifies that non-oscillating patterns are not falsely detected as oscillation.
    /// This ensures the SpanOwner implementation correctly compares actions.
    /// </summary>
    [Test]
    public async Task CalculateOscillationScore_NoPattern_DoesNotDetectOscillation()
    {
        // Arrange - Create non-repeating pattern
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 6;
        var detector = CreateLoopDetector(options);

        var entries = new List<ProgressEntry>
        {
            CreateEntry("ActionA"),
            CreateEntry("ActionB"),
            CreateEntry("ActionC"),
            CreateEntry("ActionD"),
            CreateEntry("ActionE"),
            CreateEntry("ActionF")
        };

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - Should not detect oscillation
        await Assert.That(result.DetectedType).IsNotEqualTo(LoopType.Oscillation);
    }

    /// <summary>
    /// Verifies that oscillation detection with maximum realistic window size works correctly.
    /// This is a stress test for the SpanOwner implementation.
    /// </summary>
    [Test]
    public async Task CalculateOscillationScore_MaxWindow_HandlesCorrectly()
    {
        // Arrange - Create large oscillating pattern (20 entries, A-B repeating)
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 20;
        var detector = CreateLoopDetector(options);

        var entries = new List<ProgressEntry>();
        for (var i = 0; i < 20; i++)
        {
            entries.Add(CreateEntry(i % 2 == 0 ? "ActionA" : "ActionB"));
        }

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - Should detect oscillation pattern
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.7);
    }
}
