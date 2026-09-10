// =============================================================================
// <copyright file="ForkPathDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ForkPathDefinition"/> record.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Create factory method produces valid definitions</description></item>
///   <item><description>Guard clauses validate inputs</description></item>
///   <item><description>Properties are correctly initialized</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ForkPathDefinitionTests
{
    // =============================================================================
    // A. Create Factory Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create returns a non-null definition with valid inputs.
    /// </summary>
    [Test]
    public async Task Create_WithValidInputs_ReturnsDefinition()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(ProcessStep)),
        };

        // Act
        var definition = ForkPathDefinition.Create(
            pathIndex: 0,
            steps: steps);

        // Assert
        await Assert.That(definition).IsNotNull();
    }

    /// <summary>
    /// Verifies that Create generates a unique PathId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniquePathId()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(ProcessStep)),
        };

        // Act
        var definition1 = ForkPathDefinition.Create(pathIndex: 0, steps: steps);
        var definition2 = ForkPathDefinition.Create(pathIndex: 1, steps: steps);

        // Assert
        await Assert.That(definition1.PathId).IsNotEqualTo(definition2.PathId);
    }

    /// <summary>
    /// Verifies that Create with null steps throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullSteps_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ForkPathDefinition.Create(
            pathIndex: 0,
            steps: null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with empty steps throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Create_WithEmptySteps_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => ForkPathDefinition.Create(
            pathIndex: 0,
            steps: []))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create with negative pathIndex throws ArgumentOutOfRangeException.
    /// </summary>
    [Test]
    public async Task Create_WithNegativePathIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(ProcessStep)),
        };

        // Act & Assert
        await Assert.That(() => ForkPathDefinition.Create(
            pathIndex: -1,
            steps: steps))
            .Throws<ArgumentOutOfRangeException>();
    }

    // =============================================================================
    // B. PathIndex Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create stores pathIndex correctly.
    /// </summary>
    [Test]
    public async Task Create_WithPathIndex_StoresPathIndexCorrectly()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(ProcessStep)),
        };

        // Act
        var definition = ForkPathDefinition.Create(pathIndex: 2, steps: steps);

        // Assert
        await Assert.That(definition.PathIndex).IsEqualTo(2);
    }

    // =============================================================================
    // C. Steps Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create stores steps correctly.
    /// </summary>
    [Test]
    public async Task Create_WithSteps_StoresStepsCorrectly()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(ProcessStep)),
            StepDefinition.Create(typeof(CompleteStep)),
        };

        // Act
        var definition = ForkPathDefinition.Create(pathIndex: 0, steps: steps);

        // Assert
        await Assert.That(definition.Steps).HasCount().EqualTo(2);
        await Assert.That(definition.Steps[0].StepType).IsEqualTo(typeof(ProcessStep));
        await Assert.That(definition.Steps[1].StepType).IsEqualTo(typeof(CompleteStep));
    }

    // =============================================================================
    // D. FailureHandler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create without failure handler sets FailureHandler to null.
    /// </summary>
    [Test]
    public async Task Create_WithoutFailureHandler_HasNullFailureHandler()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(ProcessStep)),
        };

        // Act
        var definition = ForkPathDefinition.Create(pathIndex: 0, steps: steps);

        // Assert
        await Assert.That(definition.FailureHandler).IsNull();
    }

    /// <summary>
    /// Verifies that Create with failure handler stores it correctly.
    /// </summary>
    [Test]
    public async Task Create_WithFailureHandler_StoresFailureHandler()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(ProcessStep)),
        };
        var failureSteps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(LogFailureStep)),
        };
        var failureHandler = FailureHandlerDefinition.Create(
            FailureHandlerScope.Step,
            failureSteps,
            isTerminal: true);

        // Act
        var definition = ForkPathDefinition.Create(
            pathIndex: 0,
            steps: steps,
            failureHandler: failureHandler);

        // Assert
        await Assert.That(definition.FailureHandler).IsNotNull();
        await Assert.That(definition.FailureHandler!.IsTerminal).IsTrue();
    }
}
