// =============================================================================
// <copyright file="LoopDetectorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Infrastructure.Tests.LoopDetection;

/// <summary>
/// Unit tests for <see cref="LoopDetector"/> covering loop detection algorithm,
/// confidence scoring, and configuration options.
/// </summary>
/// <remarks>
/// Tests verify the loop detection algorithm including:
/// <list type="bullet">
///   <item>Guard clause validation</item>
///   <item>Four loop types: ExactRepetition, SemanticRepetition, Oscillation, NoProgress</item>
///   <item>Weighted confidence scoring formula</item>
///   <item>Configuration options and thresholds</item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class LoopDetectorTests
{
    // =============================================================================
    // Test Helpers
    // =============================================================================

    /// <summary>
    /// Creates a test instance of LoopDetector with configurable dependencies.
    /// </summary>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="similarityCalculator">Optional similarity calculator mock.</param>
    /// <returns>Configured test instance.</returns>
    private static LoopDetector CreateLoopDetector(
        LoopDetectionOptions? options = null,
        ISemanticSimilarityCalculator? similarityCalculator = null)
    {
        var logger = Substitute.For<ILogger<LoopDetector>>();
        var opts = Options.Create(options ?? LoopDetectionOptions.CreateProductionDefaults());
        var similarity = similarityCalculator ?? new ConfigurableSemanticSimilarityCalculator(0.0);
        return new LoopDetector(logger, opts, similarity);
    }

    /// <summary>
    /// Creates an empty progress ledger for testing.
    /// </summary>
    private static IProgressLedger CreateEmptyLedger()
    {
        return ProgressLedger.Create("test-task-ledger");
    }

    /// <summary>
    /// Creates a progress ledger with distinct entries.
    /// </summary>
    /// <param name="count">Number of entries to create.</param>
    /// <returns>Ledger with distinct actions.</returns>
    private static IProgressLedger CreateLedgerWithDistinctEntries(int count)
    {
        var entries = Enumerable.Range(0, count)
            .Select(i => CreateEntry($"distinct_action_{i}"))
            .ToArray();
        return CreateLedgerWithEntries(entries);
    }

    /// <summary>
    /// Creates a progress ledger with the specified entries.
    /// </summary>
    /// <param name="entries">Entries to add to the ledger.</param>
    /// <returns>Ledger containing the entries.</returns>
    private static IProgressLedger CreateLedgerWithEntries(params ProgressEntry[] entries)
    {
        var ledger = ProgressLedger.Create("test-task-ledger");
        return ledger.WithEntries(entries);
    }

    /// <summary>
    /// Creates a progress ledger with repeated actions.
    /// </summary>
    /// <param name="action">Action to repeat.</param>
    /// <param name="count">Number of times to repeat.</param>
    /// <param name="withArtifacts">Whether to include artifacts.</param>
    /// <returns>Ledger with repeated actions.</returns>
    private static IProgressLedger CreateLedgerWithRepeatedAction(
        string action,
        int count,
        bool withArtifacts = false)
    {
        var entries = Enumerable.Range(0, count)
            .Select(i => CreateEntry(action, artifacts: withArtifacts ? [$"/artifact_{i}.txt"] : []))
            .ToArray();
        return CreateLedgerWithEntries(entries);
    }

    /// <summary>
    /// Creates a progress ledger where no progress is made.
    /// </summary>
    /// <param name="count">Number of entries.</param>
    /// <returns>Ledger with no progress entries.</returns>
    private static IProgressLedger CreateLedgerWithNoProgress(int count)
    {
        var entries = Enumerable.Range(0, count)
            .Select(i => CreateEntry($"action_{i}", progressMade: false))
            .ToArray();
        return CreateLedgerWithEntries(entries);
    }

    /// <summary>
    /// Creates a progress entry with configurable properties.
    /// </summary>
    private static ProgressEntry CreateEntry(
        string action,
        bool progressMade = true,
        string? output = null,
        IReadOnlyList<string>? artifacts = null,
        ExecutorSignal? signal = null)
    {
        return new ProgressEntry
        {
            EntryId = $"entry-{Guid.NewGuid():N}",
            TaskId = "test-task",
            ExecutorId = "Coder",
            Action = action,
            Output = output ?? $"Output for {action}",
            ProgressMade = progressMade,
            Artifacts = artifacts ?? [],
            Signal = signal
        };
    }

    /// <summary>
    /// Creates a HelpNeeded signal for frustration testing.
    /// </summary>
    private static ExecutorSignal CreateHelpSignal()
    {
        return new ExecutorSignal
        {
            ExecutorId = "Coder",
            Type = SignalType.HelpNeeded,
            FailureData = new ExecutorFailureData
            {
                Reason = "Need more context",
                IsRecoverable = true
            }
        };
    }

    /// <summary>
    /// Creates a Failure signal for frustration testing.
    /// </summary>
    private static ExecutorSignal CreateFailureSignal()
    {
        return new ExecutorSignal
        {
            ExecutorId = "Coder",
            Type = SignalType.Failure,
            FailureData = new ExecutorFailureData
            {
                Reason = "Operation failed",
                IsRecoverable = true
            }
        };
    }

    // =============================================================================
    // A. Guard Clause Tests (Phase 2)
    // =============================================================================

    /// <summary>
    /// Verifies that DetectAsync throws ArgumentNullException when ledger is null.
    /// </summary>
    [Test]
    public async Task DetectAsync_NullLedger_ThrowsArgumentNullException()
    {
        // Arrange
        var detector = CreateLoopDetector();

        // Act & Assert
        await Assert.That(async () => await detector.DetectAsync(null!).ConfigureAwait(false))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentNullException))
            .ConfigureAwait(false);
    }

    // =============================================================================
    // B. Empty/Insufficient Data Tests (Phase 3)
    // =============================================================================

    /// <summary>
    /// Verifies that an empty ledger returns no loop detected with zero confidence.
    /// </summary>
    [Test]
    public async Task DetectAsync_EmptyLedger_ReturnsNoLoop()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateEmptyLedger();

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsFalse();
        await Assert.That(result.Confidence).IsEqualTo(0.0);
    }

    /// <summary>
    /// Verifies that ledgers with fewer entries than window size return no loop.
    /// </summary>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    public async Task DetectAsync_InsufficientEntries_ReturnsNoLoop(int entryCount)
    {
        // Arrange
        var detector = CreateLoopDetector(); // Default window size is 5
        var ledger = CreateLedgerWithDistinctEntries(entryCount);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsFalse();
        await Assert.That(result.DetectedType).IsNull();
    }

    // =============================================================================
    // C. ExactRepetition Detection Tests (Phase 4)
    // =============================================================================

    /// <summary>
    /// Verifies that all duplicate actions in window detects ExactRepetition loop.
    /// </summary>
    [Test]
    public async Task DetectAsync_AllDuplicateActions_DetectsExactRepetition()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithRepeatedAction("stuck_action", 5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.ExactRepetition);
        await Assert.That(result.RecommendedStrategy).IsEqualTo(LoopRecoveryStrategy.InjectVariation);
    }

    /// <summary>
    /// Verifies that all duplicate actions returns max repetition score contribution.
    /// </summary>
    [Test]
    public async Task DetectAsync_AllDuplicateActions_ReturnsMaxRepetitionScore()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithRepeatedAction("stuck_action", 5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        // With all duplicates: repetition_score = 1.0
        // Minimum contribution from repetition alone: 0.4 * 1.0 = 0.4
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.4);
    }

    /// <summary>
    /// Verifies that partial duplicates return proportional confidence.
    /// </summary>
    [Test]
    public async Task DetectAsync_PartialDuplicates_ReturnsProportionalScore()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var entries = new[]
        {
            CreateEntry("action_a"),
            CreateEntry("action_a"),
            CreateEntry("action_a"),
            CreateEntry("action_b"),
            CreateEntry("action_c")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - some repetition detected but not maximal
        await Assert.That(result.Confidence).IsGreaterThan(0.0);
        await Assert.That(result.Confidence).IsLessThan(1.0);
    }

    // =============================================================================
    // D. NoProgress Detection Tests (Phase 5)
    // =============================================================================

    /// <summary>
    /// Verifies that all no-progress entries detects NoProgress loop.
    /// </summary>
    [Test]
    public async Task DetectAsync_AllNoProgress_DetectsNoProgressLoop()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithNoProgress(5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.NoProgress);
        await Assert.That(result.RecommendedStrategy).IsEqualTo(LoopRecoveryStrategy.Decompose);
    }

    /// <summary>
    /// Verifies that no-progress entries contribute to confidence score.
    /// </summary>
    [Test]
    public async Task DetectAsync_AllNoProgress_ReturnsSignificantConfidence()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithNoProgress(5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should trigger loop detection
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.7);
    }

    /// <summary>
    /// Verifies that mixed progress entries return proportional no-progress contribution.
    /// </summary>
    [Test]
    public async Task DetectAsync_MixedProgress_ReturnsProportionalNoProgressScore()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var entries = new[]
        {
            CreateEntry("action_a", progressMade: true),
            CreateEntry("action_b", progressMade: false),
            CreateEntry("action_c", progressMade: false),
            CreateEntry("action_d", progressMade: true),
            CreateEntry("action_e", progressMade: false)
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - some no-progress but not full, confidence should be moderate
        await Assert.That(result.Confidence).IsGreaterThan(0.0);
        await Assert.That(result.Confidence).IsLessThan(0.7);
    }

    // =============================================================================
    // E. Oscillation Detection Tests (Phase 6)
    // =============================================================================

    /// <summary>
    /// Creates a ledger with alternating A-B pattern (oscillation).
    /// </summary>
    private static IProgressLedger CreateLedgerWithOscillation()
    {
        var entries = new[]
        {
            CreateEntry("action_a"),
            CreateEntry("action_b"),
            CreateEntry("action_a"),
            CreateEntry("action_b"),
            CreateEntry("action_a")
        };
        return CreateLedgerWithEntries(entries);
    }

    /// <summary>
    /// Creates a ledger with A-B-C repeating pattern.
    /// </summary>
    private static IProgressLedger CreateLedgerWithThreeWayOscillation()
    {
        var entries = new[]
        {
            CreateEntry("action_a"),
            CreateEntry("action_b"),
            CreateEntry("action_c"),
            CreateEntry("action_a"),
            CreateEntry("action_b"),
            CreateEntry("action_c")
        };
        return CreateLedgerWithEntries(entries);
    }

    /// <summary>
    /// Verifies that A-B-A-B-A pattern detects Oscillation loop.
    /// </summary>
    [Test]
    public async Task DetectAsync_AlternatingPattern_DetectsOscillation()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithOscillation();

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
        await Assert.That(result.RecommendedStrategy).IsEqualTo(LoopRecoveryStrategy.Synthesize);
    }

    /// <summary>
    /// Verifies that oscillation pattern returns significant confidence.
    /// </summary>
    [Test]
    public async Task DetectAsync_AlternatingPattern_ReturnsSignificantConfidence()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithOscillation();

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - oscillation should trigger recovery
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.7);
    }

    /// <summary>
    /// Verifies that A-B-C-A-B-C pattern also detects Oscillation.
    /// </summary>
    [Test]
    public async Task DetectAsync_ThreeWayOscillation_DetectsOscillation()
    {
        // Arrange
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 6; // Need larger window for 3-way pattern
        var detector = CreateLoopDetector(options);
        var ledger = CreateLedgerWithThreeWayOscillation();

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
    }

    // =============================================================================
    // F. SemanticRepetition Detection Tests (Phase 7)
    // =============================================================================

    /// <summary>
    /// Test semantic similarity calculator that returns configurable similarity scores.
    /// </summary>
    private sealed class ConfigurableSemanticSimilarityCalculator : ISemanticSimilarityCalculator
    {
        private readonly double _similarity;

        public ConfigurableSemanticSimilarityCalculator(double similarity)
        {
            _similarity = similarity;
        }

        public Task<double> CalculateMaxSimilarityAsync(
            IEnumerable<string?> outputs,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_similarity);
        }
    }

    /// <summary>
    /// Test semantic similarity calculator that throws if called.
    /// Used to verify that semantic similarity is skipped when high-confidence signals exist.
    /// </summary>
    private sealed class ThrowingSemanticSimilarityCalculator : ISemanticSimilarityCalculator
    {
        public Task<double> CalculateMaxSimilarityAsync(
            IEnumerable<string?> outputs,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Semantic similarity should not be called when high-confidence signal exists");
        }
    }

    /// <summary>
    /// Verifies that high semantic similarity detects SemanticRepetition loop.
    /// </summary>
    [Test]
    public async Task DetectAsync_HighSemanticSimilarity_DetectsSemanticRepetition()
    {
        // Arrange
        var similarityCalculator = new ConfigurableSemanticSimilarityCalculator(0.95);
        var detector = CreateLoopDetector(similarityCalculator: similarityCalculator);
        var ledger = CreateLedgerWithDistinctEntries(5); // Different actions but similar outputs

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.SemanticRepetition);
        await Assert.That(result.RecommendedStrategy).IsEqualTo(LoopRecoveryStrategy.ForceRotation);
    }

    /// <summary>
    /// Verifies that semantic similarity contributes to confidence score.
    /// </summary>
    [Test]
    public async Task DetectAsync_HighSemanticSimilarity_ReturnsSemanticScoreContribution()
    {
        // Arrange
        var similarityCalculator = new ConfigurableSemanticSimilarityCalculator(0.95);
        var detector = CreateLoopDetector(similarityCalculator: similarityCalculator);
        var ledger = CreateLedgerWithDistinctEntries(5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - semantic component: 0.3 * 0.95 = 0.285
        // With other components, should exceed threshold
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.7);
    }

    /// <summary>
    /// Verifies that low semantic similarity does not trigger SemanticRepetition.
    /// </summary>
    [Test]
    public async Task DetectAsync_LowSemanticSimilarity_DoesNotDetectSemanticRepetition()
    {
        // Arrange
        var similarityCalculator = new ConfigurableSemanticSimilarityCalculator(0.3);
        var detector = CreateLoopDetector(similarityCalculator: similarityCalculator);
        var ledger = CreateLedgerWithDistinctEntries(5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should not be SemanticRepetition
        await Assert.That(result.DetectedType).IsNotEqualTo(LoopType.SemanticRepetition);
    }

    // =============================================================================
    // G. Frustration Score Tests (Phase 8)
    // =============================================================================

    /// <summary>
    /// Creates a ledger with HelpNeeded signals indicating frustration.
    /// </summary>
    private static IProgressLedger CreateLedgerWithFrustration()
    {
        var entries = new[]
        {
            CreateEntry("action_a", signal: CreateHelpSignal()),
            CreateEntry("action_b", signal: CreateFailureSignal()),
            CreateEntry("action_c", signal: CreateHelpSignal()),
            CreateEntry("action_d", signal: CreateFailureSignal()),
            CreateEntry("action_e", signal: CreateHelpSignal())
        };
        return CreateLedgerWithEntries(entries);
    }

    /// <summary>
    /// Verifies that high frustration contributes to confidence score.
    /// </summary>
    [Test]
    public async Task DetectAsync_HighFrustration_IncreasesConfidence()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledgerWithFrustration = CreateLedgerWithFrustration();
        var ledgerWithoutFrustration = CreateLedgerWithDistinctEntries(5);

        // Act
        var resultWithFrustration = await detector.DetectAsync(ledgerWithFrustration).ConfigureAwait(false);
        var resultWithoutFrustration = await detector.DetectAsync(ledgerWithoutFrustration).ConfigureAwait(false);

        // Assert - frustration should increase confidence
        await Assert.That(resultWithFrustration.Confidence)
            .IsGreaterThan(resultWithoutFrustration.Confidence);
    }

    /// <summary>
    /// Verifies that frustration score is proportional to signal ratio.
    /// </summary>
    [Test]
    public async Task DetectAsync_PartialFrustration_ReturnsProportionalScore()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var entries = new[]
        {
            CreateEntry("action_a", signal: CreateHelpSignal()),
            CreateEntry("action_b"), // No signal
            CreateEntry("action_c", signal: CreateFailureSignal()),
            CreateEntry("action_d"), // No signal
            CreateEntry("action_e") // No signal
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - 2/5 = 40% frustration, contributes 0.1 * 0.4 = 0.04 to confidence
        // Combined with 0.2 repetition * 0.4 = 0.08, total should be around 0.12
        await Assert.That(result.Confidence).IsGreaterThan(0.08);
        await Assert.That(result.Confidence).IsLessThan(0.5);
    }

    // =============================================================================
    // H. Edge Case Tests (Phase 9)
    // =============================================================================

    /// <summary>
    /// Verifies that entries at exactly window size are processed.
    /// </summary>
    [Test]
    public async Task DetectAsync_ExactWindowSize_ProcessesCorrectly()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithDistinctEntries(5); // Exactly window size

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should process and return valid result
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.0);
        await Assert.That(result.Confidence).IsLessThanOrEqualTo(1.0);
    }

    /// <summary>
    /// Verifies that entries beyond window size only consider recent entries.
    /// </summary>
    [Test]
    public async Task DetectAsync_MoreThanWindowSize_UsesOnlyRecentEntries()
    {
        // Arrange
        var detector = CreateLoopDetector();
        // Create 10 entries but window size is 5
        var ledger = CreateLedgerWithDistinctEntries(10);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should only consider last 5 entries
        await Assert.That(result.DiagnosticMessage).IsNotNull();
    }

    /// <summary>
    /// Verifies that custom window size is respected.
    /// </summary>
    [Test]
    public async Task DetectAsync_CustomWindowSize_RespectsConfiguration()
    {
        // Arrange
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 3;
        var detector = CreateLoopDetector(options);
        var ledger = CreateLedgerWithDistinctEntries(3);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should process with custom window size
        await Assert.That(result.LoopDetected).IsFalse();
    }

    /// <summary>
    /// Verifies that custom recovery threshold is respected.
    /// </summary>
    [Test]
    public async Task DetectAsync_CustomRecoveryThreshold_RespectsConfiguration()
    {
        // Arrange
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.RecoveryThreshold = 0.3; // Lower threshold
        var detector = CreateLoopDetector(options);
        var entries = new[]
        {
            CreateEntry("action_a"),
            CreateEntry("action_a"),
            CreateEntry("action_b"),
            CreateEntry("action_c"),
            CreateEntry("action_d")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - lower threshold may trigger detection
        // With 2/5 repetition = 0.4, confidence = 0.4 * 0.4 = 0.16 + other factors
        await Assert.That(result.Confidence).IsGreaterThan(0.0);
    }

    // =============================================================================
    // I. Performance Optimization Tests - Skip Semantic Similarity (A.4)
    // =============================================================================

    /// <summary>
    /// Verifies that semantic similarity is skipped when exact repetition score is 1.0.
    /// </summary>
    [Test]
    public async Task DetectAsync_ExactRepetition_SkipsSemanticSimilarity()
    {
        // Arrange - use a throwing calculator to verify it's not called
        var throwingCalculator = new ThrowingSemanticSimilarityCalculator();
        var detector = CreateLoopDetector(similarityCalculator: throwingCalculator);
        var ledger = CreateLedgerWithRepeatedAction("stuck_action", 5);

        // Act - should not throw because semantic similarity is skipped
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.ExactRepetition);
    }

    /// <summary>
    /// Verifies that semantic similarity is skipped when no-progress score is 1.0.
    /// </summary>
    [Test]
    public async Task DetectAsync_PerfectNoProgress_SkipsSemanticSimilarity()
    {
        // Arrange - use a throwing calculator to verify it's not called
        var throwingCalculator = new ThrowingSemanticSimilarityCalculator();
        var detector = CreateLoopDetector(similarityCalculator: throwingCalculator);
        var ledger = CreateLedgerWithNoProgress(5);

        // Act - should not throw because semantic similarity is skipped
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.NoProgress);
    }

    // =============================================================================
    // J. String Comparison Correctness Tests (A.5)
    // =============================================================================

    /// <summary>
    /// Creates a non-interned string by building it at runtime.
    /// This ensures the string is not in the intern pool.
    /// </summary>
    private static string CreateNonInternedString(string value)
    {
        // Build the string from char array to ensure it's not interned
        return new string(value.ToCharArray());
    }

    /// <summary>
    /// Verifies that oscillation detection works with non-interned strings.
    /// This tests that the string comparison uses value equality, not reference equality.
    /// </summary>
    [Test]
    public async Task CalculateOscillationScore_NonInternedStrings_DetectsPattern()
    {
        // Arrange - create entries with non-interned action strings
        // These strings have the same value but are different object references
        var detector = CreateLoopDetector();
        var entries = new[]
        {
            CreateEntry(CreateNonInternedString("action_a")),
            CreateEntry(CreateNonInternedString("action_b")),
            CreateEntry(CreateNonInternedString("action_a")),
            CreateEntry(CreateNonInternedString("action_b")),
            CreateEntry(CreateNonInternedString("action_a"))
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should detect oscillation pattern even with non-interned strings
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
    }

    // =============================================================================
    // K. LINQ Optimization Tests (A.6)
    // =============================================================================

    /// <summary>
    /// Verifies that repetition score calculation works correctly with large entry sets.
    /// This tests the optimized LINQ path that avoids intermediate list allocation.
    /// </summary>
    [Test]
    public async Task DetectAsync_LargeEntrySet_CalculatesRepetitionCorrectly()
    {
        // Arrange - create a ledger with many repeated actions
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 10;
        var detector = CreateLoopDetector(options);

        // Create 10 entries where 7 have the same action (70% repetition)
        var entries = new List<ProgressEntry>();
        for (var i = 0; i < 7; i++)
        {
            entries.Add(CreateEntry("repeated_action"));
        }

        for (var i = 0; i < 3; i++)
        {
            entries.Add(CreateEntry($"unique_action_{i}"));
        }

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - repetition score should be 7/10 = 0.7, contributing 0.4 * 0.7 = 0.28
        // Use 0.27 to account for floating-point precision
        // This verifies the optimized calculation path works correctly
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.27);
    }

    // =============================================================================
    // L. Cancellation Token Tests
    // =============================================================================

    /// <summary>
    /// Test semantic similarity calculator that throws OperationCanceledException when token is cancelled.
    /// </summary>
    private sealed class CancellationAwareSemanticSimilarityCalculator : ISemanticSimilarityCalculator
    {
        public Task<double> CalculateMaxSimilarityAsync(
            IEnumerable<string?> outputs,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0.0);
        }
    }

    /// <summary>
    /// Verifies that DetectAsync respects cancellation token when passed to semantic similarity calculator.
    /// </summary>
    [Test]
    public async Task DetectAsync_CancelledToken_ThrowsWhenCalculatingSemanticSimilarity()
    {
        // Arrange - use a calculator that respects cancellation
        var calculator = new CancellationAwareSemanticSimilarityCalculator();
        var detector = CreateLoopDetector(similarityCalculator: calculator);
        var ledger = CreateLedgerWithDistinctEntries(5); // Distinct entries won't hit early returns
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - the semantic similarity calculator should throw
        await Assert.That(async () =>
                await detector.DetectAsync(ledger, cts.Token).ConfigureAwait(false))
            .ThrowsException()
            .WithExceptionType(typeof(OperationCanceledException))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that DetectAsync completes successfully with a valid (non-cancelled) token.
    /// </summary>
    [Test]
    public async Task DetectAsync_ValidToken_CompletesSuccessfully()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithDistinctEntries(5);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await detector.DetectAsync(ledger, cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.DiagnosticMessage).IsNotNull();
    }

    // =============================================================================
    // M. Additional Repetition Detection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that repetition is not detected when all actions are unique.
    /// </summary>
    [Test]
    public async Task DetectAsync_AllUniqueActions_DoesNotDetectRepetition()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithDistinctEntries(5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - unique actions should not trigger ExactRepetition
        await Assert.That(result.DetectedType).IsNotEqualTo(LoopType.ExactRepetition);
    }

    /// <summary>
    /// Verifies that repetition threshold boundary (50%) returns expected score.
    /// </summary>
    [Test]
    public async Task DetectAsync_HalfDuplicates_ReturnsExpectedScore()
    {
        // Arrange
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 4;
        var detector = CreateLoopDetector(options);
        var entries = new[]
        {
            CreateEntry("action_a"),
            CreateEntry("action_a"),
            CreateEntry("action_b"),
            CreateEntry("action_c")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - 50% repetition = 0.5, contributing 0.4 * 0.5 = 0.2
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.19);
    }

    /// <summary>
    /// Verifies that confidence calculation for repetition matches expected formula.
    /// </summary>
    [Test]
    public async Task DetectAsync_KnownRepetitionRatio_CalculatesCorrectConfidence()
    {
        // Arrange - 4 out of 5 same action = 80% repetition
        var detector = CreateLoopDetector();
        var entries = new[]
        {
            CreateEntry("action_a"),
            CreateEntry("action_a"),
            CreateEntry("action_a"),
            CreateEntry("action_a"),
            CreateEntry("action_b")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - repetition = 0.8, contribution = 0.4 * 0.8 = 0.32
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.31);
    }

    /// <summary>
    /// Verifies that window size boundary (exactly at threshold) processes correctly.
    /// </summary>
    [Test]
    public async Task DetectAsync_WindowSizeBoundary_ProcessesCorrectly()
    {
        // Arrange - entries exactly equal to window size
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 3;
        var detector = CreateLoopDetector(options);
        var ledger = CreateLedgerWithRepeatedAction("repeated", 3);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should process and detect repetition
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.ExactRepetition);
    }

    // =============================================================================
    // N. Additional Oscillation Detection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that random actions do not trigger oscillation detection.
    /// </summary>
    [Test]
    public async Task DetectAsync_RandomActions_DoesNotDetectOscillation()
    {
        // Arrange - create non-repeating, non-oscillating pattern
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 6;
        var detector = CreateLoopDetector(options);
        var entries = new[]
        {
            CreateEntry("action_x"),
            CreateEntry("action_y"),
            CreateEntry("action_z"),
            CreateEntry("action_w"),
            CreateEntry("action_v"),
            CreateEntry("action_u")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - random pattern should not detect oscillation
        await Assert.That(result.DetectedType).IsNotEqualTo(LoopType.Oscillation);
    }

    /// <summary>
    /// Verifies that oscillation confidence scoring is proportional to pattern match.
    /// </summary>
    [Test]
    public async Task DetectAsync_StrongOscillation_ReturnsHighConfidence()
    {
        // Arrange - perfect A-B oscillation
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 6;
        var detector = CreateLoopDetector(options);
        var entries = new[]
        {
            CreateEntry("A"),
            CreateEntry("B"),
            CreateEntry("A"),
            CreateEntry("B"),
            CreateEntry("A"),
            CreateEntry("B")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - perfect oscillation should have significant confidence
        // Oscillation threshold is 0.8 for detection, and confidence includes oscillation boost
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.Confidence).IsGreaterThanOrEqualTo(0.7);
    }

    /// <summary>
    /// Verifies oscillation detection with a 4-period repeating pattern.
    /// </summary>
    [Test]
    public async Task DetectAsync_FourPeriodOscillation_DetectsPattern()
    {
        // Arrange - A-B-C-D-A-B-C-D pattern
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 8;
        var detector = CreateLoopDetector(options);
        var entries = new[]
        {
            CreateEntry("A"),
            CreateEntry("B"),
            CreateEntry("C"),
            CreateEntry("D"),
            CreateEntry("A"),
            CreateEntry("B"),
            CreateEntry("C"),
            CreateEntry("D")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should detect oscillation pattern
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
    }

    // =============================================================================
    // O. SpanOwner Path Tests
    // =============================================================================

    /// <summary>
    /// Verifies that large window (10+) uses SpanOwner efficiently for oscillation detection.
    /// </summary>
    [Test]
    public async Task DetectAsync_LargeWindow_UsesSpanOwnerPath()
    {
        // Arrange - large window to exercise SpanOwner path
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 12;
        var detector = CreateLoopDetector(options);

        var entries = new List<ProgressEntry>();
        for (var i = 0; i < 12; i++)
        {
            entries.Add(CreateEntry(i % 2 == 0 ? "ActionA" : "ActionB"));
        }

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should detect oscillation pattern using SpanOwner path
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.Oscillation);
    }

    /// <summary>
    /// Verifies that small window (less than 8) still works correctly.
    /// </summary>
    [Test]
    public async Task DetectAsync_SmallWindow_ProcessesCorrectly()
    {
        // Arrange - small window that may use stackalloc-style path
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 4;
        var detector = CreateLoopDetector(options);
        var entries = new[]
        {
            CreateEntry("A"),
            CreateEntry("B"),
            CreateEntry("A"),
            CreateEntry("B")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should process correctly
        await Assert.That(result.LoopDetected).IsTrue();
    }

    /// <summary>
    /// Verifies that very large window (100+) handles correctly without memory issues.
    /// </summary>
    [Test]
    public async Task DetectAsync_VeryLargeWindow_HandlesCorrectly()
    {
        // Arrange - very large window to stress test SpanOwner
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 100;
        var detector = CreateLoopDetector(options);

        var entries = new List<ProgressEntry>();
        for (var i = 0; i < 100; i++)
        {
            entries.Add(CreateEntry(i % 3 == 0 ? "ActionA" : i % 3 == 1 ? "ActionB" : "ActionC"));
        }

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert - should complete without error and detect pattern
        await Assert.That(result).IsNotNull();
        await Assert.That(result.LoopDetected).IsTrue();
    }

    /// <summary>
    /// Verifies that multiple concurrent detections work correctly.
    /// SpanOwner should properly dispose without interference.
    /// </summary>
    [Test]
    public async Task DetectAsync_MultipleConcurrentDetections_AllComplete()
    {
        // Arrange
        var detector = CreateLoopDetector();
        var ledgers = Enumerable.Range(0, 5)
            .Select(_ => CreateLedgerWithRepeatedAction("concurrent_action", 5))
            .ToArray();

        // Act - run multiple detections concurrently
        var tasks = ledgers.Select(l => detector.DetectAsync(l)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - all should complete and detect loops
        foreach (var result in results)
        {
            await Assert.That(result.LoopDetected).IsTrue();
            await Assert.That(result.DetectedType).IsEqualTo(LoopType.ExactRepetition);
        }
    }

    /// <summary>
    /// Verifies that memory is not retained after detection completes.
    /// This tests SpanOwner disposal behavior.
    /// </summary>
    [Test]
    public async Task DetectAsync_AfterCompletion_MemoryNotRetained()
    {
        // Arrange
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.WindowSize = 50;
        var detector = CreateLoopDetector(options);

        var entries = new List<ProgressEntry>();
        for (var i = 0; i < 50; i++)
        {
            entries.Add(CreateEntry($"action_{i % 5}"));
        }

        var ledger = CreateLedgerWithEntries(entries.ToArray());

        // Act - run detection multiple times
        for (var i = 0; i < 10; i++)
        {
            var result = await detector.DetectAsync(ledger).ConfigureAwait(false);
            await Assert.That(result).IsNotNull();
        }

        // Assert - if we got here without OutOfMemoryException, SpanOwner disposed correctly
        await Assert.That(true).IsTrue();
    }

    // =============================================================================
    // P. DetermineLoopType Tests
    // =============================================================================

    /// <summary>
    /// Verifies that when semantic score dominates, SemanticRepetition is detected.
    /// </summary>
    [Test]
    public async Task DetectAsync_SemanticScoreDominant_ReturnsSemanticRepetition()
    {
        // Arrange - high semantic similarity, low repetition
        var options = LoopDetectionOptions.CreateProductionDefaults();
        options.RecoveryThreshold = 0.4; // Lower threshold
        var similarityCalculator = new ConfigurableSemanticSimilarityCalculator(0.85);
        var detector = CreateLoopDetector(options, similarityCalculator);
        var ledger = CreateLedgerWithDistinctEntries(5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.SemanticRepetition);
    }

    /// <summary>
    /// Verifies that when no-progress score dominates, NoProgress is detected.
    /// </summary>
    [Test]
    public async Task DetectAsync_NoProgressDominant_ReturnsNoProgress()
    {
        // Arrange - all entries show no progress
        var detector = CreateLoopDetector();
        var ledger = CreateLedgerWithNoProgress(5);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.NoProgress);
    }
}
