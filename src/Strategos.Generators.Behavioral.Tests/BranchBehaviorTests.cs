// -----------------------------------------------------------------------
// <copyright file="BranchBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Real-host proofs for C#-authored <c>Branch</c> workflows, which had no behavioral coverage
/// anywhere in the repo.
/// </summary>
/// <remarks>
/// <para>
/// The proofs that exercise a REJOINING case were skipped while #175 was open. The workflow did not
/// terminate: the declared terminal's completed handler cascaded back into the branch-path step,
/// which rejoined at the terminal, so it lapped indefinitely and the Marten saga document was never
/// deleted. Measured, that was not a subtle stall — a single run recorded over 45,000 step
/// invocations in 30 seconds and was still going, and the runaway saga outlived the test that
/// started it, polluting the shared invocation log for the rest of the session.
/// </para>
/// <para>
/// What closed that cycle was the off-main-flow classification, not the branch emitter: once a
/// branch case's steps stop being main-flow successors, the declared terminal has no successor to
/// chain into and marks the saga completed. These three proofs were therefore already green before
/// the branch emitter learned to read the case, so they are coverage for the cascade, not evidence
/// for the case-level decision. That evidence is
/// <see cref="Saga_TerminalBranchCase_CompletesAtCaseEnd"/>.
/// </para>
/// <para>
/// The <c>.Complete()</c> case was never blocked, because a rejected order never entered the cycle.
/// It completes at its own last step, which is the behavior the fix had to preserve — so it is a
/// live regression guard rather than a deferred proof.
/// </para>
/// <para>
/// Every assertion below is stated as an exact per-step count for that reason. Absence of the saga
/// document cannot express the defect — a cycle that is externally killed leaves no document either,
/// so a document-absence boolean reports the broken generator as complete. The count on the terminal
/// step is the discriminating evidence: a workflow that completed ran its terminal exactly once, a
/// workflow that cycled ran it many times, and a workflow that never started ran it zero times.
/// </para>
/// <para>
/// Whole-log totals are deliberately NOT asserted. The log is shared by every workflow on the host,
/// so a total is only meaningful while all of them terminate — and a cycling branch saga inflates it
/// without bound from whichever test set it running.
/// </para>
/// <para>
/// These share the round-trip host's session-scoped <see cref="WorkflowInvocationLog"/>, so the
/// class is non-parallel and every test resets the log before it runs.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<RoundTripHostFixture>(Shared = SharedType.PerClass)]
public sealed class BranchBehaviorTests
{
    private readonly RoundTripHostFixture host;

    /// <summary>Initializes a new instance of the <see cref="BranchBehaviorTests"/> class.</summary>
    /// <param name="host">The shared real-host fixture, injected by TUnit.</param>
    public BranchBehaviorTests(RoundTripHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Both branch workflows are registered on the shared round-trip host: the step types their
    /// generated registrations add are resolvable from the running host's container.
    /// </summary>
    /// <remarks>
    /// It proves the fixtures compile, lower to a saga, and register on the EXISTING host rather
    /// than a second Postgres container, without executing a workflow at all — so a registration
    /// break stays distinguishable from an execution break.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchFixture_Registers_OnSharedRoundTripHost()
    {
        using var scope = this.host.Services.CreateScope();
        var services = scope.ServiceProvider;

        await Assert.That(services.GetService<ValidateOrder>()).IsNotNull()
            .Because("the rejoining branch workflow's pre-branch step must be registered by its generated extension.");
        await Assert.That(services.GetService<ProcessRetailOrder>()).IsNotNull()
            .Because("the explicitly declared branch case's step must be registered.");
        await Assert.That(services.GetService<ProcessWholesaleOrder>()).IsNotNull()
            .Because("the default (otherwise) branch case's step must be registered.");
        await Assert.That(services.GetService<ShipOrder>()).IsNotNull()
            .Because("the rejoining branch workflow's declared terminal must be registered.");

        await Assert.That(services.GetService<ReviewOrder>()).IsNotNull()
            .Because("the mixed branch workflow's pre-branch step must be registered.");
        await Assert.That(services.GetService<ProcessApprovedOrder>()).IsNotNull()
            .Because("the mixed branch workflow's rejoining case step must be registered.");
        await Assert.That(services.GetService<RejectOrder>()).IsNotNull()
            .Because("the mixed branch workflow's workflow-ending case step must be registered.");
        await Assert.That(services.GetService<ShipApprovedOrder>()).IsNotNull()
            .Because("the mixed branch workflow's declared terminal must be registered.");
    }

    /// <summary>
    /// A branch workflow whose cases both rejoin runs its taken path once, its untaken path never,
    /// and its declared terminal EXACTLY once.
    /// </summary>
    /// <remarks>
    /// The terminal's count is the assertion that fails against the defect. When the terminal
    /// cascaded back into a branch path that rejoins at the terminal, it ran on every lap of an
    /// unbounded cycle — many times, not once, which no boolean on the run can express.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_BranchWorkflow_CompletesWithTerminalRunningExactlyOnce()
    {
        this.host.Invocations.Reset();

        var workflowId = Guid.NewGuid();
        var outcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripBranchSaga>(
            workflowId,
            new StartRoundtripBranchCommand(
                workflowId,
                new RoundTripBranchState
                {
                    WorkflowId = workflowId,
                    Channel = RoundTripBranchChannels.Retail,
                }));

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"the branch saga must reach its terminal phase: {outcome.Diagnostic}");
        await Assert.That(outcome.DocumentRemoved).IsTrue()
            .Because("a completed branch workflow must have its Marten saga document deleted.");

        await Assert.That(this.host.Invocations.CountFor(nameof(ValidateOrder))).IsEqualTo(1)
            .Because("the pre-branch step runs once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ProcessRetailOrder))).IsEqualTo(1)
            .Because("the taken branch path runs exactly once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ProcessWholesaleOrder))).IsEqualTo(0)
            .Because("the branch cases are exclusive: the unselected path must never run.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ShipOrder))).IsEqualTo(1)
            .Because("the declared terminal runs EXACTLY once — more than one means the workflow cycled.");
    }

    /// <summary>
    /// The branch case that was not selected does not run at all.
    /// </summary>
    /// <remarks>
    /// A cycling workflow re-enters the branch on every lap, so the untaken path's count is the
    /// evidence that routing is exclusive rather than eventually-everything.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_BranchWorkflow_RunsUntakenPathZeroTimes()
    {
        this.host.Invocations.Reset();

        var workflowId = Guid.NewGuid();
        var outcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripBranchSaga>(
            workflowId,
            new StartRoundtripBranchCommand(
                workflowId,
                new RoundTripBranchState
                {
                    WorkflowId = workflowId,
                    Channel = RoundTripBranchChannels.Wholesale,
                }));

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"the branch saga must reach its terminal phase: {outcome.Diagnostic}");

        await Assert.That(this.host.Invocations.CountFor(nameof(ValidateOrder))).IsEqualTo(1)
            .Because("the pre-branch step runs once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ProcessWholesaleOrder))).IsEqualTo(1)
            .Because("an unmatched channel takes the default (otherwise) path, exactly once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ProcessRetailOrder))).IsEqualTo(0)
            .Because("the branch cases are exclusive: the unselected path must never run.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ShipOrder))).IsEqualTo(1)
            .Because("the declared terminal runs EXACTLY once — more than one means the workflow cycled.");
    }

    /// <summary>
    /// In the mixed shape, a case that declares <c>.Complete()</c> ends the workflow at its own last
    /// step, and the declared terminal never runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the discriminating case. Because the sibling case rejoins, the branch-level rejoin
    /// flag is true for this workflow, so an emitter that decides a path's ending from that flag
    /// alone routes the rejecting case to the declared terminal too — shipping an order that was
    /// rejected. Only a branch mixing both exits can catch that; a branch whose cases all exit the
    /// same way passes either way.
    /// </para>
    /// <para>
    /// It also passed on the UNFIXED generator, for a fragile reason worth stating: the terminal
    /// case's last step was excluded from the branch-path dispatch table, so it fell through to the
    /// ordinary step handler and was treated as terminal only because it happened to sit last in
    /// the emitted step list. Nothing about the case's own declaration was consulted. Admitting
    /// terminal cases to that table without also reading the case turns this red, which is what
    /// makes it a live guard rather than a restatement of the fix.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_TerminalBranchCase_CompletesAtCaseEnd()
    {
        this.host.Invocations.Reset();

        var workflowId = Guid.NewGuid();
        var outcome = await this.host.RunWorkflowWithOutcomeAsync<TerminalBranchSaga>(
            workflowId,
            new StartTerminalBranchCommand(
                workflowId,
                new TerminalBranchState
                {
                    WorkflowId = workflowId,
                    Outcome = OrderReviewOutcome.Rejected,
                }));

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"a workflow-ending branch case must complete the saga: {outcome.Diagnostic}");
        await Assert.That(outcome.DocumentRemoved).IsTrue()
            .Because("the completing case must have the Marten saga document deleted.");

        await Assert.That(this.host.Invocations.CountFor(nameof(ReviewOrder))).IsEqualTo(1)
            .Because("the pre-branch step runs once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(RejectOrder))).IsEqualTo(1)
            .Because("the workflow-ending case runs exactly once and completes the workflow there.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ProcessApprovedOrder))).IsEqualTo(0)
            .Because("the sibling rejoining case must not run when the order was rejected.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ShipApprovedOrder))).IsEqualTo(0)
            .Because("a rejected order must NEVER reach the declared terminal — routing the ending case there is the branch-level-flag defect.");
    }

    /// <summary>
    /// In the mixed shape, the rejoining case still reaches the declared terminal exactly once.
    /// </summary>
    /// <remarks>
    /// The companion to <see cref="Saga_TerminalBranchCase_CompletesAtCaseEnd"/>: a fix that reads
    /// the case instead of the branch-level flag must not break the case that genuinely rejoins.
    /// Without this, routing every case to <c>MarkCompleted()</c> would pass the rejection test.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_TerminalBranchRejoiningCase_ReachesDeclaredTerminal()
    {
        this.host.Invocations.Reset();

        var workflowId = Guid.NewGuid();
        var outcome = await this.host.RunWorkflowWithOutcomeAsync<TerminalBranchSaga>(
            workflowId,
            new StartTerminalBranchCommand(
                workflowId,
                new TerminalBranchState
                {
                    WorkflowId = workflowId,
                    Outcome = OrderReviewOutcome.Approved,
                }));

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"the rejoining case must reach the declared terminal and complete: {outcome.Diagnostic}");

        await Assert.That(this.host.Invocations.CountFor(nameof(ProcessApprovedOrder))).IsEqualTo(1)
            .Because("the approved case runs exactly once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(RejectOrder))).IsEqualTo(0)
            .Because("the rejecting case must not run for an approved order.");
        await Assert.That(this.host.Invocations.CountFor(nameof(ShipApprovedOrder))).IsEqualTo(1)
            .Because("the rejoining case must still reach the declared terminal, exactly once.");
    }

    /// <summary>
    /// A below-threshold score on a REJOINING case's last step routes to the declared
    /// low-confidence handler instead of to the branch's rejoin target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The path-end handler is the only handler that sees this step's completed event, so it is the
    /// only place the gate can be emitted. The escalation step's count is the discriminating
    /// evidence: when the gate is dropped, the handler chain is still lowered into its own phase,
    /// start command and worker handler, so nothing about the generated surface looks wrong — the
    /// step just never runs, and the claim settles on an estimate nobody trusted.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_BranchCaseLowConfidence_RoutesToHandler()
    {
        this.host.Invocations.Reset();

        var outcome = await this.RunBranchCaseConfidenceAsync(ClaimRoute.Repair, assessmentConfidence: 0.10);

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"the low-confidence handler chain terminates, so the saga must still complete: {outcome.Diagnostic}");
        await Assert.That(outcome.DocumentRemoved).IsTrue()
            .Because("a workflow diverted to its low-confidence handler must still have its Marten saga document deleted.");

        await Assert.That(this.host.Invocations.CountFor(nameof(ScreenClaim))).IsEqualTo(1)
            .Because("the pre-branch step runs once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(AssessRepairCost))).IsEqualTo(1)
            .Because("the gated step itself runs exactly once — the gate reads its result, it does not re-run it.");
        await Assert.That(this.host.Invocations.CountFor(nameof(EscalateRepairEstimate))).IsEqualTo(1)
            .Because("a score below the declared threshold must reach the declared handler, exactly once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(SettleClaim))).IsEqualTo(0)
            .Because("a diverted path must NOT rejoin the declared terminal — settling on a distrusted estimate is the whole defect.");
        await Assert.That(this.host.Invocations.CountFor(nameof(AssessTotalLoss))).IsEqualTo(0)
            .Because("the branch cases are exclusive: the unselected case must never run.");
    }

    /// <summary>
    /// A below-threshold score on a WORKFLOW-ENDING case's last step routes to the declared
    /// low-confidence handler instead of completing the saga at that step.
    /// </summary>
    /// <remarks>
    /// This is the position the regression landed on: an ending case's last step used to be
    /// excluded from the path-end dispatch and so fell through to the generic completed handler,
    /// where its gate lowered. Admitting ending cases to that dispatch — which the termination fix
    /// required — moved the step to a handler that had no confidence handling at all.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_TerminalBranchCaseLowConfidence_RoutesToHandler()
    {
        this.host.Invocations.Reset();

        var outcome = await this.RunBranchCaseConfidenceAsync(ClaimRoute.TotalLoss, assessmentConfidence: 0.10);

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"the low-confidence handler chain terminates, so the saga must still complete: {outcome.Diagnostic}");

        await Assert.That(this.host.Invocations.CountFor(nameof(AssessTotalLoss))).IsEqualTo(1)
            .Because("the gated step itself runs exactly once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(EscalateTotalLoss))).IsEqualTo(1)
            .Because("a score below the declared threshold must reach the declared handler, exactly once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(AssessRepairCost))).IsEqualTo(0)
            .Because("the branch cases are exclusive: the unselected case must never run.");
        await Assert.That(this.host.Invocations.CountFor(nameof(SettleClaim))).IsEqualTo(0)
            .Because("the ending case never reaches the declared terminal, gated or not.");
    }

    /// <summary>
    /// An at-or-above-threshold score on a REJOINING case's last step takes the ordinary path: it
    /// rejoins the declared terminal and the handler never runs.
    /// </summary>
    /// <remarks>
    /// The complement that makes the gate discriminating. An emitter that cascaded to the handler
    /// unconditionally would satisfy the below-threshold proofs on its own.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_BranchCaseSufficientConfidence_RejoinsDeclaredTerminal()
    {
        this.host.Invocations.Reset();

        var outcome = await this.RunBranchCaseConfidenceAsync(ClaimRoute.Repair, assessmentConfidence: 0.95);

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"an accepted estimate must settle and complete: {outcome.Diagnostic}");

        await Assert.That(this.host.Invocations.CountFor(nameof(AssessRepairCost))).IsEqualTo(1)
            .Because("the gated step runs once.");
        await Assert.That(this.host.Invocations.CountFor(nameof(EscalateRepairEstimate))).IsEqualTo(0)
            .Because("an accepted score must not divert to the handler.");
        await Assert.That(this.host.Invocations.CountFor(nameof(SettleClaim))).IsEqualTo(1)
            .Because("the rejoining case must still reach the declared terminal, exactly once.");
    }

    /// <summary>
    /// An at-or-above-threshold score on a WORKFLOW-ENDING case's last step still ends the workflow
    /// at that step, and never reaches the declared terminal.
    /// </summary>
    /// <remarks>
    /// The gate must not cost the ending case its ending: emitting the comparison ahead of the
    /// completion is what keeps both true at once.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_TerminalBranchCaseSufficientConfidence_CompletesAtCaseEnd()
    {
        this.host.Invocations.Reset();

        var outcome = await this.RunBranchCaseConfidenceAsync(ClaimRoute.TotalLoss, assessmentConfidence: 0.95);

        await Assert.That(outcome.Completed).IsTrue()
            .Because($"a workflow-ending case must complete the saga at its own last step: {outcome.Diagnostic}");
        await Assert.That(outcome.DocumentRemoved).IsTrue()
            .Because("the completing case must have the Marten saga document deleted.");

        await Assert.That(this.host.Invocations.CountFor(nameof(AssessTotalLoss))).IsEqualTo(1)
            .Because("the gated step runs once and ends the workflow there.");
        await Assert.That(this.host.Invocations.CountFor(nameof(EscalateTotalLoss))).IsEqualTo(0)
            .Because("an accepted score must not divert to the handler.");
        await Assert.That(this.host.Invocations.CountFor(nameof(SettleClaim))).IsEqualTo(0)
            .Because("an ending case must NEVER reach the declared terminal.");
    }

    /// <summary>
    /// Starts the branch-case confidence workflow on the shared host and waits for its outcome.
    /// </summary>
    /// <param name="route">The route that selects the branch case.</param>
    /// <param name="assessmentConfidence">The score the selected case's assessing step reports.</param>
    /// <returns>The run's outcome.</returns>
    private async Task<WorkflowRunOutcome> RunBranchCaseConfidenceAsync(
        ClaimRoute route,
        double assessmentConfidence)
    {
        var workflowId = Guid.NewGuid();
        return await this.host.RunWorkflowWithOutcomeAsync<BranchCaseConfidenceSaga>(
            workflowId,
            new StartBranchCaseConfidenceCommand(
                workflowId,
                new BranchCaseConfidenceState
                {
                    WorkflowId = workflowId,
                    Route = route,
                    AssessmentConfidence = assessmentConfidence,
                }));
    }
}
