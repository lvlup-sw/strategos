// -----------------------------------------------------------------------
// <copyright file="TerminalRejectionWorkflow.cs" company="Levelup Software">
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
// An approval that is LAST on the main flow, rejected, with a two-step
// OnRejection chain. Every existing rejection fixture puts the checkpoint
// mid-flow, which takes the object? resume overload that already dispatches.
// The void last-on-flow overload only mutated phase and never published
// Start{FirstRejection}Command, so the chain never started (#186).
// =============================================================================

/// <summary>
/// Immutable state for the last-on-flow expense-report rejection fixture.
/// </summary>
[WorkflowState]
public sealed record ExpenseReportState : IWorkflowState
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
    /// Gets a value indicating whether the submitter was told the report was refused.
    /// </summary>
    public bool SubmitterNotified { get; init; }
}

/// <summary>
/// Entry step of the last-on-flow expense-report fixture.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class SubmitExpenseReport(WorkflowInvocationLog log) : IWorkflowStep<ExpenseReportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ExpenseReportState>> ExecuteAsync(
        ExpenseReportState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(SubmitExpenseReport));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ExpenseReportState>.FromState(updated));
    }
}

/// <summary>
/// The last main-flow step before the checkpoint. Nothing follows it on the
/// approved route except the approval itself.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class AttachReceipts(WorkflowInvocationLog log) : IWorkflowStep<ExpenseReportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ExpenseReportState>> ExecuteAsync(
        ExpenseReportState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(AttachReceipts));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ExpenseReportState>.FromState(updated));
    }
}

/// <summary>
/// The FIRST step of the two-step rejection chain — the only one the approval
/// component dispatches directly.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RecordExpenseRefusal(WorkflowInvocationLog log) : IWorkflowStep<ExpenseReportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ExpenseReportState>> ExecuteAsync(
        ExpenseReportState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RecordExpenseRefusal));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ExpenseReportState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal of the approved route. AGWF010 requires a
/// <c>Finally</c>; the rejected route declares its own completion, so this
/// must never run here.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ArchiveExpenseReport(WorkflowInvocationLog log) : IWorkflowStep<ExpenseReportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ExpenseReportState>> ExecuteAsync(
        ExpenseReportState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ArchiveExpenseReport));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ExpenseReportState>.FromState(updated));
    }
}

/// <summary>
/// The SECOND step of the rejection chain. It runs only if the chain advances
/// rather than parking after the first step is named in Phase.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class NotifyExpenseSubmitter(WorkflowInvocationLog log) : IWorkflowStep<ExpenseReportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ExpenseReportState>> ExecuteAsync(
        ExpenseReportState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(NotifyExpenseSubmitter));

        var updated = state with
        {
            StepCount = state.StepCount + 1,
            SubmitterNotified = true,
        };

        return Task.FromResult(StepResult<ExpenseReportState>.FromState(updated));
    }
}

/// <summary>
/// The approver the checkpoint routes to. The generated surface is
/// <c>ResumeFinanceControllerApprovalCommand</c>.
/// </summary>
public sealed class FinanceControllerApprover
{
}

/// <summary>
/// Stands in for the finance controller, deterministically refusing so the
/// last-on-flow rejection chain is the route under test.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FinanceControllerApprovalDecisionHandler(WorkflowInvocationLog log)
{
    /// <summary>
    /// The name this handler records under when the checkpoint is reached.
    /// </summary>
    public const string ApprovalRequested = "FinanceControllerApprovalRequested";

    private readonly WorkflowInvocationLog log = log;

    /// <summary>
    /// Refuses the expense report as soon as the saga asks for a decision.
    /// </summary>
    /// <param name="requested">The generated request-approval event yielded by the saga.</param>
    /// <returns>The generated resume command carrying the rejected decision.</returns>
    public ResumeFinanceControllerApprovalCommand Handle(RequestFinanceControllerApprovalEvent requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        this.log.Record(ApprovalRequested);

        return new ResumeFinanceControllerApprovalCommand(
            requested.WorkflowId,
            ApprovalDecision.Rejected,
            "refuse",
            null);
    }
}

/// <summary>
/// The expense-report workflow: submit, attach receipts, await a finance
/// controller — and stop. A two-step rejection chain ends the workflow.
/// </summary>
[Workflow("expense-report-review")]
public static partial class ExpenseReportReviewWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition.
    /// </summary>
    public static WorkflowDefinition<ExpenseReportState> Definition =>
        Workflow<ExpenseReportState>
            .Create("expense-report-review")
            .StartWith<SubmitExpenseReport>()
            .Then<AttachReceipts>()
            .AwaitApproval<FinanceControllerApprover>(approval => approval
                .WithContext("A finance controller must accept the expense report.")
                .WithOption("accept", "Accept", "Accept the expense report.", isDefault: true)
                .WithOption("refuse", "Refuse", "Refuse the expense report.")
                .OnRejection(rejection => rejection
                    .Then<RecordExpenseRefusal>()
                    .Then<NotifyExpenseSubmitter>()
                    .Complete()))
            .Finally<ArchiveExpenseReport>();
}
