// -----------------------------------------------------------------------
// <copyright file="ConfidenceBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (DR-5 T014) that a step's
/// <c>.RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;HumanReview&gt;())</c>
/// is actually honored at runtime: the generated confidence-gated saga handler
/// compares the step result's confidence to the threshold and routes to the
/// OnLowConfidence handler step (low case) or proceeds on the primary path
/// (high case) on a real PostgreSQL-backed host.
/// </summary>
/// <remarks>
/// <para>
/// Two fixtures with distinct step CLR types (avoiding the generator's CS0101
/// same-name collision) exercise both sides of the gate:
/// <list type="bullet">
///   <item><description>
///     The low-confidence saga's classify step returns confidence 0.5 (below the
///     0.85 threshold), so the human-review handler MUST run and the primary
///     finish step MUST NOT.
///   </description></item>
///   <item><description>
///     The high-confidence saga's classify step returns confidence 0.9 (at/above
///     the threshold), so the primary finish step MUST run and the human-review
///     handler MUST NOT.
///   </description></item>
/// </list>
/// Before DR-5 the OnLowConfidence branch was not lowered into the saga at all,
/// so both scenarios took the primary path; asserting that the low case diverts
/// to the handler — and the high case does not — is the routing proof.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<WolverineHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class ConfidenceBehaviorTests
{
    private readonly WolverineHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfidenceBehaviorTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public ConfidenceBehaviorTests(WolverineHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated low-confidence workflow saga whose classify step
    /// returns confidence 0.5 (below the 0.85 threshold). Asserts the saga routed
    /// to the lowered OnLowConfidence handler step (HumanReviewStepLow ran) and
    /// the primary finish step did NOT run.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_LowConfidence_RoutesToOnLowConfidenceBranch()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartLowConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<LowConfidenceSaga>(workflowId, startCommand);

        // The saga reached its terminal phase: the single-step OnLowConfidence
        // handler calls MarkCompleted(), removing the persisted saga document.
        await Assert.That(completed).IsTrue();

        // The prepare and classify steps each ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(ConfPrepareStepLow))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ClassifyStepLow))).IsEqualTo(1);

        // Routing proof: confidence 0.5 < 0.85 → the human-review handler ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(HumanReviewStepLow))).IsEqualTo(1);

        // ...and the primary finish step was skipped (the gate diverted before it).
        await Assert.That(this.host.Invocations.CountFor(nameof(ConfFinishStepLow))).IsEqualTo(0);
    }

    /// <summary>
    /// Starts the generated high-confidence workflow saga whose classify step
    /// returns confidence 0.9 (at/above the 0.85 threshold). Asserts the saga
    /// proceeded on the primary path (the finish step ran) and did NOT route to
    /// the OnLowConfidence handler step.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_HighConfidence_ProceedsOnPrimaryPath()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartHighConfidenceCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<HighConfidenceSaga>(workflowId, startCommand);

        // The saga reached its terminal phase via the primary finish step's
        // MarkCompleted().
        await Assert.That(completed).IsTrue();

        // The prepare and classify steps each ran exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(ConfPrepareStepHigh))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ClassifyStepHigh))).IsEqualTo(1);

        // Routing proof: confidence 0.9 >= 0.85 → the primary finish step ran...
        await Assert.That(this.host.Invocations.CountFor(nameof(ConfFinishStepHigh))).IsEqualTo(1);

        // ...and the human-review handler did NOT run.
        await Assert.That(this.host.Invocations.CountFor(nameof(HumanReviewStepHigh))).IsEqualTo(0);
    }
}
