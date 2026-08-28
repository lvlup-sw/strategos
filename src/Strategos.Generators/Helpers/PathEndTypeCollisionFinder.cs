// -----------------------------------------------------------------------
// <copyright file="PathEndTypeCollisionFinder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Identifies fork-path steps that share a step type so saga <c>Handle</c>
/// emitters can bind the same completed-event stem as
/// <see cref="ForkPathCompletedNaming"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>PathEndTypeCollision</c> remains in the catalog as a historical member.
/// Option B no longer reports that diagnostic: fork path instances that share a
/// type get distinct completed CLR types via <see cref="ForkPathCompletedNaming"/>,
/// and branch completions keep one <c>Handle({StepType}Completed)</c>
/// that routes by the live case.
/// </para>
/// <para>
/// Do not invent a second routing key. Look the path up with
/// <see cref="PathRoutingKey.ForFork"/> /
/// <c>SagaEmissionContext.ForkPathsByRoutingKey</c>. Unnamed same-type paths
/// collide on <see cref="PathRoutingKey.PhaseName"/> and bind
/// <c>{PathId}_{PhaseName}Completed</c>.
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
    /// Completed-event CLR name for a saga <c>Handle</c>. Matches
    /// <see cref="ForkPathCompletedNaming.StemFor"/> so colliding unnamed
    /// same-type fork paths bind <c>{PathId}_{PhaseName}Completed</c> rather than
    /// two <c>Handle({StepType}Completed)</c> overloads (CS0111).
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <param name="phaseName">The step's phase name (routing-key phase).</param>
    /// <param name="stepTypeName">The step's type name.</param>
    /// <param name="isForkPathStep">Whether the step sits on a fork path.</param>
    /// <param name="forkKey">
    /// The routing key for this path instance. Required when two paths share
    /// <paramref name="phaseName"/>; look it up with <see cref="PathRoutingKey.ForFork"/>.
    /// </param>
    /// <returns>The completed-event type name to bind.</returns>
    public static string CompletedEventName(
        WorkflowModel model,
        string phaseName,
        string stepTypeName,
        bool isForkPathStep,
        PathRoutingKey? forkKey = null)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNullOrWhiteSpace(phaseName, nameof(phaseName));
        ThrowHelper.ThrowIfNullOrWhiteSpace(stepTypeName, nameof(stepTypeName));

        if (isForkPathStep && model.HasForks)
        {
            var naming = ForkPathCompletedNaming.For(model);
            if (forkKey is { } key)
            {
                return $"{naming.StemFor(key, stepTypeName)}Completed";
            }

            if (naming.IsSharedType(stepTypeName) && naming.IsQualifiedPhase(phaseName))
            {
                return $"{phaseName}Completed";
            }
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
