// -----------------------------------------------------------------------
// <copyright file="ApprovalTimeoutWorkflow.cs" company="Levelup Software">
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
// OnTimeout on a real host, without sleeping the wall-clock. The broker parks
// the checkpoint (SetPending) and the test injects the timeout command with
// the same request id. The first escalation step must run; the main-flow
// successor must not.
// =============================================================================

/// <summary>
/// Immutable state for the injected-timeout wire-transfer fixture.
/// </summary>
[WorkflowState]
public sealed record WireTransferState : IWorkflowState
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

/// <summary>
/// Entry step of the wire-transfer fixture, and the step the approval
/// checkpoint follows.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class SubmitWireTransfer(WorkflowInvocationLog log) : IWorkflowStep<WireTransferState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<WireTransferState>> ExecuteAsync(
        WireTransferState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(SubmitWireTransfer));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<WireTransferState>.FromState(updated));
    }
}

/// <summary>
/// The main-flow step after the checkpoint. The run under test injects a
/// timeout, so this must never run.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ReleaseWireTransfer(WorkflowInvocationLog log) : IWorkflowStep<WireTransferState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<WireTransferState>> ExecuteAsync(
        WireTransferState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ReleaseWireTransfer));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<WireTransferState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal of the approved route. The timeout route ends the
/// workflow on the escalation chain, so this must never run here.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RecordWireTransfer(WorkflowInvocationLog log) : IWorkflowStep<WireTransferState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<WireTransferState>> ExecuteAsync(
        WireTransferState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RecordWireTransfer));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<WireTransferState>.FromState(updated));
    }
}

/// <summary>
/// The first (and only) <c>OnTimeout</c> escalation step. Running is the
/// observable proof that the injected timeout command was handled.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EscalateToComplianceLead(WorkflowInvocationLog log) : IWorkflowStep<WireTransferState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<WireTransferState>> ExecuteAsync(
        WireTransferState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EscalateToComplianceLead));

        return Task.FromResult(StepResult<WireTransferState>.FromState(state));
    }
}

/// <summary>
/// The approver the checkpoint routes to. The generated surface is
/// <c>ComplianceOfficerApprovalTimeoutCommand</c>.
/// </summary>
public sealed class ComplianceOfficerApprover
{
}

/// <summary>
/// Parks the checkpoint: records that approval was requested and returns
/// <c>SetPending</c> with a deterministic request id so the test can inject
/// the matching timeout command. It never resumes.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ComplianceOfficerApprovalDecisionHandler(WorkflowInvocationLog log)
{
    /// <summary>
    /// The name this handler records under when the checkpoint is reached.
    /// </summary>
    public const string ApprovalRequested = "ComplianceOfficerApprovalRequested";

    private readonly WorkflowInvocationLog log = log;

    /// <summary>
    /// Builds the request id the timeout command must carry for this workflow.
    /// </summary>
    /// <param name="workflowId">The workflow identity.</param>
    /// <returns>The pending-approval request id.</returns>
    public static string TimeoutRequestIdFor(Guid workflowId) =>
        $"injected-timeout-{workflowId:N}";

    /// <summary>
    /// Records the checkpoint and sets the pending request id without deciding.
    /// </summary>
    /// <param name="requested">The generated request-approval event yielded by the saga.</param>
    /// <returns>The generated set-pending command.</returns>
    public SetComplianceOfficerPendingApprovalCommand Handle(RequestComplianceOfficerApprovalEvent requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        this.log.Record(ApprovalRequested);

        return new SetComplianceOfficerPendingApprovalCommand(
            requested.WorkflowId,
            TimeoutRequestIdFor(requested.WorkflowId));
    }
}

/// <summary>
/// The wire-transfer workflow: submit, await a compliance officer, release,
/// record — with an OnTimeout escalation that ends the workflow.
/// </summary>
[Workflow("wire-transfer-review")]
public static partial class WireTransferReviewWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition.
    /// </summary>
    public static WorkflowDefinition<WireTransferState> Definition =>
        Workflow<WireTransferState>
            .Create("wire-transfer-review")
            .StartWith<SubmitWireTransfer>()
            .AwaitApproval<ComplianceOfficerApprover>(approval => approval
                .WithContext("A compliance officer must release the wire transfer.")
                .WithOption("release", "Release", "Release the wire transfer.", isDefault: true)
                .OnTimeout(escalation => escalation
                    .Then<EscalateToComplianceLead>()
                    .Complete()))
            .Then<ReleaseWireTransfer>()
            .Finally<RecordWireTransfer>();
}
