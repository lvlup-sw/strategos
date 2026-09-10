// -----------------------------------------------------------------------
// <copyright file="FailureHandlerSuccessPathTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end proof that a workflow declaring a workflow-level <c>OnFailure</c> chain
/// reaches its declared terminal and completes when nothing fails.
/// </summary>
/// <remarks>
/// <para>
/// A workflow-level <c>OnFailure</c> lowers its handler steps as extra entries appended
/// to the workflow's step-name list, and they land AFTER the declared terminal. A
/// successor scan that decides "this is the last step" from list position therefore
/// hands the terminal an appended handler step as its successor: on the success path the
/// terminal cascades into the failure handler and the saga is never completed. The
/// terminal has to be classified as the last step on the MAIN FLOW, independent of what
/// any later block appended.
/// </para>
/// <para>
/// The sibling proof <see cref="FailureHandlerChainTests"/> exercises the same construct
/// on its FAILURE path, where the terminal is deliberately never reached — so it cannot
/// observe this at all. The two are complements, not duplicates.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the process-wide
/// container, host and invocation log with the failure-path proof, and resets that log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<FailureHandlerHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class FailureHandlerSuccessPathTests
{
    private readonly FailureHandlerHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailureHandlerSuccessPathTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine + Marten host fixture, injected by TUnit and shared across the
    /// whole test session.
    /// </param>
    public FailureHandlerSuccessPathTests(FailureHandlerHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The saga reaches the declared terminal step and completes THERE: the terminal runs
    /// exactly once, nothing runs after it, and the saga document is removed — with the run
    /// backed by observed step invocations rather than by document absence alone.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// "The document was removed" is deliberately not the whole oracle, and neither are the
    /// per-step counts. The declared failure chain ends in <c>Complete()</c>, so a terminal
    /// that wrongly cascades into the appended handler step STILL removes the document and
    /// still leaves each main-flow step at exactly one invocation — measured, not reasoned
    /// about. What separates the two routes is that the terminal is the last thing that ran.
    /// </remarks>
    [Test]
    public async Task Saga_FailureHandlerWorkflow_CompletesOnSuccessPath()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var startCommand = new StartOrderFulfillmentCommand(
            workflowId,
            new OrderFulfillmentState { WorkflowId = workflowId });

        var outcome = await this.host.RunToTerminalWithOutcomeAsync<OrderFulfillmentSaga>(
            workflowId,
            startCommand);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because(
                "the declared terminal must complete the saga even though the workflow-level "
                + $"OnFailure handler is appended after it in the step list — {outcome.Diagnostic}");

        // Exact per-step counts, never a whole-log total: a workflow that fails to
        // terminate outlives the test that started it and keeps incrementing the shared
        // log, so a total is not a stable oracle while any workflow on this host can hang.
        await Assert.That(this.host.Invocations.CountFor(nameof(ConfirmShipment)))
            .IsEqualTo(1)
            .Because("the declared terminal step runs exactly once on the success path");

        await Assert.That(this.host.Invocations.CountFor(nameof(ReserveInventory)))
            .IsEqualTo(1)
            .Because("the entry step runs exactly once");

        await Assert.That(this.host.Invocations.CountFor(nameof(ChargeCustomer)))
            .IsEqualTo(1)
            .Because("the guarded middle step succeeds, so it runs exactly once and is not retried");

        // The discriminating assertion: the terminal is where the saga ends, so nothing of
        // this workflow's may run after it. A terminal that cascades into the appended
        // failure-handler step satisfies every assertion above and fails only this one.
        var lastStep = this.host.Invocations.Invocations.LastOrDefault(IsOrderFulfillmentStep);

        await Assert.That(lastStep)
            .IsEqualTo(nameof(ConfirmShipment))
            .Because(
                "the declared terminal is the last step of the workflow to run; anything after "
                + "it means the terminal handed its successor to an appended off-main-flow step "
                + "instead of completing the saga");
    }

    /// <summary>
    /// The appended failure-handler step never runs on a route where nothing failed, and
    /// the steps that did run are exactly the main flow, in declaration order.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// The count and the ordered sequence are both asserted: a wrong successor that ran
    /// every step once but chained the terminal into the handler would satisfy a
    /// count-only oracle for the three main-flow steps.
    /// </remarks>
    [Test]
    public async Task Saga_FailureHandlerWorkflow_RunsHandlerZeroTimesWhenNothingFails()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var startCommand = new StartOrderFulfillmentCommand(
            workflowId,
            new OrderFulfillmentState { WorkflowId = workflowId });

        var outcome = await this.host.RunToTerminalWithOutcomeAsync<OrderFulfillmentSaga>(
            workflowId,
            startCommand);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because($"the run must reach the terminal before its route can be judged — {outcome.Diagnostic}");

        await Assert.That(this.host.Invocations.CountFor(nameof(RefundCharge)))
            .IsEqualTo(0)
            .Because(
                "nothing failed, so the workflow-level OnFailure handler step must never run; "
                + "it running is the terminal cascading into the appended handler instead of "
                + "completing the saga");

        // The recorded sequence is filtered to this fixture's own steps because the log is
        // shared with every other workflow on the session host, then compared as an ordered
        // sequence. Counts alone would accept a route that ran each step once but chained
        // the terminal into the handler.
        var observed = string.Join(
            " -> ",
            this.host.Invocations.Invocations.Where(IsOrderFulfillmentStep));

        var expected = string.Join(
            " -> ",
            nameof(ReserveInventory),
            nameof(ChargeCustomer),
            nameof(ConfirmShipment));

        await Assert.That(observed)
            .IsEqualTo(expected)
            .Because(
                "the success path runs the declared main flow in order and stops at the "
                + "terminal; any handler step in this sequence, or a terminal that is not last, "
                + "is a mis-resolved successor");
    }

    /// <summary>
    /// Decides whether a recorded step name belongs to the order-fulfillment fixture.
    /// </summary>
    /// <param name="stepName">The recorded step name.</param>
    /// <returns><see langword="true"/> when the step is one of this fixture's four steps.</returns>
    private static bool IsOrderFulfillmentStep(string stepName) =>
        stepName is nameof(ReserveInventory)
            or nameof(ChargeCustomer)
            or nameof(RefundCharge)
            or nameof(ConfirmShipment);
}
