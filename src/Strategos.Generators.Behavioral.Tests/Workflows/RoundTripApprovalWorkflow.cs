// -----------------------------------------------------------------------
// <copyright file="RoundTripApprovalWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

// =============================================================================
// FIX-5 (M10, DR-14 bucket a) — the CONTEXT-FREE approval importable family's
// bucket-(a) COMPILE proof. A context-free approval is importable per DR-12/DR-14,
// but importing one used to emit UNCOMPILABLE saga source: the wire
// `approvalPointId` is a GUID identity (Guid.NewGuid().ToString("N")), and
// WireToModelBridge.MapApprovals fed that raw GUID to ApprovalModel.Create as the
// approval-point NAME — a digit-leading GUID is not a valid C# identifier, so the
// generator threw (CS8785) and no saga was emitted. The fix DERIVES a valid
// identifier from the approver type name (shared ApprovalPointNaming.Derive), so
// this JSON import (`roundtrip-approval.workflow.json`, whose approvalPointId is a
// FIXED digit-leading GUID) is bridged + lowered through the SAME saga emitters at
// build time into RoundtripApprovalImportSaga + AddRoundtripApprovalImportWorkflow().
//
// The Behavioral.Tests BUILD compiling that generated saga is the REAL semantic
// check (parse-only trees miss compile errors — the whole point of the fix). The
// generated approval saga references Strategos.Models.ApprovalDecision (the resume
// command's discriminant), so compiling it also exercises that runtime type. The
// Docker-free RoundTripApprovalImportCompileTests references the generated surface
// (the saga, the start command, and the DERIVED-name resume command) so a revert of
// the name derivation fails THIS project's build.
//
// The `approverType` moniker resolves through the SAME step resolver as any step
// (the established import contract — see ImportFrontEndRobustnessTests M2), so the
// approver is declared below as an IWorkflowStep. Its simple name
// (RtApprovalReviewerApprover) derives the approval-point name (RtApprovalReviewer).
// =============================================================================

/// <summary>State for the context-free approval JSON import compile proof.</summary>
[WorkflowState]
public sealed record RoundTripApprovalState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the number of steps that folded their result into state.</summary>
    public int StepCount { get; init; }
}

/// <summary>Entry step of the context-free approval JSON import (precedes the approval checkpoint).</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtApprovalImportStart(WorkflowInvocationLog log) : IWorkflowStep<RoundTripApprovalState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripApprovalState>> ExecuteAsync(RoundTripApprovalState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtApprovalImportStart));
        return Task.FromResult(StepResult<RoundTripApprovalState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the context-free approval JSON import (runs once the approval resolves).</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtApprovalImportEnd(WorkflowInvocationLog log) : IWorkflowStep<RoundTripApprovalState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripApprovalState>> ExecuteAsync(RoundTripApprovalState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtApprovalImportEnd));
        return Task.FromResult(StepResult<RoundTripApprovalState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// The approver referenced by the import's <c>approverType</c> moniker. Approver monikers resolve
/// through the same step resolver as any step (the established import contract), so it is declared as
/// an <see cref="IWorkflowStep{TState}"/>. It is never invoked on the happy path; its simple name is
/// what the shared derivation turns into the approval-point name (<c>RtApprovalReviewer</c>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtApprovalReviewerApprover(WorkflowInvocationLog log) : IWorkflowStep<RoundTripApprovalState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripApprovalState>> ExecuteAsync(RoundTripApprovalState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtApprovalReviewerApprover));
        return Task.FromResult(StepResult<RoundTripApprovalState>.FromState(state));
    }
}
