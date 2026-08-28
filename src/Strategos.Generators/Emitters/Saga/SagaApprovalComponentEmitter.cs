// -----------------------------------------------------------------------
// <copyright file="SagaApprovalComponentEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Component emitter that generates approval resume handlers for a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This component emits handlers that process approval resume commands. For each
/// approval checkpoint in the workflow, it generates a handler that either:
/// <list type="bullet">
///   <item><description>Proceeds to the next step if approved</description></item>
///   <item><description>Transitions to Failed phase if rejected</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SagaApprovalComponentEmitter : ISagaComponentEmitter
{
    private readonly SagaApprovalHandlersEmitter _resumeHandlerEmitter = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        if (!model.HasApprovalPoints)
        {
            return;
        }

        var context = SagaEmissionContext.Create(model);

        foreach (var approval in model.ApprovalPoints!)
        {
            var resumeContext = BuildApprovalResumeContext(context, approval);

            sb.AppendLine();
            _resumeHandlerEmitter.EmitResumeHandler(sb, model, approval, resumeContext);

            sb.AppendLine();
            _resumeHandlerEmitter.EmitSetPendingHandler(sb, model, approval);

            if (approval.HasEscalation)
            {
                sb.AppendLine();
                _resumeHandlerEmitter.EmitTimeoutHandler(sb, model, approval);
            }
        }
    }

    /// <summary>
    /// Builds the approval resume context for a specific approval checkpoint.
    /// </summary>
    /// <param name="ctx">The saga emission context.</param>
    /// <param name="approval">The approval model.</param>
    /// <returns>The context for emitting the resume handler.</returns>
    private static ApprovalResumeContext BuildApprovalResumeContext(
        SagaEmissionContext ctx,
        ApprovalModel approval)
    {
        // An approved checkpoint resumes onto the next MAIN-FLOW step. Taking the entry at the
        // next index instead resumes onto whatever happens to sit there — an appended fork-path,
        // branch-case, failure-handler or handler-chain step — which bypasses that construct's
        // own dispatch handler and leaves the workflow with no way forward. The skip set is the
        // same shared classification the other successor scans consult.
        //
        // When that successor is a fork's JOIN, the handlers emitter must dispatch the fork
        // itself rather than Start{Join}: join is main-flow (by design) and fork paths are
        // not, so this scan lands on the join and the fork never starts (#182). ForkExtractor
        // walks through an intervening AwaitApproval when recording ForkModel.PreviousStepName,
        // so ForksByPreviousStep is keyed by the gated step — the same name as
        // ApprovalModel.PrecedingStepName. The join-step lookup is the one that names the
        // defect: the resume target is the join, and that is exactly when dispatch must change.
        var nextStepName = ctx.MainFlow.NextMainFlowStepNameAfter(approval.PrecedingStepName);

        return new ApprovalResumeContext(
            IsLastStep: nextStepName is null,
            NextStepName: nextStepName);
    }
}
