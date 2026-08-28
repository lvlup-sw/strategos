// -----------------------------------------------------------------------
// <copyright file="PathEndTypeCollisionFinder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Identifies fork-path steps that share a step type under distinct instance names
/// so saga <c>Handle</c> emitters can bind the path-qualified completed event
/// (<c>{PhaseName}Completed</c>) instead of <c>{StepType}Completed</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>PathEndTypeCollision</c> remains in the catalog as a historical member.
/// Option B no longer reports that diagnostic: fork path instances that share a
/// type get distinct completed CLR types (matching <c>Start{PhaseName}Command</c>
/// naming), and branch completions keep one <c>Handle({StepType}Completed)</c>
/// that routes by the live case.
/// </para>
/// <para>
/// Do not invent a second routing key. Qualification uses
/// <see cref="StepModel.PhaseName"/> — the same string
/// <c>PathRoutingKey.PhaseName</c> carries.
/// </para>
/// </remarks>
internal static class PathEndTypeCollisionFinder
{
    /// <summary>
    /// Returns distinct fork-path step types that appear under more than one
    /// effective name, ordered for stable consumption.
    /// </summary>
    /// <param name="forkModels">Forks whose path steps (interiors and last steps) are inspected.</param>
    /// <param name="branchPathSteps">
    /// Ignored for Handle qualification (branch completions stay on
    /// <c>{StepType}Completed</c>). Kept so existing call shapes compile.
    /// </param>
    /// <returns>Colliding type names in ordinal order.</returns>
    public static List<string> Find(
        IReadOnlyList<ForkModel> forkModels,
        IEnumerable<(string StepName, string EffectiveName)> branchPathSteps)
    {
        ThrowHelper.ThrowIfNull(forkModels, nameof(forkModels));
        ThrowHelper.ThrowIfNull(branchPathSteps, nameof(branchPathSteps));

        var collisions = new HashSet<string>(StringComparer.Ordinal);

        CollectCollidingTypes(
            forkModels
                .SelectMany(static fork => fork.Paths)
                .SelectMany(static path => path.Steps)
                .Select(static step => (step.StepName, step.EffectiveName)),
            collisions);

        return collisions.OrderBy(static n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Fork-path step types that must publish and handle a path-qualified completed event.
    /// </summary>
    /// <param name="forks">The workflow's forks, or null when none exist.</param>
    /// <returns>A set of colliding step type names.</returns>
    public static HashSet<string> CollidingForkStepTypes(IReadOnlyList<ForkModel>? forks)
    {
        if (forks is null || forks.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return new HashSet<string>(Find(forks, []), StringComparer.Ordinal);
    }

    /// <summary>
    /// Completed-event CLR name for a saga <c>Handle</c>. Fork-path instances that
    /// share a type use <c>{PhaseName}Completed</c>; every other step stays
    /// <c>{StepType}Completed</c>.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <param name="phaseName">The step's phase name (routing-key phase).</param>
    /// <param name="stepTypeName">The step's type name.</param>
    /// <param name="isForkPathStep">Whether the step sits on a fork path.</param>
    /// <returns>The completed-event type name to bind.</returns>
    public static string CompletedEventName(
        WorkflowModel model,
        string phaseName,
        string stepTypeName,
        bool isForkPathStep)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNullOrWhiteSpace(phaseName, nameof(phaseName));
        ThrowHelper.ThrowIfNullOrWhiteSpace(stepTypeName, nameof(stepTypeName));

        if (isForkPathStep
            && model.HasForks
            && CollidingForkStepTypes(model.Forks).Contains(stepTypeName))
        {
            return $"{phaseName}Completed";
        }

        return $"{stepTypeName}Completed";
    }

    private static void CollectCollidingTypes(
        IEnumerable<(string StepName, string EffectiveName)> steps,
        HashSet<string> collisions)
    {
        foreach (var group in steps.GroupBy(static s => s.StepName, StringComparer.Ordinal))
        {
            if (group.Select(static s => s.EffectiveName).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                collisions.Add(group.Key);
            }
        }
    }
}
