// -----------------------------------------------------------------------
// <copyright file="EventSourcedFailureWorkflow.cs" company="Levelup Software">
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
/// Immutable, event-sourced state for the <c>StepFailed</c> audit-event proof
/// (#138 G-5 Task 5.2). Implements <see cref="IEventSourcedState{TState}"/> so the
/// EventSourced-mode saga applies each appended event via <see cref="ApplyEvent"/>.
/// </summary>
public sealed record EventSourcedFailureState : IEventSourcedState<EventSourcedFailureState>
{
    /// <summary>
    /// Gets the Marten aggregate identity (the event stream id, equal to
    /// <see cref="WorkflowId"/>). Required because the generated <c>Add...Workflow()</c>
    /// registers an inline <c>Snapshot&lt;EventSourcedFailureState&gt;</c> projection,
    /// which only builds when the state satisfies Marten's single-stream aggregation
    /// conventions (an <c>Id</c> identity plus at least one matching <c>Apply</c>).
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
    /// event the saga appends to the stream). The <c>StepFailed</c> audit event has
    /// no <c>Apply</c> overload, so Marten's inline aggregation tolerates and skips
    /// it while it still lands in the raw stream the test reads.
    /// </summary>
    /// <param name="evt">The prepare-step completed event.</param>
    /// <returns>The aggregate seeded with the stream id and the step counted.</returns>
    public EventSourcedFailureState Apply(EventSourcedFailurePrepareStepCompleted evt) =>
        this with { Id = evt.WorkflowId, WorkflowId = evt.WorkflowId, StepCount = this.StepCount + 1 };

    /// <inheritdoc />
    public EventSourcedFailureState ApplyEvent(IProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt, nameof(evt));

        // The saga's in-memory fold. This fixture asserts on the StepFailed audit
        // event landing in the stream, not on folded state, so every event passes
        // through unchanged (audit events are observational, not state-bearing).
        return this;
    }
}

/// <summary>
/// Permanent failure raised by <see cref="EventSourcedFailingStep"/> on EVERY
/// invocation, so the saga routes to the workflow-level <c>OnFailure</c> chain
/// and the trigger handler appends the <c>StepFailed</c> audit event.
/// </summary>
public sealed class EventSourcedFailureException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcedFailureException"/>
    /// class with the supplied message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public EventSourcedFailureException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Entry step of the event-sourced failure fixture. Deterministic; records its
/// invocation and returns new state.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedFailurePrepareStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedFailureState>> ExecuteAsync(
        EventSourcedFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedFailurePrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedFailureState>.FromState(updated));
    }
}

/// <summary>
/// The failing step. Declares NO step-level resilience and throws on EVERY
/// invocation, forcing the saga to route to the workflow-level <c>OnFailure</c>
/// chain whose trigger handler appends the <c>StepFailed</c> audit event.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedFailingStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedFailureState>> ExecuteAsync(
        EventSourcedFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedFailingStep));

        throw new EventSourcedFailureException(
            "EventSourcedFailingStep always fails to force the OnFailure chain and the StepFailed audit event.");
    }
}

/// <summary>
/// The workflow-level <c>OnFailure</c> handler step run when
/// <see cref="EventSourcedFailingStep"/> fails. Records its invocation and
/// returns new state.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedNotifyFailureStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedFailureState>> ExecuteAsync(
        EventSourcedFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedNotifyFailureStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedFailureState>.FromState(updated));
    }
}

/// <summary>
/// A step that must NOT run when the preceding <see cref="EventSourcedFailingStep"/>
/// fails: once the saga routes to the OnFailure chain it reaches Failed before ever
/// cascading this step. Present only to satisfy the generator's
/// <c>Finally&lt;T&gt;()</c> terminator requirement (AGWF010).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedFailureNeverReachedStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedFailureState>> ExecuteAsync(
        EventSourcedFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedFailureNeverReachedStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedFailureState>.FromState(updated));
    }
}

/// <summary>
/// The event-sourced failure fixture workflow definition (#138 G-5 Task 5.2).
/// Declares <c>Persistence = PersistenceMode.EventSourced</c> and a failing middle
/// step with a workflow-level <c>OnFailure</c> chain, so the generator emits a saga
/// whose trigger handler appends the <c>StepFailed</c> audit event to the Marten
/// stream. Drives the generator to emit <c>EventSourcedFailureProofSaga</c>,
/// <c>StartEventSourcedFailureProofCommand</c>, and
/// <c>AddEventSourcedFailureProofWorkflow()</c>.
/// </summary>
[Workflow("event-sourced-failure-proof", Persistence = PersistenceMode.EventSourced)]
public static partial class EventSourcedFailureProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step, a middle step that always
    /// throws with NO step-level resilience, and a workflow-level <c>OnFailure</c>
    /// chain that runs <see cref="EventSourcedNotifyFailureStep"/>.
    /// </summary>
    public static WorkflowDefinition<EventSourcedFailureState> Definition => Workflow<EventSourcedFailureState>
        .Create("event-sourced-failure-proof")
        .StartWith<EventSourcedFailurePrepareStep>()
        .Then<EventSourcedFailingStep>()
        .OnFailure(flow => flow.Then<EventSourcedNotifyFailureStep>().Complete())
        .Finally<EventSourcedFailureNeverReachedStep>();
}
