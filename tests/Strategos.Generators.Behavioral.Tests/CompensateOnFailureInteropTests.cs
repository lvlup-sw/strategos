// -----------------------------------------------------------------------
// <copyright file="CompensateOnFailureInteropTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (#140 Task 3.2) that a workflow declaring BOTH a
/// step-level <c>.Compensate&lt;T&gt;()</c> AND a workflow-level <c>OnFailure</c>
/// chain composes them in the fixed order: step compensation runs FIRST, then the
/// OnFailure chain — all from a single merged saga <c>Handle(Trigger…)</c> site
/// (no CS0111 collision).
/// </summary>
/// <remarks>
/// <para>
/// Before this fix the two were mutually exclusive: the
/// <c>SagaCompensationComponentEmitter</c> no-op'd whenever the workflow also
/// declared OnFailure, so the compensation step never ran when both were present.
/// This test asserts the rollback runs exactly once, THEN the OnFailure handler
/// step runs exactly once, and the saga reaches its terminal phase.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<CompensateOnFailureHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class CompensateOnFailureInteropTests
{
    private readonly CompensateOnFailureHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompensateOnFailureInteropTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public CompensateOnFailureInteropTests(CompensateOnFailureHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated interop workflow saga whose middle step declares
    /// <c>.Compensate&lt;CofRollbackStep&gt;()</c> and always throws, with a
    /// workflow-level <c>OnFailure</c> chain. Asserts the compensation rollback ran
    /// exactly once, the OnFailure handler step ran exactly once AFTER the rollback,
    /// the terminal step never ran, and the saga reached its terminal (Failed) phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_StepCompensateAndWorkflowOnFailure_RunsCompensationThenFailureChain()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new CompensateOnFailureState { WorkflowId = workflowId };
        var startCommand = new StartCompensateOnFailureProofCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<CompensateOnFailureProofSaga>(
            workflowId,
            startCommand);

        // The saga reached its terminal phase: the saga document is deleted by
        // MarkCompleted() on the terminal OnFailure completed handler.
        await Assert.That(reachedTerminal).IsTrue();

        // Both the compensation rollback AND the OnFailure handler step ran exactly
        // once: this is the interop proof (previously mutually exclusive).
        await Assert.That(this.host.Invocations.CountFor(nameof(CofRollbackStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(CofNotifyFailure))).IsEqualTo(1);

        // Fixed ordering: the rollback ran BEFORE the OnFailure handler step.
        var invocations = this.host.Invocations.Invocations;
        var rollbackIndex = invocations.ToList().IndexOf(nameof(CofRollbackStep));
        var notifyIndex = invocations.ToList().IndexOf(nameof(CofNotifyFailure));
        await Assert.That(rollbackIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(notifyIndex).IsGreaterThan(rollbackIndex);

        // The prepare step ran once; the terminal step never ran.
        await Assert.That(this.host.Invocations.CountFor(nameof(CofPrepareStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(CofNeverReachedStep))).IsEqualTo(0);
    }
}
