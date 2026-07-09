// -----------------------------------------------------------------------
// <copyright file="NestedRepeatUntilConfidenceWorkflow.cs" company="Levelup Software">
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
/// Immutable state shared by the loop-body confidence-gate fixture workflows
/// (DR-5 / #145 gap B). Marked <see cref="WorkflowStateAttribute"/> so the source generator
/// emits a reducer used by each saga to fold every step's returned state.
/// </summary>
[WorkflowState]
public sealed record LoopConfidenceState : IWorkflowState
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
// Low-confidence scenario: the loop body's (single, hence LAST) step returns
// Confidence = 0.5, below the 0.85 threshold, so the generated loop completed
// handler's confidence gate must route to the OnLowConfidence handler
// (LoopConfReviewLow) BEFORE evaluating the loop condition — the primary finish
// step never runs. Distinct CLR types per [Workflow] definition avoid the
// generator's CS0101 same-name collision.
// =============================================================================

/// <summary>
/// Entry step of the low-confidence loop fixture (runs before the loop).
/// Deterministic; records its invocation so the test can confirm the saga started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfPrepareLow(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfPrepareLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated loop-body step of the low-confidence fixture (the single — and hence
/// LAST — body step). Returns a step result whose <c>Confidence</c> is 0.5 — below the 0.85
/// threshold — so the generated loop completed handler's confidence gate must route to
/// <see cref="LoopConfReviewLow"/> rather than continue or exit the loop.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfAssessLow(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfAssessLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The OnLowConfidence handler step for the low-confidence loop. It runs only when the loop
/// completed handler's confidence gate routes to it (confidence below threshold). As a
/// single-step OnLowConfidence handler it terminates the workflow (the generated handler calls
/// <c>MarkCompleted()</c>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfReviewLow(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfReviewLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The primary finish step of the low-confidence loop fixture (the loop's continuation). It
/// must NOT run when confidence is low, because the gate diverts to the handler branch before
/// the loop condition is evaluated. Records its invocation so the test can assert it was skipped.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfFinishLow(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfFinishLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The low-confidence loop fixture workflow definition (DR-5 / #145 gap B). The single loop-body
/// step declares
/// <c>.RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;LoopConfReviewLow&gt;())</c>
/// and returns confidence 0.5, so the generated loop completed handler must route to the review
/// handler. Drives the generator to emit <c>LowLoopConfidenceSaga</c>,
/// <c>StartLowLoopConfidenceCommand</c>, and <c>AddLowLoopConfidenceWorkflow()</c>.
/// </summary>
[Workflow("low-loop-confidence")]
public static partial class LowLoopConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step, a <c>RepeatUntil</c> loop whose single body
    /// step is confidence-gated (returns 0.5, below the 0.85 threshold) with an
    /// <c>OnLowConfidence</c> branch running <see cref="LoopConfReviewLow"/>, and a primary
    /// finish step that must be skipped when confidence is low.
    /// </summary>
    public static WorkflowDefinition<LoopConfidenceState> Definition => Workflow<LoopConfidenceState>
        .Create("low-loop-confidence")
        .StartWith<LoopConfPrepareLow>()
        .RepeatUntil(
            state => true,
            "Refinement",
            loop => loop
                .Then<LoopConfAssessLow>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<LoopConfReviewLow>())),
            maxIterations: 3)
        .Finally<LoopConfFinishLow>();
}

// =============================================================================
// High-confidence scenario: the loop body's step returns Confidence = 0.9,
// at/above the 0.85 threshold, so the loop completed handler's confidence gate
// must NOT fire — the loop condition is evaluated (here it exits) and the finish
// step runs. The review handler must NOT run. Distinct CLR types again.
// =============================================================================

/// <summary>
/// Entry step of the high-confidence loop fixture. Deterministic; records its invocation.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfPrepareHigh(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfPrepareHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated loop-body step of the high-confidence fixture. Returns a step result
/// whose <c>Confidence</c> is 0.9 — at/above the 0.85 threshold — so the loop completed handler's
/// gate must NOT fire and the loop proceeds to its condition evaluation (which exits), never
/// routing to <see cref="LoopConfReviewHigh"/>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfAssessHigh(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfAssessHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.WithConfidence(updated, 0.9));
    }
}

/// <summary>
/// The OnLowConfidence handler step for the high-confidence fixture. It must NOT run, because
/// confidence is at/above the threshold. Records its invocation so the test can assert it was
/// skipped.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfReviewHigh(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfReviewHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The primary finish step of the high-confidence loop fixture (the loop's continuation). As the
/// workflow's <c>Finally</c> step, its completion drives the saga to its terminal phase and
/// <c>MarkCompleted()</c>. It must run when confidence is high and the loop exits.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LoopConfFinishHigh(WorkflowInvocationLog log) : IWorkflowStep<LoopConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoopConfidenceState>> ExecuteAsync(
        LoopConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LoopConfFinishHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoopConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The high-confidence loop fixture workflow definition. The single loop-body step declares the
/// same confidence gate but returns confidence 0.9, so the generated loop completed handler must
/// NOT route to the review handler and the loop's condition (here <c>state =&gt; true</c>) exits
/// to <see cref="LoopConfFinishHigh"/>. Drives the generator to emit
/// <c>HighLoopConfidenceSaga</c>, <c>StartHighLoopConfidenceCommand</c>, and
/// <c>AddHighLoopConfidenceWorkflow()</c>.
/// </summary>
[Workflow("high-loop-confidence")]
public static partial class HighLoopConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step, a <c>RepeatUntil</c> loop whose single body
    /// step is confidence-gated (returns 0.9, at/above the 0.85 threshold) whose
    /// <c>OnLowConfidence</c> branch (<see cref="LoopConfReviewHigh"/>) must be skipped, and a
    /// finish step that should run once the loop exits.
    /// </summary>
    public static WorkflowDefinition<LoopConfidenceState> Definition => Workflow<LoopConfidenceState>
        .Create("high-loop-confidence")
        .StartWith<LoopConfPrepareHigh>()
        .RepeatUntil(
            state => true,
            "Refinement",
            loop => loop
                .Then<LoopConfAssessHigh>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<LoopConfReviewHigh>())),
            maxIterations: 3)
        .Finally<LoopConfFinishHigh>();
}
