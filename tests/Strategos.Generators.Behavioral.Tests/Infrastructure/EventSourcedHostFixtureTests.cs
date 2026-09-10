// -----------------------------------------------------------------------
// <copyright file="EventSourcedHostFixtureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Smoke test for the <see cref="EventSourcedHostFixture"/> (#138 G-5 Task 5.1):
/// proves an EventSourced-mode generated saga stands up on a real Marten event
/// store and that a baseline workflow event round-trips through the Marten stream.
/// This is the infrastructure backbone the <c>StepFailed</c> / <c>LowConfidenceRouted</c>
/// audit-event tests build on.
/// </summary>
/// <remarks>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation log.
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<EventSourcedHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class EventSourcedHostFixtureTests
{
    private readonly EventSourcedHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcedHostFixtureTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared EventSourced Wolverine+Marten host fixture, injected by TUnit and
    /// shared across the entire test session.
    /// </param>
    public EventSourcedHostFixtureTests(EventSourcedHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Runs the event-sourced happy-path saga end-to-end and asserts a baseline
    /// workflow event round-trips through the Marten stream: the generated handlers
    /// appended events (Started + step completions) to <c>WorkflowId</c>'s stream,
    /// which is exactly the surface the audit-event tests read.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EventSourcedFixture_Smoke_AppendsAndReadsStream()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new EventSourcedAuditState { WorkflowId = workflowId };
        var startCommand = new StartEventSourcedHappyCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<EventSourcedHappySaga>(workflowId, startCommand);

        // The saga reached its terminal phase: the Finally step's MarkCompleted()
        // removed the persisted saga document.
        await Assert.That(completed).IsTrue();

        // Both deterministic steps ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(EventSourcedHappyStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(EventSourcedHappyFinishStep))).IsEqualTo(1);

        // Round-trip proof: the EventSourced-mode handlers appended events to the
        // Marten stream keyed by WorkflowId, so reading the stream back returns a
        // non-empty list of event payloads — the baseline the audit-event tests rely on.
        var events = await this.host.ReadStreamEventsAsync(workflowId);
        await Assert.That(events).IsNotEmpty();

        // The two step-completed events both folded through the stream (the
        // generated handlers append the step-completed event before ApplyEvent).
        var completedEventCount =
            events.Count(e => e.GetType().Name.EndsWith("Completed", StringComparison.Ordinal));
        await Assert.That(completedEventCount).IsGreaterThanOrEqualTo(2);
    }
}
