// =============================================================================
// <copyright file="AgentSelectionFeaturesTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Selection;

namespace Strategos.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="AgentSelection.Features"/> property covering
/// contextual feature exposure in selection results.
/// </summary>
[Property("Category", "Unit")]
public class AgentSelectionFeaturesTests
{
    // =============================================================================
    // A. Features Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AgentSelection can store Features.
    /// </summary>
    [Test]
    public async Task Features_CanBeSet()
    {
        // Arrange
        var features = new TaskFeatures
        {
            Category = TaskCategory.CodeGeneration,
            Complexity = 0.7,
            MatchedKeywords = ["implement", "function"],
        };

        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.CodeGeneration,
            Features = features,
        };

        // Assert
        await Assert.That(selection.Features).IsNotNull();
        await Assert.That(selection.Features!.Category).IsEqualTo(TaskCategory.CodeGeneration);
        await Assert.That(selection.Features.Complexity).IsEqualTo(0.7);
    }

    /// <summary>
    /// Verifies that AgentSelection Features defaults to null.
    /// </summary>
    [Test]
    public async Task Features_DefaultsToNull()
    {
        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.General,
        };

        // Assert
        await Assert.That(selection.Features).IsNull();
    }

    /// <summary>
    /// Verifies that Features can be explicitly set to null.
    /// </summary>
    [Test]
    public async Task Features_CanBeExplicitlyNull()
    {
        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.General,
            Features = null,
        };

        // Assert
        await Assert.That(selection.Features).IsNull();
    }

    // =============================================================================
    // B. Features Consistency Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Features.Category matches TaskCategory.
    /// </summary>
    [Test]
    public async Task Features_CategoryMatchesTaskCategory()
    {
        // Arrange
        var features = TaskFeatures.CreateForCategory(TaskCategory.DataAnalysis);

        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.DataAnalysis,
            Features = features,
        };

        // Assert
        await Assert.That(selection.Features!.Category).IsEqualTo(selection.TaskCategory);
    }

    // =============================================================================
    // C. Features with Matched Keywords Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Features preserves matched keywords.
    /// </summary>
    [Test]
    public async Task Features_PreservesMatchedKeywords()
    {
        // Arrange
        var features = new TaskFeatures
        {
            Category = TaskCategory.WebSearch,
            MatchedKeywords = ["search", "web", "lookup"],
        };

        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.WebSearch,
            Features = features,
        };

        // Assert
        await Assert.That(selection.Features!.MatchedKeywords).HasCount(3);
        await Assert.That(selection.Features.MatchedKeywords).Contains("search");
    }

    // =============================================================================
    // D. Features with Complexity Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Features preserves complexity score.
    /// </summary>
    [Test]
    public async Task Features_PreservesComplexity()
    {
        // Arrange
        var features = new TaskFeatures
        {
            Category = TaskCategory.Reasoning,
            Complexity = 0.85,
        };

        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.Reasoning,
            Features = features,
        };

        // Assert
        await Assert.That(selection.Features!.Complexity).IsEqualTo(0.85);
        await Assert.That(selection.Features.IsComplex).IsTrue();
    }

    /// <summary>
    /// Verifies that simple task features are preserved.
    /// </summary>
    [Test]
    public async Task Features_PreservesSimpleTask()
    {
        // Arrange
        var features = new TaskFeatures
        {
            Category = TaskCategory.TextGeneration,
            Complexity = 0.1,
        };

        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.TextGeneration,
            Features = features,
        };

        // Assert
        await Assert.That(selection.Features!.IsSimple).IsTrue();
        await Assert.That(selection.Features.IsComplex).IsFalse();
    }

    // =============================================================================
    // E. Record Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Features is immutable with 'with' expression.
    /// </summary>
    [Test]
    public async Task Features_ImmutableWithExpression()
    {
        // Arrange
        var originalFeatures = new TaskFeatures { Complexity = 0.3 };
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = TaskCategory.General,
            Features = originalFeatures,
        };

        var newFeatures = new TaskFeatures { Complexity = 0.8 };

        // Act
        var modified = selection with { Features = newFeatures };

        // Assert - Original unchanged
        await Assert.That(selection.Features!.Complexity).IsEqualTo(0.3);
        await Assert.That(modified.Features!.Complexity).IsEqualTo(0.8);
    }

    // =============================================================================
    // F. All Categories with Features Tests
    // =============================================================================

    /// <summary>
    /// Verifies that all TaskCategory values work with Features.
    /// </summary>
    [Test]
    [Arguments(TaskCategory.General)]
    [Arguments(TaskCategory.CodeGeneration)]
    [Arguments(TaskCategory.DataAnalysis)]
    [Arguments(TaskCategory.WebSearch)]
    [Arguments(TaskCategory.FileOperation)]
    [Arguments(TaskCategory.Reasoning)]
    [Arguments(TaskCategory.TextGeneration)]
    public async Task Features_WorksWithAllCategories(TaskCategory category)
    {
        // Arrange
        var features = TaskFeatures.CreateForCategory(category);

        // Act
        var selection = new AgentSelection
        {
            SelectedAgentId = "agent-1",
            TaskCategory = category,
            Features = features,
        };

        // Assert
        await Assert.That(selection.Features!.Category).IsEqualTo(category);
        await Assert.That(selection.TaskCategory).IsEqualTo(category);
    }
}
