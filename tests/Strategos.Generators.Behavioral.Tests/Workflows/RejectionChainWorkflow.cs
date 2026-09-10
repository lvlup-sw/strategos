// -----------------------------------------------------------------------
// <copyright file="RejectionChainWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Models;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

// =============================================================================
// A rejected approval whose OnRejection chain has MORE THAN ONE step.
//
// Every other rejection and escalation fixture in the repository declares a chain
// of exactly one step, and a one-step chain cannot distinguish "the chain ran" from
// "the chain's first step ran and the saga completed" — the two produce an identical
// observation. So the whole class of chain-truncation defects is invisible to the
// existing corpus, including the approval fixture that runs on a real host, which
// exercises only the APPROVED route.
//
// A rejection chain is dispatched by the approval component at its FIRST step only,
// and through the GENERIC start command, which makes the generic completed handler
// the chain's only routing site. Failure handlers look similar but are not: they
// mint their own command and event types and chain them in their own component, so
// their generic handlers are inert and their adjacency is unobservable.
//
// The chain declares Complete(), so the workflow ends when the chain ends: the
// main-flow steps after the checkpoint must never run on this route.
// =============================================================================

/// <summary>
/// Immutable state for the rejected purchase-requisition fixture.
/// </summary>
[WorkflowState]
public sealed record PurchaseRequisitionState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the originator was told the requisition was refused.
    /// </summary>
    public bool OriginatorNotified { get; init; }
}

/// <summary>
/// Entry step of the rejected-requisition fixture, and the step the approval checkpoint
/// follows.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class PrepareRequisition(WorkflowInvocationLog log) : IWorkflowStep<PurchaseRequisitionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<PurchaseRequisitionState>> ExecuteAsync(
        PurchaseRequisitionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(PrepareRequisition));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<PurchaseRequisitionState>.FromState(updated));
    }
}

/// <summary>
/// The main-flow step after the checkpoint. The decision under test is a rejection and
/// the rejection chain declares its own completion, so this must never run.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class PlaceSupplierOrder(WorkflowInvocationLog log) : IWorkflowStep<PurchaseRequisitionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<PurchaseRequisitionState>> ExecuteAsync(
        PurchaseRequisitionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(PlaceSupplierOrder));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<PurchaseRequisitionState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal of the main flow, reached only on the approved route. The
/// rejection chain ends the workflow itself, so this must never run here.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ClosePurchaseRequisition(WorkflowInvocationLog log) : IWorkflowStep<PurchaseRequisitionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<PurchaseRequisitionState>> ExecuteAsync(
        PurchaseRequisitionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ClosePurchaseRequisition));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<PurchaseRequisitionState>.FromState(updated));
    }
}

/// <summary>
/// The FIRST step of the two-step rejection chain — the only one the approval component
/// dispatches directly.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RecordRequisitionRejection(WorkflowInvocationLog log)
    : IWorkflowStep<PurchaseRequisitionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<PurchaseRequisitionState>> ExecuteAsync(
        PurchaseRequisitionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RecordRequisitionRejection));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<PurchaseRequisitionState>.FromState(updated));
    }
}

/// <summary>
/// The SECOND step of the rejection chain. Nothing dispatches it but the completed handler
/// of the step before it, so it runs only if the chain advances rather than terminating at
/// its first step.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class NotifyRequisitionOriginator(WorkflowInvocationLog log)
    : IWorkflowStep<PurchaseRequisitionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<PurchaseRequisitionState>> ExecuteAsync(
        PurchaseRequisitionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(NotifyRequisitionOriginator));

        var updated = state with
        {
            StepCount = state.StepCount + 1,
            OriginatorNotified = true,
        };

        return Task.FromResult(StepResult<PurchaseRequisitionState>.FromState(updated));
    }
}

/// <summary>
/// The approver the checkpoint routes to. The approval point's generated identifier is
/// derived from this type's simple name with the <c>Approver</c> suffix stripped, so the
/// generated surface is <c>ResumePurchasingManagerApprovalCommand</c> and
/// <c>RequestPurchasingManagerApprovalEvent</c>.
/// </summary>
public sealed class PurchasingManagerApprover
{
}

/// <summary>
/// Stands in for the human at the checkpoint, deterministically REJECTING — which is what
/// puts the workflow onto the rejection chain.
/// </summary>
/// <remarks>
/// Registered by explicit type on the host rather than by naming convention, so nothing
/// else in the assembly is pulled into handler discovery alongside it.
/// </remarks>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class PurchasingManagerApprovalDecisionHandler(WorkflowInvocationLog log)
{
    /// <summary>
    /// The name this handler records under when the checkpoint is reached.
    /// </summary>
    public const string ApprovalRequested = "PurchasingManagerApprovalRequested";

    private readonly WorkflowInvocationLog log = log;

    /// <summary>
    /// Refuses the requisition as soon as the saga asks for a decision.
    /// </summary>
    /// <param name="requested">The generated request-approval event yielded by the saga.</param>
    /// <returns>The generated resume command carrying the rejected decision.</returns>
    public ResumePurchasingManagerApprovalCommand Handle(RequestPurchasingManagerApprovalEvent requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        this.log.Record(ApprovalRequested);

        return new ResumePurchasingManagerApprovalCommand(
            requested.WorkflowId,
            ApprovalDecision.Rejected,
            "refuse",
            null);
    }
}

/// <summary>
/// The purchase-requisition workflow definition: prepare, await a purchasing manager's
/// decision, place the order, close the requisition — with a two-step rejection chain that
/// ends the workflow.
/// </summary>
[Workflow("purchase-requisition-review")]
public static partial class PurchaseRequisitionReviewWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition.
    /// </summary>
    public static WorkflowDefinition<PurchaseRequisitionState> Definition =>
        Workflow<PurchaseRequisitionState>
            .Create("purchase-requisition-review")
            .StartWith<PrepareRequisition>()
            .AwaitApproval<PurchasingManagerApprover>(approval => approval
                .WithContext("A purchase requisition requires a purchasing manager's decision.")
                .WithOption("release", "Release", "Release the requisition to the supplier.", isDefault: true)
                .WithOption("refuse", "Refuse", "Refuse the requisition.")
                .OnRejection(rejection => rejection
                    .Then<RecordRequisitionRejection>()
                    .Then<NotifyRequisitionOriginator>()
                    .Complete()))
            .Then<PlaceSupplierOrder>()
            .Finally<ClosePurchaseRequisition>();
}
