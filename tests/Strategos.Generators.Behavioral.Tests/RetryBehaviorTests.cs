// -----------------------------------------------------------------------
// <copyright file="RetryBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (DR-2 T011) that a step's <c>.WithRetry(2)</c>
/// is actually honored at runtime: the generated
/// <c>Configure(HandlerChain)</c> per-handler policy causes Wolverine to retry
/// a transiently-failing step on a real PostgreSQL-backed host.
/// </summary>
/// <remarks>
/// <para>
/// The flaky step throws on attempts 1 and 2 and succeeds on attempt 3. Without
/// the generated retry policy the saga would dead-letter after the first
/// failure and never complete; with it, the step is retried until it succeeds
/// and the saga reaches its terminal phase (the saga document is removed by
/// <c>MarkCompleted()</c>). Asserting exactly three invocations plus completion
/// proves the retry lowering, not just that the saga eventually ran.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation
/// log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<WolverineHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class RetryBehaviorTests
{
    private readonly WolverineHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public RetryBehaviorTests(WolverineHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated retry-proof workflow saga whose middle step declares
    /// <c>.WithRetry(2)</c> and throws on its first two invocations. Asserts the
    /// flaky step was invoked exactly three times (initial + two retries) and
    /// the saga reached its terminal/completed phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepWithWithRetry2_InvokesStepExactlyTwiceThenSucceeds()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new RetryState { WorkflowId = workflowId };
        var startCommand = new StartRetryProofCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<RetryProofSaga>(workflowId, startCommand);

        // The saga reached its terminal phase: it only gets past the flaky step
        // (and on to the terminal finish step + MarkCompleted) if the retry
        // policy carried the step through its two transient failures.
        await Assert.That(completed).IsTrue();

        // The flaky step ran exactly three times: the initial attempt plus the
        // two retries lowered from .WithRetry(2). This is the retry proof.
        await Assert.That(this.host.Invocations.CountFor(nameof(RetryFlakyStep))).IsEqualTo(3);

        // The deterministic prepare and finish steps each ran exactly once (the
        // saga, not the retry policy, drives them).
        await Assert.That(this.host.Invocations.CountFor(nameof(RetryPrepareStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RetryFinishStep))).IsEqualTo(1);
    }
}
