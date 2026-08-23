// -----------------------------------------------------------------------
// <copyright file="IntermediateConfidenceWorkflow.cs" company="Levelup Software">
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
/// Immutable state shared by the two INTERMEDIATE-position confidence-gate fixture
/// workflows (#145). Marked <see cref="WorkflowStateAttribute"/> so the source generator
/// emits a reducer each saga uses to fold every step's returned state.
/// </summary>
[WorkflowState]
public sealed record IntermediateConfidenceState : IWorkflowState
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
// Fork-path scenario: the gated step is the FIRST of a TWO-step fork path, so it is an
// INTERMEDIATE path step — not the path's last step, which is the position the fork
// path-completed handler intercepts. An intermediate path step falls through to the
// generic completed handler instead, and that handler's confidence gate is what must
// route below-threshold results to the declared handler. Distinct CLR types per
// [Workflow] definition avoid the generator's CS0101 same-name collision.
// =============================================================================

/// <summary>
/// Entry step of the underwriting fixture, before the fork. Deterministic; records its
/// invocation so the test can confirm the saga actually started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwritingIntakeStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(UnderwritingIntakeStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated INTERMEDIATE fork-path step: the first of two steps on the first
/// fork path. Returns confidence 0.5, below the 0.85 threshold, so the gate must divert to
/// <see cref="UnderwritingManualReviewStep"/> rather than chain to the path's next step.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwritingRiskScoreStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(UnderwritingRiskScoreStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The fork path's LAST step, which follows the gated one. It is what makes the gated step
/// intermediate, and it must NOT run when the gate diverts — running it would mean the
/// intermediate gate was skipped and the path simply chained on.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwritingPricingStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(UnderwritingPricingStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The declared <c>OnLowConfidence</c> handler for the intermediate fork-path step. It runs
/// only when the gate routes to it, and as a single-step handler chain it terminates the
/// workflow — the generated handler calls <c>MarkCompleted()</c>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwritingManualReviewStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(UnderwritingManualReviewStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The single step of the SECOND fork path (deterministic, not gated). It exists so the
/// fork is well-formed; its completion marks path 1 succeeded, but the join never fires
/// because path 0 diverted to the handler and never reached Success.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwritingComplianceStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(UnderwritingComplianceStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The join step. It must NOT run when the gated path diverts: path 0 is never marked
/// succeeded, so join readiness never becomes true.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwritingAggregateStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(UnderwritingAggregateStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal step, after the join. It must NOT run when the gated path diverts
/// (the join never fires); the handler chain terminates the workflow instead.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwritingIssuePolicyStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(UnderwritingIssuePolicyStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The intermediate fork-path confidence fixture (#145). Its fork's first path has TWO
/// steps, and the FIRST of them — an intermediate position — carries
/// <c>.RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;UnderwritingManualReviewStep&gt;())</c>
/// and returns confidence 0.5. Drives the generator to emit
/// <c>IntermediateForkConfidenceSaga</c>, <c>StartIntermediateForkConfidenceCommand</c> and
/// <c>AddIntermediateForkConfidenceWorkflow()</c>.
/// </summary>
[Workflow("intermediate-fork-confidence")]
public static partial class IntermediateForkConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: an intake step, a fork whose first path runs a gated
    /// INTERMEDIATE step followed by a pricing step that must be skipped when the gate
    /// diverts, a deterministic second path, a join and a declared terminal step.
    /// </summary>
    public static WorkflowDefinition<IntermediateConfidenceState> Definition =>
        Workflow<IntermediateConfidenceState>
            .Create("intermediate-fork-confidence")
            .StartWith<UnderwritingIntakeStep>()
            .Fork(
                path => path
                    .Then<UnderwritingRiskScoreStep>(step => step
                        .RequireConfidence(0.85)
                        .OnLowConfidence(alt => alt.Then<UnderwritingManualReviewStep>()))
                    .Then<UnderwritingPricingStep>(),
                path => path.Then<UnderwritingComplianceStep>())
            .Join<UnderwritingAggregateStep>()
            .Finally<UnderwritingIssuePolicyStep>();
}

// =============================================================================
// Loop-body scenario: the gated step is the FIRST of a TWO-step loop body, so it is an
// INTERMEDIATE body step — not the body's last step, which is the position the loop
// completed handler intercepts. The body carries no nested fork, so the last body step is
// unambiguously the revise step regardless of how path steps are spliced into the step
// list.
// =============================================================================

/// <summary>
/// Entry step of the manuscript fixture, before the loop. Deterministic; records its
/// invocation so the test can confirm the saga actually started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ManuscriptIntakeStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ManuscriptIntakeStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The confidence-gated INTERMEDIATE loop-body step: the first of two body steps. Returns
/// confidence 0.5, below the 0.85 threshold, so the gate must divert to
/// <see cref="ManuscriptEditorReviewStep"/> rather than chain to the next body step.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ManuscriptCritiqueStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ManuscriptCritiqueStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>
/// The loop body's LAST step, which follows the gated one. It is what makes the gated step
/// intermediate, and it must NOT run when the gate diverts.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ManuscriptReviseStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ManuscriptReviseStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The declared <c>OnLowConfidence</c> handler for the intermediate loop-body step. As a
/// single-step handler chain it terminates the workflow.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ManuscriptEditorReviewStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ManuscriptEditorReviewStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal step after the loop. It must NOT run when the gate diverts.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ManuscriptPublishStep(WorkflowInvocationLog log) : IWorkflowStep<IntermediateConfidenceState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<IntermediateConfidenceState>> ExecuteAsync(
        IntermediateConfidenceState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ManuscriptPublishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<IntermediateConfidenceState>.FromState(updated));
    }
}

/// <summary>
/// The intermediate loop-body confidence fixture (#145). Its <c>RepeatUntil</c> body has TWO
/// steps, and the FIRST of them — an intermediate position — carries
/// <c>.RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;ManuscriptEditorReviewStep&gt;())</c>
/// and returns confidence 0.5. Drives the generator to emit
/// <c>IntermediateLoopConfidenceSaga</c>, <c>StartIntermediateLoopConfidenceCommand</c> and
/// <c>AddIntermediateLoopConfidenceWorkflow()</c>.
/// </summary>
[Workflow("intermediate-loop-confidence")]
public static partial class IntermediateLoopConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: an intake step, a <c>RepeatUntil</c> loop whose FIRST of
    /// two body steps is confidence-gated, and a declared terminal step. Neither the body's
    /// last step nor the terminal may run when the gate diverts.
    /// </summary>
    public static WorkflowDefinition<IntermediateConfidenceState> Definition =>
        Workflow<IntermediateConfidenceState>
            .Create("intermediate-loop-confidence")
            .StartWith<ManuscriptIntakeStep>()
            .RepeatUntil(
                state => true,
                "Refinement",
                loop => loop
                    .Then<ManuscriptCritiqueStep>(step => step
                        .RequireConfidence(0.85)
                        .OnLowConfidence(alt => alt.Then<ManuscriptEditorReviewStep>()))
                    .Then<ManuscriptReviseStep>(),
                maxIterations: 3)
            .Finally<ManuscriptPublishStep>();
}
