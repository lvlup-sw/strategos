// =============================================================================
// <copyright file="StepResultTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Steps;

namespace Strategos.Tests.Steps;

/// <summary>
/// Unit tests for <see cref="StepResult{TState}"/>.
/// </summary>
[Property("Category", "Unit")]
public class StepResultTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that FromState creates a result with the provided state.
    /// </summary>
    [Test]
    public async Task FromState_WithState_CreatesResult()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };

        // Act
        var result = StepResult<TestWorkflowState>.FromState(state);

        // Assert
        await Assert.That(result.UpdatedState).IsEqualTo(state);
        await Assert.That(result.Confidence).IsNull();
        await Assert.That(result.Metadata).IsNull();
    }

    /// <summary>
    /// Verifies that FromState throws for null state.
    /// </summary>
    [Test]
    public async Task FromState_WithNullState_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => StepResult<TestWorkflowState>.FromState(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that WithConfidence creates a result with confidence score.
    /// </summary>
    [Test]
    public async Task WithConfidence_SetsConfidenceScore()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        const double confidence = 0.95;

        // Act
        var result = StepResult<TestWorkflowState>.WithConfidence(state, confidence);

        // Assert
        await Assert.That(result.UpdatedState).IsEqualTo(state);
        await Assert.That(result.Confidence).IsEqualTo(confidence);
    }

    /// <summary>
    /// Verifies that WithConfidence validates confidence is within range.
    /// </summary>
    [Test]
    public async Task WithConfidence_WithInvalidConfidence_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };

        // Act & Assert - Confidence > 1
        await Assert.That(() => StepResult<TestWorkflowState>.WithConfidence(state, 1.5))
            .Throws<ArgumentOutOfRangeException>();

        // Act & Assert - Confidence < 0
        await Assert.That(() => StepResult<TestWorkflowState>.WithConfidence(state, -0.1))
            .Throws<ArgumentOutOfRangeException>();
    }

    // =============================================================================
    // B. Metadata Tests
    // =============================================================================

    /// <summary>
    /// Verifies that result can be created with metadata.
    /// </summary>
    [Test]
    public async Task Constructor_WithMetadata_SetsMetadata()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var metadata = new Dictionary<string, object>
        {
            ["ExecutionTimeMs"] = 150L,
            ["ModelUsed"] = "gpt-4",
        };

        // Act
        var result = new StepResult<TestWorkflowState>(state, null, metadata);

        // Assert
        await Assert.That(result.Metadata).IsNotNull();
        await Assert.That(result.Metadata!.Count).IsEqualTo(2);
        await Assert.That(result.Metadata["ExecutionTimeMs"]).IsEqualTo(150L);
        await Assert.That(result.Metadata["ModelUsed"]).IsEqualTo("gpt-4");
    }

    /// <summary>
    /// Verifies that WithMetadata method creates new result with metadata.
    /// </summary>
    [Test]
    public async Task WithMetadata_CreatesResultWithMetadata()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var original = StepResult<TestWorkflowState>.FromState(state);
        var metadata = new Dictionary<string, object> { ["Key"] = "Value" };

        // Act
        var result = original.WithMetadata(metadata);

        // Assert
        await Assert.That(result.Metadata).IsNotNull();
        await Assert.That(result.Metadata!["Key"]).IsEqualTo("Value");
    }

    // =============================================================================
    // C. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepResult is an immutable record.
    /// </summary>
    [Test]
    public async Task StepResult_IsImmutableRecord()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var original = StepResult<TestWorkflowState>.FromState(state);

        // Act - Use record with syntax
        var modified = original with { Confidence = 0.8 };

        // Assert
        await Assert.That(original.Confidence).IsNull();
        await Assert.That(modified.Confidence).IsEqualTo(0.8);
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that UpdatedState is preserved through with syntax.
    /// </summary>
    [Test]
    public async Task WithSyntax_PreservesState()
    {
        // Arrange
        var state = new TestWorkflowState { WorkflowId = Guid.NewGuid(), OrderId = "ORD-123" };
        var original = StepResult<TestWorkflowState>.FromState(state);

        // Act
        var modified = original with { Confidence = 0.9 };

        // Assert
        await Assert.That(modified.UpdatedState).IsEqualTo(state);
    }
}
