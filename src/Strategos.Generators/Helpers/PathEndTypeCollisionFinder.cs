// -----------------------------------------------------------------------
// <copyright file="PathEndTypeCollisionFinder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Finds exclusive-path steps that share a completed-event type under distinct
/// instance names. Both authoring roots — C# <c>[Workflow]</c> and JSON
/// import — call this before emission so a colliding shape cannot lower to CS0111.
/// </summary>
internal static class PathEndTypeCollisionFinder
{
    /// <summary>
    /// Returns distinct colliding step type names, ordered for stable diagnostics.
    /// </summary>
    /// <param name="forkModels">Forks whose path steps (interiors and last steps) are inspected.</param>
    /// <param name="branchPathSteps">
    /// Branch-path steps as <c>(StepName, EffectiveName)</c>. JSON import rejects
    /// branch points, so that root passes an empty sequence.
    /// </param>
    /// <returns>Colliding type names in ordinal order.</returns>
    public static List<string> Find(
        IReadOnlyList<ForkModel> forkModels,
        IEnumerable<(string StepName, string EffectiveName)> branchPathSteps)
    {
        ThrowHelper.ThrowIfNull(forkModels, nameof(forkModels));
        ThrowHelper.ThrowIfNull(branchPathSteps, nameof(branchPathSteps));

        var collisions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fork in forkModels)
        {
            CollectCollidingTypes(
                fork.Paths.SelectMany(static p => p.Steps).Select(static s => (s.StepName, s.EffectiveName)),
                collisions);
        }

        CollectCollidingTypes(branchPathSteps, collisions);

        return collisions.OrderBy(static n => n, StringComparer.Ordinal).ToList();
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
