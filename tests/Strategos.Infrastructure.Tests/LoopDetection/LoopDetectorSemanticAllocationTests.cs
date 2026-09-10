// =============================================================================
// <copyright file="LoopDetectorSemanticAllocationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Infrastructure.Tests.LoopDetection;

/// <summary>
/// Unit tests for <see cref="LoopDetector"/> verifying semantic similarity
/// allocation optimizations pass IEnumerable directly.
/// </summary>
[Property("Category", "Unit")]
public sealed class LoopDetectorSemanticAllocationTests
{
    /// <summary>
    /// Semantic similarity calculator that accepts IEnumerable and returns configurable similarity.
    /// </summary>
    private sealed class EnumerableAcceptingCalculator : ISemanticSimilarityCalculator
    {
        private readonly double _similarity;
        private bool _wasCalledWithEnumerable;

        public EnumerableAcceptingCalculator(double similarity)
        {
            _similarity = similarity;
        }

        /// <summary>
        /// Gets whether the calculator was called with an IEnumerable parameter.
        /// </summary>
        public bool WasCalledWithEnumerable => _wasCalledWithEnumerable;

        /// <inheritdoc/>
        public Task<double> CalculateMaxSimilarityAsync(
            IEnumerable<string?> outputs,
            CancellationToken cancellationToken = default)
        {
            _wasCalledWithEnumerable = true;
            // Enumerate to ensure the IEnumerable is valid
            _ = outputs.ToList();
            return Task.FromResult(_similarity);
        }
    }

    /// <summary>
    /// Creates a test instance of LoopDetector with the specified calculator.
    /// </summary>
    private static LoopDetector CreateLoopDetector(
        LoopDetectionOptions? options = null,
        ISemanticSimilarityCalculator? similarityCalculator = null)
    {
        var logger = Substitute.For<ILogger<LoopDetector>>();
        var opts = Options.Create(options ?? new LoopDetectionOptions
        {
            WindowSize = 5,
            RecoveryThreshold = 0.7,
            SimilarityThreshold = 0.8,
            RepetitionScoreWeight = 0.4,
            SemanticScoreWeight = 0.3,
            TimeScoreWeight = 0.2,
            FrustrationScoreWeight = 0.1
        });
        var similarity = similarityCalculator ?? new EnumerableAcceptingCalculator(0.0);
        return new LoopDetector(logger, opts, similarity);
    }

    /// <summary>
    /// Creates a progress entry with the specified properties.
    /// </summary>
    /// <param name="action">The action description.</param>
    /// <param name="progressMade">Whether progress was made.</param>
    /// <param name="output">The output value. Use empty string to explicitly set null.</param>
    /// <returns>A new progress entry.</returns>
    private static ProgressEntry CreateEntry(
        string action,
        bool progressMade = true,
        string? output = "default")
    {
        // Use sentinel value "default" to indicate default output should be generated
        // Empty string "" or null means explicit null output
        var actualOutput = output == "default" ? $"Output for {action}" : output;

        return new ProgressEntry
        {
            EntryId = $"entry-{Guid.NewGuid():N}",
            TaskId = "test-task",
            ExecutorId = "Coder",
            Action = action,
            Output = actualOutput,
            ProgressMade = progressMade,
            Artifacts = []
        };
    }

    /// <summary>
    /// Creates a progress entry with an explicit null output.
    /// </summary>
    /// <param name="action">The action description.</param>
    /// <param name="progressMade">Whether progress was made.</param>
    /// <returns>A new progress entry with null output.</returns>
    private static ProgressEntry CreateEntryWithNullOutput(
        string action,
        bool progressMade = true)
    {
        return new ProgressEntry
        {
            EntryId = $"entry-{Guid.NewGuid():N}",
            TaskId = "test-task",
            ExecutorId = "Coder",
            Action = action,
            Output = null,
            ProgressMade = progressMade,
            Artifacts = []
        };
    }

    /// <summary>
    /// Creates a progress ledger with the specified entries.
    /// </summary>
    private static IProgressLedger CreateLedgerWithEntries(params ProgressEntry[] entries)
    {
        var ledger = ProgressLedger.Create("test-task-ledger");
        return ledger.WithEntries(entries);
    }

    /// <summary>
    /// Verifies that semantic similarity detection works correctly with IEnumerable parameter.
    /// </summary>
    [Test]
    public async Task DetectAsync_WithSemanticSimilarity_ProducesCorrectScore()
    {
        // Arrange
        var calculator = new EnumerableAcceptingCalculator(0.9);
        var detector = CreateLoopDetector(similarityCalculator: calculator);

        var entries = new[]
        {
            CreateEntry("Action0", output: "Similar output"),
            CreateEntry("Action1", output: "Similar output"),
            CreateEntry("Action2", output: "Similar output"),
            CreateEntry("Action3", output: "Similar output"),
            CreateEntry("Action4", output: "Similar output")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        var result = await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(result.LoopDetected).IsTrue();
        await Assert.That(result.DetectedType).IsEqualTo(LoopType.SemanticRepetition);
        await Assert.That(calculator.WasCalledWithEnumerable).IsTrue();
    }

    /// <summary>
    /// Verifies that the IEnumerable parameter contains the correct output values.
    /// </summary>
    [Test]
    public async Task DetectAsync_SemanticCalculation_PassesCorrectOutputs()
    {
        // Arrange
        var capturedOutputs = new List<string?>();
        var calculator = Substitute.For<ISemanticSimilarityCalculator>();
        calculator.CalculateMaxSimilarityAsync(
                Arg.Do<IEnumerable<string?>>(o => capturedOutputs.AddRange(o)),
                Arg.Any<CancellationToken>())
            .Returns(0.5);

        var detector = CreateLoopDetector(similarityCalculator: calculator);

        var entries = new[]
        {
            CreateEntry("Action0", output: "Output A"),
            CreateEntry("Action1", output: "Output B"),
            CreateEntry("Action2", output: "Output C"),
            CreateEntry("Action3", output: "Output D"),
            CreateEntry("Action4", output: "Output E")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(capturedOutputs).HasCount(5);
        await Assert.That(capturedOutputs).Contains("Output A");
        await Assert.That(capturedOutputs).Contains("Output B");
        await Assert.That(capturedOutputs).Contains("Output C");
        await Assert.That(capturedOutputs).Contains("Output D");
        await Assert.That(capturedOutputs).Contains("Output E");
    }

    /// <summary>
    /// Verifies that null outputs are correctly passed through the IEnumerable.
    /// </summary>
    [Test]
    public async Task DetectAsync_WithNullOutputs_HandlesCorrectly()
    {
        // Arrange
        var capturedOutputs = new List<string?>();
        var calculator = Substitute.For<ISemanticSimilarityCalculator>();
        calculator.CalculateMaxSimilarityAsync(
                Arg.Do<IEnumerable<string?>>(o => capturedOutputs.AddRange(o)),
                Arg.Any<CancellationToken>())
            .Returns(0.0);

        var detector = CreateLoopDetector(similarityCalculator: calculator);

        var entries = new[]
        {
            CreateEntryWithNullOutput("Action0"),
            CreateEntry("Action1", output: "Output B"),
            CreateEntryWithNullOutput("Action2"),
            CreateEntry("Action3", output: "Output D"),
            CreateEntryWithNullOutput("Action4")
        };
        var ledger = CreateLedgerWithEntries(entries);

        // Act
        await detector.DetectAsync(ledger).ConfigureAwait(false);

        // Assert
        await Assert.That(capturedOutputs).HasCount(5);
        // Verify null values are preserved
        var nullCount = capturedOutputs.Count(o => o is null);
        await Assert.That(nullCount).IsEqualTo(3);
    }
}
