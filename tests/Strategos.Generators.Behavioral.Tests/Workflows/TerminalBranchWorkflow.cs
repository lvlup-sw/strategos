// -----------------------------------------------------------------------
// <copyright file="TerminalBranchWorkflow.cs" company="Levelup Software">
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

// =============================================================================
// The MIXED branch shape — one case rejoins, the other ends the workflow — which
// is the shape that discriminates a real fix from a plausible-looking one:
//
//     ReviewOrder
//       ├── Outcome == Approved → ProcessApprovedOrder ──→ ShipApprovedOrder (terminal)
//       └── otherwise           → RejectOrder .Complete()  ends here
//
// A rejected order must NOT ship. Because the approved case rejoins, the
// branch-LEVEL rejoin flag is true for this workflow, so an emitter that decides
// a path's ending from that flag alone routes the rejecting case to
// ShipApprovedOrder too — a rejected order is shipped, and the flag-reading fix
// still passes any test whose branch has a single uniform exit. Only a mixed
// branch separates "reads the branch-level flag" from "reads the case".
//
// The observable signature is the count on ShipApprovedOrder: it must be zero on
// a rejected run, and RejectOrder must be the step that completes the workflow.
// See #175.
// =============================================================================

/// <summary>The outcome of reviewing an order, which selects the branch case.</summary>
public enum OrderReviewOutcome
{
    /// <summary>The order was rejected and must not ship.</summary>
    Rejected = 0,

    /// <summary>The order passed review and continues to fulfilment.</summary>
    Approved = 1,
}

/// <summary>State for the mixed branch workflow: an order that is either approved or rejected.</summary>
[WorkflowState]
public sealed record TerminalBranchState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the review outcome that selects the branch case.</summary>
    public OrderReviewOutcome Outcome { get; init; }

    /// <summary>Gets the number of steps that folded their result into state.</summary>
    public int StepCount { get; init; }
}

/// <summary>Pre-branch step: reviews the order and decides which case applies.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ReviewOrder(WorkflowInvocationLog log) : IWorkflowStep<TerminalBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<TerminalBranchState>> ExecuteAsync(TerminalBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ReviewOrder));
        return Task.FromResult(StepResult<TerminalBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Approved branch path: processes the order, then rejoins the main flow to ship it.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ProcessApprovedOrder(WorkflowInvocationLog log) : IWorkflowStep<TerminalBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<TerminalBranchState>> ExecuteAsync(TerminalBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ProcessApprovedOrder));
        return Task.FromResult(StepResult<TerminalBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// Rejecting branch path: ends the workflow at this step. A rejected order must never reach the
/// declared terminal, so this step — not <see cref="ShipApprovedOrder"/> — completes the workflow.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RejectOrder(WorkflowInvocationLog log) : IWorkflowStep<TerminalBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<TerminalBranchState>> ExecuteAsync(TerminalBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(RejectOrder));
        return Task.FromResult(StepResult<TerminalBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Declared terminal step, reachable only from the approved path.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ShipApprovedOrder(WorkflowInvocationLog log) : IWorkflowStep<TerminalBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<TerminalBranchState>> ExecuteAsync(TerminalBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ShipApprovedOrder));
        return Task.FromResult(StepResult<TerminalBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// A branch workflow mixing a rejoining case with a workflow-ending <c>.Complete()</c> case, both
/// alongside a declared terminal. Drives the generator to emit <c>TerminalBranchSaga</c>,
/// <c>StartTerminalBranchCommand</c> and <c>AddTerminalBranchWorkflow()</c>.
/// </summary>
[Workflow("terminal-branch")]
public static partial class TerminalBranchWorkflowDefinition
{
    /// <summary>Gets the fluent definition: review, then either process and ship, or reject and stop.</summary>
    public static WorkflowDefinition<TerminalBranchState> Definition => Workflow<TerminalBranchState>
        .Create("terminal-branch")
        .StartWith<ReviewOrder>()
        .Branch(
            state => state.Outcome,
            BranchCase<TerminalBranchState, OrderReviewOutcome>.When(
                OrderReviewOutcome.Approved,
                path => path.Then<ProcessApprovedOrder>()),
            BranchCase<TerminalBranchState, OrderReviewOutcome>.Otherwise(
                path => path.Then<RejectOrder>().Complete()))
        .Finally<ShipApprovedOrder>();
}
