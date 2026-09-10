// -----------------------------------------------------------------------
// <copyright file="HostFixtureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// End-to-end behavioral test that compiles a Strategos workflow (the source
/// generator emits its Wolverine+Marten saga) and RUNS it against a real
/// PostgreSQL container, asserting happy-path completion.
/// </summary>
/// <remarks>
/// <para>
/// This is the acceptance test for the reusable runtime host fixture
/// (<see cref="WolverineHostFixture"/>) that later behavioral tasks
/// (retry/timeout/compensation/confidence) build on. It proves a generated
/// saga runs to completion on a real host with real Marten-backed saga
/// storage and inbox/outbox.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares a single
/// process-wide container + host and observes a process-shared invocation
/// log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<WolverineHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class HostFixtureTests
{
    private readonly WolverineHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostFixtureTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public HostFixtureTests(WolverineHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated happy-path workflow saga and awaits completion,
    /// asserting that every step ran exactly once and the saga reached its
    /// terminal/completed phase (the saga document is removed by
    /// <c>MarkCompleted()</c>).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Host_FixtureWorkflow_StartsAndCompletesHappyPath()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new HappyPathState { WorkflowId = workflowId };
        var startCommand = new StartHappyPathCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync(workflowId, startCommand);

        // The saga reached its terminal phase: MarkCompleted() removed the
        // persisted saga document, which the host observes as completion.
        await Assert.That(completed).IsTrue();

        // Each instrumented step ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(RecordFirstStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RecordSecondStep))).IsEqualTo(1);

        // Total step invocations across the run is exactly two (no replays).
        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(2);
    }
}
