// =============================================================================
// <copyright file="ForkPointDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ForkPointDefinition"/> record.
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
public class ForkPointDefinitionTests
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
        var path1Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var path2Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ValidateStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: path1Steps),
            ForkPathDefinition.Create(pathIndex: 1, steps: path2Steps),
        };

        // Act
        var definition = ForkPointDefinition.Create(
            fromStepId: "step-1",
            paths: paths,
            joinStepId: "join-step");

        // Assert
        await Assert.That(definition).IsNotNull();
    }

    /// <summary>
    /// Verifies that Create generates a unique ForkPointId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueForkPointId()
    {
        // Arrange
        var path1Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var path2Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ValidateStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: path1Steps),
            ForkPathDefinition.Create(pathIndex: 1, steps: path2Steps),
        };

        // Act
        var definition1 = ForkPointDefinition.Create("step-1", paths, "join-step");
        var definition2 = ForkPointDefinition.Create("step-2", paths, "join-step");

        // Assert
        await Assert.That(definition1.ForkPointId).IsNotEqualTo(definition2.ForkPointId);
    }

    /// <summary>
    /// Verifies that Create with null fromStepId throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullFromStepId_ThrowsArgumentNullException()
    {
        // Arrange
        var path1Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var path2Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ValidateStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: path1Steps),
            ForkPathDefinition.Create(pathIndex: 1, steps: path2Steps),
        };

        // Act & Assert
        await Assert.That(() => ForkPointDefinition.Create(
            fromStepId: null!,
            paths: paths,
            joinStepId: "join-step"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with null paths throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullPaths_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ForkPointDefinition.Create(
            fromStepId: "step-1",
            paths: null!,
            joinStepId: "join-step"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with null joinStepId throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullJoinStepId_ThrowsArgumentNullException()
    {
        // Arrange
        var path1Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var path2Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ValidateStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: path1Steps),
            ForkPathDefinition.Create(pathIndex: 1, steps: path2Steps),
        };

        // Act & Assert
        await Assert.That(() => ForkPointDefinition.Create(
            fromStepId: "step-1",
            paths: paths,
            joinStepId: null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with less than two paths throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Create_WithLessThanTwoPaths_ThrowsArgumentException()
    {
        // Arrange
        var pathSteps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: pathSteps),
        };

        // Act & Assert
        await Assert.That(() => ForkPointDefinition.Create(
            fromStepId: "step-1",
            paths: paths,
            joinStepId: "join-step"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create with zero paths throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Create_WithZeroPaths_ThrowsArgumentException()
    {
        // Arrange
        var paths = new List<ForkPathDefinition>();

        // Act & Assert
        await Assert.That(() => ForkPointDefinition.Create(
            fromStepId: "step-1",
            paths: paths,
            joinStepId: "join-step"))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // B. FromStepId Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create stores fromStepId correctly.
    /// </summary>
    [Test]
    public async Task Create_WithFromStepId_StoresFromStepIdCorrectly()
    {
        // Arrange
        var path1Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var path2Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ValidateStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: path1Steps),
            ForkPathDefinition.Create(pathIndex: 1, steps: path2Steps),
        };

        // Act
        var definition = ForkPointDefinition.Create(
            fromStepId: "my-origin-step",
            paths: paths,
            joinStepId: "join-step");

        // Assert
        await Assert.That(definition.FromStepId).IsEqualTo("my-origin-step");
    }

    // =============================================================================
    // C. Paths Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create stores paths correctly.
    /// </summary>
    [Test]
    public async Task Create_WithPaths_StoresPathsCorrectly()
    {
        // Arrange
        var path1Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var path2Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ValidateStep)) };
        var path3Steps = new List<StepDefinition> { StepDefinition.Create(typeof(CompleteStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: path1Steps),
            ForkPathDefinition.Create(pathIndex: 1, steps: path2Steps),
            ForkPathDefinition.Create(pathIndex: 2, steps: path3Steps),
        };

        // Act
        var definition = ForkPointDefinition.Create(
            fromStepId: "step-1",
            paths: paths,
            joinStepId: "join-step");

        // Assert
        await Assert.That(definition.Paths).HasCount().EqualTo(3);
        await Assert.That(definition.Paths[0].PathIndex).IsEqualTo(0);
        await Assert.That(definition.Paths[1].PathIndex).IsEqualTo(1);
        await Assert.That(definition.Paths[2].PathIndex).IsEqualTo(2);
    }

    // =============================================================================
    // D. JoinStepId Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create stores joinStepId correctly.
    /// </summary>
    [Test]
    public async Task Create_WithJoinStepId_StoresJoinStepIdCorrectly()
    {
        // Arrange
        var path1Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ProcessStep)) };
        var path2Steps = new List<StepDefinition> { StepDefinition.Create(typeof(ValidateStep)) };
        var paths = new List<ForkPathDefinition>
        {
            ForkPathDefinition.Create(pathIndex: 0, steps: path1Steps),
            ForkPathDefinition.Create(pathIndex: 1, steps: path2Steps),
        };

        // Act
        var definition = ForkPointDefinition.Create(
            fromStepId: "step-1",
            paths: paths,
            joinStepId: "my-join-step");

        // Assert
        await Assert.That(definition.JoinStepId).IsEqualTo("my-join-step");
    }
}
