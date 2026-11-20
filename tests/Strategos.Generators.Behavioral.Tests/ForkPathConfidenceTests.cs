// -----------------------------------------------------------------------
// <copyright file="ForkPathConfidenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (DR-4 / #145 gap A) that confidence gating declared on a
/// FORK PATH step is actually lowered and honored at runtime — previously it reached the
/// IR but no emitter consumed it (the AGWF022 <c>Deferred</c> debt). The gated step is a
/// fork path's last step; the generated fork path-completed handler now compares the
/// step result's confidence to the threshold and, when below, routes to the
/// <c>OnLowConfidence</c> handler chain (a Wolverine cascade) and appends the
/// <c>LowConfidenceRouted</c> audit stream event after the fork paths settle, before
/// the join can fire.
/// </summary>
/// <remarks>
/// <para>
/// The workflow is EventSourced so the proof can inspect BOTH the routing (the review
/// handler ran, the join + settle steps did not) and the appended
/// <c>LowConfidenceRouted</c> audit stream event on the Marten stream.
/// </para>
/// <para>
/// Runs are driven with <see cref="EventSourcedHostFixture.RunToTerminalAsync{TSaga}"/>
/// (not the exception-asserting <c>RunWorkflowAsync</c>) because the two parallel fork
/// paths append to the same Marten event stream, so Marten's optimistic concurrency can
/// retry one path's append — an expected, self-healing condition that
/// <c>TrackActivity()</c>'s default exception assertion would otherwise surface. The
/// authoritative terminal signal is the saga document's removal by <c>MarkCompleted()</c>.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single process-wide
/// container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<EventSourcedHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class ForkPathConfidenceTests
{
    private readonly EventSourcedHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForkPathConfidenceTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared EventSourced Wolverine+Marten host fixture, injected by TUnit and
    /// shared across the entire test session.
    /// </param>
    public ForkPathConfidenceTests(EventSourcedHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Runs the fork-path confidence saga whose gated fork-path step returns confidence
    /// 0.5 (below the 0.85 threshold). Asserts the fork path-completed handler routed to
    /// the lowered <c>OnLowConfidence</c> handler (the review step ran) and diverted
    /// before the join — the aggregate/settle steps did NOT run.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_ForkPathLowConfidence_RoutesToOnLowConfidenceHandler()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ForkPathConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartForkPathConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunToTerminalAsync<ForkPathConfidenceSaga>(workflowId, startCommand);

        // The saga reached its terminal phase: the single-step OnLowConfidence handler
        // calls MarkCompleted(), removing the persisted saga document.
        await Assert.That(completed).IsTrue();

        // The intake step and the gated fork-path step each ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(ForkConfIntakeStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ForkConfAssessStep))).IsEqualTo(1);

        // Routing proof: confidence 0.5 < 0.85 → the OnLowConfidence handler ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(ForkConfReviewStep))).IsEqualTo(1);

        // ...and the gate diverted before the path was marked joined, so neither the join
        // step nor the terminal settle step ran.
        await Assert.That(this.host.Invocations.CountFor(nameof(ForkConfAggregateStep))).IsEqualTo(0);
        await Assert.That(this.host.Invocations.CountFor(nameof(ForkConfSettleStep))).IsEqualTo(0);
    }

    /// <summary>
    /// Runs the same fork-path confidence saga and asserts a <c>LowConfidenceRouted</c>
    /// audit event — carrying the gated fork-path step name, the observed score, and the
    /// threshold — lands in the workflow's Marten stream (EventSourced-mode proof).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_ForkPathLowConfidence_AppendsLowConfidenceRoutedEvent()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ForkPathConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartForkPathConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunToTerminalAsync<ForkPathConfidenceSaga>(workflowId, startCommand);

        await Assert.That(completed).IsTrue();

        // Routing proof: confidence 0.5 < 0.85 → the review handler ran.
        await Assert.That(this.host.Invocations.CountFor(nameof(ForkConfReviewStep))).IsEqualTo(1);

        // Audit-event proof: a LowConfidenceRouted event landed in the Marten stream.
        var routed = await this.host.WaitForStreamEventAsync<ForkPathConfidenceLowConfidenceRouted>(workflowId);
        await Assert.That(routed).IsNotNull();

        // It carries the gated fork-path step name, the observed score, and the threshold.
        await Assert.That(routed!.StepName).IsEqualTo(nameof(ForkConfAssessStep));
        await Assert.That(routed.Confidence).IsEqualTo(0.5);
        await Assert.That(routed.Threshold).IsEqualTo(0.85);
    }

    /// <summary>
    /// Exercises both completion orders without relying on worker timing.
    /// </summary>
    /// <param name="gatedPathFirst">Whether the below-threshold result is handled first.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Saga_LowConfidenceRouteWaitsForSiblingCompletion(bool gatedPathFirst)
    {
        var workflowId = Guid.NewGuid();
        var state = new ForkPathConfidenceState { WorkflowId = workflowId };
        var saga = new ForkPathConfidenceSaga
        {
            WorkflowId = workflowId,
            State = state,
            Fork_ForkPathConfidence_Fork0_Path0Status = ForkPathStatus.InProgress,
            Fork_ForkPathConfidence_Fork0_Path1Status = ForkPathStatus.InProgress,
        };
        var session = Substitute.For<IDocumentSession>();
        var events = session.Events;
        var logger = NullLogger<ForkPathConfidenceSaga>.Instance;
        var gatedResult = new ForkConfAssessStepCompleted(workflowId, Guid.NewGuid(), state, 0.5, DateTimeOffset.UtcNow);
        var siblingResult = new ForkConfSecondPathStepCompleted(workflowId, Guid.NewGuid(), state, null, DateTimeOffset.UtcNow);

        var first = gatedPathFirst ? saga.Handle(gatedResult, session, logger) : saga.Handle(siblingResult, session, logger);
        await Assert.That(first.ToArray().Length).IsEqualTo(0);
        events.DidNotReceive().Append(workflowId, Arg.Any<ForkPathConfidenceLowConfidenceRouted>());

        var second = gatedPathFirst ? saga.Handle(siblingResult, session, logger) : saga.Handle(gatedResult, session, logger);
        var route = second.Single();
        await Assert.That(route).IsTypeOf<StartForkConfReviewStepCommand>();
        await Assert.That(saga.Phase).IsEqualTo(ForkPathConfidencePhase.ForkConfReviewStep);
        await Assert.That(saga.Fork_ForkPathConfidence_Fork0_Path0Status).IsEqualTo(ForkPathStatus.Success);
        await Assert.That(saga.Fork_ForkPathConfidence_Fork0_Path1Status).IsEqualTo(ForkPathStatus.Success);
        events.Received(1).Append(
            workflowId,
            Arg.Is<ForkPathConfidenceLowConfidenceRouted>(evt =>
                evt.StepName == nameof(ForkConfAssessStep)
                && evt.Confidence == 0.5
                && evt.Threshold == 0.85));
    }
}
