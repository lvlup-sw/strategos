// =============================================================================
// <copyright file="TaskCategoryClassifier.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;

namespace Strategos.Selection;

/// <summary>
/// Classifies task descriptions into <see cref="TaskCategory"/> categories
/// for Thompson Sampling agent selection.
/// </summary>
/// <remarks>
/// <para>
/// The classifier uses keyword matching to infer task categories from natural
/// language descriptions. Keywords are matched case-insensitively.
/// </para>
/// <para>
/// Classification priority (first match wins):
/// <list type="number">
///   <item><description>CodeGeneration: code, program, function, debug, algorithm, implement, refactor, compile</description></item>
///   <item><description>DataAnalysis: analyze, statistics, chart, visualize, data, graph, correlation</description></item>
///   <item><description>WebSearch: search, lookup, browse, web, internet, url, website</description></item>
///   <item><description>FileOperation: file, directory, document, folder, path, read, write, csv, json</description></item>
///   <item><description>Reasoning: reason, plan, decide, evaluate, compare, assess, judge</description></item>
///   <item><description>TextGeneration: write, summarize, translate, compose, draft, edit</description></item>
///   <item><description>General: default when no keywords match</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class TaskCategoryClassifier : ITaskCategoryClassifier
{
    /// <summary>
    /// Keywords that indicate CodeGeneration tasks.
    /// </summary>
    private static readonly string[] CodeKeywords =
    [
        "code", "program", "function", "debug", "algorithm", "implement",
        "refactor", "compile", "class", "method", "syntax", "programming",
    ];

    /// <summary>
    /// Keywords that indicate DataAnalysis tasks.
    /// </summary>
    private static readonly string[] AnalysisKeywords =
    [
        "analyze", "analysis", "statistics", "chart", "visualize", "visualization",
        "data", "graph", "plot", "metric", "trend", "correlation", "regression",
    ];

    /// <summary>
    /// Keywords that indicate WebSearch tasks.
    /// </summary>
    private static readonly string[] WebKeywords =
    [
        "search", "lookup", "browse", "web", "internet",
        "url", "website", "online", "google",
    ];

    /// <summary>
    /// Keywords that indicate FileOperation tasks.
    /// </summary>
    private static readonly string[] FileKeywords =
    [
        "file", "directory", "document", "folder", "path",
        "filesystem", "disk", "storage", "csv", "json", "xml", "pdf",
    ];

    /// <summary>
    /// Keywords that indicate Reasoning tasks.
    /// </summary>
    private static readonly string[] ReasoningKeywords =
    [
        "reason", "plan", "decide", "evaluate", "compare",
        "assess", "judge", "logic", "think", "consider",
    ];

    /// <summary>
    /// Keywords that indicate TextGeneration tasks.
    /// </summary>
    private static readonly string[] TextKeywords =
    [
        "summarize", "translate", "compose", "draft", "edit",
        "paraphrase", "rewrite", "generate text",
    ];

    /// <summary>
    /// Classifies a task description into a <see cref="TaskCategory"/>.
    /// </summary>
    /// <param name="description">The task description to classify.</param>
    /// <returns>
    /// The inferred <see cref="TaskCategory"/> based on keyword matching,
    /// or <see cref="TaskCategory.General"/> if no keywords match.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Classification is case-insensitive and returns on first keyword match.
    /// The order of priority is: CodeGeneration, DataAnalysis, WebSearch,
    /// FileOperation, Reasoning, TextGeneration, then General (default).
    /// </para>
    /// </remarks>
    /// <inheritdoc/>
    public TaskCategory Classify(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return TaskCategory.General;
        }

        var lowerDescription = description.ToLowerInvariant();

        // Check CodeGeneration keywords first
        if (ContainsAny(lowerDescription, CodeKeywords))
        {
            return TaskCategory.CodeGeneration;
        }

        // Check DataAnalysis keywords
        if (ContainsAny(lowerDescription, AnalysisKeywords))
        {
            return TaskCategory.DataAnalysis;
        }

        // Check WebSearch keywords
        if (ContainsAny(lowerDescription, WebKeywords))
        {
            return TaskCategory.WebSearch;
        }

        // Check FileOperation keywords
        if (ContainsAny(lowerDescription, FileKeywords))
        {
            return TaskCategory.FileOperation;
        }

        // Check Reasoning keywords
        if (ContainsAny(lowerDescription, ReasoningKeywords))
        {
            return TaskCategory.Reasoning;
        }

        // Check TextGeneration keywords
        if (ContainsAny(lowerDescription, TextKeywords))
        {
            return TaskCategory.TextGeneration;
        }

        // Default to General
        return TaskCategory.General;
    }

    /// <summary>
    /// Checks if the text contains any of the specified keywords.
    /// </summary>
    /// <param name="text">The text to search (should be lowercase).</param>
    /// <param name="keywords">The keywords to look for.</param>
    /// <returns>True if any keyword is found; otherwise, false.</returns>
    private static bool ContainsAny(string text, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
