// -----------------------------------------------------------------------
// <copyright file="LowConfidenceChainTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (G-4 / #139) for MULTI-STEP and REJOINING
/// <c>OnLowConfidence</c> handlers running on a real PostgreSQL-backed
/// Wolverine+Marten host:
/// <list type="bullet">
///   <item><description>
///     A two-step OnLowConfidence chain runs BOTH handler steps in order
///     (before #139 only the first <c>Then&lt;T&gt;</c> was lowered).
///   </description></item>
///   <item><description>
///     A handler declared <c>.RejoinMainFlow()</c> resumes the MAIN flow at the
///     step after the gated step rather than terminating.
///   </description></item>
///   <item><description>
///     A handler with no rejoin marker still terminates the workflow
///     (back-compat default).
///   </description></item>
/// </list>
/// </summary>
/// <remarks>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation log.
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<WolverineHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class LowConfidenceChainTests
{
    private readonly WolverineHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="LowConfidenceChainTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public LowConfidenceChainTests(WolverineHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Task 4.1: a step gated at confidence 0.85 returns 0.5 and declares a
    /// TWO-step OnLowConfidence chain
    /// (<c>OnLowConfidence(alt =&gt; alt.Then&lt;A&gt;().Then&lt;B&gt;())</c>). Asserts both
    /// handler steps ran, exactly once each, and in order (A before B) — proving
    /// the whole chain is lowered, not just the first <c>Then&lt;T&gt;</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_OnLowConfidenceTwoStepChain_RunsBothInOrder()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ChainConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartLowConfidenceChainCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<LowConfidenceChainSaga>(workflowId, startCommand);

        // The chain's last step terminates the (default terminating) handler.
        await Assert.That(completed).IsTrue();

        // The gated step ran once and diverted to the handler chain.
        await Assert.That(this.host.Invocations.CountFor(nameof(ChainClassifyStep))).IsEqualTo(1);

        // Both handler steps ran exactly once...
        await Assert.That(this.host.Invocations.CountFor(nameof(ChainHandlerAStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ChainHandlerBStep))).IsEqualTo(1);

        // ...in order: A before B.
        var invocations = this.host.Invocations.Invocations.ToList();
        var indexA = invocations.IndexOf(nameof(ChainHandlerAStep));
        var indexB = invocations.IndexOf(nameof(ChainHandlerBStep));
        await Assert.That(indexA).IsGreaterThanOrEqualTo(0);
        await Assert.That(indexB).IsGreaterThanOrEqualTo(0);
        await Assert.That(indexA).IsLessThan(indexB);

        // The primary finish step was skipped (the gate diverted before it).
        await Assert.That(this.host.Invocations.CountFor(nameof(ChainFinishStep))).IsEqualTo(0);
    }

    /// <summary>
    /// Task 4.2: a step gated at confidence 0.85 returns 0.5 and declares a
    /// single-step REJOINING handler
    /// (<c>OnLowConfidence(alt =&gt; alt.Then&lt;H&gt;().RejoinMainFlow())</c>). Asserts the
    /// handler ran and the saga then RESUMED the main flow at the step after the
    /// gated step (the finish step ran), in order — handler before finish.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_OnLowConfidenceRejoin_ResumesMainFlowAfterGatedStep()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ChainConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartLowConfidenceRejoinCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<LowConfidenceRejoinSaga>(workflowId, startCommand);

        // The main-flow finish step's MarkCompleted() ends the saga.
        await Assert.That(completed).IsTrue();

        // The gated step ran once and diverted to the handler.
        await Assert.That(this.host.Invocations.CountFor(nameof(RejoinClassifyStep))).IsEqualTo(1);

        // The handler ran once...
        await Assert.That(this.host.Invocations.CountFor(nameof(RejoinHandlerStep))).IsEqualTo(1);

        // ...and the saga REJOINED the main flow: the finish step ran.
        await Assert.That(this.host.Invocations.CountFor(nameof(RejoinFinishStep))).IsEqualTo(1);

        // Order: the handler ran before the rejoined main-flow finish step.
        var invocations = this.host.Invocations.Invocations.ToList();
        var indexHandler = invocations.IndexOf(nameof(RejoinHandlerStep));
        var indexFinish = invocations.IndexOf(nameof(RejoinFinishStep));
        await Assert.That(indexHandler).IsGreaterThanOrEqualTo(0);
        await Assert.That(indexFinish).IsGreaterThanOrEqualTo(0);
        await Assert.That(indexHandler).IsLessThan(indexFinish);
    }

    /// <summary>
    /// Task 4.2 (back-compat default): a step gated at confidence 0.85 returns 0.5
    /// and declares a single-step handler with NO rejoin marker
    /// (<c>OnLowConfidence(alt =&gt; alt.Then&lt;H&gt;())</c>). Asserts the handler ran and
    /// TERMINATED the workflow — the main-flow finish step did NOT run.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_OnLowConfidenceTerminating_CompletesWorkflow()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ChainConfidenceState { WorkflowId = workflowId };
        var startCommand = new StartLowConfidenceTerminatingCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<LowConfidenceTerminatingSaga>(workflowId, startCommand);

        // The handler step's MarkCompleted() ends the saga.
        await Assert.That(completed).IsTrue();

        // The gated step ran once and diverted to the handler.
        await Assert.That(this.host.Invocations.CountFor(nameof(TermClassifyStep))).IsEqualTo(1);

        // The handler ran once...
        await Assert.That(this.host.Invocations.CountFor(nameof(TermHandlerStep))).IsEqualTo(1);

        // ...and the workflow TERMINATED: the main-flow finish step did NOT run.
        await Assert.That(this.host.Invocations.CountFor(nameof(TermFinishStep))).IsEqualTo(0);
    }
}
