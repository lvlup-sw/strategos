// -----------------------------------------------------------------------
// <copyright file="TimeoutWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>
/// Immutable state for the timeout behavioral fixtures (DR-4).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer the saga uses to fold each step's returned state. <see cref="StepCount"/>
/// is appended to by each step so a test can confirm how far the workflow actually
/// progressed through the generated saga.
/// </remarks>
[WorkflowState]
public sealed record TimeoutState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }
}

// NOTE: The slow and fast workflows deliberately share NO step types. The source
// generator emits worker handlers / commands / events per step TYPE within the
// same compilation, so a step type reused across two [Workflow] definitions would
// produce duplicate generated types (CS0101). Each workflow therefore gets its own
// kickoff/timed/follow-on step classes.

/// <summary>
/// Instant kickoff step for the slow scenario (no timeout). Exists because the
/// timed step must be a <c>.Then&lt;T&gt;(configure)</c> — <c>StartWith</c> has no
/// configure overload — so a workflow needs a leading non-timed step before the
/// timed one.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class SlowKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<TimeoutState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<TimeoutState>> ExecuteAsync(
        TimeoutState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(SlowKickoffStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<TimeoutState>.FromState(updated));
    }
}

/// <summary>
/// A deliberately slow step (sleeps ~500 ms) configured with a 50 ms timeout. The
/// saga's deadline-race timeout message therefore arrives long before this step's
/// <c>Completed</c> event, so the saga must route to its failure path while this
/// step is still in flight.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class SlowTimedStep(WorkflowInvocationLog log) : IWorkflowStep<TimeoutState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public async Task<StepResult<TimeoutState>> ExecuteAsync(
        TimeoutState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(SlowTimedStep));

        // Exceed the configured 50 ms deadline. This is an honest deadline race,
        // not hard cancellation: the step runs to completion; what matters is the
        // timeout message reaches the saga first. Do NOT honour the cancellation
        // token here — the saga-level deadline race does not cancel the in-flight
        // handler, and the test must observe the step running to completion.
        await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        var updated = state with { StepCount = state.StepCount + 1 };
        return StepResult<TimeoutState>.FromState(updated);
    }
}

/// <summary>
/// A step that must NOT run when the preceding <see cref="SlowTimedStep"/> times
/// out: if the timeout fires, the saga routes to Failed and <c>MarkCompleted()</c>
/// before ever cascading this step's start command. Its invocation count being
/// zero is the observable proof that the timeout routed away from the happy path.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class NeverReachedStep(WorkflowInvocationLog log) : IWorkflowStep<TimeoutState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<TimeoutState>> ExecuteAsync(
        TimeoutState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(NeverReachedStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<TimeoutState>.FromState(updated));
    }
}

/// <summary>
/// Instant kickoff step for the fast scenario (no timeout). Distinct from
/// <see cref="SlowKickoffStep"/> so the two workflows share no step type.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FastKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<TimeoutState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<TimeoutState>> ExecuteAsync(
        TimeoutState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FastKickoffStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<TimeoutState>.FromState(updated));
    }
}

/// <summary>
/// A fast step (~10 ms) configured with a generous 5 s timeout. It completes well
/// before its deadline, so its <c>Completed</c> event wins the race and the saga
/// chains forward; the timeout message arrives later as a no-op.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FastTimedStep(WorkflowInvocationLog log) : IWorkflowStep<TimeoutState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public async Task<StepResult<TimeoutState>> ExecuteAsync(
        TimeoutState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FastTimedStep));

        await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);

        var updated = state with { StepCount = state.StepCount + 1 };
        return StepResult<TimeoutState>.FromState(updated);
    }
}

/// <summary>
/// The follow-on step after <see cref="FastTimedStep"/> in the fast scenario. It
/// runs only because the fast step completed before its deadline; its completion
/// drives the saga to its terminal <c>Completed</c> phase.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FollowOnStep(WorkflowInvocationLog log) : IWorkflowStep<TimeoutState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<TimeoutState>> ExecuteAsync(
        TimeoutState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FollowOnStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<TimeoutState>.FromState(updated));
    }
}

/// <summary>
/// Workflow whose middle step exceeds its timeout: the saga must route to the
/// timeout/failure path while the slow step is still running, so the final
/// <see cref="NeverReachedStep"/> never executes.
/// </summary>
[Workflow("timeout-slow")]
public static partial class TimeoutSlowWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: kickoff → slow step (50 ms timeout, sleeps
    /// 500 ms) → a step that must not be reached if the timeout fires.
    /// </summary>
    public static WorkflowDefinition<TimeoutState> Definition => Workflow<TimeoutState>
        .Create("timeout-slow")
        .StartWith<SlowKickoffStep>()
        .Then<SlowTimedStep>(step => step
            .WithTimeout(TimeSpan.FromMilliseconds(50)))
        .Finally<NeverReachedStep>();
}

/// <summary>
/// Workflow whose timed step completes well within its (generous) deadline, so
/// the saga chains forward to completion and the later timeout message is a no-op.
/// </summary>
[Workflow("timeout-fast")]
public static partial class TimeoutFastWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: kickoff → fast step (5 s timeout, ~10 ms) →
    /// follow-on step that drives the saga to completion.
    /// </summary>
    public static WorkflowDefinition<TimeoutState> Definition => Workflow<TimeoutState>
        .Create("timeout-fast")
        .StartWith<FastKickoffStep>()
        .Then<FastTimedStep>(step => step
            .WithTimeout(TimeSpan.FromSeconds(5)))
        .Finally<FollowOnStep>();
}
