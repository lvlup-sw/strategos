// -----------------------------------------------------------------------
// <copyright file="TimeoutBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof that a step's <c>.WithTimeout(t)</c> lowers into a
/// working Wolverine saga deadline race (DR-4), run against a real PostgreSQL
/// container.
/// </summary>
/// <remarks>
/// <para>
/// This is a saga-level deadline race, not hard cancellation of an in-flight
/// handler: the slow step runs to completion; what is proven is that the saga
/// routes to its timeout/failure path because the scheduled timeout message
/// reached the saga before the step's completion event.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares a single
/// process-wide container + host and observes a process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<TimeoutHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class TimeoutBehaviorTests
{
    private readonly TimeoutHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten timeout host fixture, injected by TUnit and
    /// shared across the entire test session.
    /// </param>
    public TimeoutBehaviorTests(TimeoutHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Proves that when a step exceeds its timeout, the saga routes to the
    /// timeout/failure path while the slow step is still in flight: the step after
    /// the timed step (<see cref="NeverReachedStep"/>) never executes, because the
    /// timeout drove the saga to Failed + <c>MarkCompleted()</c> before its start
    /// command could be cascaded.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepExceedsTimeout_RoutesToTimeoutPath()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new TimeoutState { WorkflowId = workflowId };
        var startCommand = new StartTimeoutSlowCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<TimeoutSlowSaga>(
            workflowId,
            startCommand);

        // The saga reached a terminal phase (its document was removed by
        // MarkCompleted on the timeout/failure route).
        await Assert.That(reachedTerminal).IsTrue();

        // The kickoff and the slow step both ran (the slow step runs to completion
        // — this is a deadline race, not hard cancellation).
        await Assert.That(this.host.Invocations.CountFor(nameof(SlowKickoffStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(SlowTimedStep))).IsEqualTo(1);

        // The decisive observation: the step AFTER the timed step never ran. The
        // timeout routed the saga away from the happy path before its start
        // command could be cascaded.
        await Assert.That(this.host.Invocations.CountFor(nameof(NeverReachedStep))).IsEqualTo(0);
    }

    /// <summary>
    /// Proves that when a step completes before its (generous) timeout, the saga
    /// chains forward normally and the later timeout message is a harmless no-op:
    /// the follow-on step runs and the workflow reaches its terminal phase without
    /// being double-failed.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepCompletesBeforeTimeout_TimeoutHandlerIsNoOp()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new TimeoutState { WorkflowId = workflowId };
        var startCommand = new StartTimeoutFastCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<TimeoutFastSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal Completed phase (document removed).
        await Assert.That(reachedTerminal).IsTrue();

        // Every step ran exactly once: the fast step completed before its deadline
        // and the saga chained forward to the follow-on step.
        await Assert.That(this.host.Invocations.CountFor(nameof(FastKickoffStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(FastTimedStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(FollowOnStep))).IsEqualTo(1);

        // No replays / double-runs: the late timeout message was a no-op (the saga
        // was already gone, so Wolverine ignored the TimeoutMessage), and nothing
        // re-ran the chain.
        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(3);
    }
}
