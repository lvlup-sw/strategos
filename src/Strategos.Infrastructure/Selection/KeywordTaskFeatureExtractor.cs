// =============================================================================
// <copyright file="KeywordTaskFeatureExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.Logging;

using Strategos.Abstractions;
using Strategos.Selection;

namespace Strategos.Infrastructure.Selection;

/// <summary>
/// Keyword-based implementation of <see cref="ITaskFeatureExtractor"/> for extracting
/// task features from descriptions.
/// </summary>
/// <remarks>
/// <para>
/// Uses keyword matching to classify tasks into categories and heuristics to estimate
/// complexity. This is a simple but effective approach for MVP that can be replaced
/// with embedding-based extraction in the future.
/// </para>
/// <para>
/// Complexity estimation uses:
/// <list type="bullet">
///   <item><description>Description length (normalized)</description></item>
///   <item><description>Sentence count (approximated by periods)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class KeywordTaskFeatureExtractor : ITaskFeatureExtractor
{
    private readonly ILogger<KeywordTaskFeatureExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeywordTaskFeatureExtractor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> is null.
    /// </exception>
    public KeywordTaskFeatureExtractor(ILogger<KeywordTaskFeatureExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        _logger = logger;
    }

    /// <summary>
    /// Maximum description length for complexity normalization.
    /// </summary>
    private const int MaxDescriptionLength = 500;

    /// <summary>
    /// Maximum sentence count for complexity normalization.
    /// </summary>
    private const int MaxSentences = 10;

    /// <summary>
    /// Keywords for each task category, in priority order.
    /// </summary>
    private static readonly Dictionary<TaskCategory, string[]> CategoryKeywords = new()
    {
        [TaskCategory.CodeGeneration] =
        [
            "code", "program", "function", "debug", "algorithm", "implement",
            "refactor", "compile", "class", "method", "syntax", "programming",
        ],
        [TaskCategory.DataAnalysis] =
        [
            "analyze", "analysis", "statistics", "chart", "visualize", "visualization",
            "data", "graph", "plot", "metric", "trend", "correlation", "regression",
        ],
        [TaskCategory.WebSearch] =
        [
            "search", "lookup", "browse", "web", "internet",
            "url", "website", "online", "google",
        ],
        [TaskCategory.FileOperation] =
        [
            "file", "directory", "document", "folder", "path",
            "filesystem", "disk", "storage", "csv", "json", "xml", "pdf",
        ],
        [TaskCategory.Reasoning] =
        [
            "reason", "plan", "decide", "evaluate", "compare",
            "assess", "judge", "logic", "think", "consider",
        ],
        [TaskCategory.TextGeneration] =
        [
            "summarize", "translate", "compose", "draft", "edit",
            "paraphrase", "rewrite", "generate text",
        ],
    };

    /// <summary>
    /// Priority order for category matching (first match wins).
    /// </summary>
    private static readonly TaskCategory[] CategoryPriority =
    [
        TaskCategory.CodeGeneration,
        TaskCategory.DataAnalysis,
        TaskCategory.WebSearch,
        TaskCategory.FileOperation,
        TaskCategory.Reasoning,
        TaskCategory.TextGeneration,
    ];

    /// <inheritdoc/>
    public TaskFeatures ExtractFeatures(AgentSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var description = context.TaskDescription;

        if (string.IsNullOrWhiteSpace(description))
        {
            _logger.LogWarning("Empty or null task description provided, returning default features");
            return TaskFeatures.CreateDefault();
        }

        var lowerDescription = description.ToLowerInvariant();
        var (category, matchedKeywords) = ClassifyWithKeywords(lowerDescription);
        var complexity = EstimateComplexity(description);

        return new TaskFeatures
        {
            Category = category,
            Complexity = complexity,
            MatchedKeywords = matchedKeywords,
        };
    }

    /// <summary>
    /// Classifies the description and returns matched keywords.
    /// </summary>
    /// <param name="lowerDescription">Lowercase description text.</param>
    /// <returns>The matched category and list of matched keywords.</returns>
    private static (TaskCategory Category, IReadOnlyList<string> MatchedKeywords) ClassifyWithKeywords(
        string lowerDescription)
    {
        foreach (var category in CategoryPriority)
        {
            var keywords = CategoryKeywords[category];
            var matched = FindMatchedKeywords(lowerDescription, keywords);

            if (matched.Count > 0)
            {
                return (category, matched);
            }
        }

        return (TaskCategory.General, []);
    }

    /// <summary>
    /// Finds all keywords that match in the description.
    /// </summary>
    /// <param name="lowerDescription">Lowercase description text.</param>
    /// <param name="keywords">Keywords to search for.</param>
    /// <returns>List of matched keywords.</returns>
    private static List<string> FindMatchedKeywords(string lowerDescription, string[] keywords)
    {
        var matched = new List<string>();

        foreach (var keyword in keywords)
        {
            if (lowerDescription.Contains(keyword, StringComparison.Ordinal))
            {
                matched.Add(keyword);
            }
        }

        return matched;
    }

    /// <summary>
    /// Estimates task complexity based on description characteristics.
    /// </summary>
    /// <param name="description">The task description.</param>
    /// <returns>Complexity score in [0, 1].</returns>
    /// <remarks>
    /// <para>
    /// Simple heuristics for MVP:
    /// <list type="bullet">
    ///   <item><description>Length: longer descriptions tend to indicate more complex tasks</description></item>
    ///   <item><description>Sentences: more sentences suggest multiple requirements</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Future enhancements could include:
    /// <list type="bullet">
    ///   <item><description>Technical keyword density</description></item>
    ///   <item><description>Dependency detection (multiple steps implied)</description></item>
    ///   <item><description>LLM-based complexity estimation</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private static double EstimateComplexity(string description)
    {
        // Normalize length contribution (0 to 0.5)
        var lengthScore = Math.Min((double)description.Length / MaxDescriptionLength, 1.0) * 0.5;

        // Approximate sentence count (periods) and normalize (0 to 0.5)
        var sentenceCount = description.Count(c => c == '.');
        var sentenceScore = Math.Min((double)sentenceCount / MaxSentences, 1.0) * 0.5;

        // Combine and clamp to [0, 1]
        var complexity = lengthScore + sentenceScore;
        return Math.Min(Math.Max(complexity, 0.0), 1.0);
    }
}
