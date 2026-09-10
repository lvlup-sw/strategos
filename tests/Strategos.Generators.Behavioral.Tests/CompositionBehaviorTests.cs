// -----------------------------------------------------------------------
// <copyright file="CompositionBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (epic #135, DR-10, T019/T020/T021) that the four
/// lowered step-resilience capabilities COMPOSE and behave at the edges when
/// declared together on a SINGLE step:
/// <c>.WithRetry(n).WithTimeout(t).Compensate&lt;T&gt;().RequireConfidence(x)
/// .OnLowConfidence(alt =&gt; alt.Then&lt;H&gt;())</c>, run against a real
/// PostgreSQL container.
/// </summary>
/// <remarks>
/// <para>
/// This is the first time all four are exercised together. They lower onto
/// disjoint generated surfaces — retry + compensation on the worker handler's
/// <c>Configure(HandlerChain)</c>, timeout as a cascaded saga deadline race, and
/// the confidence gate in the step's completed handler — so they compose without
/// a duplicate-<c>Handle</c> collision. The tests assert the design's precedence:
/// <list type="bullet">
///   <item><description>
///     <b>T019a</b> transient-then-success + HIGH confidence: retried, completes
///     on the primary path (no compensation, no low-confidence route).
///   </description></item>
///   <item><description>
///     <b>T019b</b> first-try success + LOW confidence: routes to the
///     OnLowConfidence handler, NOT compensation (success ≠ failure).
///   </description></item>
///   <item><description>
///     <b>T019c</b> always-throws: retries exhaust → compensation runs → Failed.
///   </description></item>
///   <item><description>
///     <b>T020</b> timeout firing mid-retry: fails-by-timeout exactly once; a late
///     completion / retry does not resurrect the terminated saga.
///   </description></item>
///   <item><description>
///     <b>T021</b> INV-7: every retry attempt receives the same immutable input.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because these share the single
/// process-wide container + host and observe the process-shared invocation log
/// and probes.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<CompositionHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class CompositionBehaviorTests
{
    private readonly CompositionHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositionBehaviorTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten composition host fixture, injected by TUnit and
    /// shared across the entire test session.
    /// </param>
    public CompositionBehaviorTests(CompositionHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// T019a — composition precedence: a step with all four resilience capabilities
    /// that throws on attempts 1-2 then succeeds on attempt 3 with HIGH confidence
    /// (0.9 ≥ 0.85). Asserts the composed retry carried it through (three attempts),
    /// the saga proceeded on the primary path to the finish step, and NEITHER the
    /// low-confidence handler NOR the compensation ran.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepWithAllResilience_RetriesThenHighConfidenceCompletesNotCompensates()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new CompositionState { WorkflowId = workflowId };
        var startCommand = new StartCompositionTransientCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompositionTransientSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal phase via the primary finish step's
        // MarkCompleted() — it only gets there if retry carried the step through.
        await Assert.That(reachedTerminal).IsTrue();

        // Retry precedence: the all-four step ran exactly three times (initial +
        // two retries lowered from .WithRetry(2)), composed with the other three.
        await Assert.That(this.host.Invocations.CountFor(nameof(TransientAllResilienceStep))).IsEqualTo(3);

        // Kickoff + the primary finish step each ran once (high-confidence success).
        await Assert.That(this.host.Invocations.CountFor(nameof(TransientKickoffStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(TransientFinishStep))).IsEqualTo(1);

        // Precedence proof: success with acceptable confidence did NOT route to the
        // OnLowConfidence handler...
        await Assert.That(this.host.Invocations.CountFor(nameof(TransientReviewStep))).IsEqualTo(0);

        // ...and did NOT trip the compensation (failure) path.
        await Assert.That(this.host.Invocations.CountFor(nameof(TransientRollbackStep))).IsEqualTo(0);
    }

    /// <summary>
    /// T019b — composition precedence: a step with all four resilience capabilities
    /// that SUCCEEDS on its first try but returns LOW confidence (0.5 &lt; 0.85).
    /// Asserts that success-but-low-confidence routes to the OnLowConfidence handler
    /// (NOT compensation — a low-confidence success is not a failure) and the
    /// primary finish step is skipped.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepWithAllResilience_FirstTryLowConfidenceRoutesNotCompensates()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new CompositionState { WorkflowId = workflowId };
        var startCommand = new StartCompositionLowConfidenceCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompositionLowConfidenceSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal phase via the OnLowConfidence handler's
        // MarkCompleted().
        await Assert.That(reachedTerminal).IsTrue();

        // The step succeeded on its FIRST attempt (no retry needed).
        await Assert.That(this.host.Invocations.CountFor(nameof(LowConfAllResilienceStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(LowConfKickoffStep))).IsEqualTo(1);

        // Precedence proof: confidence 0.5 < 0.85 → the OnLowConfidence handler ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(LowConfReviewStep))).IsEqualTo(1);

        // ...the compensation (failure) path did NOT run — a low-confidence success
        // is not a failure, so it must not roll back...
        await Assert.That(this.host.Invocations.CountFor(nameof(LowConfRollbackStep))).IsEqualTo(0);

        // ...and the primary finish step was skipped (the gate diverted to the
        // handler, which completed the saga).
        await Assert.That(this.host.Invocations.CountFor(nameof(LowConfFinishStep))).IsEqualTo(0);
    }

    /// <summary>
    /// T019c — composition precedence: a step with all four resilience capabilities
    /// that throws on EVERY attempt. Asserts the composed retry exhausts (three
    /// attempts), the compensation (rollback) runs exactly once, the saga reaches
    /// its terminal Failed phase, and NEITHER the low-confidence handler NOR the
    /// finish step run (the step never produced a completed event).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepWithAllResilience_RetriesExhaustThenCompensatesNotLowConfidence()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new CompositionState { WorkflowId = workflowId };
        var startCommand = new StartCompositionFailCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompositionFailSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal Failed phase: the compensation completed
        // handler calls MarkCompleted(), removing the persisted saga document.
        await Assert.That(reachedTerminal).IsTrue();

        // Retry precedence: the always-throwing step ran exactly three times
        // (initial + two retries) before retries exhausted.
        await Assert.That(this.host.Invocations.CountFor(nameof(FailAllResilienceStep))).IsEqualTo(3);

        // Compensation precedence: the rollback ran exactly once after exhaustion.
        await Assert.That(this.host.Invocations.CountFor(nameof(FailRollbackStep))).IsEqualTo(1);

        // The kickoff ran once.
        await Assert.That(this.host.Invocations.CountFor(nameof(FailKickoffStep))).IsEqualTo(1);

        // Precedence proof: a thrown (never-completed) step did NOT route to the
        // confidence handler (there was no completed event to gate)...
        await Assert.That(this.host.Invocations.CountFor(nameof(FailReviewStep))).IsEqualTo(0);

        // ...and the happy-path finish step never ran (compensation routed to Failed).
        await Assert.That(this.host.Invocations.CountFor(nameof(FailFinishStep))).IsEqualTo(0);
    }

    /// <summary>
    /// T020 — timeout-vs-retry race + late-completion idempotency: a step with
    /// <c>.WithRetry(3, 200ms).WithTimeout(50ms)</c> whose attempts each sleep
    /// ~100ms, so the 50ms saga deadline fires mid-retry. Asserts the saga
    /// fails-by-timeout exactly once (reaches its terminal Failed phase, observed by
    /// saga-document absence) and that the late completion / continued retry
    /// activity does NOT resurrect or re-fail the saga: the step after the timed
    /// step never runs, and the timeout fired only after at least one attempt
    /// started (the deadline raced mid-retry).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_TimeoutDuringRetry_FailsByTimeoutIdempotently_LateCompletedDoesNotResurrect()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        this.host.Race.Reset();

        var initialState = new CompositionState { WorkflowId = workflowId };
        var startCommand = new StartCompositionRaceCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompositionRaceSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal Failed phase exactly once: the timeout's
        // MarkCompleted() removed the saga document, and the absence is the
        // authoritative terminal signal. (Idempotency: the race-guarded timeout
        // handler routes to Failed only while the step's phase is current; the
        // saga's document then being gone means no later message re-failed it.)
        await Assert.That(reachedTerminal).IsTrue();

        // The kickoff ran once; at least one slow attempt started (so the timeout
        // genuinely raced an in-flight retry, not a never-started step).
        await Assert.That(this.host.Invocations.CountFor(nameof(RaceKickoffStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RaceSlowStep))).IsGreaterThanOrEqualTo(1);
        await Assert.That(this.host.Race.AttemptStarts.Count).IsGreaterThanOrEqualTo(1);

        // Late-completion idempotency: the step AFTER the timed step never ran. The
        // timeout routed the saga to Failed + MarkCompleted() before its start
        // command could be cascaded, and no late completion / retry resurrected the
        // saga to push the chain forward.
        await Assert.That(this.host.Invocations.CountFor(nameof(RaceNeverReachedStep))).IsEqualTo(0);

        // Give any in-flight late retry one more poll budget to (not) resurrect the
        // saga, then re-confirm it stayed terminal and never reached the next step.
        await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        await Assert.That(this.host.Invocations.CountFor(nameof(RaceNeverReachedStep))).IsEqualTo(0);
    }

    /// <summary>
    /// T021 — INV-7: a retried step (<c>.WithRetry(2)</c>) that throws twice then
    /// succeeds records the input <see cref="CompositionState"/> instance it
    /// received on each attempt. Asserts every attempt received the SAME immutable
    /// input value (Wolverine re-delivers the same envelope across retries; the saga
    /// never mutates the input across attempts).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepRetried_EachAttemptReceivesIdenticalImmutableState()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        this.host.Immutable.Reset();

        var initialState = new CompositionState { WorkflowId = workflowId, Token = "immutable-run-token" };
        var startCommand = new StartCompositionImmutableCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompositionImmutableSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal Completed phase (the step eventually
        // succeeded on its third attempt and the finish step completed).
        await Assert.That(reachedTerminal).IsTrue();

        // The retried step ran exactly three times (initial + two retries).
        await Assert.That(this.host.Invocations.CountFor(nameof(ImmutableRetriedStep))).IsEqualTo(3);

        // INV-7: the probe captured one input per attempt.
        var seenInputs = this.host.Immutable.SeenInputs;
        await Assert.That(seenInputs.Count).IsEqualTo(3);

        // Every attempt received the SAME immutable input value: identical record
        // value (structural equality) across all attempts, and the per-run token was
        // never mutated across retries.
        var first = seenInputs[0];
        foreach (var seen in seenInputs)
        {
            await Assert.That(seen).IsEqualTo(first);
            await Assert.That(seen.Token).IsEqualTo("immutable-run-token");
            await Assert.That(seen.WorkflowId).IsEqualTo(workflowId);
        }
    }

    /// <summary>
    /// T021 (property-style) — INV-7 over a couple of distinct
    /// <see cref="CompositionState"/> shapes. Reruns the identical-immutable-input
    /// assertion with varying initial token + step-count seeds to confirm the
    /// invariant holds independent of the input shape: every retry attempt of the
    /// same step always receives the same immutable input value the saga handed it,
    /// never a mutated one.
    /// </summary>
    /// <param name="token">The per-run token seeded into the initial state.</param>
    /// <param name="initialStepCount">The initial step count seeded into the state.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("alpha-shape", 0)]
    [Arguments("beta-shape", 7)]
    [Arguments("", 42)]
    public async Task Saga_StepRetried_ImmutableInputInvariantHoldsAcrossStateShapes(
        string token,
        int initialStepCount)
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        this.host.Immutable.Reset();

        var initialState = new CompositionState
        {
            WorkflowId = workflowId,
            Token = token,
            StepCount = initialStepCount,
        };
        var startCommand = new StartCompositionImmutableCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompositionImmutableSaga>(
            workflowId,
            startCommand);

        await Assert.That(reachedTerminal).IsTrue();

        // The retried step ran three times (initial + two retries) for every shape.
        await Assert.That(this.host.Invocations.CountFor(nameof(ImmutableRetriedStep))).IsEqualTo(3);

        var seenInputs = this.host.Immutable.SeenInputs;
        await Assert.That(seenInputs.Count).IsEqualTo(3);

        // Property: for ANY input shape, every attempt sees the same immutable value
        // the kickoff folded forward (the kickoff increments StepCount once before
        // the retried step), with the seeded token preserved across all attempts.
        var first = seenInputs[0];
        foreach (var seen in seenInputs)
        {
            await Assert.That(seen).IsEqualTo(first);
            await Assert.That(seen.Token).IsEqualTo(token);
            await Assert.That(seen.WorkflowId).IsEqualTo(workflowId);
            await Assert.That(seen.StepCount).IsEqualTo(initialStepCount + 1);
        }
    }
}
