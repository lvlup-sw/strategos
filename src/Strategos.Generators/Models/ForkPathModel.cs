// -----------------------------------------------------------------------
// <copyright file="ForkPathModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Represents a single path within a fork construct for code generation.
/// </summary>
/// <remarks>
/// <para>
/// Fork path models capture the structure of parallel execution paths.
/// The source generator uses this model to emit:
/// - Path-specific phase enum values with path index prefix
/// - Path status tracking properties in saga state
/// - Path step handlers with join readiness checks.
/// </para>
/// <para>
/// Each path carries its steps as configured <see cref="StepModel"/> records (mirroring the
/// top-level and loop emitters' step model) rather than bare names, so per-step configuration
/// declared via <c>Then&lt;TStep&gt;(step =&gt; step.ValidateState(...))</c> is preserved and lowers
/// into the saga exactly as a top-level/loop step's does.
/// </para>
/// </remarks>
/// <param name="PathIndex">The zero-based index of this path within the fork.</param>
/// <param name="Steps">The ordered list of configured steps in this path.</param>
/// <param name="HasFailureHandler">Whether this path has a failure handler defined.</param>
/// <param name="IsTerminalOnFailure">Whether the failure handler terminates without recovery.</param>
/// <param name="FailureHandlerStepNames">The optional list of failure handler step names.</param>
internal sealed record ForkPathModel(
    int PathIndex,
    IReadOnlyList<StepModel> Steps,
    bool HasFailureHandler,
    bool IsTerminalOnFailure,
    IReadOnlyList<string>? FailureHandlerStepNames = null)
{
    /// <summary>
    /// Gets the ordered phase names of the steps in this path.
    /// </summary>
    /// <remarks>
    /// Projects <see cref="Steps"/> to their <see cref="StepModel.PhaseName"/> (which includes the
    /// loop prefix when the path is inside a loop). Emitters that key off step phase names continue
    /// to consume this projection unchanged.
    /// </remarks>
    public IReadOnlyList<string> StepNames => [.. Steps.Select(s => s.PhaseName)];

    /// <summary>
    /// Gets the first step name in this path.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Steps"/> is empty.</exception>
    public string FirstStepName => Steps.Count > 0
        ? Steps[0].PhaseName
        : throw new InvalidOperationException("Cannot access FirstStepName: Steps is empty.");

    /// <summary>
    /// Gets the last step name in this path.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Steps"/> is empty.</exception>
    public string LastStepName => Steps.Count > 0
        ? Steps[Steps.Count - 1].PhaseName
        : throw new InvalidOperationException("Cannot access LastStepName: Steps is empty.");

    /// <summary>
    /// Gets the property name for tracking this path's status.
    /// </summary>
    /// <remarks>
    /// Returns "Path{N}Status" where N is the path index (e.g., "Path0Status", "Path1Status").
    /// </remarks>
    public string StatusPropertyName => $"Path{PathIndex}Status";

    /// <summary>
    /// Gets the property name for storing this path's final state.
    /// </summary>
    /// <remarks>
    /// Returns "Path{N}State" where N is the path index (e.g., "Path0State", "Path1State").
    /// </remarks>
    public string StatePropertyName => $"Path{PathIndex}State";

    /// <summary>
    /// Creates a new <see cref="ForkPathModel"/> with validation.
    /// </summary>
    /// <param name="pathIndex">The zero-based index of this path. Must be non-negative.</param>
    /// <param name="steps">The ordered list of configured steps. Must have at least one step.</param>
    /// <param name="hasFailureHandler">Whether this path has a failure handler defined.</param>
    /// <param name="isTerminalOnFailure">Whether the failure handler terminates without recovery.</param>
    /// <param name="failureHandlerStepNames">The optional list of failure handler step names.</param>
    /// <returns>A validated <see cref="ForkPathModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="steps"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="steps"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pathIndex"/> is negative.</exception>
    public static ForkPathModel Create(
        int pathIndex,
        IReadOnlyList<StepModel> steps,
        bool hasFailureHandler,
        bool isTerminalOnFailure,
        IReadOnlyList<string>? failureHandlerStepNames = null)
    {
        ThrowHelper.ThrowIfNull(steps, nameof(steps));
        ThrowHelper.ThrowIfLessThan(pathIndex, 0, nameof(pathIndex));

        if (steps.Count == 0)
        {
            throw new ArgumentException("Fork path must have at least one step.", nameof(steps));
        }

        return new ForkPathModel(
            PathIndex: pathIndex,
            Steps: steps,
            HasFailureHandler: hasFailureHandler,
            IsTerminalOnFailure: isTerminalOnFailure,
            FailureHandlerStepNames: failureHandlerStepNames);
    }
}
