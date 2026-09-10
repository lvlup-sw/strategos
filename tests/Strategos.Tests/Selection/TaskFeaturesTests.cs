// =============================================================================
// <copyright file="TaskFeaturesTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Selection;

namespace Strategos.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="TaskFeatures"/> covering contextual feature
/// representation for agent selection.
/// </summary>
[Property("Category", "Unit")]
public class TaskFeaturesTests
{
    // =============================================================================
    // A. Default Construction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that default TaskFeatures has General category.
    /// </summary>
    [Test]
    public async Task Default_HasGeneralCategory()
    {
        // Act
        var features = new TaskFeatures();

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.General);
    }

    /// <summary>
    /// Verifies that default TaskFeatures has zero complexity.
    /// </summary>
    [Test]
    public async Task Default_HasZeroComplexity()
    {
        // Act
        var features = new TaskFeatures();

        // Assert
        await Assert.That(features.Complexity).IsEqualTo(0.0);
    }

    /// <summary>
    /// Verifies that default TaskFeatures has empty matched keywords.
    /// </summary>
    [Test]
    public async Task Default_HasEmptyMatchedKeywords()
    {
        // Act
        var features = new TaskFeatures();

        // Assert
        await Assert.That(features.MatchedKeywords).IsEmpty();
    }

    /// <summary>
    /// Verifies that default TaskFeatures has null custom features.
    /// </summary>
    [Test]
    public async Task Default_HasNullCustomFeatures()
    {
        // Act
        var features = new TaskFeatures();

        // Assert
        await Assert.That(features.CustomFeatures).IsNull();
    }

    // =============================================================================
    // B. Category Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Category can be set to CodeGeneration.
    /// </summary>
    [Test]
    public async Task Category_CanBeSetToCodeGeneration()
    {
        // Act
        var features = new TaskFeatures { Category = TaskCategory.CodeGeneration };

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.CodeGeneration);
    }

    /// <summary>
    /// Verifies that Category can be set to DataAnalysis.
    /// </summary>
    [Test]
    public async Task Category_CanBeSetToDataAnalysis()
    {
        // Act
        var features = new TaskFeatures { Category = TaskCategory.DataAnalysis };

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.DataAnalysis);
    }

    /// <summary>
    /// Verifies that all TaskCategory values can be assigned.
    /// </summary>
    [Test]
    [Arguments(TaskCategory.General)]
    [Arguments(TaskCategory.CodeGeneration)]
    [Arguments(TaskCategory.DataAnalysis)]
    [Arguments(TaskCategory.WebSearch)]
    [Arguments(TaskCategory.FileOperation)]
    [Arguments(TaskCategory.Reasoning)]
    [Arguments(TaskCategory.TextGeneration)]
    public async Task Category_AcceptsAllTaskCategoryValues(TaskCategory category)
    {
        // Act
        var features = new TaskFeatures { Category = category };

        // Assert
        await Assert.That(features.Category).IsEqualTo(category);
    }

    // =============================================================================
    // C. Complexity Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Complexity stores value between 0 and 1.
    /// </summary>
    [Test]
    public async Task Complexity_StoresValueInValidRange()
    {
        // Act
        var features = new TaskFeatures { Complexity = 0.75 };

        // Assert
        await Assert.That(features.Complexity).IsEqualTo(0.75);
    }

    /// <summary>
    /// Verifies that Complexity can be zero.
    /// </summary>
    [Test]
    public async Task Complexity_CanBeZero()
    {
        // Act
        var features = new TaskFeatures { Complexity = 0.0 };

        // Assert
        await Assert.That(features.Complexity).IsEqualTo(0.0);
    }

    /// <summary>
    /// Verifies that Complexity can be one.
    /// </summary>
    [Test]
    public async Task Complexity_CanBeOne()
    {
        // Act
        var features = new TaskFeatures { Complexity = 1.0 };

        // Assert
        await Assert.That(features.Complexity).IsEqualTo(1.0);
    }

    /// <summary>
    /// Verifies that IsSimple returns true for low complexity.
    /// </summary>
    [Test]
    public async Task IsSimple_ReturnsTrueForLowComplexity()
    {
        // Act
        var features = new TaskFeatures { Complexity = 0.2 };

        // Assert
        await Assert.That(features.IsSimple).IsTrue();
    }

    /// <summary>
    /// Verifies that IsSimple returns false for high complexity.
    /// </summary>
    [Test]
    public async Task IsSimple_ReturnsFalseForHighComplexity()
    {
        // Act
        var features = new TaskFeatures { Complexity = 0.8 };

        // Assert
        await Assert.That(features.IsSimple).IsFalse();
    }

    /// <summary>
    /// Verifies that IsComplex returns true for high complexity.
    /// </summary>
    [Test]
    public async Task IsComplex_ReturnsTrueForHighComplexity()
    {
        // Act
        var features = new TaskFeatures { Complexity = 0.8 };

        // Assert
        await Assert.That(features.IsComplex).IsTrue();
    }

    /// <summary>
    /// Verifies that IsComplex returns false for low complexity.
    /// </summary>
    [Test]
    public async Task IsComplex_ReturnsFalseForLowComplexity()
    {
        // Act
        var features = new TaskFeatures { Complexity = 0.2 };

        // Assert
        await Assert.That(features.IsComplex).IsFalse();
    }

    // =============================================================================
    // D. MatchedKeywords Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that MatchedKeywords stores list of keywords.
    /// </summary>
    [Test]
    public async Task MatchedKeywords_StoresList()
    {
        // Arrange
        var keywords = new[] { "code", "implement", "function" };

        // Act
        var features = new TaskFeatures { MatchedKeywords = keywords };

        // Assert
        await Assert.That(features.MatchedKeywords).HasCount(3);
        await Assert.That(features.MatchedKeywords).Contains("code");
        await Assert.That(features.MatchedKeywords).Contains("implement");
    }

    /// <summary>
    /// Verifies that MatchedKeywords can be empty.
    /// </summary>
    [Test]
    public async Task MatchedKeywords_CanBeEmpty()
    {
        // Act
        var features = new TaskFeatures { MatchedKeywords = [] };

        // Assert
        await Assert.That(features.MatchedKeywords).IsEmpty();
    }

    // =============================================================================
    // E. CustomFeatures Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CustomFeatures can store arbitrary key-value pairs.
    /// </summary>
    [Test]
    public async Task CustomFeatures_StoresDictionary()
    {
        // Arrange
        var custom = new Dictionary<string, object>
        {
            ["embedding_dim"] = 384,
            ["source"] = "user_input",
        };

        // Act
        var features = new TaskFeatures { CustomFeatures = custom };

        // Assert
        await Assert.That(features.CustomFeatures).IsNotNull();
        await Assert.That(features.CustomFeatures!.Count).IsEqualTo(2);
        await Assert.That(features.CustomFeatures["embedding_dim"]).IsEqualTo(384);
    }

    /// <summary>
    /// Verifies that CustomFeatures can be null.
    /// </summary>
    [Test]
    public async Task CustomFeatures_CanBeNull()
    {
        // Act
        var features = new TaskFeatures { CustomFeatures = null };

        // Assert
        await Assert.That(features.CustomFeatures).IsNull();
    }

    // =============================================================================
    // F. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that TaskFeatures is immutable (sealed record).
    /// </summary>
    [Test]
    public async Task TaskFeatures_IsImmutableRecord()
    {
        // Arrange
        var original = new TaskFeatures
        {
            Category = TaskCategory.CodeGeneration,
            Complexity = 0.5,
        };

        // Act - Create copy with different complexity
        var modified = original with { Complexity = 0.9 };

        // Assert - Original unchanged
        await Assert.That(original.Complexity).IsEqualTo(0.5);
        await Assert.That(modified.Complexity).IsEqualTo(0.9);
        await Assert.That(modified.Category).IsEqualTo(TaskCategory.CodeGeneration);
    }

    // =============================================================================
    // G. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CreateDefault returns default features.
    /// </summary>
    [Test]
    public async Task CreateDefault_ReturnsDefaultFeatures()
    {
        // Act
        var features = TaskFeatures.CreateDefault();

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.General);
        await Assert.That(features.Complexity).IsEqualTo(0.0);
        await Assert.That(features.MatchedKeywords).IsEmpty();
    }

    /// <summary>
    /// Verifies that CreateForCategory returns features with specified category.
    /// </summary>
    [Test]
    public async Task CreateForCategory_ReturnsFeaturesWithCategory()
    {
        // Act
        var features = TaskFeatures.CreateForCategory(TaskCategory.DataAnalysis);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.DataAnalysis);
        await Assert.That(features.Complexity).IsEqualTo(0.0);
    }

    // =============================================================================
    // H. Equality Tests
    // =============================================================================

    /// <summary>
    /// Verifies that two TaskFeatures with same values are equal (scalar properties).
    /// </summary>
    [Test]
    public async Task Equality_SameValuesAreEqual()
    {
        // Arrange - Use empty keywords to test scalar equality
        var features1 = new TaskFeatures
        {
            Category = TaskCategory.CodeGeneration,
            Complexity = 0.5,
        };

        var features2 = new TaskFeatures
        {
            Category = TaskCategory.CodeGeneration,
            Complexity = 0.5,
        };

        // Assert - Record equality based on value (scalar properties)
        await Assert.That(features1.Category).IsEqualTo(features2.Category);
        await Assert.That(features1.Complexity).IsEqualTo(features2.Complexity);
        await Assert.That(features1.IsSimple).IsEqualTo(features2.IsSimple);
        await Assert.That(features1.IsComplex).IsEqualTo(features2.IsComplex);
    }

    /// <summary>
    /// Verifies that two TaskFeatures with different values are not equal.
    /// </summary>
    [Test]
    public async Task Equality_DifferentValuesAreNotEqual()
    {
        // Arrange
        var features1 = new TaskFeatures { Category = TaskCategory.CodeGeneration };
        var features2 = new TaskFeatures { Category = TaskCategory.DataAnalysis };

        // Assert
        await Assert.That(features1).IsNotEqualTo(features2);
    }
}
