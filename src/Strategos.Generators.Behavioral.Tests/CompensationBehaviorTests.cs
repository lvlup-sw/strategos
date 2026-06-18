// -----------------------------------------------------------------------
// <copyright file="CompensationBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (DR-3 T012) that a step's
/// <c>.WithRetry(2).Compensate&lt;RollbackStep&gt;()</c> actually runs its
/// compensation at runtime once retries are exhausted: the generated worker
/// <c>Configure(HandlerChain)</c> error chain publishes the trigger
/// failure-handler command, and the generated saga compensation chain dispatches
/// and runs the rollback step, then routes the saga to its terminal Failed phase.
/// </summary>
/// <remarks>
/// <para>
/// The compensated step throws on every invocation, so Wolverine retries it three
/// times total (initial + two retries) and then the lowered compensation path
/// fires. Before DR-3 the generator emitted a never-published trigger command and
/// the compensation step never ran; with DR-3 the rollback runs exactly once and
/// the saga reaches its terminal phase (the saga document is removed by
/// <c>MarkCompleted()</c>). Asserting exactly one rollback invocation plus terminal
/// completion proves the compensation lowering, not just that the saga eventually
/// stopped.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<CompensationHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class CompensationBehaviorTests
{
    private readonly CompensationHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompensationBehaviorTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public CompensationBehaviorTests(CompensationHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated compensation-proof workflow saga whose middle step
    /// declares <c>.WithRetry(2).Compensate&lt;RollbackStep&gt;()</c> and throws on
    /// every invocation. Asserts the compensation <see cref="RollbackStep"/> ran
    /// exactly once, the failing step ran three times (initial + two retries), the
    /// terminal step never ran, and the saga reached its terminal (Failed) phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_RetryExhaustedWithCompensate_RunsCompensationOnceAndTransitionsToFailed()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new CompensationState { WorkflowId = workflowId };
        var startCommand = new StartCompensationProofCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompensationProofSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal phase: the saga document is deleted by
        // MarkCompleted() on the compensation completed handler, so its absence is
        // the terminal-Failed signal.
        await Assert.That(reachedTerminal).IsTrue();

        // The compensation step ran exactly once: this is the compensation proof.
        await Assert.That(this.host.Invocations.CountFor(nameof(RollbackStep))).IsEqualTo(1);

        // The failing step ran three times (initial attempt + two retries lowered
        // from .WithRetry(2)) before compensation fired.
        await Assert.That(this.host.Invocations.CountFor(nameof(CompensatedFailingStep))).IsEqualTo(3);

        // The deterministic prepare step ran once; the terminal step never ran
        // because compensation routed the saga to Failed before the happy path.
        await Assert.That(this.host.Invocations.CountFor(nameof(CompensationPrepareStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(CompensationNeverReachedStep))).IsEqualTo(0);
    }
}
