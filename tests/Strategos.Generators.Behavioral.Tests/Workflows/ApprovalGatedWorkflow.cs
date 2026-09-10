// -----------------------------------------------------------------------
// <copyright file="ApprovalGatedWorkflow.cs" company="Levelup Software">
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
// The C#-authored approval checkpoint's real-host fixture.
//
// AwaitApproval had no behavioral coverage at all. The JSON-import approval family
// is a COMPILE + DI-registration proof: it asserts that importing an approval emits
// a saga that builds and resolves, and it never starts one. Nothing in the repository
// had ever run an approval checkpoint on a Wolverine + Marten host.
//
// That gap matters twice over for the same appending mechanism the fork and failure
// constructs hit. An approval's OnRejection and OnTimeout steps are lowered as extra
// entries appended to the workflow's step-name list AFTER the declared terminal, so:
//
//   1. the terminal is no longer the last entry, and a positional successor scan
//      cascades it into the rejection step rather than completing the saga; and
//   2. the approval RESUME scan is a third, separate successor scan, and it used to
//      index the step list raw with no filter at all — resuming onto whatever entry
//      happened to sit at the next index, path step or not.
//
// The approved path is the one that reaches the terminal, so it is the one that can
// observe either defect. Rejection and escalation must both stay at zero invocations.
//
// Nothing schedules the approval timeout on this host, so the escalation branch is
// unreachable by construction here; its step is declared so the appending source is
// present in the step list, which is the mechanism under test.
// =============================================================================

/// <summary>
/// Immutable state for the approval-gated credit-limit review fixture.
/// </summary>
[WorkflowState]
public sealed record CreditLimitReviewState : IWorkflowState
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
    /// Gets a value indicating whether the applicant was told the request was declined.
    /// </summary>
    public bool ApplicantDeclined { get; init; }
}

/// <summary>
/// Entry step of the approval-gated fixture. It is the step the approval checkpoint
/// follows, so its completed handler is the one that requests approval instead of
/// chaining to a successor.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class AssessCreditRisk(WorkflowInvocationLog log) : IWorkflowStep<CreditLimitReviewState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CreditLimitReviewState>> ExecuteAsync(
        CreditLimitReviewState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(AssessCreditRisk));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CreditLimitReviewState>.FromState(updated));
    }
}

/// <summary>
/// The first main-flow step AFTER the approval checkpoint, and therefore the step the
/// approval resume must target. Its running is the observable proof that the resume
/// landed on the main flow rather than on an appended rejection or escalation step.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class IssueCreditLine(WorkflowInvocationLog log) : IWorkflowStep<CreditLimitReviewState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CreditLimitReviewState>> ExecuteAsync(
        CreditLimitReviewState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(IssueCreditLine));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CreditLimitReviewState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal step. It must run exactly once and complete the saga, with
/// nothing of this workflow's running after it.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RecordCreditDecision(WorkflowInvocationLog log) : IWorkflowStep<CreditLimitReviewState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CreditLimitReviewState>> ExecuteAsync(
        CreditLimitReviewState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RecordCreditDecision));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CreditLimitReviewState>.FromState(updated));
    }
}

/// <summary>
/// The approval's <c>OnRejection</c> step. The run under test is approved, so this must
/// never execute — it is one of the two entries appended after the terminal, and it
/// running is the terminal (or the resume) landing on an off-main-flow step.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class NotifyApplicantDeclined(WorkflowInvocationLog log) : IWorkflowStep<CreditLimitReviewState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CreditLimitReviewState>> ExecuteAsync(
        CreditLimitReviewState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(NotifyApplicantDeclined));

        var updated = state with { ApplicantDeclined = true };
        return Task.FromResult(StepResult<CreditLimitReviewState>.FromState(updated));
    }
}

/// <summary>
/// The approval's <c>OnTimeout</c> escalation step, and the second entry appended after
/// the terminal. Nothing schedules the timeout on this host, so it must never execute.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EscalateToCreditCommittee(WorkflowInvocationLog log) : IWorkflowStep<CreditLimitReviewState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CreditLimitReviewState>> ExecuteAsync(
        CreditLimitReviewState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EscalateToCreditCommittee));

        return Task.FromResult(StepResult<CreditLimitReviewState>.FromState(state));
    }
}

/// <summary>
/// The approver the checkpoint routes to. The approval point's generated identifier is
/// derived from this type's simple name with the <c>Approver</c> suffix stripped, so the
/// generated surface is <c>ResumeCreditOfficerApprovalCommand</c> and
/// <c>RequestCreditOfficerApprovalEvent</c>.
/// </summary>
public sealed class CreditOfficerApprover
{
}

/// <summary>
/// Stands in for the human at the approval checkpoint, deterministically approving.
/// </summary>
/// <remarks>
/// <para>
/// The saga yields a request-approval event and then waits: on a real deployment an
/// integration handler brokers that to a person and eventually publishes the resume
/// command. This handler is that broker, reduced to an immediate approval, so the
/// approved path is reachable without a human and without a timer.
/// </para>
/// <para>
/// It records its own invocation, which is what lets a test tell "the checkpoint was
/// reached and approved" apart from "the approval was skipped entirely" — two very
/// different runs that produce the same set of executed steps.
/// </para>
/// <para>
/// It is registered by explicit type on the host rather than by naming convention, so
/// nothing else in the assembly is pulled into handler discovery alongside it.
/// </para>
/// </remarks>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CreditOfficerApprovalDecisionHandler(WorkflowInvocationLog log)
{
    /// <summary>
    /// The name this handler records under when the checkpoint is reached.
    /// </summary>
    public const string ApprovalRequested = "CreditOfficerApprovalRequested";

    private readonly WorkflowInvocationLog log = log;

    /// <summary>
    /// Approves the request as soon as the saga asks for a decision.
    /// </summary>
    /// <param name="requested">The generated request-approval event yielded by the saga.</param>
    /// <returns>The generated resume command carrying the approved decision.</returns>
    public ResumeCreditOfficerApprovalCommand Handle(RequestCreditOfficerApprovalEvent requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        this.log.Record(ApprovalRequested);

        return new ResumeCreditOfficerApprovalCommand(
            requested.WorkflowId,
            ApprovalDecision.Approved,
            "approve",
            null);
    }
}

/// <summary>
/// The approval-gated credit-limit review workflow definition.
/// </summary>
/// <remarks>
/// The checkpoint sits between the entry step and the rest of the main flow, and
/// declares both an <c>OnRejection</c> chain and an <c>OnTimeout</c> escalation — the
/// two blocks that append step names after the declared terminal.
/// </remarks>
[Workflow("credit-limit-review")]
public static partial class CreditLimitReviewWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: assess, await a credit officer's decision,
    /// issue the line, record the decision.
    /// </summary>
    public static WorkflowDefinition<CreditLimitReviewState> Definition => Workflow<CreditLimitReviewState>
        .Create("credit-limit-review")
        .StartWith<AssessCreditRisk>()
        .AwaitApproval<CreditOfficerApprover>(approval => approval
            .WithContext("A credit limit increase requires a credit officer's decision.")
            .WithOption("approve", "Approve", "Grant the requested credit limit.", isDefault: true)
            .WithOption("decline", "Decline", "Refuse the requested credit limit.")
            .OnRejection(rejection => rejection
                .Then<NotifyApplicantDeclined>()
                .Complete())
            .OnTimeout(escalation => escalation
                .Then<EscalateToCreditCommittee>()
                .Complete()))
        .Then<IssueCreditLine>()
        .Finally<RecordCreditDecision>();
}
