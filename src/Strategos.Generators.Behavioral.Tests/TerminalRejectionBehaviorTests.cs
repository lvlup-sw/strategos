// -----------------------------------------------------------------------
// <copyright file="TerminalRejectionBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// A last-on-flow approval, rejected, walks its two-step <c>OnRejection</c>
/// chain and completes. Mid-flow rejection already worked; this is the void
/// last-on-flow arm that used to park the saga (#186).
/// </summary>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ApprovalHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class TerminalRejectionBehaviorTests
{
    private readonly ApprovalHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalRejectionBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">The shared Wolverine + Marten host fixture.</param>
    public TerminalRejectionBehaviorTests(ApprovalHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The last-on-flow rejected route starts the chain and walks every step.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_LastOnFlowRejected_RunsEveryStepOfTheRejectionChain()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var outcome = await this.RunExpenseReportAsync(workflowId);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because(
                "the rejection chain declares Complete(), so walking it to the end must "
                + $"complete the saga — {outcome.Diagnostic}");

        await Assert.That(
                this.host.Invocations.CountFor(FinanceControllerApprovalDecisionHandler.ApprovalRequested))
            .IsEqualTo(1)
            .Because("the checkpoint is last on the main flow and must still be reached");

        await Assert.That(this.host.Invocations.CountFor(nameof(RecordExpenseRefusal)))
            .IsEqualTo(1)
            .Because(
                "the last-on-flow rejected arm must publish Start{FirstRejection}; a void "
                + "handler that only sets Phase parks here forever (#186)");

        await Assert.That(this.host.Invocations.CountFor(nameof(NotifyExpenseSubmitter)))
            .IsEqualTo(1)
            .Because("a two-step rejection chain must run BOTH steps");

        await Assert.That(this.host.Invocations.CountFor(nameof(ArchiveExpenseReport)))
            .IsEqualTo(0)
            .Because("the declared Finally belongs to the approved route");
    }

    /// <summary>
    /// The rejected route runs in declaration order and never invents a
    /// main-flow successor after the checkpoint.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_LastOnFlowRejected_EndsAtTheChain()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var outcome = await this.RunExpenseReportAsync(workflowId);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because($"the route can only be read off a completed run — {outcome.Diagnostic}");

        var recorded = this.host.Invocations.Invocations
            .Where(IsExpenseReportStep)
            .ToArray();

        var observed = string.Join(" -> ", recorded);
        var expected = string.Join(
            " -> ",
            nameof(SubmitExpenseReport),
            nameof(AttachReceipts),
            FinanceControllerApprovalDecisionHandler.ApprovalRequested,
            nameof(RecordExpenseRefusal),
            nameof(NotifyExpenseSubmitter));

        await Assert.That(observed)
            .IsEqualTo(expected)
            .Because(
                "the last-on-flow rejected route submits, attaches receipts, pauses once, "
                + "then walks the rejection chain in declaration order and ends");
    }

    private static bool IsExpenseReportStep(string recordedName) =>
        recordedName is nameof(SubmitExpenseReport)
            or nameof(AttachReceipts)
            or nameof(RecordExpenseRefusal)
            or nameof(NotifyExpenseSubmitter)
            or nameof(ArchiveExpenseReport)
            or FinanceControllerApprovalDecisionHandler.ApprovalRequested;

    private Task<WorkflowRunOutcome> RunExpenseReportAsync(Guid workflowId) =>
        this.host.RunWorkflowWithOutcomeAsync<ExpenseReportReviewSaga>(
            workflowId,
            new StartExpenseReportReviewCommand(
                workflowId,
                new ExpenseReportState { WorkflowId = workflowId }));
}
