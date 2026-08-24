// -----------------------------------------------------------------------
// <copyright file="ApprovalBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// The first real-host proof of a C#-authored <c>AwaitApproval</c> checkpoint: the
/// approved path reaches the declared terminal and completes the saga.
/// </summary>
/// <remarks>
/// <para>
/// The construct previously had no behavioral coverage at all. The JSON-import approval
/// family proves only that an imported approval emits a saga that compiles and resolves
/// from the container; it never starts one, so no test anywhere had observed an approval
/// checkpoint pause, resume, and finish.
/// </para>
/// <para>
/// Two defects of the same appending mechanism are visible only on this path. An
/// approval's rejection and escalation steps are lowered as entries appended to the
/// step-name list AFTER the declared terminal, so a positional successor scan cascades
/// the terminal into the rejection step; and the approval RESUME scan is a third,
/// separate scan which used to index that list raw, with no filter at all, resuming onto
/// whatever entry happened to sit at the next index.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares a process-wide container
/// and host and resets the shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ApprovalHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class ApprovalBehaviorTests
{
    private readonly ApprovalHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine + Marten host fixture, injected by TUnit and shared across
    /// the whole test session.
    /// </param>
    public ApprovalBehaviorTests(ApprovalHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The approved path completes: the checkpoint is reached and approved, the terminal
    /// runs exactly once and is the last step to run, and neither the rejection step nor
    /// the escalation step executes.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_ApprovedPath_CompletesWithoutRunningRejection()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var outcome = await this.RunApprovalWorkflowAsync(workflowId);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because(
                "an approved checkpoint must resume the main flow and reach the declared "
                + $"terminal, which completes the saga — {outcome.Diagnostic}");

        // The checkpoint really was reached: without this a run that never paused for
        // approval would satisfy every other assertion in this test.
        await Assert.That(
                this.host.Invocations.CountFor(CreditOfficerApprovalDecisionHandler.ApprovalRequested))
            .IsEqualTo(1)
            .Because("the saga must ask for a decision exactly once before resuming");

        // Exact per-step counts, never a whole-log total: a workflow that fails to
        // terminate outlives the test that started it and keeps incrementing the shared
        // log, so a total is not a stable oracle.
        await Assert.That(this.host.Invocations.CountFor(nameof(NotifyApplicantDeclined)))
            .IsEqualTo(0)
            .Because(
                "the decision was Approved, so the OnRejection step must never run; it running "
                + "is an appended off-main-flow entry being treated as a successor");

        await Assert.That(this.host.Invocations.CountFor(nameof(EscalateToCreditCommittee)))
            .IsEqualTo(0)
            .Because(
                "nothing scheduled the approval timeout, so the OnTimeout escalation step must "
                + "never run");

        await Assert.That(this.host.Invocations.CountFor(nameof(RecordCreditDecision)))
            .IsEqualTo(1)
            .Because("the declared terminal runs exactly once");

        // The discriminating assertion. The rejection chain ends in Complete(), so a
        // terminal that wrongly cascades into it still removes the saga document and still
        // leaves each main-flow step at one invocation. What separates the two routes is
        // that nothing runs after the terminal.
        var lastStep = this.host.Invocations.Invocations.LastOrDefault(IsCreditLimitReviewStep);

        await Assert.That(lastStep)
            .IsEqualTo(nameof(RecordCreditDecision))
            .Because(
                "the declared terminal is the last step of the workflow to run; anything after "
                + "it means the terminal took an appended rejection or escalation step as its "
                + "successor instead of completing the saga");
    }

    /// <summary>
    /// The step the approval resumes onto is a main-flow step, never one of the appended
    /// rejection or escalation path steps.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// The resume target is read off the recorded sequence: whatever step ran immediately
    /// after the checkpoint was answered IS what the resume handler dispatched. Asserting
    /// the full ordered sequence at the same time keeps a step that merely ran the right
    /// number of times from passing for the wrong route.
    /// </remarks>
    [Test]
    public async Task Saga_ApprovalResume_TargetsMainFlowStep()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var outcome = await this.RunApprovalWorkflowAsync(workflowId);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because($"the resume target can only be read off a run that got past the checkpoint — {outcome.Diagnostic}");

        var recorded = this.host.Invocations.Invocations
            .Where(IsCreditLimitReviewStep)
            .ToArray();

        var checkpoint = Array.IndexOf(
            recorded,
            CreditOfficerApprovalDecisionHandler.ApprovalRequested);

        await Assert.That(checkpoint)
            .IsGreaterThanOrEqualTo(0)
            .Because("the approval checkpoint must appear in the recorded sequence");

        await Assert.That(checkpoint + 1)
            .IsLessThan(recorded.Length)
            .Because("an approved checkpoint resumes onto a step, so something must follow it");

        await Assert.That(recorded[checkpoint + 1])
            .IsEqualTo(nameof(IssueCreditLine))
            .Because(
                "the approval resumes onto the next MAIN-FLOW step. Resolving the successor by "
                + "raw list position instead resumes onto whatever entry sits at the next index "
                + "— an appended rejection, escalation or path step — which bypasses that "
                + "construct's own dispatch handler and strands the workflow");

        var observed = string.Join(" -> ", recorded);
        var expected = string.Join(
            " -> ",
            nameof(AssessCreditRisk),
            CreditOfficerApprovalDecisionHandler.ApprovalRequested,
            nameof(IssueCreditLine),
            nameof(RecordCreditDecision));

        await Assert.That(observed)
            .IsEqualTo(expected)
            .Because(
                "the approved path runs the declared main flow in order, pausing once at the "
                + "checkpoint, and stops at the terminal");
    }

    /// <summary>
    /// Decides whether a recorded name belongs to the credit-limit review fixture — its
    /// four steps plus the approval checkpoint marker.
    /// </summary>
    /// <param name="recordedName">The recorded name.</param>
    /// <returns><see langword="true"/> when the name belongs to this fixture.</returns>
    private static bool IsCreditLimitReviewStep(string recordedName) =>
        recordedName is nameof(AssessCreditRisk)
            or nameof(IssueCreditLine)
            or nameof(RecordCreditDecision)
            or nameof(NotifyApplicantDeclined)
            or nameof(EscalateToCreditCommittee)
            or CreditOfficerApprovalDecisionHandler.ApprovalRequested;

    /// <summary>
    /// Starts the approval-gated workflow and waits for the observed outcome.
    /// </summary>
    /// <param name="workflowId">The workflow identity to run under.</param>
    /// <returns>The observed outcome of the run.</returns>
    private Task<WorkflowRunOutcome> RunApprovalWorkflowAsync(Guid workflowId) =>
        this.host.RunWorkflowWithOutcomeAsync<CreditLimitReviewSaga>(
            workflowId,
            new StartCreditLimitReviewCommand(
                workflowId,
                new CreditLimitReviewState { WorkflowId = workflowId }));
}
