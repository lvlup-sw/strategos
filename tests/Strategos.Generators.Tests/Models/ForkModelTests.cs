// -----------------------------------------------------------------------
// <copyright file="ForkModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="ForkModel"/>.
/// </summary>
[Property("Category", "Unit")]
public class ForkModelTests
{
    private static List<StepModel> Steps(params string[] names)
        => [.. names.Select(n => StepModel.Create(n, $"TestNamespace.{n}"))];

    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid params returns a model.
    /// </summary>
    [Test]
    public async Task Create_WithValidParams_ReturnsModel()
    {
        // Arrange
        var path1 = ForkPathModel.Create(0, Steps("Step1"), false, false);
        var path2 = ForkPathModel.Create(1, Steps("Step2"), false, false);

        // Act
        var model = ForkModel.Create(
            forkId: "fork1",
            previousStepName: "StartStep",
            paths: new List<ForkPathModel> { path1, path2 },
            joinStepName: "JoinStep");

        // Assert
        await Assert.That(model.ForkId).IsEqualTo("fork1");
        await Assert.That(model.PreviousStepName).IsEqualTo("StartStep");
        await Assert.That(model.Paths.Count).IsEqualTo(2);
        await Assert.That(model.JoinStepName).IsEqualTo("JoinStep");
    }

    /// <summary>
    /// Verifies that Create throws for less than two paths.
    /// </summary>
    [Test]
    public async Task Create_WithLessThanTwoPaths_ThrowsArgumentException()
    {
        // Arrange
        var path1 = ForkPathModel.Create(0, Steps("Step1"), false, false);

        // Act & Assert
        await Assert.That(() => ForkModel.Create(
            forkId: "fork1",
            previousStepName: "StartStep",
            paths: new List<ForkPathModel> { path1 },
            joinStepName: "JoinStep"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws for null paths.
    /// </summary>
    [Test]
    public async Task Create_WithNullPaths_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ForkModel.Create(
            forkId: "fork1",
            previousStepName: "StartStep",
            paths: null!,
            joinStepName: "JoinStep"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for null fork ID.
    /// </summary>
    [Test]
    public async Task Create_WithNullForkId_ThrowsArgumentException()
    {
        // Arrange
        var paths = CreateTwoPaths();

        // Act & Assert
        await Assert.That(() => ForkModel.Create(
            forkId: null!,
            previousStepName: "StartStep",
            paths: paths,
            joinStepName: "JoinStep"))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // B. Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that HasAnyFailureHandler returns true when any path has handler.
    /// </summary>
    [Test]
    public async Task HasAnyFailureHandler_WithHandler_ReturnsTrue()
    {
        // Arrange
        var path1 = ForkPathModel.Create(0, Steps("Step1"), hasFailureHandler: true, isTerminalOnFailure: false);
        var path2 = ForkPathModel.Create(1, Steps("Step2"), hasFailureHandler: false, isTerminalOnFailure: false);

        var model = ForkModel.Create("fork1", "StartStep", new List<ForkPathModel> { path1, path2 }, "JoinStep");

        // Assert
        await Assert.That(model.HasAnyFailureHandler).IsTrue();
    }

    /// <summary>
    /// Verifies that HasAnyFailureHandler returns false when no paths have handlers.
    /// </summary>
    [Test]
    public async Task HasAnyFailureHandler_WithNoHandlers_ReturnsFalse()
    {
        // Arrange
        var paths = CreateTwoPaths();
        var model = ForkModel.Create("fork1", "StartStep", paths, "JoinStep");

        // Assert
        await Assert.That(model.HasAnyFailureHandler).IsFalse();
    }

    /// <summary>
    /// Verifies that PathCount returns correct count.
    /// </summary>
    [Test]
    public async Task PathCount_ReturnsCorrectCount()
    {
        // Arrange
        var paths = CreateTwoPaths();
        var model = ForkModel.Create("fork1", "StartStep", paths, "JoinStep");

        // Assert
        await Assert.That(model.PathCount).IsEqualTo(2);
    }

    // =============================================================================
    // Test Helpers
    // =============================================================================

    private static List<ForkPathModel> CreateTwoPaths()
    {
        return new List<ForkPathModel>
        {
            ForkPathModel.Create(0, Steps("Step1"), false, false),
            ForkPathModel.Create(1, Steps("Step2"), false, false),
        };
    }
}
