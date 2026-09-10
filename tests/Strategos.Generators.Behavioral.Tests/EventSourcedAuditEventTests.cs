// -----------------------------------------------------------------------
// <copyright file="EventSourcedAuditEventTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (#138 G-5) that an EventSourced-mode workflow emits
/// the named audit <b>stream events</b> — <c>StepFailed</c> on terminal failure and
/// <c>LowConfidenceRouted</c> on confidence-gated routing — to the Marten event
/// stream. This resolves the step-resilience design's Open Question #1
/// (audit-event taxonomy): until now terminal-failure / low-confidence audit was
/// captured only as queryable saga document properties + structured logs, not as
/// named stream events.
/// </summary>
/// <remarks>
/// <para>
/// The events are inspected by reading the workflow's raw Marten stream
/// (<c>FetchStreamAsync</c>), the same surface the <c>EventSourcedHostFixture</c>
/// smoke test proves round-trips. The audit events carry no Marten <c>Apply</c>
/// overload, so the inline snapshot projection tolerates them while they still land
/// in the stream.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because they share the single
/// process-wide container + host and observe the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<EventSourcedHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class EventSourcedAuditEventTests
{
    private readonly EventSourcedHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcedAuditEventTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared EventSourced Wolverine+Marten host fixture, injected by TUnit and
    /// shared across the entire test session.
    /// </param>
    public EventSourcedAuditEventTests(EventSourcedHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Runs the event-sourced failure-proof saga (a middle step that always throws,
    /// NO step-level resilience, with a workflow-level <c>OnFailure</c> chain) and
    /// asserts a <c>StepFailed</c> audit event — carrying the failed step name and
    /// the exception type — lands in the workflow's Marten stream.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_EventSourcedStepFailure_AppendsStepFailedEvent()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new EventSourcedFailureState { WorkflowId = workflowId };
        var startCommand = new StartEventSourcedFailureProofCommand(workflowId, initialState);

        var reachedTerminal = await this.host.RunToTerminalAsync<EventSourcedFailureProofSaga>(
            workflowId,
            startCommand);

        await Assert.That(reachedTerminal).IsTrue();

        // The OnFailure handler ran (the chain routed), so the failure path executed.
        await Assert.That(this.host.Invocations.CountFor(nameof(EventSourcedNotifyFailureStep)))
            .IsEqualTo(1);

        // Audit-event proof: a StepFailed event landed in the Marten stream.
        var stepFailed = await this.host.WaitForStreamEventAsync<EventSourcedFailureProofStepFailed>(workflowId);
        await Assert.That(stepFailed).IsNotNull();

        // It carries the failed step name and the exception type (the worker captures
        // the short type name via ex.GetType().Name, which the trigger forwards).
        await Assert.That(stepFailed!.FailedStepName).IsEqualTo(nameof(EventSourcedFailingStep));
        await Assert.That(stepFailed.ExceptionType).IsEqualTo(nameof(EventSourcedFailureException));
    }

    /// <summary>
    /// Runs the event-sourced low-confidence saga (its classify step returns
    /// confidence 0.5, below the 0.85 threshold) and asserts a
    /// <c>LowConfidenceRouted</c> audit event — carrying the gated step name, the
    /// observed score, and the threshold — lands in the workflow's Marten stream.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_EventSourcedLowConfidence_AppendsLowConfidenceRoutedEvent()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new EventSourcedLowConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartEventSourcedLowConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<EventSourcedLowConfidenceSaga>(
            workflowId,
            startCommand);

        await Assert.That(completed).IsTrue();

        // Routing proof: confidence 0.5 < 0.85 → the review handler ran.
        await Assert.That(this.host.Invocations.CountFor(nameof(EventSourcedLcReviewStep)))
            .IsEqualTo(1);

        // Audit-event proof: a LowConfidenceRouted event landed in the Marten stream.
        var routed = await this.host.WaitForStreamEventAsync<EventSourcedLowConfidenceLowConfidenceRouted>(workflowId);
        await Assert.That(routed).IsNotNull();

        // It carries the gated step name, the observed score, and the threshold.
        await Assert.That(routed!.StepName).IsEqualTo(nameof(EventSourcedLcClassifyStep));
        await Assert.That(routed.Confidence).IsEqualTo(0.5);
        await Assert.That(routed.Threshold).IsEqualTo(0.85);
    }
}
