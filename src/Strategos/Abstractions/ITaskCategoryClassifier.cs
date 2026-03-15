// =============================================================================
// <copyright file="ITaskCategoryClassifier.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Selection;

namespace Strategos.Abstractions;

/// <summary>
/// Classifies task descriptions into categories for agent selection.
/// </summary>
public interface ITaskCategoryClassifier
{
    /// <summary>
    /// Classifies a task description into a <see cref="TaskCategory"/>.
    /// </summary>
    /// <param name="taskDescription">The task description to classify.</param>
    /// <returns>The inferred <see cref="TaskCategory"/>.</returns>
    TaskCategory Classify(string? taskDescription);
}
