// -----------------------------------------------------------------------
// <copyright file="FailureHandlerChainTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (#140 G-3/CL-3) that a workflow-level
/// <c>OnFailure(flow =&gt; flow.Then&lt;NotifyFailure&gt;())</c> chain actually
/// RUNS at runtime when a step fails: the generated saga publishes the trigger,
/// dispatches the failure-handler worker command, the (previously missing) worker
/// handler runs the OnFailure handler step, and the saga routes to its terminal
/// Failed phase.
/// </summary>
/// <remarks>
/// <para>
/// Before this fix the workflow-level OnFailure chain was doubly dead: nothing
/// published the <c>Trigger{Pascal}FailureHandlerCommand</c> for a non-compensated
/// failing step, and even when dispatched, the
/// <c>ExecuteFailureHandler_..WorkerCommand</c> had no worker <c>Handle</c>, so the
/// handler step never executed. This test asserts the chain runs exactly once and
/// the saga reaches its terminal phase (its document is removed by
/// <c>MarkCompleted()</c>).
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<FailureHandlerHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class FailureHandlerChainTests
{
    private readonly FailureHandlerHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailureHandlerChainTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public FailureHandlerChainTests(FailureHandlerHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated failure-handler-proof workflow saga whose middle step
    /// always throws (with NO step-level resilience). Asserts the workflow-level
    /// <c>OnFailure</c> handler step <see cref="NotifyFailure"/> ran exactly once,
    /// the prepare step ran once, the terminal step never ran, and the saga reached
    /// its terminal (Failed) phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_WorkflowOnFailureChain_RunsHandlerStep()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new FailureHandlerState { WorkflowId = workflowId };
        var startCommand = new StartFailureHandlerProofCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<FailureHandlerProofSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal phase: the saga document is deleted by
        // MarkCompleted() on the terminal failure-handler completed handler, so its
        // absence is the terminal-Failed signal.
        await Assert.That(reachedTerminal).IsTrue();

        // The OnFailure handler step ran exactly once: this is the proof the
        // previously-dead chain now executes.
        await Assert.That(this.host.Invocations.CountFor(nameof(NotifyFailure))).IsEqualTo(1);

        // The deterministic prepare step ran once; the failing step ran (at least
        // once); the terminal step never ran because the failure routed the saga to
        // the OnFailure chain before the happy path.
        await Assert.That(this.host.Invocations.CountFor(nameof(FailureHandlerPrepareStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(FailureHandlerFailingStep))).IsGreaterThanOrEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(FailureHandlerNeverReachedStep))).IsEqualTo(0);
    }
}
