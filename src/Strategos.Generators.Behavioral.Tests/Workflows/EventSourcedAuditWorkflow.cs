// -----------------------------------------------------------------------
// <copyright file="EventSourcedAuditWorkflow.cs" company="Levelup Software">
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
/// Immutable, event-sourced state for the audit-event behavioral fixtures
/// (#138 G-5). Implements <see cref="IEventSourcedState{TState}"/> so the
/// generated saga, which runs in <see cref="PersistenceMode.EventSourced"/>
/// mode, applies each appended event via <see cref="ApplyEvent"/> (a pure fold)
/// instead of the reducer pattern.
/// </summary>
/// <remarks>
/// <para>
/// In EventSourced mode the generated saga handlers call
/// <c>session.Events.Append(WorkflowId, evt)</c> and then
/// <c>State = State.ApplyEvent(evt)</c>; the appended events round-trip through
/// the Marten event stream, which is exactly the surface the audit-event tests
/// inspect.
/// </para>
/// </remarks>
public sealed record EventSourcedAuditState : IEventSourcedState<EventSourcedAuditState>
{
    /// <summary>
    /// Gets the Marten aggregate identity (the event stream id, equal to
    /// <see cref="WorkflowId"/>). The generated <c>Add...Workflow()</c> registers an
    /// inline <c>Snapshot&lt;EventSourcedAuditState&gt;</c> projection, so this state
    /// must satisfy Marten's single-stream aggregation conventions (an <c>Id</c>
    /// identity plus <c>Create</c>/<c>Apply</c> methods) for the host to start.
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
    /// Marten single-stream aggregation seed: builds the aggregate from the
    /// workflow's <c>Started</c> event (the first event in every stream).
    /// </summary>
    /// <param name="started">The generated workflow-started event.</param>
    /// <returns>The seed aggregate keyed on the workflow id.</returns>
    public static EventSourcedAuditState Create(EventSourcedHappyStarted started)
    {
        ArgumentNullException.ThrowIfNull(started, nameof(started));
        return new EventSourcedAuditState { Id = started.WorkflowId, WorkflowId = started.WorkflowId };
    }

    /// <summary>
    /// Marten aggregation fold for the prepare step's completed event.
    /// </summary>
    /// <param name="evt">The prepare-step completed event.</param>
    /// <returns>The aggregate with the step counted.</returns>
    public EventSourcedAuditState Apply(EventSourcedHappyStepCompleted evt) =>
        this with { StepCount = this.StepCount + 1 };

    /// <summary>
    /// Marten aggregation fold for the finish step's completed event.
    /// </summary>
    /// <param name="evt">The finish-step completed event.</param>
    /// <returns>The aggregate with the step counted.</returns>
    public EventSourcedAuditState Apply(EventSourcedHappyFinishStepCompleted evt) =>
        this with { StepCount = this.StepCount + 1 };

    /// <inheritdoc />
    public EventSourcedAuditState ApplyEvent(IProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt, nameof(evt));

        // The saga's in-memory fold (INV-1): count every step-completed event.
        // Unrecognized events (Started, audit events) pass through unchanged —
        // they are observational, not state-bearing.
        return evt switch
        {
            EventSourcedHappyStepCompleted => this with { StepCount = this.StepCount + 1 },
            EventSourcedHappyFinishStepCompleted => this with { StepCount = this.StepCount + 1 },
            _ => this,
        };
    }
}

/// <summary>
/// Entry step of the event-sourced happy-path fixture (#138 G-5 Task 5.1).
/// Deterministic; records its invocation and returns new state.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedHappyStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedAuditState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedAuditState>> ExecuteAsync(
        EventSourcedAuditState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedHappyStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedAuditState>.FromState(updated));
    }
}

/// <summary>
/// Terminal step of the event-sourced happy-path fixture. As the workflow's
/// <c>Finally</c> step, its completion drives the saga to its terminal phase and
/// <c>MarkCompleted()</c>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EventSourcedHappyFinishStep(WorkflowInvocationLog log) : IWorkflowStep<EventSourcedAuditState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<EventSourcedAuditState>> ExecuteAsync(
        EventSourcedAuditState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EventSourcedHappyFinishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<EventSourcedAuditState>.FromState(updated));
    }
}

/// <summary>
/// The event-sourced happy-path fixture workflow definition (#138 G-5 Task 5.1).
/// Declares <c>Persistence = PersistenceMode.EventSourced</c>, so the generator
/// emits a saga whose handlers append events to the Marten stream. Used by the
/// <c>EventSourcedHostFixture</c> smoke test to prove a baseline workflow event
/// round-trips through the stream. Drives the generator to emit
/// <c>EventSourcedHappySaga</c>, <c>StartEventSourcedHappyCommand</c>, and
/// <c>AddEventSourcedHappyWorkflow()</c>.
/// </summary>
[Workflow("event-sourced-happy", Persistence = PersistenceMode.EventSourced)]
public static partial class EventSourcedHappyWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step and a terminal finish step,
    /// both deterministic, run in event-sourced persistence mode.
    /// </summary>
    public static WorkflowDefinition<EventSourcedAuditState> Definition => Workflow<EventSourcedAuditState>
        .Create("event-sourced-happy")
        .StartWith<EventSourcedHappyStep>()
        .Finally<EventSourcedHappyFinishStep>();
}
