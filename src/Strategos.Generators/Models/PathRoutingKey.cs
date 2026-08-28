// -----------------------------------------------------------------------
// <copyright file="PathRoutingKey.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Construct that owns a path-end or in-path successor entry.
/// </summary>
internal enum PathConstructKind
{
    /// <summary>
    /// A parallel <c>Fork</c> path. Two paths may share a step type at once.
    /// </summary>
    Fork = 0,

    /// <summary>
    /// An exclusive <c>Branch</c> case. The same <see cref="PathRoutingKey.PhaseName"/>
    /// on two cases is still the exclusive-path name-collision diagnostic.
    /// </summary>
    Branch = 1,
}

/// <summary>
/// Identity-carrying key for path-end maps and in-path successor lookup.
/// </summary>
/// <remarks>
/// <para>
/// Routing maps must not key by bare CLR type name. <see cref="PhaseName"/> is
/// <c>InstanceName ?? StepName</c>, loop-prefixed when the construct sits inside a
/// loop — the same string the step list and <c>Start{PhaseName}Command</c> already use.
/// </para>
/// <para>
/// Forks are parallel, so two path-ends can share a <see cref="PhaseName"/>. The
/// (<see cref="Construct"/>, <see cref="ConstructId"/>, <see cref="PathId"/>) triple
/// keeps those entries distinct. Branches are exclusive; the duplicate-name diagnostic still rejects the
/// same <see cref="PhaseName"/> on two cases, and successor lookup for a branch
/// completion uses saga state (which case is live), not a last-write-win type map.
/// </para>
/// <para>
/// Events, worker, and saga <c>Handle</c> emitters must read this type from
/// <c>SagaEmissionContext.ForkPathsByRoutingKey</c>,
/// <c>SagaEmissionContext.BranchPathsByRoutingKey</c>, and
/// <see cref="MainFlowClassification.TryGetSuccessorWithinPath(PathRoutingKey, out string)"/>.
/// Do not invent a second key.
/// </para>
/// </remarks>
/// <param name="Construct">Whether this entry belongs to a fork path or a branch case.</param>
/// <param name="ConstructId">The fork id or branch id that owns the path.</param>
/// <param name="PathId">
/// Fork: <c>Path{n}</c> (zero-based path index, matching <c>Path{n}Status</c>).
/// Branch: the case's <c>BranchPathPrefix</c>.
/// </param>
/// <param name="PhaseName">The step's phase name (effective name, loop-prefixed when inside a loop).</param>
internal readonly record struct PathRoutingKey(
    PathConstructKind Construct,
    string ConstructId,
    string PathId,
    string PhaseName)
{
    /// <summary>
    /// Builds a fork-path routing key.
    /// </summary>
    /// <param name="forkId">The fork's id.</param>
    /// <param name="pathIndex">The zero-based path index.</param>
    /// <param name="phaseName">The step's phase name on that path.</param>
    /// <returns>A key whose <see cref="PathId"/> is <c>Path{pathIndex}</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required string is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a required string is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pathIndex"/> is negative.</exception>
    public static PathRoutingKey ForFork(string forkId, int pathIndex, string phaseName)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(forkId, nameof(forkId));
        ThrowHelper.ThrowIfLessThan(pathIndex, 0, nameof(pathIndex));
        ThrowHelper.ThrowIfNullOrWhiteSpace(phaseName, nameof(phaseName));
        return new PathRoutingKey(PathConstructKind.Fork, forkId, $"Path{pathIndex}", phaseName);
    }

    /// <summary>
    /// Builds a branch-case routing key.
    /// </summary>
    /// <param name="branchId">The branch's id.</param>
    /// <param name="branchPathPrefix">The case's path prefix.</param>
    /// <param name="phaseName">The step's phase name on that case.</param>
    /// <returns>A key whose <see cref="PathId"/> is <paramref name="branchPathPrefix"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required string is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a required string is empty or whitespace.</exception>
    public static PathRoutingKey ForBranch(string branchId, string branchPathPrefix, string phaseName)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(branchId, nameof(branchId));
        ThrowHelper.ThrowIfNullOrWhiteSpace(branchPathPrefix, nameof(branchPathPrefix));
        ThrowHelper.ThrowIfNullOrWhiteSpace(phaseName, nameof(phaseName));
        return new PathRoutingKey(PathConstructKind.Branch, branchId, branchPathPrefix, phaseName);
    }
}
