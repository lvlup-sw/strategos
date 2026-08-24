// -----------------------------------------------------------------------
// <copyright file="IntermediateConfidenceBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end proof (#145) that confidence gating declared on an INTERMEDIATE — non-last —
/// step of a fork path or a loop body is lowered and honored at runtime.
/// </summary>
/// <remarks>
/// <para>
/// The last step of a fork path or a loop body is intercepted by a dedicated path-end
/// handler, and each of those handlers carries its own confidence gate. An intermediate step
/// is not intercepted: it falls through to the generic completed handler, and it is that
/// handler's gate — which applies no position test — that must route a below-threshold
/// result to the declared <c>OnLowConfidence</c> handler. These two runs are what establish
/// the routing actually happens on a real host rather than only appearing in emitted text.
/// </para>
/// <para>
/// Each run asserts three things together, because none alone is proof: the declared handler
/// ran, the step the gated one would otherwise have chained to did NOT, and the saga reached
/// a terminal phase. Completion is never taken from the boolean alone — an absent saga
/// document is also what a saga that was never created looks like — so every assertion is
/// paired with non-zero invocation counts for the steps that must have run.
/// </para>
/// <para>
/// Counts are read per step name, never as a whole-log total: the log is process-shared, and
/// a total is not attributable to one test.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single process-wide
/// container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<WolverineHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class IntermediateConfidenceBehaviorTests
{
    private readonly WolverineHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntermediateConfidenceBehaviorTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared across the
    /// entire test session.
    /// </param>
    public IntermediateConfidenceBehaviorTests(WolverineHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The gated INTERMEDIATE fork-path step returns confidence 0.5, below its 0.85
    /// threshold. The generic completed handler's gate must route to the declared handler
    /// instead of chaining to the path's next step, and the workflow must still reach a
    /// terminal phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_IntermediateForkPathLowConfidence_RoutesToHandler()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new IntermediateConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartIntermediateForkConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunToTerminalAsync<IntermediateForkConfidenceSaga>(
            workflowId,
            startCommand);

        // The saga started and reached the gated step — without these the run proves nothing,
        // because an absent saga document is also what "never created" looks like.
        await Assert.That(this.host.Invocations.CountFor(nameof(UnderwritingIntakeStep)))
            .IsEqualTo(1)
            .Because("the entry step must have run for this to be a real run of the workflow");
        await Assert.That(this.host.Invocations.CountFor(nameof(UnderwritingRiskScoreStep)))
            .IsEqualTo(1)
            .Because("the gated intermediate fork-path step must have run and produced its score");

        // Routing proof: the declared OnLowConfidence handler ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(UnderwritingManualReviewStep)))
            .IsEqualTo(1)
            .Because("confidence 0.5 is below the 0.85 threshold, so the declared handler must run");

        // ...instead of the step the gated one would otherwise have chained to.
        await Assert.That(this.host.Invocations.CountFor(nameof(UnderwritingPricingStep)))
            .IsEqualTo(0)
            .Because(
                "the gate diverts the path, so the fork path's LAST step must not run — "
                + "running it would mean the intermediate gate was skipped and the path chained on");

        // The diverted path is never marked succeeded, so the join never fires.
        await Assert.That(this.host.Invocations.CountFor(nameof(UnderwritingAggregateStep)))
            .IsEqualTo(0)
            .Because("the join cannot fire while the gated path is diverted");
        await Assert.That(this.host.Invocations.CountFor(nameof(UnderwritingIssuePolicyStep)))
            .IsEqualTo(0)
            .Because("the declared terminal step sits after the join, which never fires");

        await Assert.That(completed)
            .IsTrue()
            .Because("the single-step handler chain terminates the workflow, removing the saga document");
    }

    /// <summary>
    /// The gated INTERMEDIATE loop-body step returns confidence 0.5, below its 0.85
    /// threshold. The generic completed handler's gate must route to the declared handler
    /// instead of chaining to the loop body's last step, and the workflow must still reach a
    /// terminal phase.
    /// </summary>
    /// <remarks>
    /// The loop body carries no nested fork, so its last body step is unambiguously the
    /// revise step no matter how path steps are spliced into the workflow's step list — the
    /// boundary this assertion depends on is not one the splice order can move.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_IntermediateLoopBodyLowConfidence_RoutesToHandler()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new IntermediateConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartIntermediateLoopConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunToTerminalAsync<IntermediateLoopConfidenceSaga>(
            workflowId,
            startCommand);

        await Assert.That(this.host.Invocations.CountFor(nameof(ManuscriptIntakeStep)))
            .IsEqualTo(1)
            .Because("the entry step must have run for this to be a real run of the workflow");
        await Assert.That(this.host.Invocations.CountFor(nameof(ManuscriptCritiqueStep)))
            .IsEqualTo(1)
            .Because("the gated intermediate loop-body step must have run and produced its score");

        // Routing proof: the declared OnLowConfidence handler ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(ManuscriptEditorReviewStep)))
            .IsEqualTo(1)
            .Because("confidence 0.5 is below the 0.85 threshold, so the declared handler must run");

        // ...instead of the loop body's LAST step.
        await Assert.That(this.host.Invocations.CountFor(nameof(ManuscriptReviseStep)))
            .IsEqualTo(0)
            .Because(
                "the gate diverts before the body's last step, which is the step the loop "
                + "completed handler would have intercepted");

        await Assert.That(this.host.Invocations.CountFor(nameof(ManuscriptPublishStep)))
            .IsEqualTo(0)
            .Because("the loop never completes an iteration, so the declared terminal never runs");

        await Assert.That(completed)
            .IsTrue()
            .Because("the single-step handler chain terminates the workflow, removing the saga document");
    }
}
