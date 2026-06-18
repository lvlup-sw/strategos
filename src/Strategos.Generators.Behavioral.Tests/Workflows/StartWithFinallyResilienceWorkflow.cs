// -----------------------------------------------------------------------
// <copyright file="StartWithFinallyResilienceWorkflow.cs" company="Levelup Software">
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
/// Immutable state for the StartWith/Finally step-config behavioral fixtures (#141).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer used by the saga to fold each step's returned state.
/// </remarks>
[WorkflowState]
public sealed record StartFinallyState : IWorkflowState
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

// NOTE: The two workflows below deliberately share NO step types. The source
// generator emits worker handlers / commands / events per step TYPE within the
// same compilation, so a step type reused across two [Workflow] definitions would
// produce duplicate generated types (CS0101). Each workflow therefore gets its own
// step classes.

/// <summary>
/// The ENTRY step of the StartWith-retry behavioral workflow, declared via the new
/// <c>StartWith&lt;TStep&gt;(step =&gt; step.WithRetry(2))</c> overload (#141). It is
/// deliberately flaky: it records every invocation in the shared
/// <see cref="WorkflowInvocationLog"/> and throws a transient exception on attempts 1
/// and 2, succeeding only on attempt 3. The recorded invocation count therefore equals
/// the number of attempts Wolverine made, which the behavioral test asserts to prove
/// the retry policy lowered from the <c>StartWith</c> configure lambda actually retried
/// the FIRST step.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class StartFlakyStep(WorkflowInvocationLog log) : IWorkflowStep<StartFinallyState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<StartFinallyState>> ExecuteAsync(
        StartFinallyState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        // Record FIRST, then decide based on this invocation's attempt number.
        this.log.Record(nameof(StartFlakyStep));
        var attempt = this.log.CountFor(nameof(StartFlakyStep));

        if (attempt < 3)
        {
            throw new TransientStepException(
                $"StartFlakyStep transient failure on attempt {attempt}.");
        }

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<StartFinallyState>.FromState(updated));
    }
}

/// <summary>
/// The terminal step of the StartWith-retry behavioral workflow. Deterministic (never
/// throws); records its invocation. As the workflow's <c>Finally</c> step, its
/// completion drives the saga to its terminal phase and <c>MarkCompleted()</c>, so the
/// test only observes it once the flaky entry step has finally succeeded.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class StartFinishStep(WorkflowInvocationLog log) : IWorkflowStep<StartFinallyState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<StartFinallyState>> ExecuteAsync(
        StartFinallyState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(StartFinishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<StartFinallyState>.FromState(updated));
    }
}

/// <summary>
/// The ENTRY step of the Finally-timeout behavioral workflow. Deterministic and fast;
/// records its invocation so the test can confirm the saga started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FinallyKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<StartFinallyState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<StartFinallyState>> ExecuteAsync(
        StartFinallyState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FinallyKickoffStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<StartFinallyState>.FromState(updated));
    }
}

/// <summary>
/// The TERMINAL step of the Finally-timeout behavioral workflow, declared via the new
/// <c>Finally&lt;TStep&gt;(step =&gt; step.WithTimeout(...))</c> overload (#141). It sleeps
/// ~500 ms while configured with a 50 ms timeout, so the saga's deadline-race timeout
/// message arrives long before this step's <c>Completed</c> event; the saga must route
/// to its timeout/failure path while this terminal step is still in flight. Its single
/// invocation (the step runs to completion — this is a deadline race, not hard
/// cancellation) together with the saga reaching a terminal phase via the timeout route
/// is the proof that the timeout lowered from the <c>Finally</c> configure lambda.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FinallySlowTimedStep(WorkflowInvocationLog log) : IWorkflowStep<StartFinallyState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public async Task<StepResult<StartFinallyState>> ExecuteAsync(
        StartFinallyState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FinallySlowTimedStep));

        // Exceed the configured 50 ms deadline. Honest deadline race: the step runs to
        // completion; what matters is the timeout message reaches the saga first. Do
        // NOT honour the cancellation token — the saga-level deadline race does not
        // cancel the in-flight handler.
        await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        var updated = state with { StepCount = state.StepCount + 1 };
        return StepResult<StartFinallyState>.FromState(updated);
    }
}

/// <summary>
/// The StartWith-retry behavioral workflow definition (#141). The entry step declares
/// <c>.WithRetry(2)</c> inline via the new <c>StartWith</c> configure overload. The
/// <see cref="WorkflowAttribute"/> drives the source generator to emit the saga, the
/// worker handler for <see cref="StartFlakyStep"/> carrying the generated
/// <c>Configure(HandlerChain)</c> retry policy, the start command
/// (<c>StartStartWithRetryProofCommand</c>), and the
/// <c>AddStartWithRetryProofWorkflow()</c> DI extension.
/// </summary>
[Workflow("start-with-retry-proof")]
public static partial class StartWithRetryProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a flaky ENTRY step declaring <c>.WithRetry(2)</c>
    /// (two retries after the initial attempt, up to three total invocations) via the
    /// new <c>StartWith</c> configure overload, then a terminal finish step whose
    /// completion the test waits on.
    /// </summary>
    public static WorkflowDefinition<StartFinallyState> Definition => Workflow<StartFinallyState>
        .Create("start-with-retry-proof")
        .StartWith<StartFlakyStep>(step => step.WithRetry(2))
        .Finally<StartFinishStep>();
}

/// <summary>
/// The Finally-timeout behavioral workflow definition (#141). The TERMINAL step declares
/// <c>.WithTimeout(50 ms)</c> inline via the new <c>Finally</c> configure overload and
/// then exceeds its deadline, so the saga must route to its timeout/failure path. The
/// <see cref="WorkflowAttribute"/> drives the source generator to emit the saga, the
/// worker handler for <see cref="FinallySlowTimedStep"/> carrying the generated timeout
/// deadline-race, the start command (<c>StartFinallyTimeoutProofCommand</c>), and the
/// <c>AddFinallyTimeoutProofWorkflow()</c> DI extension.
/// </summary>
[Workflow("finally-timeout-proof")]
public static partial class FinallyTimeoutProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a fast kickoff step, then a slow TERMINAL step
    /// declaring <c>.WithTimeout(50 ms)</c> via the new <c>Finally</c> configure overload
    /// while sleeping ~500 ms, so the timeout fires before its completion event.
    /// </summary>
    public static WorkflowDefinition<StartFinallyState> Definition => Workflow<StartFinallyState>
        .Create("finally-timeout-proof")
        .StartWith<FinallyKickoffStep>()
        .Finally<FinallySlowTimedStep>(step => step
            .WithTimeout(TimeSpan.FromMilliseconds(50)));
}
