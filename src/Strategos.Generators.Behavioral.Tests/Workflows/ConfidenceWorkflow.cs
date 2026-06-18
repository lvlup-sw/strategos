// -----------------------------------------------------------------------
// <copyright file="ConfidenceWorkflow.cs" company="Levelup Software">
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
/// Immutable state shared by the confidence-gate fixture workflows (DR-5 T014).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer used by each saga to fold every step's returned state.
/// </remarks>
[WorkflowState]
public sealed record ConfidenceState : IWorkflowState
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
// Low-confidence scenario: the classify step returns Confidence = 0.5, below
// the 0.85 threshold, so the saga must route to the OnLowConfidence handler
// (HumanReviewStepLow) instead of the primary finish step. Distinct CLR types
// per [Workflow] definition avoid the generator's CS0101 same-name collision.
// =============================================================================

/// <summary>
/// Entry step of the low-confidence fixture workflow. Deterministic; records
/// its invocation so the test can confirm the saga started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ConfPrepareStepLow(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ConfPrepareStepLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated step of the low-confidence fixture. Returns a step
/// result whose <c>Confidence</c> is 0.5 — below the 0.85 threshold — so the
/// generated saga's confidence gate must route to
/// <see cref="HumanReviewStepLow"/> rather than the primary finish step.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ClassifyStepLow(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ClassifyStepLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The low-confidence handler step. It only runs when the confidence gate
/// routes to it (confidence below threshold). As a single-step OnLowConfidence
/// handler it terminates the workflow (the generated handler calls
/// <c>MarkCompleted()</c>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class HumanReviewStepLow(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(HumanReviewStepLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The primary finish step of the low-confidence fixture. It must NOT run when
/// confidence is low, because the gate diverts to the handler branch before
/// this step is reached. Records its invocation so the test can assert it was
/// skipped.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ConfFinishStepLow(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ConfFinishStepLow));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The low-confidence fixture workflow definition. The classify step declares
/// <c>.RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;HumanReviewStepLow&gt;())</c>
/// and returns confidence 0.5, so the generated saga must route to the human
/// review handler. Drives the generator to emit <c>LowConfidenceSaga</c>,
/// <c>StartLowConfidenceCommand</c>, and <c>AddLowConfidenceWorkflow()</c>.
/// </summary>
[Workflow("low-confidence")]
public static partial class LowConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step, a confidence-gated classify
    /// step (returns 0.5, below the 0.85 threshold) whose <c>OnLowConfidence</c>
    /// branch runs <see cref="HumanReviewStepLow"/>, and a primary finish step
    /// that should be skipped when confidence is low.
    /// </summary>
    public static WorkflowDefinition<ConfidenceState> Definition => Workflow<ConfidenceState>
        .Create("low-confidence")
        .StartWith<ConfPrepareStepLow>()
        .Then<ClassifyStepLow>(step => step
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt.Then<HumanReviewStepLow>()))
        .Finally<ConfFinishStepLow>();
}

// =============================================================================
// High-confidence scenario: the classify step returns Confidence = 0.9, at/above
// the 0.85 threshold, so the saga must proceed on the primary path to the finish
// step and the human review handler must NOT run. Distinct CLR types again.
// =============================================================================

/// <summary>
/// Entry step of the high-confidence fixture workflow. Deterministic; records
/// its invocation.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ConfPrepareStepHigh(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ConfPrepareStepHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated step of the high-confidence fixture. Returns a step
/// result whose <c>Confidence</c> is 0.9 — at/above the 0.85 threshold — so the
/// generated saga's confidence gate must proceed down the primary path and NOT
/// route to <see cref="HumanReviewStepHigh"/>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ClassifyStepHigh(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ClassifyStepHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.WithConfidence(updated, 0.9));
    }
}

/// <summary>
/// The low-confidence handler step for the high-confidence fixture. It must NOT
/// run, because confidence is at/above the threshold. Records its invocation so
/// the test can assert it was skipped.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class HumanReviewStepHigh(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(HumanReviewStepHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The primary finish step of the high-confidence fixture. As the workflow's
/// <c>Finally</c> step, its completion drives the saga to its terminal phase and
/// <c>MarkCompleted()</c>. It must run on the primary path (high confidence).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ConfFinishStepHigh(WorkflowInvocationLog log) : IWorkflowStep<ConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ConfidenceState>> ExecuteAsync(
        ConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ConfFinishStepHigh));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The high-confidence fixture workflow definition. The classify step declares
/// the same confidence gate but returns confidence 0.9, so the generated saga
/// must proceed on the primary path to <see cref="ConfFinishStepHigh"/> and the
/// human review handler must NOT run. Drives the generator to emit
/// <c>HighConfidenceSaga</c>, <c>StartHighConfidenceCommand</c>, and
/// <c>AddHighConfidenceWorkflow()</c>.
/// </summary>
[Workflow("high-confidence")]
public static partial class HighConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step, a confidence-gated classify
    /// step (returns 0.9, at/above the 0.85 threshold) whose
    /// <c>OnLowConfidence</c> branch (<see cref="HumanReviewStepHigh"/>) must be
    /// skipped, and a primary finish step that should run.
    /// </summary>
    public static WorkflowDefinition<ConfidenceState> Definition => Workflow<ConfidenceState>
        .Create("high-confidence")
        .StartWith<ConfPrepareStepHigh>()
        .Then<ClassifyStepHigh>(step => step
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt.Then<HumanReviewStepHigh>()))
        .Finally<ConfFinishStepHigh>();
}
