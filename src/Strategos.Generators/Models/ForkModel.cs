// -----------------------------------------------------------------------
// <copyright file="ForkModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Represents a fork construct (parallel execution) within a workflow for code generation.
/// </summary>
/// <remarks>
/// <para>
/// Fork models capture the structure of Fork/Join constructs in the workflow DSL.
/// The source generator uses this model to emit:
/// - Parallel dispatch commands for all fork paths
/// - Path status tracking properties in saga state
/// - Join readiness checks that wait for all paths to complete
/// - Join step handler that merges path states.
/// </para>
/// <para>
/// All fork paths execute concurrently. The join step executes only after
/// all paths reach a terminal status (Success, Failed, or FailedWithRecovery).
/// </para>
/// </remarks>
/// <param name="ForkId">The unique identifier for the fork point.</param>
/// <param name="PreviousStepName">The step that precedes this fork.</param>
/// <param name="Paths">The parallel execution paths (minimum 2 required).</param>
/// <param name="JoinStepName">The step where all paths converge.</param>
internal sealed record ForkModel(
    string ForkId,
    string PreviousStepName,
    IReadOnlyList<ForkPathModel> Paths,
    string JoinStepName)
{
    /// <summary>
    /// Gets the number of parallel paths in this fork.
    /// </summary>
    public int PathCount => Paths.Count;

    /// <summary>
    /// Gets whether any path can divert on low confidence.
    /// </summary>
    public bool HasConfidenceGate => Paths.Any(path => path.ConfidenceGateStep is not null);

    /// <summary>
    /// Gets the generated property prefix for a fork path.
    /// </summary>
    public string PathPropertyPrefix(int pathIndex) => $"Fork_{ForkId.Replace("-", "_")}_Path{pathIndex}";

    /// <summary>
    /// Gets a value indicating whether any path has a failure handler.
    /// </summary>
    public bool HasAnyFailureHandler => Paths.Any(p => p.HasFailureHandler);

    /// <summary>
    /// Gets a value indicating whether any path terminates on failure.
    /// </summary>
    public bool HasAnyTerminalFailure => Paths.Any(p => p.IsTerminalOnFailure);

    /// <summary>
    /// Gets the handler method name for initiating the fork.
    /// </summary>
    public string ForkHandlerMethodName => $"HandleFork_{ForkId}";

    /// <summary>
    /// Gets the handler method name for checking join readiness.
    /// </summary>
    public string JoinReadinessMethodName => $"CheckJoinReady_{ForkId}";

    /// <summary>
    /// Creates a new <see cref="ForkModel"/> with validation.
    /// </summary>
    /// <param name="forkId">The unique identifier for the fork point. Cannot be null or whitespace.</param>
    /// <param name="previousStepName">The step that precedes this fork. Cannot be null or whitespace.</param>
    /// <param name="paths">The parallel execution paths. Must have at least 2 paths.</param>
    /// <param name="joinStepName">The step where paths converge. Cannot be null or whitespace.</param>
    /// <returns>A validated <see cref="ForkModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static ForkModel Create(
        string forkId,
        string previousStepName,
        IReadOnlyList<ForkPathModel> paths,
        string joinStepName)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(forkId, nameof(forkId));
        ThrowHelper.ThrowIfNullOrWhiteSpace(previousStepName, nameof(previousStepName));
        ThrowHelper.ThrowIfNull(paths, nameof(paths));
        ThrowHelper.ThrowIfNullOrWhiteSpace(joinStepName, nameof(joinStepName));

        if (paths.Count < 2)
        {
            throw new ArgumentException("Fork must have at least two paths.", nameof(paths));
        }

        return new ForkModel(
            ForkId: forkId,
            PreviousStepName: previousStepName,
            Paths: paths,
            JoinStepName: joinStepName);
    }
}
