// -----------------------------------------------------------------------
// <copyright file="LowConfidenceChainWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>
/// Immutable state shared by the multi-step / rejoining low-confidence fixture
/// workflows (G-4 / #139). Distinct from <c>ConfidenceState</c> only by name so
/// the generator emits an independent reducer per fixture set.
/// </summary>
[WorkflowState]
public sealed record ChainConfidenceState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }
}

// =============================================================================
// Two-step chain scenario (Task 4.1): the classify step returns Confidence = 0.5
// (below the 0.85 threshold), so the saga must route to a TWO-step OnLowConfidence
// handler chain — ChainHandlerAStep then ChainHandlerBStep, in that order — before
// the workflow ends. Distinct CLR types per [Workflow] definition avoid the
// generator's CS0101 same-name collision.
// =============================================================================

/// <summary>
/// Entry step of the two-step-chain fixture. Records its invocation.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ChainPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ChainPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated step of the two-step-chain fixture. Returns confidence
/// 0.5 (below the 0.85 threshold) so the generated saga routes to the two-step
/// OnLowConfidence handler chain.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ChainClassifyStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ChainClassifyStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// First step of the two-step OnLowConfidence handler chain. Must run before
/// <see cref="ChainHandlerBStep"/>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ChainHandlerAStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ChainHandlerAStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// Second step of the two-step OnLowConfidence handler chain. As the terminal
/// step of a (default) terminating handler it ends the workflow.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ChainHandlerBStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ChainHandlerBStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The primary finish step of the two-step-chain fixture. Must NOT run because
/// the gate diverts to the (terminating) handler chain.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ChainFinishStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ChainFinishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The two-step-chain fixture workflow definition. The classify step declares a
/// confidence gate whose <c>OnLowConfidence</c> branch chains
/// <c>ChainHandlerAStep</c> then <c>ChainHandlerBStep</c>, and returns confidence
/// 0.5 so the generated saga must run BOTH handler steps in order.
/// </summary>
[Workflow("low-confidence-chain")]
public static partial class LowConfidenceChainWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition with a two-step OnLowConfidence handler chain.
    /// </summary>
    public static WorkflowDefinition<ChainConfidenceState> Definition => Workflow<ChainConfidenceState>
        .Create("low-confidence-chain")
        .StartWith<ChainPrepareStep>()
        .Then<ChainClassifyStep>(step => step
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt
                .Then<ChainHandlerAStep>()
                .Then<ChainHandlerBStep>()))
        .Finally<ChainFinishStep>();
}

// =============================================================================
// Rejoin scenario (Task 4.2): the classify step returns Confidence = 0.5 (below
// threshold), routing to a single-step OnLowConfidence handler that is declared
// REJOINING via .RejoinMainFlow(). After the handler runs, the saga must resume
// the MAIN flow at the step AFTER the gated step (RejoinFinishStep), instead of
// terminating. Distinct CLR types again.
// =============================================================================

/// <summary>
/// Entry step of the rejoin fixture. Records its invocation.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RejoinPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RejoinPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated step of the rejoin fixture. Returns confidence 0.5 (below
/// the 0.85 threshold) so the saga routes to the rejoining handler.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RejoinClassifyStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RejoinClassifyStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The rejoining OnLowConfidence handler step. After it runs, the saga resumes
/// the main flow at <see cref="RejoinFinishStep"/> (the step after the gated
/// step) rather than terminating, because the handler declared
/// <c>.RejoinMainFlow()</c>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RejoinHandlerStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RejoinHandlerStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The main-flow finish step that a REJOINING handler resumes into. It must run
/// after <see cref="RejoinHandlerStep"/> when the gate diverts and the handler
/// rejoins; as the workflow's <c>Finally</c> step its completion ends the saga.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RejoinFinishStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RejoinFinishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The rejoin fixture workflow definition. The classify step's
/// <c>OnLowConfidence</c> handler declares <c>.RejoinMainFlow()</c>, so after the
/// handler step runs the saga must resume the main flow at
/// <see cref="RejoinFinishStep"/>.
/// </summary>
[Workflow("low-confidence-rejoin")]
public static partial class LowConfidenceRejoinWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition with a single-step REJOINING OnLowConfidence
    /// handler.
    /// </summary>
    public static WorkflowDefinition<ChainConfidenceState> Definition => Workflow<ChainConfidenceState>
        .Create("low-confidence-rejoin")
        .StartWith<RejoinPrepareStep>()
        .Then<RejoinClassifyStep>(step => step
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt
                .Then<RejoinHandlerStep>()
                .RejoinMainFlow()))
        .Finally<RejoinFinishStep>();
}

// =============================================================================
// Terminating scenario (Task 4.2, back-compat default): the classify step returns
// Confidence = 0.5, routing to a single-step OnLowConfidence handler with NO
// rejoin marker. The handler must TERMINATE the workflow (MarkCompleted), and the
// main-flow finish step must NOT run. Distinct CLR types again.
// =============================================================================

/// <summary>
/// Entry step of the terminating fixture. Records its invocation.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TermPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TermPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated step of the terminating fixture. Returns confidence 0.5
/// (below the 0.85 threshold) so the saga routes to the terminating handler.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TermClassifyStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TermClassifyStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The terminating OnLowConfidence handler step. With no rejoin marker it is the
/// terminal step of the workflow (the generated handler calls
/// <c>MarkCompleted()</c>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TermHandlerStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TermHandlerStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The main-flow finish step of the terminating fixture. It must NOT run, because
/// the (default) terminating handler ends the workflow.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TermFinishStep(WorkflowInvocationLog log) : IWorkflowStep<ChainConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ChainConfidenceState>> ExecuteAsync(
        ChainConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TermFinishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ChainConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The terminating fixture workflow definition (back-compat default). The classify
/// step's <c>OnLowConfidence</c> handler has no rejoin marker, so the handler step
/// terminates the workflow and <see cref="TermFinishStep"/> must NOT run.
/// </summary>
[Workflow("low-confidence-terminating")]
public static partial class LowConfidenceTerminatingWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition with a single-step TERMINATING OnLowConfidence
    /// handler (no rejoin marker).
    /// </summary>
    public static WorkflowDefinition<ChainConfidenceState> Definition => Workflow<ChainConfidenceState>
        .Create("low-confidence-terminating")
        .StartWith<TermPrepareStep>()
        .Then<TermClassifyStep>(step => step
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt
                .Then<TermHandlerStep>()))
        .Finally<TermFinishStep>();
}
