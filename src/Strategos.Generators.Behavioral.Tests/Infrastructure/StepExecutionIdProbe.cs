// -----------------------------------------------------------------------
// <copyright file="StepExecutionIdProbe.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// One observation of the durable identity a step saw on its
/// <see cref="StepContext"/> for a single dispatch.
/// </summary>
/// <param name="StepName">The observing step's name.</param>
/// <param name="ExecutionId">The durable execution identity the step observed.</param>
/// <param name="RollbackId">The rollback identity, or <see langword="null"/> for forward execution.</param>
/// <param name="IsCompensation">Whether the step observed an inverse execution.</param>
public sealed record StepExecutionObservation(
    string StepName,
    Guid ExecutionId,
    Guid? RollbackId,
    bool IsCompensation);

/// <summary>
/// Process-shared observation sink for the <see cref="StepContext.ExecutionId"/>
/// idempotency contract. Instrumented workflow steps record the identity triple
/// they were handed by the generated worker handler, so a behavioral test can
/// assert that a redelivery of the same worker command reaches user code with the
/// same <see cref="StepContext.ExecutionId"/>, and that an inverse execution
/// surfaces its rollback identity there.
/// </summary>
/// <remarks>
/// Registered as a DI singleton on the host so every step resolved by the
/// generated <c>Add{Name}Workflow()</c> registration shares one instance. Reset at
/// the start of each test to isolate it from runs that shared the session host.
/// </remarks>
public sealed class StepExecutionIdProbe
{
    private readonly ConcurrentQueue<StepExecutionObservation> observations = new();

    /// <summary>
    /// Gets the ordered observations recorded so far.
    /// </summary>
    public IReadOnlyList<StepExecutionObservation> Observations => this.observations.ToArray();

    /// <summary>
    /// Records the durable identity the named step observed on its context.
    /// </summary>
    /// <param name="stepName">The step name (typically <c>nameof(TStep)</c>).</param>
    /// <param name="context">The step context handed to the step by the generated handler.</param>
    public void Record(string stepName, StepContext context)
    {
        ArgumentNullException.ThrowIfNull(stepName, nameof(stepName));
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        this.observations.Enqueue(new StepExecutionObservation(
            stepName,
            context.ExecutionId,
            context.RollbackId,
            context.IsCompensation));
    }

    /// <summary>
    /// Returns every observation recorded for the named step, in order.
    /// </summary>
    /// <param name="stepName">The step name to filter by.</param>
    /// <returns>The matching observations.</returns>
    public IReadOnlyList<StepExecutionObservation> For(string stepName)
    {
        ArgumentNullException.ThrowIfNull(stepName, nameof(stepName));

        return this.observations
            .Where(o => string.Equals(o.StepName, stepName, StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>
    /// Clears all recorded observations. Called at the start of each test.
    /// </summary>
    public void Reset()
    {
        this.observations.Clear();
    }
}
