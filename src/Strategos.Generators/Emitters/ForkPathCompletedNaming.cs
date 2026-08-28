// -----------------------------------------------------------------------
// <copyright file="ForkPathCompletedNaming.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters;

/// <summary>
/// Names completed events and worker commands for fork-path step instances.
/// </summary>
/// <remarks>
/// <para>
/// Wolverine binds <c>Handle</c> by message CLR type. Linear steps and unique-type
/// fork paths keep <c>{StepType}Completed</c>. Fork-path instances that share a
/// type publish a path-qualified completed event so two parallel completions are
/// distinct CLR types.
/// </para>
/// <para>
/// Qualification reuses the start-command stem <c>{PhaseName}</c>
/// (<c>InstanceName ?? StepName</c>, loop-prefixed when inside a loop). When
/// <see cref="SagaEmissionContext.ForkPathsByRoutingKey"/> has more than one key
/// with that <see cref="PathRoutingKey.PhaseName"/>, the stem is
/// <c>{PathId}_{PhaseName}</c> (for example <c>Path0_AnalyzeStep</c>).
/// </para>
/// <para>
/// T1c saga <c>Handle</c> emitters must bind to these stems: dispatch
/// <c>Execute{stem}WorkerCommand</c> from <c>Start{startStem}Command</c> and handle
/// <c>{stem}Completed</c>. <c>startStem</c> equals <c>PhaseName</c> unless the
/// routing-key map has a colliding phase name, in which case it is the path-qualified
/// stem. Look the path up with <see cref="PathRoutingKey.ForFork"/>.
/// </para>
/// </remarks>
internal sealed class ForkPathCompletedNaming
{
    private readonly Dictionary<PathRoutingKey, string> _stemsByKey;
    private readonly HashSet<string> _qualifiedPhaseNames;
    private readonly HashSet<string> _sharedTypes;
    private readonly IReadOnlyList<QualifiedForkPathInstance> _qualifiedInstances;

    private ForkPathCompletedNaming(
        Dictionary<PathRoutingKey, string> stemsByKey,
        HashSet<string> qualifiedPhaseNames,
        HashSet<string> sharedTypes,
        IReadOnlyList<QualifiedForkPathInstance> qualifiedInstances)
    {
        _stemsByKey = stemsByKey;
        _qualifiedPhaseNames = qualifiedPhaseNames;
        _sharedTypes = sharedTypes;
        _qualifiedInstances = qualifiedInstances;
    }

    /// <summary>
    /// Gets the fork-path instances whose completed message is path-qualified.
    /// </summary>
    public IReadOnlyList<QualifiedForkPathInstance> QualifiedInstances => _qualifiedInstances;

    /// <summary>
    /// Builds naming for <paramref name="model"/> from
    /// <see cref="SagaEmissionContext.ForkPathsByRoutingKey"/>.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <returns>The naming table for this model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    public static ForkPathCompletedNaming For(WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var forkPathsByRoutingKey = SagaEmissionContext.Create(model).ForkPathsByRoutingKey;
        var phaseNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in forkPathsByRoutingKey.Keys)
        {
            phaseNameCounts[key.PhaseName] = phaseNameCounts.TryGetValue(key.PhaseName, out var count)
                ? count + 1
                : 1;
        }

        var instances = new List<(PathRoutingKey Key, StepModel Step)>();
        if (model.Forks is not null)
        {
            foreach (var fork in model.Forks)
            {
                foreach (var path in fork.Paths)
                {
                    foreach (var step in path.Steps)
                    {
                        instances.Add((PathRoutingKey.ForFork(fork.ForkId, path.PathIndex, step.PhaseName), step));
                    }
                }
            }
        }

        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, step) in instances)
        {
            typeCounts[step.StepName] = typeCounts.TryGetValue(step.StepName, out var count)
                ? count + 1
                : 1;
        }

        var stemsByKey = new Dictionary<PathRoutingKey, string>();
        var qualifiedPhaseNames = new HashSet<string>(StringComparer.Ordinal);
        var sharedTypes = new HashSet<string>(StringComparer.Ordinal);
        var qualifiedInstances = new List<QualifiedForkPathInstance>();

        foreach (var (key, step) in instances)
        {
            if (typeCounts[step.StepName] <= 1)
            {
                stemsByKey[key] = step.StepName;
                continue;
            }

            sharedTypes.Add(step.StepName);
            phaseNameCounts.TryGetValue(key.PhaseName, out var phaseCount);
            var stem = phaseCount > 1
                ? $"{key.PathId}_{key.PhaseName}"
                : key.PhaseName;
            stemsByKey[key] = stem;
            qualifiedPhaseNames.Add(step.PhaseName);
            qualifiedInstances.Add(new QualifiedForkPathInstance(key, step, stem));
        }

        return new ForkPathCompletedNaming(
            stemsByKey,
            qualifiedPhaseNames,
            sharedTypes,
            qualifiedInstances);
    }

    /// <summary>
    /// Returns the message stem for a fork-path step: the step type when the type
    /// is unique on fork paths, otherwise the path-qualified stem.
    /// </summary>
    /// <param name="key">The routing key for this path instance.</param>
    /// <param name="stepType">The step's CLR type name (<see cref="StepModel.StepName"/>).</param>
    /// <returns>The stem used in <c>{stem}Completed</c> and <c>Execute{stem}WorkerCommand</c>.</returns>
    public string StemFor(PathRoutingKey key, string stepType) =>
        _stemsByKey.TryGetValue(key, out var stem) ? stem : stepType;

    /// <summary>
    /// Stem for <c>Start{stem}Command</c> on a fork-path instance.
    /// </summary>
    /// <param name="key">The routing key for this path instance.</param>
    /// <param name="stepType">The step's CLR type name.</param>
    /// <returns>
    /// <see cref="PathRoutingKey.PhaseName"/> when the type is unique on fork paths;
    /// otherwise the same stem as <see cref="StemFor"/> (phase name, or
    /// <c>{PathId}_{PhaseName}</c> when phase names collide).
    /// </returns>
    public string StartCommandStem(PathRoutingKey key, string stepType) =>
        IsSharedType(stepType) ? StemFor(key, stepType) : key.PhaseName;

    /// <summary>
    /// Returns whether <paramref name="phaseName"/> is a shared-type fork-path instance.
    /// </summary>
    /// <param name="phaseName">The step's phase name.</param>
    /// <returns><see langword="true"/> when this phase publishes a qualified completed event.</returns>
    public bool IsQualifiedPhase(string phaseName) => _qualifiedPhaseNames.Contains(phaseName);

    /// <summary>
    /// Returns whether <paramref name="stepType"/> appears on more than one fork-path instance.
    /// </summary>
    /// <param name="stepType">The step's CLR type name.</param>
    /// <returns><see langword="true"/> when fork-path instances share this type.</returns>
    public bool IsSharedType(string stepType) => _sharedTypes.Contains(stepType);

    /// <summary>
    /// Returns whether <paramref name="stepType"/> still needs the unqualified
    /// <c>{StepType}Completed</c> / <c>Execute{StepType}WorkerCommand</c> surface.
    /// </summary>
    /// <param name="stepType">The step's CLR type name.</param>
    /// <param name="model">The workflow model.</param>
    /// <returns>
    /// <see langword="true"/> when the type is unique on fork paths, or when a
    /// linear / non-fork use remains after qualification.
    /// </returns>
    public bool HasUnqualifiedUse(string stepType, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));

        if (!_sharedTypes.Contains(stepType))
        {
            return true;
        }

        if (model.Steps is null)
        {
            return false;
        }

        foreach (var step in model.Steps)
        {
            if (string.Equals(step.StepName, stepType, StringComparison.Ordinal)
                && !_qualifiedPhaseNames.Contains(step.PhaseName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns qualified instances whose step type is <paramref name="stepType"/>.
    /// </summary>
    /// <param name="stepType">The step's CLR type name.</param>
    /// <returns>The matching qualified instances, possibly empty.</returns>
    public IReadOnlyList<QualifiedForkPathInstance> InstancesForType(string stepType)
    {
        if (!_sharedTypes.Contains(stepType))
        {
            return [];
        }

        return [.. _qualifiedInstances.Where(i => string.Equals(i.Step.StepName, stepType, StringComparison.Ordinal))];
    }
}

/// <summary>
/// A fork-path step instance that publishes a path-qualified completed event.
/// </summary>
/// <param name="Key">The routing key for this path instance.</param>
/// <param name="Step">The step on that path.</param>
/// <param name="Stem">
/// The message stem: <c>PhaseName</c>, or <c>{PathId}_{PhaseName}</c> when more
/// than one routing key shares that phase name.
/// </param>
internal readonly record struct QualifiedForkPathInstance(
    PathRoutingKey Key,
    StepModel Step,
    string Stem)
{
    /// <summary>
    /// Gets a value indicating whether the stem includes <see cref="PathRoutingKey.PathId"/>
    /// because more than one routing key shares <see cref="PathRoutingKey.PhaseName"/>.
    /// </summary>
    public bool IsPathQualified =>
        string.Equals(Stem, $"{Key.PathId}_{Key.PhaseName}", StringComparison.Ordinal);
}
