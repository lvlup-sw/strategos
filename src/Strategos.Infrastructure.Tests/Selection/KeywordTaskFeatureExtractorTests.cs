// =============================================================================
// <copyright file="KeywordTaskFeatureExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Abstractions;
using Strategos.Infrastructure.Selection;
using Strategos.Selection;

namespace Strategos.Infrastructure.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="KeywordTaskFeatureExtractor"/> covering feature extraction
/// from task descriptions for contextual agent selection.
/// </summary>
[Property("Category", "Unit")]
public class KeywordTaskFeatureExtractorTests
{
    private readonly ITaskFeatureExtractor _extractor = new KeywordTaskFeatureExtractor(NullLogger<KeywordTaskFeatureExtractor>.Instance);

    // =============================================================================
    // A. Category Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that code-related tasks are classified as CodeGeneration.
    /// </summary>
    [Test]
    [Arguments("Implement a binary search algorithm")]
    [Arguments("Debug this Python function")]
    [Arguments("Write a sorting program")]
    [Arguments("Refactor the authentication module")]
    public async Task ExtractFeatures_CodeTask_ReturnsCategoryCodeGeneration(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.CodeGeneration);
    }

    /// <summary>
    /// Verifies that data analysis tasks are classified as DataAnalysis.
    /// </summary>
    [Test]
    [Arguments("Analyze the sales data for Q4")]
    [Arguments("Create a visualization of user trends")]
    [Arguments("Calculate statistics for the dataset")]
    [Arguments("Plot correlation between variables")]
    public async Task ExtractFeatures_AnalysisTask_ReturnsCategoryDataAnalysis(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.DataAnalysis);
    }

    /// <summary>
    /// Verifies that web search tasks are classified as WebSearch.
    /// </summary>
    [Test]
    [Arguments("Search for best practices in API design")]
    [Arguments("Browse the documentation website")]
    [Arguments("Lookup the latest news on the web")]
    [Arguments("Find information on the internet")]
    public async Task ExtractFeatures_WebTask_ReturnsCategoryWebSearch(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.WebSearch);
    }

    /// <summary>
    /// Verifies that file operation tasks are classified as FileOperation.
    /// </summary>
    [Test]
    [Arguments("Read the CSV file and process it")]
    [Arguments("Create a new directory for outputs")]
    [Arguments("Parse the JSON document")]
    [Arguments("Move files to the archive folder")]
    public async Task ExtractFeatures_FileTask_ReturnsCategoryFileOperation(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.FileOperation);
    }

    /// <summary>
    /// Verifies that reasoning tasks are classified as Reasoning.
    /// </summary>
    [Test]
    [Arguments("Plan the migration strategy")]
    [Arguments("Evaluate the trade-offs between approaches")]
    [Arguments("Decide which option to use")]
    [Arguments("Compare the performance of solutions")]
    public async Task ExtractFeatures_ReasoningTask_ReturnsCategoryReasoning(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.Reasoning);
    }

    /// <summary>
    /// Verifies that text generation tasks are classified as TextGeneration.
    /// </summary>
    [Test]
    [Arguments("Summarize this article")]
    [Arguments("Translate the text to Spanish")]
    [Arguments("Draft an email response")]
    [Arguments("Compose a technical blog post")]
    public async Task ExtractFeatures_TextTask_ReturnsCategoryTextGeneration(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.TextGeneration);
    }

    /// <summary>
    /// Verifies that unclassifiable tasks default to General.
    /// </summary>
    [Test]
    [Arguments("Do something")]
    [Arguments("Help me")]
    [Arguments("Process this")]
    public async Task ExtractFeatures_UnknownTask_ReturnsCategoryGeneral(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.General);
    }

    // =============================================================================
    // B. Null/Empty Description Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null description returns default features.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_NullDescription_ReturnsDefaultFeatures()
    {
        // Arrange
        var context = CreateContext(null!);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.General);
        await Assert.That(features.Complexity).IsEqualTo(0.0);
        await Assert.That(features.MatchedKeywords).IsEmpty();
    }

    /// <summary>
    /// Verifies that empty description returns default features.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_EmptyDescription_ReturnsDefaultFeatures()
    {
        // Arrange
        var context = CreateContext(string.Empty);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.General);
        await Assert.That(features.Complexity).IsEqualTo(0.0);
    }

    /// <summary>
    /// Verifies that whitespace-only description returns default features.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_WhitespaceDescription_ReturnsDefaultFeatures()
    {
        // Arrange
        var context = CreateContext("   \t\n  ");

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.General);
        await Assert.That(features.Complexity).IsEqualTo(0.0);
    }

    // =============================================================================
    // C. Complexity Estimation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that short descriptions have low complexity.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_ShortDescription_ReturnsLowComplexity()
    {
        // Arrange
        var context = CreateContext("Sort a list");

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Complexity).IsLessThan(0.3);
        await Assert.That(features.IsSimple).IsTrue();
    }

    /// <summary>
    /// Verifies that long descriptions have higher complexity.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_LongDescription_ReturnsHigherComplexity()
    {
        // Arrange
        var longDescription = """
            Implement a comprehensive authentication system that supports
            OAuth2.0, JWT tokens, and multi-factor authentication. The system
            should integrate with multiple identity providers including Google,
            GitHub, and Azure AD. Include rate limiting, session management,
            and audit logging. Ensure all security best practices are followed
            including proper token rotation and secure storage of secrets.
            The implementation should be fully tested with unit tests and
            integration tests covering all edge cases.
            """;
        var context = CreateContext(longDescription);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Complexity).IsGreaterThan(0.5);
    }

    /// <summary>
    /// Verifies that complexity is clamped to [0, 1].
    /// </summary>
    [Test]
    public async Task ExtractFeatures_VeryLongDescription_ReturnsComplexityAtMostOne()
    {
        // Arrange
        var veryLongDescription = string.Concat(Enumerable.Repeat(
            "Implement a complex multi-step algorithm. ", 100));
        var context = CreateContext(veryLongDescription);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Complexity).IsLessThanOrEqualTo(1.0);
        await Assert.That(features.Complexity).IsGreaterThanOrEqualTo(0.0);
    }

    // =============================================================================
    // D. Matched Keywords Tests
    // =============================================================================

    /// <summary>
    /// Verifies that matched keywords are captured.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_WithKeywords_ReturnsMatchedKeywords()
    {
        // Arrange
        var context = CreateContext("Implement and debug a function");

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.MatchedKeywords).IsNotEmpty();
        // Should contain at least one of: implement, debug, function
    }

    /// <summary>
    /// Verifies that no keywords match for generic descriptions.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_NoKeywords_ReturnsEmptyMatchedKeywords()
    {
        // Arrange
        var context = CreateContext("Do something now");

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.MatchedKeywords).IsEmpty();
    }

    // =============================================================================
    // E. Case Insensitivity Tests
    // =============================================================================

    /// <summary>
    /// Verifies that classification is case insensitive.
    /// </summary>
    [Test]
    [Arguments("CODE this function")]
    [Arguments("Code this function")]
    [Arguments("code this function")]
    [Arguments("CODE THIS FUNCTION")]
    public async Task ExtractFeatures_CaseInsensitive_ReturnsSameCategory(string description)
    {
        // Arrange
        var context = CreateContext(description);

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features.Category).IsEqualTo(TaskCategory.CodeGeneration);
    }

    // =============================================================================
    // F. Priority Order Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CodeGeneration takes priority over other categories.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_MultipleKeywords_CodeGenerationHasPriority()
    {
        // Arrange - Contains both "code" and "data" keywords
        var context = CreateContext("Code a data analysis tool");

        // Act
        var features = _extractor.ExtractFeatures(context);

        // Assert - Code should take priority
        await Assert.That(features.Category).IsEqualTo(TaskCategory.CodeGeneration);
    }

    // =============================================================================
    // G. Determinism Tests
    // =============================================================================

    /// <summary>
    /// Verifies that extraction is deterministic.
    /// </summary>
    [Test]
    public async Task ExtractFeatures_SameInput_ReturnsSameOutput()
    {
        // Arrange
        var context = CreateContext("Implement a sorting algorithm");

        // Act
        var features1 = _extractor.ExtractFeatures(context);
        var features2 = _extractor.ExtractFeatures(context);

        // Assert
        await Assert.That(features1.Category).IsEqualTo(features2.Category);
        await Assert.That(features1.Complexity).IsEqualTo(features2.Complexity);
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static AgentSelectionContext CreateContext(string description) => new()
    {
        WorkflowId = Guid.NewGuid(),
        StepName = "TestStep",
        TaskDescription = description,
        AvailableAgents = ["agent-1", "agent-2"],
    };
}
