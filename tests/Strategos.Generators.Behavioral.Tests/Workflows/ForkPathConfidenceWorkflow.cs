// -----------------------------------------------------------------------
// <copyright file="ForkPathConfidenceWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Agents.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>
/// Immutable, event-sourced state for the fork-path confidence-gate proof
/// (DR-4 / #145 gap A). Implements <see cref="IEventSourcedState{TState}"/> so the
/// EventSourced-mode saga folds each appended event via <see cref="ApplyEvent"/> and
/// Marten's inline snapshot projection builds from the stream.
/// </summary>
public sealed record ForkPathConfidenceState : IEventSourcedState<ForkPathConfidenceState>
{
    /// <summary>
    /// Gets the Marten aggregate identity (the event stream id, equal to
    /// <see cref="WorkflowId"/>). Required because the generated
    /// <c>AddForkPathConfidenceWorkflow()</c> registers an inline
    /// <c>Snapshot&lt;ForkPathConfidenceState&gt;</c> projection, which only builds when
    /// the state satisfies Marten's single-stream aggregation conventions (an <c>Id</c>
    /// identity plus at least one matching <c>Apply</c>).
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of step-completed events folded into state so far.
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// Marten aggregation fold for the intake step's completed event (the first event
    /// the saga appends to the stream). The <c>LowConfidenceRouted</c> audit event has no
    /// <c>Apply</c> overload, so Marten's inline aggregation tolerates and skips it while
    /// it still lands in the raw stream the test reads.
    /// </summary>
    /// <param name="evt">The intake-step completed event.</param>
    /// <returns>The aggregate seeded with the stream id and the step counted.</returns>
    public ForkPathConfidenceState Apply(ForkConfIntakeStepCompleted evt) =>
        this with { Id = evt.WorkflowId, WorkflowId = evt.WorkflowId, StepCount = this.StepCount + 1 };

    /// <inheritdoc />
    public ForkPathConfidenceState ApplyEvent(IProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt, nameof(evt));

        // The saga's in-memory fold. This fixture asserts on the LowConfidenceRouted
        // audit event landing in the stream, not on folded state, so every event passes
        // through unchanged (audit events are observational, not state-bearing).
        return this;
    }
}

/// <summary>
/// Entry step of the fork-path confidence fixture (runs before the fork).
/// Deterministic; records its invocation and returns new state.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ForkConfIntakeStep(WorkflowInvocationLog log) : IWorkflowStep<ForkPathConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ForkPathConfidenceState>> ExecuteAsync(
        ForkPathConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ForkConfIntakeStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ForkPathConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated fork-path step (the LAST — and only — step of the first fork
/// path). Returns a step result whose <c>Confidence</c> is 0.5 — below the 0.85
/// threshold — so the generated fork path-completed handler's confidence gate routes to
/// <see cref="ForkConfReviewStep"/> AND appends the <c>LowConfidenceRouted</c> audit
/// event, instead of marking the path complete and joining.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ForkConfAssessStep(WorkflowInvocationLog log) : IWorkflowStep<ForkPathConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ForkPathConfidenceState>> ExecuteAsync(
        ForkPathConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ForkConfAssessStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ForkPathConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The OnLowConfidence handler step for the gated fork path. Runs only when the fork
/// path's confidence gate routes to it (confidence below threshold). As a single-step
/// OnLowConfidence handler it terminates the workflow (the generated handler calls
/// <c>MarkCompleted()</c>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ForkConfReviewStep(WorkflowInvocationLog log) : IWorkflowStep<ForkPathConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ForkPathConfidenceState>> ExecuteAsync(
        ForkPathConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ForkConfReviewStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ForkPathConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The single step of the SECOND fork path (deterministic, not confidence-gated). It
/// runs so the fork is well-formed; its completion marks path 1 succeeded but the join
/// never fires because path 0 diverted to the handler and never reached Success.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ForkConfSecondPathStep(WorkflowInvocationLog log) : IWorkflowStep<ForkPathConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ForkPathConfidenceState>> ExecuteAsync(
        ForkPathConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ForkConfSecondPathStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ForkPathConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The join step. It must NOT run when the gated path diverts on low confidence: the
/// gate routes path 0 away before it is marked succeeded, so the join readiness check
/// never becomes true. Records its invocation so the test can assert it was skipped.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ForkConfAggregateStep(WorkflowInvocationLog log) : IWorkflowStep<ForkPathConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ForkPathConfidenceState>> ExecuteAsync(
        ForkPathConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ForkConfAggregateStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ForkPathConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The terminal step after the join. It must NOT run when the gated path diverts on low
/// confidence (the join never fires). Records its invocation so the test can assert it
/// was skipped.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ForkConfSettleStep(WorkflowInvocationLog log) : IWorkflowStep<ForkPathConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ForkPathConfidenceState>> ExecuteAsync(
        ForkPathConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ForkConfSettleStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ForkPathConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The fork-path confidence fixture workflow definition (DR-4 / #145 gap A). Declares
/// <c>Persistence = PersistenceMode.EventSourced</c> and a <c>Fork</c> whose first path's
/// last step (<see cref="ForkConfAssessStep"/>) is confidence-gated (returns 0.5, below
/// the 0.85 threshold) with an <c>OnLowConfidence</c> branch that runs
/// <see cref="ForkConfReviewStep"/>. The generated fork path-completed handler's
/// confidence gate must route to the handler AND append the <c>LowConfidenceRouted</c>
/// audit event. Drives the generator to emit <c>ForkPathConfidenceSaga</c>,
/// <c>StartForkPathConfidenceCommand</c>, and <c>AddForkPathConfidenceWorkflow()</c>.
/// </summary>
[Workflow("fork-path-confidence", Persistence = PersistenceMode.EventSourced)]
public static partial class ForkPathConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: an intake step, a fork whose first path's last step is
    /// confidence-gated (returns 0.5, below the 0.85 threshold) with an
    /// <c>OnLowConfidence</c> handler, a deterministic second path, a join step and a
    /// terminal settle step — the join and settle steps must be skipped when the gate
    /// diverts.
    /// </summary>
    public static WorkflowDefinition<ForkPathConfidenceState> Definition =>
        Workflow<ForkPathConfidenceState>
            .Create("fork-path-confidence")
            .StartWith<ForkConfIntakeStep>()
            .Fork(
                path => path.Then<ForkConfAssessStep>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<ForkConfReviewStep>())),
                path => path.Then<ForkConfSecondPathStep>())
            .Join<ForkConfAggregateStep>()
            .Finally<ForkConfSettleStep>();
}
