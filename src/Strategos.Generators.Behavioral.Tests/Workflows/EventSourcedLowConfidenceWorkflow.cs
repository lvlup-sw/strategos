// -----------------------------------------------------------------------
// <copyright file="EventSourcedLowConfidenceWorkflow.cs" company="Levelup Software">
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
/// Immutable, event-sourced state for the <c>LowConfidenceRouted</c> audit-event
/// proof (#138 G-5 Task 5.3). Implements <see cref="IEventSourcedState{TState}"/>
/// so the EventSourced-mode saga applies each appended event via
/// <see cref="ApplyEvent"/>.
/// </summary>
public sealed record EventSourcedLowConfidenceState : IEventSourcedState<EventSourcedLowConfidenceState>
{
    /// <summary>
    /// Gets the Marten aggregate identity (the event stream id, equal to
    /// <see cref="WorkflowId"/>). Required because the generated <c>Add...Workflow()</c>
    /// registers an inline <c>Snapshot&lt;EventSourcedLowConfidenceState&gt;</c>
    /// projection, which only builds when the state satisfies Marten's single-stream
    /// aggregation conventions (an <c>Id</c> identity plus at least one matching <c>Apply</c>).
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
    /// Marten aggregation fold for the prepare step's completed event (the first
    /// event the saga appends to the stream). The <c>LowConfidenceRouted</c> audit
    /// event has no <c>Apply</c> overload, so Marten's inline aggregation tolerates
    /// and skips it while it still lands in the raw stream the test reads.
    /// </summary>
    /// <param name="evt">The prepare-step completed event.</param>
    /// <returns>The aggregate seeded with the stream id and the step counted.</returns>
    public EventSourcedLowConfidenceState Apply(EventSourcedLcPrepareStepCompleted evt) =>
        this with { Id = this.WorkflowId, StepCount = this.StepCount + 1 };

    /// <inheritdoc />
    public EventSourcedLowConfidenceState ApplyEvent(IProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt, nameof(evt));

        // The saga's in-memory fold. This fixture asserts on the LowConfidenceRouted
        // audit event landing in the stream, not on folded state, so every event
        // passes through unchanged (audit events are observational, not state-bearing).
        return this;
    }
}

/// <summary>
/// Entry step of the event-sourced low-confidence fixture. Deterministic;
/// records its invocation and returns new state.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedLcPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedLowConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedLowConfidenceState>> ExecuteAsync(
        EventSourcedLowConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedLcPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedLowConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated step. Returns a step result whose <c>Confidence</c> is
/// 0.5 — below the 0.85 threshold — so the generated EventSourced saga's
/// confidence gate routes to <see cref="EventSourcedLcReviewStep"/> AND appends
/// the <c>LowConfidenceRouted</c> audit event to the Marten stream.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedLcClassifyStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedLowConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedLowConfidenceState>> ExecuteAsync(
        EventSourcedLowConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedLcClassifyStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedLowConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The low-confidence handler step. Runs only when the confidence gate routes to
/// it (confidence below threshold). As a single-step OnLowConfidence handler it
/// terminates the workflow (the generated handler calls <c>MarkCompleted()</c>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedLcReviewStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedLowConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedLowConfidenceState>> ExecuteAsync(
        EventSourcedLowConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedLcReviewStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedLowConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The primary finish step. It must NOT run when confidence is low, because the
/// gate diverts to the handler branch first.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedLcFinishStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedLowConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedLowConfidenceState>> ExecuteAsync(
        EventSourcedLowConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedLcFinishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedLowConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The event-sourced low-confidence fixture workflow definition (#138 G-5 Task
/// 5.3). Declares <c>Persistence = PersistenceMode.EventSourced</c> and a
/// confidence-gated classify step (returns 0.5, below the 0.85 threshold) whose
/// <c>OnLowConfidence</c> branch runs <see cref="EventSourcedLcReviewStep"/>, so the
/// generated saga's confidence gate appends the <c>LowConfidenceRouted</c> audit
/// event to the Marten stream. Drives the generator to emit
/// <c>EventSourcedLowConfidenceSaga</c>, <c>StartEventSourcedLowConfidenceCommand</c>,
/// and <c>AddEventSourcedLowConfidenceWorkflow()</c>.
/// </summary>
[Workflow("event-sourced-low-confidence", Persistence = PersistenceMode.EventSourced)]
public static partial class EventSourcedLowConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step, a confidence-gated classify
    /// step (returns 0.5, below the 0.85 threshold) whose <c>OnLowConfidence</c>
    /// branch runs <see cref="EventSourcedLcReviewStep"/>, and a primary finish step
    /// that should be skipped when confidence is low.
    /// </summary>
    public static WorkflowDefinition<EventSourcedLowConfidenceState> Definition =>
        Workflow<EventSourcedLowConfidenceState>
            .Create("event-sourced-low-confidence")
            .StartWith<EventSourcedLcPrepareStep>()
            .Then<EventSourcedLcClassifyStep>(step => step
                .RequireConfidence(0.85)
                .OnLowConfidence(alt => alt.Then<EventSourcedLcReviewStep>()))
            .Finally<EventSourcedLcFinishStep>();
}
