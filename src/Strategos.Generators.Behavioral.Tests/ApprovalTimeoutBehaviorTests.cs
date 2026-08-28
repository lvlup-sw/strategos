// -----------------------------------------------------------------------
// <copyright file="ApprovalTimeoutBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Real-host <c>OnTimeout</c>: the broker parks the checkpoint and the test
/// injects the timeout command. No wall-clock sleep.
/// </summary>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ApprovalHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class ApprovalTimeoutBehaviorTests
{
    private readonly ApprovalHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalTimeoutBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">The shared Wolverine + Marten host fixture.</param>
    public ApprovalTimeoutBehaviorTests(ApprovalHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Injecting the timeout command runs the first escalation step and
    /// completes the saga without touching the approved main flow.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_InjectedTimeout_RunsFirstEscalationStep()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        var invocationsBefore = this.host.Invocations.TotalCount;

        await this.host.PublishAndWaitAsync(
            new StartWireTransferReviewCommand(
                workflowId,
                new WireTransferState { WorkflowId = workflowId }));

        var requestId = await this.host.WaitForPendingApprovalRequestIdAsync<WireTransferReviewSaga>(workflowId);

        await Assert.That(
                this.host.Invocations.CountFor(ComplianceOfficerApprovalDecisionHandler.ApprovalRequested))
            .IsEqualTo(1)
            .Because("the checkpoint must be parked before a timeout can be injected");

        await Assert.That(this.host.Invocations.CountFor(nameof(EscalateToComplianceLead)))
            .IsEqualTo(0)
            .Because("nothing has delivered the timeout command yet");

        await this.host.PublishAndWaitAsync(
            new ComplianceOfficerApprovalTimeoutCommand(workflowId, requestId));

        var outcome = await this.host.WaitForCompletionAsync<WireTransferReviewSaga>(
            workflowId,
            invocationsBefore);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because(
                "the OnTimeout chain declares Complete(), so the injected timeout must "
                + $"finish the saga — {outcome.Diagnostic}");

        await Assert.That(this.host.Invocations.CountFor(nameof(EscalateToComplianceLead)))
            .IsEqualTo(1)
            .Because("the timeout handler must dispatch the first escalation step");

        await Assert.That(this.host.Invocations.CountFor(nameof(ReleaseWireTransfer)))
            .IsEqualTo(0)
            .Because("the approved main-flow successor must not run on the timeout route");

        await Assert.That(this.host.Invocations.CountFor(nameof(RecordWireTransfer)))
            .IsEqualTo(0)
            .Because("the declared terminal belongs to the approved route");
    }
}
