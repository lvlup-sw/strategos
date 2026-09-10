// =============================================================================
// <copyright file="LoopDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="LoopDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class LoopDefinitionTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid params returns a definition.
    /// </summary>
    [Test]
    public async Task Create_WithValidParams_ReturnsDefinition()
    {
        // Arrange
        const string loopName = "Refinement";
        const string fromStepId = "step-123";
        const int maxIterations = 5;
        var bodySteps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(TestCritiqueStep)),
            StepDefinition.Create(typeof(TestRefineStep)),
        };

        // Act
        var loop = LoopDefinition.Create(loopName, fromStepId, maxIterations, bodySteps);

        // Assert
        await Assert.That(loop.LoopName).IsEqualTo("Refinement");
        await Assert.That(loop.FromStepId).IsEqualTo("step-123");
        await Assert.That(loop.MaxIterations).IsEqualTo(5);
        await Assert.That(loop.BodySteps.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that Create generates a unique LoopId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueLoopId()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };

        // Act
        var loop1 = LoopDefinition.Create("Loop1", "step-1", 5, bodySteps);
        var loop2 = LoopDefinition.Create("Loop2", "step-2", 5, bodySteps);

        // Assert
        await Assert.That(loop1.LoopId).IsNotNull();
        await Assert.That(loop1.LoopId).IsNotEqualTo(loop2.LoopId);
    }

    /// <summary>
    /// Verifies that Create throws for null loop name.
    /// </summary>
    [Test]
    public async Task Create_WithNullLoopName_ThrowsArgumentNullException()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };

        // Act & Assert
        await Assert.That(() => LoopDefinition.Create(null!, "step-1", 5, bodySteps))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for null from step ID.
    /// </summary>
    [Test]
    public async Task Create_WithNullFromStepId_ThrowsArgumentNullException()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };

        // Act & Assert
        await Assert.That(() => LoopDefinition.Create("Loop", null!, 5, bodySteps))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for null body steps.
    /// </summary>
    [Test]
    public async Task Create_WithNullBodySteps_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => LoopDefinition.Create("Loop", "step-1", 5, null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for zero max iterations.
    /// </summary>
    [Test]
    public async Task Create_WithZeroMaxIterations_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };

        // Act & Assert
        await Assert.That(() => LoopDefinition.Create("Loop", "step-1", 0, bodySteps))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that Create throws for negative max iterations.
    /// </summary>
    [Test]
    public async Task Create_WithNegativeMaxIterations_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };

        // Act & Assert
        await Assert.That(() => LoopDefinition.Create("Loop", "step-1", -1, bodySteps))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that Create throws for empty body steps.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyBodySteps_ThrowsArgumentException()
    {
        // Arrange
        var bodySteps = new List<StepDefinition>();

        // Act & Assert
        await Assert.That(() => LoopDefinition.Create("Loop", "step-1", 5, bodySteps))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // B. Default Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ContinuationStepId defaults to null.
    /// </summary>
    [Test]
    public async Task Create_ContinuationStepId_DefaultsToNull()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };

        // Act
        var loop = LoopDefinition.Create("Loop", "step-1", 5, bodySteps);

        // Assert
        await Assert.That(loop.ContinuationStepId).IsNull();
    }

    // =============================================================================
    // C. WithContinuation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithContinuation sets the continuation step ID.
    /// </summary>
    [Test]
    public async Task WithContinuation_SetsContinuationStepId()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };
        var loop = LoopDefinition.Create("Loop", "step-1", 5, bodySteps);

        // Act
        var updated = loop.WithContinuation("step-next");

        // Assert
        await Assert.That(updated.ContinuationStepId).IsEqualTo("step-next");
    }

    /// <summary>
    /// Verifies that WithContinuation preserves original instance.
    /// </summary>
    [Test]
    public async Task WithContinuation_PreservesOriginal()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };
        var original = LoopDefinition.Create("Loop", "step-1", 5, bodySteps);

        // Act
        var updated = original.WithContinuation("step-next");

        // Assert
        await Assert.That(original.ContinuationStepId).IsNull();
        await Assert.That(updated.ContinuationStepId).IsNotNull();
    }

    /// <summary>
    /// Verifies that WithContinuation throws for null step ID.
    /// </summary>
    [Test]
    public async Task WithContinuation_WithNullStepId_ThrowsArgumentNullException()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };
        var loop = LoopDefinition.Create("Loop", "step-1", 5, bodySteps);

        // Act & Assert
        await Assert.That(() => loop.WithContinuation(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that LoopDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task LoopDefinition_IsImmutableRecord()
    {
        // Arrange
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestCritiqueStep)) };
        var original = LoopDefinition.Create("Loop", "step-1", 5, bodySteps);

        // Act - Use record with syntax
        var modified = original with { MaxIterations = 10 };

        // Assert
        await Assert.That(original.MaxIterations).IsEqualTo(5);
        await Assert.That(modified.MaxIterations).IsEqualTo(10);
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that body steps list is preserved correctly.
    /// </summary>
    [Test]
    public async Task BodySteps_PreservesStepOrder()
    {
        // Arrange
        var bodySteps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(TestCritiqueStep)),
            StepDefinition.Create(typeof(TestRefineStep)),
        };

        // Act
        var loop = LoopDefinition.Create("Loop", "step-1", 5, bodySteps);

        // Assert
        await Assert.That(loop.BodySteps[0].StepTypeName).IsEqualTo("TestCritiqueStep");
        await Assert.That(loop.BodySteps[1].StepTypeName).IsEqualTo("TestRefineStep");
    }
}

/// <summary>
/// Test step class for loop testing.
/// </summary>
internal sealed class TestCritiqueStep
{
}

/// <summary>
/// Test step class for loop testing.
/// </summary>
internal sealed class TestRefineStep
{
}
