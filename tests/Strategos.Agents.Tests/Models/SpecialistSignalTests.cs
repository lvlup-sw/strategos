// =============================================================================
// <copyright file="SpecialistSignalTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for <see cref="SpecialistSignal"/> covering factory methods and signal data.
/// </summary>
[Property("Category", "Unit")]
public class SpecialistSignalTests
{
    // =============================================================================
    // A. Success Factory Method Tests (4 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that Success creates a signal with correct type and specialist.
    /// </summary>
    [Test]
    public async Task Success_CreatesSignalWithCorrectTypeAndSpecialist()
    {
        // Act
        var signal = SpecialistSignal.Success(
            SpecialistType.Coder,
            result: "Code generated",
            confidence: 0.95);

        // Assert
        await Assert.That(signal.Type).IsEqualTo(SignalType.Success);
        await Assert.That(signal.Specialist).IsEqualTo(SpecialistType.Coder);
    }

    /// <summary>
    /// Verifies that Success populates SuccessData correctly.
    /// </summary>
    [Test]
    public async Task Success_PopulatesSuccessData()
    {
        // Arrange
        var artifacts = new List<string> { "/path/to/file.cs" };

        // Act
        var signal = SpecialistSignal.Success(
            SpecialistType.Analyst,
            result: "Analysis complete",
            confidence: 0.85,
            artifacts: artifacts,
            nextSuggestion: "Run tests");

        // Assert
        await Assert.That(signal.SuccessData).IsNotNull();
        await Assert.That(signal.SuccessData!.Result).IsEqualTo("Analysis complete");
        await Assert.That(signal.SuccessData!.Confidence).IsEqualTo(0.85);
        await Assert.That(signal.SuccessData!.Artifacts).Contains("/path/to/file.cs");
        await Assert.That(signal.SuccessData!.NextSuggestion).IsEqualTo("Run tests");
    }

    /// <summary>
    /// Verifies that Success with no artifacts uses empty list.
    /// </summary>
    [Test]
    public async Task Success_WithoutArtifacts_UsesEmptyList()
    {
        // Act
        var signal = SpecialistSignal.Success(
            SpecialistType.WebSurfer,
            result: "Search complete",
            confidence: 0.9);

        // Assert
        await Assert.That(signal.SuccessData!.Artifacts).IsEmpty();
    }

    /// <summary>
    /// Verifies that Success sets timestamp automatically.
    /// </summary>
    [Test]
    public async Task Success_SetsTimestampAutomatically()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var signal = SpecialistSignal.Success(
            SpecialistType.FileSurfer,
            result: "File read",
            confidence: 1.0);

        // Assert
        var after = DateTimeOffset.UtcNow;
        await Assert.That(signal.Timestamp).IsGreaterThanOrEqualTo(before);
        await Assert.That(signal.Timestamp).IsLessThanOrEqualTo(after);
    }

    // =============================================================================
    // B. Failure Factory Method Tests (4 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that Failure creates a signal with correct type.
    /// </summary>
    [Test]
    public async Task Failure_CreatesSignalWithCorrectType()
    {
        // Act
        var signal = SpecialistSignal.Failure(
            SpecialistType.Coder,
            reason: "Compilation error");

        // Assert
        await Assert.That(signal.Type).IsEqualTo(SignalType.Failure);
        await Assert.That(signal.Specialist).IsEqualTo(SpecialistType.Coder);
    }

    /// <summary>
    /// Verifies that Failure populates FailureData correctly.
    /// </summary>
    [Test]
    public async Task Failure_PopulatesFailureData()
    {
        // Arrange
        var hints = new List<string> { "Check syntax", "Review imports" };

        // Act
        var signal = SpecialistSignal.Failure(
            SpecialistType.Analyst,
            reason: "Data format error",
            partialResult: "Parsed 50%",
            recoveryHints: hints,
            shouldRetry: true);

        // Assert
        await Assert.That(signal.FailureData).IsNotNull();
        await Assert.That(signal.FailureData!.Reason).IsEqualTo("Data format error");
        await Assert.That(signal.FailureData!.PartialResult).IsEqualTo("Parsed 50%");
        await Assert.That(signal.FailureData!.RecoveryHints).Contains("Check syntax");
        await Assert.That(signal.FailureData!.ShouldRetry).IsTrue();
    }

    /// <summary>
    /// Verifies that Failure defaults ShouldRetry to false.
    /// </summary>
    [Test]
    public async Task Failure_DefaultsShouldRetryToFalse()
    {
        // Act
        var signal = SpecialistSignal.Failure(
            SpecialistType.WebSurfer,
            reason: "Page not found");

        // Assert
        await Assert.That(signal.FailureData!.ShouldRetry).IsFalse();
    }

    /// <summary>
    /// Verifies that Failure with no recovery hints uses empty list.
    /// </summary>
    [Test]
    public async Task Failure_WithoutRecoveryHints_UsesEmptyList()
    {
        // Act
        var signal = SpecialistSignal.Failure(
            SpecialistType.FileSurfer,
            reason: "File not found");

        // Assert
        await Assert.That(signal.FailureData!.RecoveryHints).IsEmpty();
    }

    // =============================================================================
    // C. HelpNeeded Factory Method Tests (4 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that HelpNeeded creates a signal with correct type.
    /// </summary>
    [Test]
    public async Task HelpNeeded_CreatesSignalWithCorrectType()
    {
        // Act
        var signal = SpecialistSignal.HelpNeeded(
            SpecialistType.Coder,
            BlockerType.InsufficientData,
            context: "Need database schema");

        // Assert
        await Assert.That(signal.Type).IsEqualTo(SignalType.HelpNeeded);
        await Assert.That(signal.Specialist).IsEqualTo(SpecialistType.Coder);
    }

    /// <summary>
    /// Verifies that HelpNeeded populates HelpNeededData correctly.
    /// </summary>
    [Test]
    public async Task HelpNeeded_PopulatesHelpNeededData()
    {
        // Arrange
        var suggestions = new List<string> { "Query FileSurfer", "Ask user" };

        // Act
        var signal = SpecialistSignal.HelpNeeded(
            SpecialistType.Analyst,
            BlockerType.AmbiguousGoal,
            context: "Unclear data format requirement",
            suggestions: suggestions,
            canProceedPartial: true);

        // Assert
        await Assert.That(signal.HelpNeededData).IsNotNull();
        await Assert.That(signal.HelpNeededData!.Blocker).IsEqualTo(BlockerType.AmbiguousGoal);
        await Assert.That(signal.HelpNeededData!.Context).IsEqualTo("Unclear data format requirement");
        await Assert.That(signal.HelpNeededData!.Suggestions).Contains("Query FileSurfer");
        await Assert.That(signal.HelpNeededData!.CanProceedPartial).IsTrue();
    }

    /// <summary>
    /// Verifies that HelpNeeded defaults CanProceedPartial to false.
    /// </summary>
    [Test]
    public async Task HelpNeeded_DefaultsCanProceedPartialToFalse()
    {
        // Act
        var signal = SpecialistSignal.HelpNeeded(
            SpecialistType.WebSurfer,
            BlockerType.ExternalDependency,
            context: "API unavailable");

        // Assert
        await Assert.That(signal.HelpNeededData!.CanProceedPartial).IsFalse();
    }

    /// <summary>
    /// Verifies that HelpNeeded with no suggestions uses empty list.
    /// </summary>
    [Test]
    public async Task HelpNeeded_WithoutSuggestions_UsesEmptyList()
    {
        // Act
        var signal = SpecialistSignal.HelpNeeded(
            SpecialistType.FileSurfer,
            BlockerType.ResourceExhausted,
            context: "Token limit reached");

        // Assert
        await Assert.That(signal.HelpNeededData!.Suggestions).IsEmpty();
    }

    // =============================================================================
    // D. Signal Data Isolation Tests (3 tests)
    // =============================================================================

    /// <summary>
    /// Verifies that Success signal has null FailureData and HelpNeededData.
    /// </summary>
    [Test]
    public async Task Success_HasNullOtherData()
    {
        // Act
        var signal = SpecialistSignal.Success(
            SpecialistType.Coder,
            result: "Done",
            confidence: 1.0);

        // Assert
        await Assert.That(signal.SuccessData).IsNotNull();
        await Assert.That(signal.FailureData).IsNull();
        await Assert.That(signal.HelpNeededData).IsNull();
    }

    /// <summary>
    /// Verifies that Failure signal has null SuccessData and HelpNeededData.
    /// </summary>
    [Test]
    public async Task Failure_HasNullOtherData()
    {
        // Act
        var signal = SpecialistSignal.Failure(
            SpecialistType.Analyst,
            reason: "Error");

        // Assert
        await Assert.That(signal.SuccessData).IsNull();
        await Assert.That(signal.FailureData).IsNotNull();
        await Assert.That(signal.HelpNeededData).IsNull();
    }

    /// <summary>
    /// Verifies that HelpNeeded signal has null SuccessData and FailureData.
    /// </summary>
    [Test]
    public async Task HelpNeeded_HasNullOtherData()
    {
        // Act
        var signal = SpecialistSignal.HelpNeeded(
            SpecialistType.WebSurfer,
            BlockerType.CapabilityMismatch,
            context: "Cannot perform task");

        // Assert
        await Assert.That(signal.SuccessData).IsNull();
        await Assert.That(signal.FailureData).IsNull();
        await Assert.That(signal.HelpNeededData).IsNotNull();
    }
}

