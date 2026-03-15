// =============================================================================
// <copyright file="TaskCategoryClassifierTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Selection;

namespace Strategos.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="TaskCategoryClassifier"/> covering task
/// description classification into <see cref="TaskCategory"/> values.
/// </summary>
[Property("Category", "Unit")]
public class TaskCategoryClassifierTests
{
    // =============================================================================
    // A. CodeGeneration Classification Tests
    // =============================================================================

    /// <summary>
    /// Verifies that "code" keyword returns CodeGeneration.
    /// </summary>
    [Test]
    public async Task Classify_ContainsCode_ReturnsCodeGeneration()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Write some code for me");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.CodeGeneration);
    }

    /// <summary>
    /// Verifies that "implement" keyword returns CodeGeneration.
    /// </summary>
    [Test]
    public async Task Classify_ContainsImplement_ReturnsCodeGeneration()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Implement a new feature");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.CodeGeneration);
    }

    /// <summary>
    /// Verifies that "debug" keyword returns CodeGeneration.
    /// </summary>
    [Test]
    public async Task Classify_ContainsDebug_ReturnsCodeGeneration()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Debug this issue");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.CodeGeneration);
    }

    /// <summary>
    /// Verifies that "refactor" keyword returns CodeGeneration.
    /// </summary>
    [Test]
    public async Task Classify_ContainsRefactor_ReturnsCodeGeneration()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Refactor the legacy module");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.CodeGeneration);
    }

    // =============================================================================
    // B. DataAnalysis Classification Tests
    // =============================================================================

    /// <summary>
    /// Verifies that "analyze" keyword returns DataAnalysis.
    /// </summary>
    [Test]
    public async Task Classify_ContainsAnalyze_ReturnsDataAnalysis()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Analyze the sales data");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.DataAnalysis);
    }

    /// <summary>
    /// Verifies that "statistics" keyword returns DataAnalysis.
    /// </summary>
    [Test]
    public async Task Classify_ContainsStatistics_ReturnsDataAnalysis()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Calculate statistics for the dataset");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.DataAnalysis);
    }

    /// <summary>
    /// Verifies that "chart" keyword returns DataAnalysis.
    /// </summary>
    [Test]
    public async Task Classify_ContainsChart_ReturnsDataAnalysis()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Create a chart showing trends");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.DataAnalysis);
    }

    /// <summary>
    /// Verifies that "visualize" keyword returns DataAnalysis.
    /// </summary>
    [Test]
    public async Task Classify_ContainsVisualize_ReturnsDataAnalysis()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Visualize the correlation between values");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.DataAnalysis);
    }

    // =============================================================================
    // C. WebSearch Classification Tests
    // =============================================================================

    /// <summary>
    /// Verifies that "search" keyword returns WebSearch.
    /// </summary>
    [Test]
    public async Task Classify_ContainsSearch_ReturnsWebSearch()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Search for information about topic");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.WebSearch);
    }

    /// <summary>
    /// Verifies that "browse" keyword returns WebSearch.
    /// </summary>
    [Test]
    public async Task Classify_ContainsBrowse_ReturnsWebSearch()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Browse the web for results");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.WebSearch);
    }

    /// <summary>
    /// Verifies that "internet" keyword returns WebSearch.
    /// </summary>
    [Test]
    public async Task Classify_ContainsInternet_ReturnsWebSearch()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Look on the internet for answers");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.WebSearch);
    }

    // =============================================================================
    // D. FileOperation Classification Tests
    // =============================================================================

    /// <summary>
    /// Verifies that "file" keyword returns FileOperation.
    /// </summary>
    [Test]
    public async Task Classify_ContainsFile_ReturnsFileOperation()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Read the file contents");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.FileOperation);
    }

    /// <summary>
    /// Verifies that "directory" keyword returns FileOperation.
    /// </summary>
    [Test]
    public async Task Classify_ContainsDirectory_ReturnsFileOperation()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("List the directory");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.FileOperation);
    }

    /// <summary>
    /// Verifies that "csv" keyword returns FileOperation.
    /// </summary>
    [Test]
    public async Task Classify_ContainsCsv_ReturnsFileOperation()
    {
        // Act - Use "csv" without "data" to avoid DataAnalysis match
        var result = new TaskCategoryClassifier().Classify("Parse the csv contents");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.FileOperation);
    }

    // =============================================================================
    // E. Reasoning Classification Tests
    // =============================================================================

    /// <summary>
    /// Verifies that "reason" keyword returns Reasoning.
    /// </summary>
    [Test]
    public async Task Classify_ContainsReason_ReturnsReasoning()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Reason through this problem");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.Reasoning);
    }

    /// <summary>
    /// Verifies that "evaluate" keyword returns Reasoning.
    /// </summary>
    [Test]
    public async Task Classify_ContainsEvaluate_ReturnsReasoning()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Evaluate the options carefully");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.Reasoning);
    }

    // =============================================================================
    // F. TextGeneration Classification Tests
    // =============================================================================

    /// <summary>
    /// Verifies that "summarize" keyword returns TextGeneration.
    /// </summary>
    [Test]
    public async Task Classify_ContainsSummarize_ReturnsTextGeneration()
    {
        // Act - Use "summarize" without "document" to avoid FileOperation match
        var result = new TaskCategoryClassifier().Classify("Summarize this article");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.TextGeneration);
    }

    /// <summary>
    /// Verifies that "translate" keyword returns TextGeneration.
    /// </summary>
    [Test]
    public async Task Classify_ContainsTranslate_ReturnsTextGeneration()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Translate to Spanish");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.TextGeneration);
    }

    // =============================================================================
    // G. General/Default Classification Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null input returns General.
    /// </summary>
    [Test]
    public async Task Classify_NullInput_ReturnsGeneral()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify(null);

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.General);
    }

    /// <summary>
    /// Verifies that empty string returns General.
    /// </summary>
    [Test]
    public async Task Classify_EmptyString_ReturnsGeneral()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify(string.Empty);

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.General);
    }

    /// <summary>
    /// Verifies that whitespace-only string returns General.
    /// </summary>
    [Test]
    public async Task Classify_WhitespaceOnly_ReturnsGeneral()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("   ");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.General);
    }

    /// <summary>
    /// Verifies that unmatched description returns General.
    /// </summary>
    [Test]
    public async Task Classify_NoMatchingKeywords_ReturnsGeneral()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("Hello world");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.General);
    }

    // =============================================================================
    // H. Case Insensitivity Tests
    // =============================================================================

    /// <summary>
    /// Verifies that classification is case insensitive.
    /// </summary>
    [Test]
    public async Task Classify_UpperCaseKeyword_MatchesCorrectly()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("ANALYZE the DATA");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.DataAnalysis);
    }

    /// <summary>
    /// Verifies that mixed case is handled.
    /// </summary>
    [Test]
    public async Task Classify_MixedCaseKeyword_MatchesCorrectly()
    {
        // Act
        var result = new TaskCategoryClassifier().Classify("ImPlEmEnT a function");

        // Assert
        await Assert.That(result).IsEqualTo(TaskCategory.CodeGeneration);
    }

    // =============================================================================
    // I. Priority Tests (First Match Wins)
    // =============================================================================

    /// <summary>
    /// Verifies that CodeGeneration takes priority over DataAnalysis.
    /// </summary>
    [Test]
    public async Task Classify_MultipleMatches_CodeTakesPriorityOverAnalysis()
    {
        // "code" matches CodeGeneration, "analyze" matches DataAnalysis
        // CodeGeneration should win due to priority order
        var result = new TaskCategoryClassifier().Classify("Analyze the code");

        // Assert - "code" should match first
        await Assert.That(result).IsEqualTo(TaskCategory.CodeGeneration);
    }

    /// <summary>
    /// Verifies that earlier categories take priority.
    /// </summary>
    [Test]
    public async Task Classify_DataOverWeb_WhenBothPresent()
    {
        // "data" matches DataAnalysis, "search" matches WebSearch
        // DataAnalysis should win due to priority order
        var result = new TaskCategoryClassifier().Classify("Search the data");

        // Assert - CodeGeneration check comes before WebSearch, but "data" is DataAnalysis
        // Actually "search" comes before "data" in the string, but classification checks categories in order
        await Assert.That(result).IsEqualTo(TaskCategory.DataAnalysis);
    }
}
