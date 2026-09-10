// =============================================================================
// <copyright file="AgentBeliefTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Selection;

namespace Strategos.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="AgentBelief"/> covering Beta distribution
/// belief state for Thompson Sampling agent selection.
/// </summary>
[Property("Category", "Unit")]
public class AgentBeliefTests
{
    // =============================================================================
    // A. CreatePrior Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CreatePrior sets default Alpha value.
    /// </summary>
    [Test]
    public async Task CreatePrior_SetsDefaultAlpha()
    {
        // Act
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");

        // Assert
        await Assert.That(belief.Alpha).IsEqualTo(AgentBelief.DefaultPriorAlpha);
        await Assert.That(belief.Alpha).IsEqualTo(2.0);
    }

    /// <summary>
    /// Verifies that CreatePrior sets default Beta value.
    /// </summary>
    [Test]
    public async Task CreatePrior_SetsDefaultBeta()
    {
        // Act
        var belief = AgentBelief.CreatePrior("claude-3", "DataAnalysis");

        // Assert
        await Assert.That(belief.Beta).IsEqualTo(AgentBelief.DefaultPriorBeta);
        await Assert.That(belief.Beta).IsEqualTo(2.0);
    }

    /// <summary>
    /// Verifies that CreatePrior sets agent ID correctly.
    /// </summary>
    [Test]
    public async Task CreatePrior_SetsAgentId()
    {
        // Act
        var belief = AgentBelief.CreatePrior("gpt-4o-mini", "General");

        // Assert
        await Assert.That(belief.AgentId).IsEqualTo("gpt-4o-mini");
    }

    /// <summary>
    /// Verifies that CreatePrior sets task category correctly.
    /// </summary>
    [Test]
    public async Task CreatePrior_SetsTaskCategory()
    {
        // Act
        var belief = AgentBelief.CreatePrior("llama-3", "WebSearch");

        // Assert
        await Assert.That(belief.TaskCategory).IsEqualTo("WebSearch");
    }

    /// <summary>
    /// Verifies that CreatePrior initializes observation count to zero.
    /// </summary>
    [Test]
    public async Task CreatePrior_SetsObservationCountToZero()
    {
        // Act
        var belief = AgentBelief.CreatePrior("mistral", "Reasoning");

        // Assert
        await Assert.That(belief.ObservationCount).IsEqualTo(0);
    }

    // =============================================================================
    // B. WithSuccess Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithSuccess increments Alpha by 1.
    /// </summary>
    [Test]
    public async Task WithSuccess_IncrementsAlphaByOne()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");
        var originalAlpha = belief.Alpha;

        // Act
        var updated = belief.WithSuccess();

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(originalAlpha + 1.0);
        await Assert.That(updated.Alpha).IsEqualTo(3.0);
    }

    /// <summary>
    /// Verifies that WithSuccess does not change Beta.
    /// </summary>
    [Test]
    public async Task WithSuccess_DoesNotChangeBeta()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");
        var originalBeta = belief.Beta;

        // Act
        var updated = belief.WithSuccess();

        // Assert
        await Assert.That(updated.Beta).IsEqualTo(originalBeta);
    }

    /// <summary>
    /// Verifies that WithSuccess increments observation count.
    /// </summary>
    [Test]
    public async Task WithSuccess_IncrementsObservationCount()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");

        // Act
        var updated = belief.WithSuccess();

        // Assert
        await Assert.That(updated.ObservationCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that WithSuccess updates timestamp.
    /// </summary>
    [Test]
    public async Task WithSuccess_UpdatesTimestamp()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");
        var originalTimestamp = belief.UpdatedAt;

        // Small delay to ensure timestamp difference
        await Task.Delay(10).ConfigureAwait(false);

        // Act
        var updated = belief.WithSuccess();

        // Assert
        await Assert.That(updated.UpdatedAt).IsGreaterThanOrEqualTo(originalTimestamp);
    }

    /// <summary>
    /// Verifies that WithSuccess returns new immutable instance.
    /// </summary>
    [Test]
    public async Task WithSuccess_ReturnsNewInstance()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");

        // Act
        var updated = belief.WithSuccess();

        // Assert
        await Assert.That(updated).IsNotEqualTo(belief);
        await Assert.That(belief.Alpha).IsEqualTo(2.0); // Original unchanged
    }

    // =============================================================================
    // C. WithFailure Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithFailure increments Beta by 1.
    /// </summary>
    [Test]
    public async Task WithFailure_IncrementsBetaByOne()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");
        var originalBeta = belief.Beta;

        // Act
        var updated = belief.WithFailure();

        // Assert
        await Assert.That(updated.Beta).IsEqualTo(originalBeta + 1.0);
        await Assert.That(updated.Beta).IsEqualTo(3.0);
    }

    /// <summary>
    /// Verifies that WithFailure does not change Alpha.
    /// </summary>
    [Test]
    public async Task WithFailure_DoesNotChangeAlpha()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");
        var originalAlpha = belief.Alpha;

        // Act
        var updated = belief.WithFailure();

        // Assert
        await Assert.That(updated.Alpha).IsEqualTo(originalAlpha);
    }

    /// <summary>
    /// Verifies that WithFailure increments observation count.
    /// </summary>
    [Test]
    public async Task WithFailure_IncrementsObservationCount()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");

        // Act
        var updated = belief.WithFailure();

        // Assert
        await Assert.That(updated.ObservationCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that WithFailure returns new immutable instance.
    /// </summary>
    [Test]
    public async Task WithFailure_ReturnsNewInstance()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");

        // Act
        var updated = belief.WithFailure();

        // Assert
        await Assert.That(updated).IsNotEqualTo(belief);
        await Assert.That(belief.Beta).IsEqualTo(2.0); // Original unchanged
    }

    // =============================================================================
    // D. Mean Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Mean returns Alpha / (Alpha + Beta).
    /// </summary>
    [Test]
    public async Task Mean_ReturnsCorrectValue()
    {
        // Arrange - Default prior: Alpha=2, Beta=2
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");

        // Act
        var mean = belief.Mean;

        // Assert - Expected: 2 / (2 + 2) = 0.5
        await Assert.That(mean).IsEqualTo(0.5);
    }

    /// <summary>
    /// Verifies that Mean increases after success.
    /// </summary>
    [Test]
    public async Task Mean_IncreasesAfterSuccess()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");
        var originalMean = belief.Mean;

        // Act
        var updated = belief.WithSuccess();

        // Assert - Alpha=3, Beta=2: 3/(3+2) = 0.6 > 0.5
        await Assert.That(updated.Mean).IsGreaterThan(originalMean);
        await Assert.That(updated.Mean).IsEqualTo(0.6);
    }

    /// <summary>
    /// Verifies that Mean decreases after failure.
    /// </summary>
    [Test]
    public async Task Mean_DecreasesAfterFailure()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");
        var originalMean = belief.Mean;

        // Act
        var updated = belief.WithFailure();

        // Assert - Alpha=2, Beta=3: 2/(2+3) = 0.4 < 0.5
        await Assert.That(updated.Mean).IsLessThan(originalMean);
        await Assert.That(updated.Mean).IsEqualTo(0.4);
    }

    // =============================================================================
    // E. Variance Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Variance returns correct formula: αβ / ((α+β)²(α+β+1)).
    /// </summary>
    [Test]
    public async Task Variance_ReturnsCorrectValue()
    {
        // Arrange - Default prior: Alpha=2, Beta=2
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");

        // Act
        var variance = belief.Variance;

        // Assert - Expected: (2*2) / ((4)² * 5) = 4 / 80 = 0.05
        await Assert.That(variance).IsEqualTo(0.05);
    }

    /// <summary>
    /// Verifies that Variance decreases with more observations.
    /// </summary>
    [Test]
    public async Task Variance_DecreasesWithMoreObservations()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");
        var originalVariance = belief.Variance;

        // Act - Add several observations
        var updated = belief.WithSuccess().WithSuccess().WithFailure();

        // Assert - More observations = lower variance
        await Assert.That(updated.Variance).IsLessThan(originalVariance);
    }

    // =============================================================================
    // F. Id Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Id is composite of AgentId and TaskCategory.
    /// </summary>
    [Test]
    public async Task Id_IsCompositeKey()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "CodeGeneration");

        // Act
        var id = belief.Id;

        // Assert
        await Assert.That(id).IsEqualTo("gpt-4o_CodeGeneration");
    }

    /// <summary>
    /// Verifies that Id contains underscore separator.
    /// </summary>
    [Test]
    public async Task Id_ContainsUnderscoreSeparator()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("claude-3-opus", "DataAnalysis");

        // Act
        var id = belief.Id;

        // Assert
        await Assert.That(id).Contains("_");
        await Assert.That(id).IsEqualTo("claude-3-opus_DataAnalysis");
    }

    // =============================================================================
    // G. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that original belief is unchanged after WithSuccess.
    /// </summary>
    [Test]
    public async Task Immutability_OriginalUnchangedAfterSuccess()
    {
        // Arrange
        var original = AgentBelief.CreatePrior("gpt-4o", "General");
        var originalAlpha = original.Alpha;
        var originalBeta = original.Beta;
        var originalCount = original.ObservationCount;

        // Act
        _ = original.WithSuccess();

        // Assert - Original should be unchanged
        await Assert.That(original.Alpha).IsEqualTo(originalAlpha);
        await Assert.That(original.Beta).IsEqualTo(originalBeta);
        await Assert.That(original.ObservationCount).IsEqualTo(originalCount);
    }

    /// <summary>
    /// Verifies that original belief is unchanged after WithFailure.
    /// </summary>
    [Test]
    public async Task Immutability_OriginalUnchangedAfterFailure()
    {
        // Arrange
        var original = AgentBelief.CreatePrior("gpt-4o", "General");
        var originalAlpha = original.Alpha;
        var originalBeta = original.Beta;
        var originalCount = original.ObservationCount;

        // Act
        _ = original.WithFailure();

        // Assert - Original should be unchanged
        await Assert.That(original.Alpha).IsEqualTo(originalAlpha);
        await Assert.That(original.Beta).IsEqualTo(originalBeta);
        await Assert.That(original.ObservationCount).IsEqualTo(originalCount);
    }

    // =============================================================================
    // H. Chained Updates Tests
    // =============================================================================

    /// <summary>
    /// Verifies multiple chained successes update correctly.
    /// </summary>
    [Test]
    public async Task ChainedSuccesses_UpdatesCorrectly()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");

        // Act - 3 successes
        var updated = belief.WithSuccess().WithSuccess().WithSuccess();

        // Assert - Alpha: 2 + 3 = 5, Beta: 2
        await Assert.That(updated.Alpha).IsEqualTo(5.0);
        await Assert.That(updated.Beta).IsEqualTo(2.0);
        await Assert.That(updated.ObservationCount).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies multiple chained failures update correctly.
    /// </summary>
    [Test]
    public async Task ChainedFailures_UpdatesCorrectly()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");

        // Act - 2 failures
        var updated = belief.WithFailure().WithFailure();

        // Assert - Alpha: 2, Beta: 2 + 2 = 4
        await Assert.That(updated.Alpha).IsEqualTo(2.0);
        await Assert.That(updated.Beta).IsEqualTo(4.0);
        await Assert.That(updated.ObservationCount).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies mixed success and failure chain.
    /// </summary>
    [Test]
    public async Task MixedChain_UpdatesCorrectly()
    {
        // Arrange
        var belief = AgentBelief.CreatePrior("gpt-4o", "General");

        // Act - 2 successes, 1 failure
        var updated = belief.WithSuccess().WithFailure().WithSuccess();

        // Assert - Alpha: 2 + 2 = 4, Beta: 2 + 1 = 3
        await Assert.That(updated.Alpha).IsEqualTo(4.0);
        await Assert.That(updated.Beta).IsEqualTo(3.0);
        await Assert.That(updated.ObservationCount).IsEqualTo(3);
    }
}
