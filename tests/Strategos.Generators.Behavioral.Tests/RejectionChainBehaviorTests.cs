// -----------------------------------------------------------------------
// <copyright file="RejectionChainBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// A rejected approval whose <c>OnRejection</c> chain has more than one step runs every
/// step of that chain, in order, and then completes.
/// </summary>
/// <remarks>
/// <para>
/// Every rejection and escalation fixture in the repository declared a chain of exactly one
/// step, including the real-host approval proof — which additionally only ever walks the
/// APPROVED route. A one-step chain produces the same observation whether the chain
/// advances or truncates, so the entire chain-truncation class was unobservable.
/// </para>
/// <para>
/// The rejection chain is dispatched by the approval component at its first step only, and
/// through the generic start command, so the generic completed handler is the chain's only
/// routing site. Give that handler no in-path successor and the chain's first step marks
/// the saga completed and deletes its own document, stranding the rest of the chain — with
/// no diagnostic, because nothing in the emitted source is malformed.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares a process-wide container
/// and host and resets the shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ApprovalHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class RejectionChainBehaviorTests
{
    private readonly ApprovalHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="RejectionChainBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine + Marten host fixture, injected by TUnit and shared across
    /// the whole test session.
    /// </param>
    public RejectionChainBehaviorTests(ApprovalHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The rejected route walks the whole chain: the checkpoint is refused, both rejection
    /// steps run once each in declaration order, and the saga completes.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_RejectedPath_RunsEveryStepOfTheRejectionChain()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var outcome = await this.RunRequisitionWorkflowAsync(workflowId);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because(
                "the rejection chain declares Complete(), so walking it to the end must complete "
                + $"the saga — {outcome.Diagnostic}");

        // The checkpoint really was reached and refused: without this a run that never
        // paused for a decision would satisfy every other assertion here.
        await Assert.That(
                this.host.Invocations.CountFor(PurchasingManagerApprovalDecisionHandler.ApprovalRequested))
            .IsEqualTo(1)
            .Because("the saga must ask for a decision exactly once before taking the rejection route");

        // Exact per-step counts, never a whole-log total: a workflow that fails to
        // terminate outlives the test that started it and keeps incrementing the shared
        // log, so a total is not a stable oracle.
        await Assert.That(this.host.Invocations.CountFor(nameof(RecordRequisitionRejection)))
            .IsEqualTo(1)
            .Because("the approval component dispatches the chain's first step on a rejected decision");

        // The discriminating assertion. Nothing dispatches the chain's SECOND step but the
        // completed handler of the step before it, so this count is zero exactly when that
        // handler completed the saga instead of chaining.
        await Assert.That(this.host.Invocations.CountFor(nameof(NotifyRequisitionOriginator)))
            .IsEqualTo(1)
            .Because(
                "a rejection chain of two steps must run BOTH. Zero here means the chain's first "
                + "step was emitted with no in-path successor, so it marked the saga completed and "
                + "deleted the document the rest of the chain needed");
    }

    /// <summary>
    /// The rejected route runs the chain in declaration order and stops there: it declared
    /// its own completion, so no main-flow step after the checkpoint runs.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// Asserting the full ordered sequence keeps a step that merely ran the right number of
    /// times from passing for the wrong route — a chain that ran its steps out of order, or
    /// one that rejoined the main flow after completing, would both satisfy the counts.
    /// </remarks>
    [Test]
    public async Task Saga_RejectedPath_EndsAtTheChainWithoutRejoiningTheMainFlow()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var outcome = await this.RunRequisitionWorkflowAsync(workflowId);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because($"the route can only be read off a run that got past the checkpoint — {outcome.Diagnostic}");

        await Assert.That(this.host.Invocations.CountFor(nameof(PlaceSupplierOrder)))
            .IsEqualTo(0)
            .Because(
                "the decision was Rejected and the chain declared Complete(), so the main-flow step "
                + "after the checkpoint must never run");

        await Assert.That(this.host.Invocations.CountFor(nameof(ClosePurchaseRequisition)))
            .IsEqualTo(0)
            .Because("the declared terminal belongs to the approved route, which this run never took");

        var recorded = this.host.Invocations.Invocations
            .Where(IsPurchaseRequisitionStep)
            .ToArray();

        var observed = string.Join(" -> ", recorded);
        var expected = string.Join(
            " -> ",
            nameof(PrepareRequisition),
            PurchasingManagerApprovalDecisionHandler.ApprovalRequested,
            nameof(RecordRequisitionRejection),
            nameof(NotifyRequisitionOriginator));

        await Assert.That(observed)
            .IsEqualTo(expected)
            .Because(
                "the rejected route runs the entry step, pauses once at the checkpoint, then walks "
                + "the rejection chain in declaration order and ends");
    }

    /// <summary>
    /// Decides whether a recorded name belongs to the purchase-requisition fixture — its
    /// five steps plus the approval checkpoint marker.
    /// </summary>
    /// <param name="recordedName">The recorded name.</param>
    /// <returns><see langword="true"/> when the name belongs to this fixture.</returns>
    private static bool IsPurchaseRequisitionStep(string recordedName) =>
        recordedName is nameof(PrepareRequisition)
            or nameof(PlaceSupplierOrder)
            or nameof(ClosePurchaseRequisition)
            or nameof(RecordRequisitionRejection)
            or nameof(NotifyRequisitionOriginator)
            or PurchasingManagerApprovalDecisionHandler.ApprovalRequested;

    /// <summary>
    /// Starts the requisition workflow and waits for the observed outcome.
    /// </summary>
    /// <param name="workflowId">The workflow identity to run under.</param>
    /// <returns>The observed outcome of the run.</returns>
    private Task<WorkflowRunOutcome> RunRequisitionWorkflowAsync(Guid workflowId) =>
        this.host.RunWorkflowWithOutcomeAsync<PurchaseRequisitionReviewSaga>(
            workflowId,
            new StartPurchaseRequisitionReviewCommand(
                workflowId,
                new PurchaseRequisitionState { WorkflowId = workflowId }));
}
