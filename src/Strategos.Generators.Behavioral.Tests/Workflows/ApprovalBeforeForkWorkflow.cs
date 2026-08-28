// -----------------------------------------------------------------------
// <copyright file="ApprovalBeforeForkWorkflow.cs" company="Levelup Software">
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
// Then<A> → AwaitApproval → Fork → Join → Finally.
//
// The approval resume's next main-flow step is the JOIN (fork paths are
// off-main-flow). Resuming onto Start{Join} never dispatches the paths, so
// join-readiness stays false and the saga hangs (#182).
// =============================================================================

/// <summary>
/// Immutable state for the approval-before-fork loan-origination fixture.
/// </summary>
[WorkflowState]
public sealed record LoanOriginationState : IWorkflowState
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
/// The gated step: the approval checkpoint follows it, and the fork follows
/// the checkpoint.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ReceiveLoanApplication(WorkflowInvocationLog log) : IWorkflowStep<LoanOriginationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoanOriginationState>> ExecuteAsync(
        LoanOriginationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ReceiveLoanApplication));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoanOriginationState>.FromState(updated));
    }
}

/// <summary>
/// First fork path. Runs only if the approval resume dispatched the fork.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ScoreCredit(WorkflowInvocationLog log) : IWorkflowStep<LoanOriginationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoanOriginationState>> ExecuteAsync(
        LoanOriginationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ScoreCredit));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoanOriginationState>.FromState(updated));
    }
}

/// <summary>
/// Second fork path. A resume that starts only one path hangs the join.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class VerifyIncome(WorkflowInvocationLog log) : IWorkflowStep<LoanOriginationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoanOriginationState>> ExecuteAsync(
        LoanOriginationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(VerifyIncome));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoanOriginationState>.FromState(updated));
    }
}

/// <summary>
/// The join. Running this without both paths having finished is the #182 hang
/// if the resume targeted the join directly — the join handler waits on path
/// status that was never set to InProgress, and the document is never deleted.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class MergeAssessment(WorkflowInvocationLog log) : IWorkflowStep<LoanOriginationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoanOriginationState>> ExecuteAsync(
        LoanOriginationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(MergeAssessment));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoanOriginationState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal. It runs only after the join completes.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class IssueLoan(WorkflowInvocationLog log) : IWorkflowStep<LoanOriginationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<LoanOriginationState>> ExecuteAsync(
        LoanOriginationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(IssueLoan));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<LoanOriginationState>.FromState(updated));
    }
}

/// <summary>
/// The approver the checkpoint routes to. The generated surface is
/// <c>ResumeUnderwriterApprovalCommand</c>.
/// </summary>
public sealed class UnderwriterApprover
{
}

/// <summary>
/// Stands in for the underwriter, deterministically releasing the application
/// so the approved path is the one that must dispatch the fork.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class UnderwriterApprovalDecisionHandler(WorkflowInvocationLog log)
{
    /// <summary>
    /// The name this handler records under when the checkpoint is reached.
    /// </summary>
    public const string ApprovalRequested = "UnderwriterApprovalRequested";

    private readonly WorkflowInvocationLog log = log;

    /// <summary>
    /// Releases the application as soon as the saga asks for a decision.
    /// </summary>
    /// <param name="requested">The generated request-approval event yielded by the saga.</param>
    /// <returns>The generated resume command carrying the approved decision.</returns>
    public ResumeUnderwriterApprovalCommand Handle(RequestUnderwriterApprovalEvent requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        this.log.Record(ApprovalRequested);

        return new ResumeUnderwriterApprovalCommand(
            requested.WorkflowId,
            ApprovalDecision.Approved,
            "release",
            null);
    }
}

/// <summary>
/// The loan-origination workflow: receive the application, await an underwriter,
/// score and verify in parallel, merge, issue the loan.
/// </summary>
[Workflow("loan-origination")]
public static partial class LoanOriginationWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition.
    /// </summary>
    public static WorkflowDefinition<LoanOriginationState> Definition =>
        Workflow<LoanOriginationState>
            .Create("loan-origination")
            .StartWith<ReceiveLoanApplication>()
            .AwaitApproval<UnderwriterApprover>(approval => approval
                .WithContext("An underwriter must release the application before scoring.")
                .WithOption("release", "Release", "Release the application to scoring.", isDefault: true)
                .WithOption("hold", "Hold", "Hold the application."))
            .Fork(
                path => path.Then<ScoreCredit>(),
                path => path.Then<VerifyIncome>())
            .Join<MergeAssessment>()
            .Finally<IssueLoan>();
}
