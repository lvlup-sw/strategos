// -----------------------------------------------------------------------
// <copyright file="ApprovalBeforeForkBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// An approval immediately before a fork resumes onto the fork dispatch, not
/// the join. Resuming onto <c>Start{Join}</c> leaves every path Pending and
/// hangs the saga (#182).
/// </summary>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ApprovalHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class ApprovalBeforeForkBehaviorTests
{
    private readonly ApprovalHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalBeforeForkBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">The shared Wolverine + Marten host fixture.</param>
    public ApprovalBeforeForkBehaviorTests(ApprovalHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The approved checkpoint reaches both fork paths, the join, and the
    /// declared terminal, and the saga document is deleted.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_ApprovalBeforeFork_DispatchesBothPathsAndCompletes()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var outcome = await this.RunLoanOriginationAsync(workflowId);

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because(
                "an approval immediately before a fork must still reach both paths, the join, "
                + $"and the terminal — {outcome.Diagnostic}");

        // ForkExtractor walks through AwaitApproval, so ForkModel.PreviousStepName is
        // the gated step. That step's completed handler is the fork dispatch (ForkAtStep
        // wins over ApprovalAtStep in SagaStepHandlersEmitter, which this stream does
        // not own). The resume-handler rewrite is still the #182 emission contract;
        // this host proof is the acceptance: the shape completes instead of hanging.

        await Assert.That(this.host.Invocations.CountFor(nameof(ScoreCredit)))
            .IsEqualTo(1)
            .Because("path 0 runs only when the resume dispatches the fork");

        await Assert.That(this.host.Invocations.CountFor(nameof(VerifyIncome)))
            .IsEqualTo(1)
            .Because("path 1 must start with path 0; one path alone hangs the join");

        await Assert.That(this.host.Invocations.CountFor(nameof(MergeAssessment)))
            .IsEqualTo(1)
            .Because("the join runs after both paths, not instead of the fork dispatch");

        await Assert.That(this.host.Invocations.CountFor(nameof(IssueLoan)))
            .IsEqualTo(1)
            .Because("the declared terminal runs exactly once after the join");
    }

    private Task<WorkflowRunOutcome> RunLoanOriginationAsync(Guid workflowId) =>
        this.host.RunWorkflowWithOutcomeAsync<LoanOriginationSaga>(
            workflowId,
            new StartLoanOriginationCommand(
                workflowId,
                new LoanOriginationState { WorkflowId = workflowId }));
}
