// -----------------------------------------------------------------------
// <copyright file="CompositionEdgeWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

// =============================================================================
// Composition edge-case fixtures (epic #135, DR-10).
//
//   T020 — timeout-vs-retry race + late-completion idempotency.
//   T021 — INV-7: the same immutable input flows to every retry attempt.
//
// These reuse CompositionState (defined in CompositionWorkflow.cs) but get their
// own distinct step CLR types (the generator emits per-step-TYPE handlers within
// one compilation; reusing a type across [Workflow]s would emit duplicate
// generated types -> CS0101).
// =============================================================================

/// <summary>
/// Process-shared probe for the timeout-vs-retry race (T020). Counts how many
/// times the saga's timeout handler routed the workflow to its Failed terminal
/// (the saga must fail-by-timeout exactly ONCE even though retry/late-completion
/// machinery keeps firing), and whether a late step-completed event tried to
/// resurrect the already-terminated saga.
/// </summary>
/// <remarks>
/// Registered as a DI singleton on the host. The probe is a side channel to the
/// invocation log: the timeout-failure terminal is observed by saga-document
/// absence (the standard signal), and this probe additionally records the raw
/// attempt timeline so the test can confirm the deadline fired mid-retry.
/// </remarks>
public sealed class CompositionRaceProbe
{
    private readonly ConcurrentQueue<DateTimeOffset> attemptStarts = new();

    /// <summary>
    /// Gets the ordered attempt-start timestamps recorded by the slow retried step.
    /// </summary>
    public IReadOnlyList<DateTimeOffset> AttemptStarts => this.attemptStarts.ToArray();

    /// <summary>
    /// Records that the slow retried step began an attempt.
    /// </summary>
    public void RecordAttemptStart() => this.attemptStarts.Enqueue(DateTimeOffset.UtcNow);

    /// <summary>
    /// Clears the probe at the start of each test to isolate it from prior runs.
    /// </summary>
    public void Reset() => this.attemptStarts.Clear();
}

// -----------------------------------------------------------------------------
// T020 — race workflow.
// -----------------------------------------------------------------------------

/// <summary>Leading kickoff step for the race scenario.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RaceKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RaceKickoffStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// A slow retried step (T020): declares <c>.WithRetry(3, 200ms cooldown)</c> and
/// <c>.WithTimeout(50ms)</c>, and each attempt sleeps ~100ms — longer than the
/// 50ms deadline — so the scheduled saga timeout fires while the step is still
/// retrying. Each attempt eventually throws so Wolverine keeps retrying until the
/// timeout has long since routed the saga to Failed; the LATE completion / retry
/// activity must NOT resurrect the terminated saga (the timeout race guard + the
/// saga's absence are the idempotency proof).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
/// <param name="probe">The shared race probe injected by the host.</param>
public sealed class RaceSlowStep(WorkflowInvocationLog log, CompositionRaceProbe probe)
    : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;
    private readonly CompositionRaceProbe probe = probe;

    /// <inheritdoc />
    public async Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RaceSlowStep));
        this.probe.RecordAttemptStart();

        // Each attempt outlasts the 50ms deadline. Do NOT honour the cancellation
        // token: the saga-level deadline race does not hard-cancel the in-flight
        // handler — what matters is that the scheduled timeout message reaches the
        // saga first while the step is still mid-retry.
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);

        // Throw so Wolverine retries (per .WithRetry(3)). This keeps the step
        // re-firing AFTER the 50ms timeout has already terminated the saga, so a
        // late completion / retry must not be able to resurrect or re-fail it.
        throw new CompositionTransientException(
            "RaceSlowStep always throws so retries keep firing past the already-fired timeout deadline.");
    }
}

/// <summary>A step that must NOT run when the slow step times out: the timeout
/// routes the saga to Failed + <c>MarkCompleted()</c> before this step's start
/// command could be cascaded.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RaceNeverReachedStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RaceNeverReachedStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// T020 race workflow: the slow step's per-attempt sleep (~100ms) outlasts its
/// 50ms timeout while <c>.WithRetry(3, 200ms)</c> keeps it firing, so the
/// scheduled saga timeout wins the race mid-retry and routes the saga to Failed
/// exactly once; late completions / retries must not resurrect it.
/// </summary>
[Workflow("composition-race")]
public static partial class CompositionRaceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: kickoff -> slow retried+timed step
    /// (<c>.WithRetry(3, 200ms).WithTimeout(50ms)</c>, each attempt ~100ms) ->
    /// a step that must not be reached once the timeout fires.
    /// </summary>
    public static WorkflowDefinition<CompositionState> Definition => Workflow<CompositionState>
        .Create("composition-race")
        .StartWith<RaceKickoffStep>()
        .Then<RaceSlowStep>(step => step
            .WithRetry(3, TimeSpan.FromMilliseconds(200))
            .WithTimeout(TimeSpan.FromMilliseconds(50)))
        .Finally<RaceNeverReachedStep>();
}

// -----------------------------------------------------------------------------
// T021 — immutable-input workflow.
// -----------------------------------------------------------------------------

/// <summary>
/// Process-shared probe for INV-7 (T021): records the input
/// <see cref="CompositionState"/> instance a retried step received on each
/// attempt, so the test can confirm Wolverine re-delivers the SAME immutable
/// value across attempts (no mutation-across-attempts).
/// </summary>
/// <remarks>Registered as a DI singleton on the host.</remarks>
public sealed class CompositionImmutableProbe
{
    private readonly ConcurrentQueue<CompositionState> seenInputs = new();

    /// <summary>
    /// Gets the ordered sequence of input state instances seen across attempts.
    /// </summary>
    public IReadOnlyList<CompositionState> SeenInputs => this.seenInputs.ToArray();

    /// <summary>
    /// Records the input state instance an attempt received.
    /// </summary>
    /// <param name="input">The input state the step was handed this attempt.</param>
    public void RecordInput(CompositionState input)
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));
        this.seenInputs.Enqueue(input);
    }

    /// <summary>
    /// Clears the probe at the start of each test to isolate it from prior runs.
    /// </summary>
    public void Reset() => this.seenInputs.Clear();
}

/// <summary>Leading kickoff step for the immutable-input scenario. Seeds the
/// state token forward (folded into state) so the retried step's input carries a
/// stable, per-run-identifiable value.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImmutableKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImmutableKickoffStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// A transient retried step (T021): declares <c>.WithRetry(2)</c>, throws on its
/// first two attempts and succeeds on the third. On EVERY attempt it records the
/// input state instance it received into the shared
/// <see cref="CompositionImmutableProbe"/>, so the test can assert INV-7: each
/// retry attempt receives the SAME immutable input value (Wolverine re-delivers
/// the same envelope; the saga never mutates the input across attempts).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
/// <param name="probe">The shared immutable-input probe injected by the host.</param>
public sealed class ImmutableRetriedStep(WorkflowInvocationLog log, CompositionImmutableProbe probe)
    : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;
    private readonly CompositionImmutableProbe probe = probe;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImmutableRetriedStep));

        // Record the input this attempt received BEFORE deciding to throw, so the
        // probe captures the input on every attempt (including the failing ones).
        this.probe.RecordInput(state);

        var attempt = this.log.CountFor(nameof(ImmutableRetriedStep));
        if (attempt < 3)
        {
            throw new CompositionTransientException(
                $"ImmutableRetriedStep transient failure on attempt {attempt}.");
        }

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>Terminal step for the immutable-input scenario; its completion drives
/// the saga to its terminal Completed phase once the retried step succeeds.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImmutableFinishStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImmutableFinishStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// T021 immutable-input workflow: the retried step throws twice then succeeds,
/// recording its input on each attempt so the test can assert every attempt
/// received the same immutable <see cref="CompositionState"/> value (INV-7).
/// </summary>
[Workflow("composition-immutable")]
public static partial class CompositionImmutableWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: kickoff -> transient retried step
    /// (<c>.WithRetry(2)</c>, throws twice then succeeds) -> terminal finish.
    /// </summary>
    public static WorkflowDefinition<CompositionState> Definition => Workflow<CompositionState>
        .Create("composition-immutable")
        .StartWith<ImmutableKickoffStep>()
        .Then<ImmutableRetriedStep>(step => step.WithRetry(2))
        .Finally<ImmutableFinishStep>();
}
