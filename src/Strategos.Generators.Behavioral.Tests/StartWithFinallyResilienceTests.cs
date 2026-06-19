// -----------------------------------------------------------------------
// <copyright file="StartWithFinallyResilienceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (#141) that per-step resilience declared inline on the
/// ENTRY step via <c>StartWith&lt;TStep&gt;(s =&gt; s.WithRetry(...))</c> and on the
/// TERMINAL step via <c>Finally&lt;TStep&gt;(s =&gt; s.WithTimeout(...))</c> is honored at
/// runtime, run against a real PostgreSQL-backed Wolverine+Marten host.
/// </summary>
/// <remarks>
/// <para>
/// These two tests are the mutation proof for the #141 builder overloads. Before the
/// overloads existed, the entry and terminal steps could carry no config, so the
/// generator emitted no per-handler <c>Configure(HandlerChain)</c> retry policy for the
/// entry step and no timeout deadline race for the terminal step. Reverting the builder
/// change re-breaks the workflow definitions at compile time (the configure lambda no
/// longer binds), so these proofs cannot pass without the lowered emission.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because they share the single
/// process-wide container + host and observe the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<WolverineHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class StartWithFinallyResilienceTests
{
    private readonly WolverineHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartWithFinallyResilienceTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared across the
    /// entire test session.
    /// </param>
    public StartWithFinallyResilienceTests(WolverineHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Proves that <c>.WithRetry(2)</c> declared on the ENTRY step via the new
    /// <c>StartWith</c> configure overload is honored at runtime: the flaky entry step
    /// throws on its first two invocations and succeeds on the third, so Wolverine's
    /// generated per-handler retry policy must carry it through. Asserts the entry step
    /// was invoked exactly three times (initial + two retries) and the saga reached its
    /// terminal phase — neither of which happens without the retry lowering, since
    /// without it the entry step would dead-letter on the first failure and the saga
    /// would never reach its terminal finish step.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_StartWithStepWithRetry_RetriesIndependently()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new StartFinallyState { WorkflowId = workflowId };
        var startCommand = new StartStartWithRetryProofCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<StartWithRetryProofSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal phase: it only gets past the flaky ENTRY step
        // (and on to the terminal finish step + MarkCompleted) if the retry policy
        // lowered from StartWith(s => s.WithRetry(2)) carried it through two transient
        // failures.
        await Assert.That(completed).IsTrue();

        // The flaky ENTRY step ran exactly three times: the initial attempt plus the two
        // retries lowered from .StartWith<StartFlakyStep>(s => s.WithRetry(2)). This is
        // the StartWith retry proof.
        await Assert.That(this.host.Invocations.CountFor(nameof(StartFlakyStep))).IsEqualTo(3);

        // The deterministic terminal finish step ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(StartFinishStep))).IsEqualTo(1);
    }

    /// <summary>
    /// Proves that <c>.WithTimeout(50 ms)</c> declared on the TERMINAL step via the new
    /// <c>Finally</c> configure overload is honored at runtime: the terminal step sleeps
    /// ~3 s, so the saga's deadline-race timeout message reaches it before the step's
    /// completion event and routes the saga to its timeout/failure path while the step is
    /// still in flight. Asserts the kickoff and the slow terminal step both ran (the slow
    /// step runs to completion — a deadline race, not hard cancellation) AND, route-
    /// specifically, that the timeout PREEMPTED the completion: the slow step found its
    /// own saga document already removed when it finished
    /// (<see cref="FinallySlowTimedStep.TimeoutPreemptedMarker"/>). That last assertion is
    /// what prevents a false pass on a normal completion — both terminals delete the saga,
    /// so "reached terminal" alone cannot distinguish the timeout route.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_FinallyStepWithTimeout_RoutesToTimeoutPath()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new StartFinallyState { WorkflowId = workflowId };
        var startCommand = new StartFinallyTimeoutProofCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<FinallyTimeoutProofSaga>(
            workflowId,
            startCommand);

        // The saga reached a terminal phase: its document was removed by MarkCompleted on
        // the timeout/failure route driven by the deadline race lowered from
        // .Finally<FinallySlowTimedStep>(s => s.WithTimeout(50 ms)).
        await Assert.That(reachedTerminal).IsTrue();

        // The kickoff and the slow TERMINAL step both ran (the slow step runs to
        // completion — this is a deadline race, not hard cancellation).
        await Assert.That(this.host.Invocations.CountFor(nameof(FinallyKickoffStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(FinallySlowTimedStep))).IsEqualTo(1);

        // ROUTE-SPECIFIC: the 50 ms timeout preempted the ~3 s step — the step found its
        // saga already gone (Failed terminal) when it finished. A normal completion would
        // leave the marker absent and fail here, so this cannot false-pass on the wrong
        // terminal path.
        await Assert.That(this.host.Invocations.CountFor(FinallySlowTimedStep.TimeoutPreemptedMarker))
            .IsEqualTo(1);
    }
}
