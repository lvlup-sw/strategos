// =============================================================================
// <copyright file="AgentBeliefPartialCreditTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Selection;

namespace Strategos.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="AgentBelief.WithOutcome"/> method covering
/// partial credit belief updates.
/// </summary>
[Property("Category", "Unit")]
public class AgentBeliefPartialCreditTests
{
    // =============================================================================
    // A. Basic WithOutcome Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithOutcome with success increments Alpha.
    /// </summary>
    [Test]
    public async Task WithOutcome_Success_IncrementsAlpha()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 1.0);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta);
    }

    /// <summary>
    /// Verifies that WithOutcome with failure increments Beta.
    /// </summary>
    [Test]
    public async Task WithOutcome_Failure_IncrementsBeta()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Failed();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta + 1.0);
    }

    /// <summary>
    /// Verifies that WithOutcome increments ObservationCount.
    /// </summary>
    [Test]
    public async Task WithOutcome_AnyOutcome_IncrementsObservationCount()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.ObservationCount).IsEqualTo(belief.ObservationCount + 1);
    }

    /// <summary>
    /// Verifies that WithOutcome updates timestamp.
    /// </summary>
    [Test]
    public async Task WithOutcome_AnyOutcome_UpdatesTimestamp()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var originalTime = belief.UpdatedAt;
        await Task.Delay(10).ConfigureAwait(false); // Ensure time passes
        var outcome = AgentOutcome.Succeeded();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.UpdatedAt).IsGreaterThanOrEqualTo(originalTime);
    }

    // =============================================================================
    // B. Partial Credit Tests (Confidence-Based)
    // =============================================================================

    /// <summary>
    /// Verifies that high confidence success increments Alpha fully.
    /// </summary>
    [Test]
    public async Task WithOutcome_HighConfidenceSuccess_IncrementsAlphaFully()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded(confidence: 1.0);

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 1.0);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta);
    }

    /// <summary>
    /// Verifies that low confidence success uses partial credit.
    /// </summary>
    [Test]
    public async Task WithOutcome_LowConfidenceSuccess_UsesPartialCredit()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded(confidence: 0.6);

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert - With partial credit, alpha increases by confidence, beta by (1 - confidence)
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 0.6);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta + 0.4);
    }

    /// <summary>
    /// Verifies that confidence on failure still uses partial credit.
    /// </summary>
    /// <remarks>
    /// When confidence is provided, it represents the credit value directly,
    /// regardless of the Success flag. This allows for partial credit scenarios.
    /// </remarks>
    [Test]
    public async Task WithOutcome_ConfidenceOnFailure_UsesConfidenceAsCredit()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Failed(confidence: 0.3);

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert - Confidence is used directly as credit, even on failure
        // This enables partial credit for "partially successful" outcomes
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 0.3);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta + 0.7);
    }

    /// <summary>
    /// Verifies that zero confidence success applies minimal alpha increment.
    /// </summary>
    [Test]
    public async Task WithOutcome_ZeroConfidenceSuccess_AppliesNoAlphaCredit()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded(confidence: 0.0);

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert - With 0 confidence on success, alpha + 0, beta + 1
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta + 1.0);
    }

    /// <summary>
    /// Verifies that half confidence gives balanced credit.
    /// </summary>
    [Test]
    public async Task WithOutcome_HalfConfidenceSuccess_GivesBalancedCredit()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded(confidence: 0.5);

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 0.5);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta + 0.5);
    }

    // =============================================================================
    // C. No Confidence (Default Behavior) Tests
    // =============================================================================

    /// <summary>
    /// Verifies that success without confidence defaults to full credit.
    /// </summary>
    [Test]
    public async Task WithOutcome_SuccessNoConfidence_DefaultsToFullCredit()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 1.0);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta);
    }

    /// <summary>
    /// Verifies that failure without confidence defaults to full penalty.
    /// </summary>
    [Test]
    public async Task WithOutcome_FailureNoConfidence_DefaultsToFullPenalty()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Failed();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta + 1.0);
    }

    // =============================================================================
    // D. Preserves Other Properties Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithOutcome preserves AgentId.
    /// </summary>
    [Test]
    public async Task WithOutcome_PreservesAgentId()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-123", "General");
        var outcome = AgentOutcome.Succeeded();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.AgentId).IsEqualTo("agent-123");
    }

    /// <summary>
    /// Verifies that WithOutcome preserves TaskCategory.
    /// </summary>
    [Test]
    public async Task WithOutcome_PreservesTaskCategory()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "CodeGeneration");
        var outcome = AgentOutcome.Succeeded();

        // Act
        var updated = belief.WithOutcome(outcome);

        // Assert
        await Assert.That(updated.TaskCategory).IsEqualTo("CodeGeneration");
    }

    // =============================================================================
    // E. Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithOutcome throws on null outcome.
    /// </summary>
    [Test]
    public async Task WithOutcome_NullOutcome_ThrowsArgumentNullException()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");

        // Act & Assert
        await Assert.That(() => belief.WithOutcome(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // F. Multiple Updates Tests
    // =============================================================================

    /// <summary>
    /// Verifies that multiple successes accumulate correctly.
    /// </summary>
    [Test]
    public async Task WithOutcome_MultipleSuccesses_AccumulatesCorrectly()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded();

        // Act
        var updated = belief
            .WithOutcome(outcome)
            .WithOutcome(outcome)
            .WithOutcome(outcome);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 3.0);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta);
        await Assert.That(updated.ObservationCount).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that mixed outcomes accumulate correctly.
    /// </summary>
    [Test]
    public async Task WithOutcome_MixedOutcomes_AccumulatesCorrectly()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var success = AgentOutcome.Succeeded();
        var failure = AgentOutcome.Failed();

        // Act
        var updated = belief
            .WithOutcome(success)
            .WithOutcome(failure)
            .WithOutcome(success);

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(belief.Alpha + 2.0);
        await Assert.That(updated.Beta).IsEqualTo(belief.Beta + 1.0);
        await Assert.That(updated.ObservationCount).IsEqualTo(3);
    }

    // =============================================================================
    // G. Equivalence with WithSuccess/WithFailure Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithOutcome(success) is equivalent to WithSuccess().
    /// </summary>
    [Test]
    public async Task WithOutcome_SuccessNoConfidence_EquivalentToWithSuccess()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Succeeded();

        // Act
        var withOutcome = belief.WithOutcome(outcome);
        var withSuccess = belief.WithSuccess();

        // Assert
        await Assert.That(withOutcome.Alpha).IsEqualTo(withSuccess.Alpha);
        await Assert.That(withOutcome.Beta).IsEqualTo(withSuccess.Beta);
        await Assert.That(withOutcome.ObservationCount).IsEqualTo(withSuccess.ObservationCount);
    }

    /// <summary>
    /// Verifies that WithOutcome(failure) is equivalent to WithFailure().
    /// </summary>
    [Test]
    public async Task WithOutcome_FailureNoConfidence_EquivalentToWithFailure()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("agent-1", "General");
        var outcome = AgentOutcome.Failed();

        // Act
        var withOutcome = belief.WithOutcome(outcome);
        var withFailure = belief.WithFailure();

        // Assert
        await Assert.That(withOutcome.Alpha).IsEqualTo(withFailure.Alpha);
        await Assert.That(withOutcome.Beta).IsEqualTo(withFailure.Beta);
        await Assert.That(withOutcome.ObservationCount).IsEqualTo(withFailure.ObservationCount);
    }
}
