// -----------------------------------------------------------------------
// <copyright file="NestedRepeatUntilConfidenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (DR-5 / #145 gap B) that confidence gating declared on a
/// nested-<c>RepeatUntil</c> loop-body step is actually lowered and honored at runtime —
/// previously ALL loop-body confidence was dropped from the IR by step extraction (structurally
/// undiagnosable) and inert. Task 009 promoted the loop body to configured <c>StepModel</c>
/// records on <c>LoopModel.BodySteps</c>; this proves the generated loop completed handler now
/// compares the loop body's LAST step result confidence to the threshold and, when below, routes
/// to the <c>OnLowConfidence</c> handler chain (a Wolverine cascade) BEFORE evaluating the loop
/// condition — instead of continuing or exiting the loop.
/// </summary>
/// <remarks>
/// <para>
/// Two fixtures with distinct step CLR types (avoiding the generator's CS0101 same-name
/// collision) exercise both sides of the gate: the low-confidence loop's assess step returns
/// confidence 0.5 (below 0.85), so the review handler MUST run and the finish step MUST NOT; the
/// high-confidence loop's assess step returns 0.9 (at/above), so the loop condition is evaluated
/// (it exits) and the finish step MUST run while the review handler MUST NOT.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single process-wide
/// container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<WolverineHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class NestedRepeatUntilConfidenceTests
{
    private readonly WolverineHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="NestedRepeatUntilConfidenceTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared across the entire
    /// test session.
    /// </param>
    public NestedRepeatUntilConfidenceTests(WolverineHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated low-confidence loop saga whose single loop-body step returns
    /// confidence 0.5 (below the 0.85 threshold). Asserts the loop completed handler routed to
    /// the lowered <c>OnLowConfidence</c> handler (the review step ran) and diverted before the
    /// loop condition — the primary finish step did NOT run.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_LoopBodyLowConfidence_RoutesToOnLowConfidenceHandler()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new LoopConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartLowLoopConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<LowLoopConfidenceSaga>(workflowId, startCommand);

        // The saga reached its terminal phase: the single-step OnLowConfidence handler calls
        // MarkCompleted(), removing the persisted saga document.
        await Assert.That(completed).IsTrue();

        // The prepare and loop-body assess steps each ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfPrepareLow))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfAssessLow))).IsEqualTo(1);

        // Routing proof: confidence 0.5 < 0.85 → the review handler ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfReviewLow))).IsEqualTo(1);

        // ...and the gate diverted before the loop condition, so the finish step was skipped.
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfFinishLow))).IsEqualTo(0);
    }

    /// <summary>
    /// Starts the generated high-confidence loop saga whose loop-body step returns confidence 0.9
    /// (at/above the 0.85 threshold). Asserts the loop completed handler did NOT fire the gate:
    /// the loop condition was evaluated (it exits) and the finish step ran, while the review
    /// handler did NOT.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_LoopBodyHighConfidence_ContinuesLoopEvaluation()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new LoopConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartHighLoopConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<HighLoopConfidenceSaga>(workflowId, startCommand);

        // The saga reached its terminal phase via the finish step's MarkCompleted().
        await Assert.That(completed).IsTrue();

        // The prepare and loop-body assess steps each ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfPrepareHigh))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfAssessHigh))).IsEqualTo(1);

        // Routing proof: confidence 0.9 >= 0.85 → the gate did not fire; the loop condition
        // evaluated (exit) and the finish step ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfFinishHigh))).IsEqualTo(1);

        // ...and the OnLowConfidence review handler did NOT run.
        await Assert.That(this.host.Invocations.CountFor(nameof(LoopConfReviewHigh))).IsEqualTo(0);
    }
}
