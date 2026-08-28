// -----------------------------------------------------------------------
// <copyright file="SagaApprovalHandlersEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits handler methods for approval resume commands in a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates handlers that process approval resume commands.
/// The behavior differs based on the approval result:
/// <list type="bullet">
///   <item><description>
///     Approved: Proceeds to the next step, or dispatches a following fork, or completes
///     if the approval is last on the main flow
///   </description></item>
///   <item><description>
///     Rejected: Transitions to the rejection chain's first step, or to Failed
///   </description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SagaApprovalHandlersEmitter
{
    /// <summary>
    /// Emits a handler method for an approval resume command.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="approval">The approval model.</param>
    /// <param name="context">The context with information about the next step.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public void EmitResumeHandler(
        StringBuilder sb,
        WorkflowModel model,
        ApprovalModel approval,
        ApprovalResumeContext context)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(approval, nameof(approval));
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var commandName = $"Resume{approval.ApprovalPointName}ApprovalCommand";

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the approval resume command for {approval.ApprovalPointName}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"cmd\">The approval resume command.</param>");

        // A join is on the main flow, so NextStepName can be the join even though the
        // construct that must run first is the fork. Dispatch the fork's paths, not
        // Start{Join} — that hangs the saga with every path still Pending (#182).
        var forkAtJoin = FindForkJoinedAt(model, context.NextStepName);
        if (forkAtJoin is not null)
        {
            EmitForkDispatchResumeHandler(sb, model, approval, commandName, forkAtJoin);
            return;
        }

        // Last on the main flow with no rejection chain: void handler, complete or fail.
        // Last on the main flow WITH a rejection chain cannot stay void — nothing else
        // publishes Start{FirstRejection}Command, so the chain never starts (#186).
        if (context.IsLastStep && !approval.HasRejection)
        {
            EmitFinalStepResumeHandler(sb, model, approval, commandName);
        }
        else
        {
            EmitNonFinalStepResumeHandler(sb, model, approval, commandName, context.NextStepName);
        }
    }

    private static void EmitFinalStepResumeHandler(
        StringBuilder sb,
        WorkflowModel model,
        ApprovalModel approval,
        string commandName)
    {
        // Final step - void return, sets Completed on approval or Failed on rejection
        sb.AppendLine("    public void Handle(");
        sb.AppendLine($"        {commandName} cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine("        PendingApprovalRequestId = null;");
        sb.AppendLine();
        sb.AppendLine("        switch (cmd.Decision)");
        sb.AppendLine("        {");
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Approved:");
        sb.AppendLine("                if (!string.IsNullOrEmpty(cmd.Instructions))");
        sb.AppendLine("                {");
        sb.AppendLine("                    ApprovalInstructions = cmd.Instructions;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Completed;");
        sb.AppendLine("                MarkCompleted();");
        sb.AppendLine("                break;");
        sb.AppendLine();
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Rejected:");
        EmitRejectionHandling(sb, model, approval, isVoidHandler: true);
        sb.AppendLine("                break;");
        sb.AppendLine();
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Deferred:");
        sb.AppendLine("                // Stay in approval phase, await another response");
        sb.AppendLine("                break;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void EmitNonFinalStepResumeHandler(
        StringBuilder sb,
        WorkflowModel model,
        ApprovalModel approval,
        string commandName,
        string? nextStepName)
    {
        // Returns nullable object to allow a next-step command, a rejection-chain
        // start command, or null when the last main-flow step is approved (or deferred).
        sb.AppendLine($"    /// <returns>The command to start the next step, or null if deferred or completed.</returns>");
        sb.AppendLine($"    public object? Handle(");
        sb.AppendLine($"        {commandName} cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine("        PendingApprovalRequestId = null;");
        sb.AppendLine();
        sb.AppendLine("        switch (cmd.Decision)");
        sb.AppendLine("        {");
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Approved:");
        sb.AppendLine("                if (!string.IsNullOrEmpty(cmd.Instructions))");
        sb.AppendLine("                {");
        sb.AppendLine("                    ApprovalInstructions = cmd.Instructions;");
        sb.AppendLine("                }");
        sb.AppendLine();
        if (string.IsNullOrEmpty(nextStepName))
        {
            sb.AppendLine($"                Phase = {model.PhaseEnumName}.Completed;");
            sb.AppendLine("                MarkCompleted();");
            sb.AppendLine("                return null;");
        }
        else
        {
            sb.AppendLine($"                return new Start{nextStepName}Command(WorkflowId);");
        }

        sb.AppendLine();
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Rejected:");
        EmitRejectionHandling(sb, model, approval, isVoidHandler: false);
        sb.AppendLine();
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Deferred:");
        sb.AppendLine("                // Stay in approval phase, await another response");
        sb.AppendLine("                return null;");
        sb.AppendLine();
        sb.AppendLine("            default:");
        sb.AppendLine("                return null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits a resume handler that dispatches every path of the fork whose join is the
    /// next main-flow step, instead of publishing <c>Start{Join}</c>.
    /// </summary>
    private static void EmitForkDispatchResumeHandler(
        StringBuilder sb,
        WorkflowModel model,
        ApprovalModel approval,
        string commandName,
        ForkModel fork)
    {
        var sanitizedId = fork.ForkId.Replace("-", "_");

        sb.AppendLine("    /// <returns>The start commands for every fork path, or null if deferred.</returns>");
        sb.AppendLine("    public IEnumerable<object>? Handle(");
        sb.AppendLine($"        {commandName} cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine("        PendingApprovalRequestId = null;");
        sb.AppendLine();
        sb.AppendLine("        switch (cmd.Decision)");
        sb.AppendLine("        {");
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Approved:");
        sb.AppendLine("                if (!string.IsNullOrEmpty(cmd.Instructions))");
        sb.AppendLine("                {");
        sb.AppendLine("                    ApprovalInstructions = cmd.Instructions;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Forking_{sanitizedId};");
        foreach (var path in fork.Paths)
        {
            sb.AppendLine($"                Fork_{sanitizedId}_Path{path.PathIndex}Status = Strategos.Definitions.ForkPathStatus.InProgress;");
        }

        sb.AppendLine();
        sb.AppendLine("                return new object[]");
        sb.AppendLine("                {");
        foreach (var path in fork.Paths)
        {
            if (path.StepNames.Count > 0)
            {
                sb.AppendLine($"                    new Start{path.StepNames[0]}Command(WorkflowId),");
            }
        }

        sb.AppendLine("                };");
        sb.AppendLine();
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Rejected:");
        EmitRejectionHandling(sb, model, approval, isVoidHandler: false, returnAsEnumerable: true);
        sb.AppendLine();
        sb.AppendLine("            case Strategos.Models.ApprovalDecision.Deferred:");
        sb.AppendLine("                // Stay in approval phase, await another response");
        sb.AppendLine("                return null;");
        sb.AppendLine();
        sb.AppendLine("            default:");
        sb.AppendLine("                return null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Finds the fork that joins at <paramref name="stepName"/>, if any.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The candidate join step name.</param>
    /// <returns>The fork, or <see langword="null"/> when the name is not a join.</returns>
    private static ForkModel? FindForkJoinedAt(WorkflowModel model, string? stepName)
    {
        if (string.IsNullOrEmpty(stepName) || !model.HasForks)
        {
            return null;
        }

        foreach (var fork in model.Forks!)
        {
            if (string.Equals(fork.JoinStepName, stepName, StringComparison.Ordinal))
            {
                return fork;
            }
        }

        return null;
    }

    /// <summary>
    /// Emits a handler method for an approval timeout command.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="approval">The approval model.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public void EmitTimeoutHandler(
        StringBuilder sb,
        WorkflowModel model,
        ApprovalModel approval)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(approval, nameof(approval));

        var commandName = $"{approval.ApprovalPointName}ApprovalTimeoutCommand";

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the timeout command for {approval.ApprovalPointName} approval.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"cmd\">The timeout command.</param>");
        sb.AppendLine($"    /// <returns>The command to start escalation, or null if approval already received.</returns>");
        sb.AppendLine($"    public object? Handle(");
        sb.AppendLine($"        {commandName} cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine();
        sb.AppendLine("        // Race condition guard: check if approval was already received");
        sb.AppendLine("        if (PendingApprovalRequestId != cmd.ApprovalRequestId)");
        sb.AppendLine("        {");
        sb.AppendLine("            return null; // Approval already received");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        PendingApprovalRequestId = null;");
        sb.AppendLine();

        // Handle escalation path
        if (approval.EscalationSteps is not null && approval.EscalationSteps.Count > 0)
        {
            // Escalation steps configured - transition to first step
            var firstStep = approval.EscalationSteps[0].StepName;
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.{firstStep};");
            sb.AppendLine($"        return new Start{firstStep}Command(WorkflowId);");
        }
        else if (approval.NestedEscalationApprovals is not null && approval.NestedEscalationApprovals.Count > 0)
        {
            // Nested approval configured - transition to escalated approval phase
            var nestedApproval = approval.NestedEscalationApprovals[0];
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.{nestedApproval.PhaseName};");
            sb.AppendLine($"        return new Request{nestedApproval.ApprovalPointName}ApprovalEvent(");
            sb.AppendLine("            WorkflowId,");
            sb.AppendLine($"            \"{nestedApproval.ApprovalPointName}\",");
            sb.AppendLine("            \"Escalated from timeout\",");
            sb.AppendLine("            TimeSpan.FromHours(4),");
            sb.AppendLine("            null);");
        }
        else if (approval.IsEscalationTerminal)
        {
            // Terminal escalation - fail workflow
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine("        MarkCompleted();");
            sb.AppendLine("        return null;");
        }
        else
        {
            // No escalation configured - fail workflow
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine("        MarkCompleted();");
            sb.AppendLine("        return null;");
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits a handler method for setting the pending approval request ID.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="approval">The approval model.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public void EmitSetPendingHandler(
        StringBuilder sb,
        WorkflowModel model,
        ApprovalModel approval)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(approval, nameof(approval));

        var commandName = $"Set{approval.ApprovalPointName}PendingApprovalCommand";

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the set pending approval command for {approval.ApprovalPointName}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"cmd\">The set pending approval command.</param>");
        sb.AppendLine("    public void Handle(");
        sb.AppendLine($"        {commandName} cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine("        PendingApprovalRequestId = cmd.ApprovalRequestId;");
        sb.AppendLine("    }");
    }

    private static void EmitRejectionHandling(
        StringBuilder sb,
        WorkflowModel model,
        ApprovalModel approval,
        bool isVoidHandler,
        bool returnAsEnumerable = false)
    {
        // Check if approval has rejection steps
        if (approval.HasRejection)
        {
            var firstRejectionStep = approval.RejectionSteps![0].StepName;
            sb.AppendLine($"                Phase = {model.PhaseEnumName}.{firstRejectionStep};");
            if (returnAsEnumerable)
            {
                sb.AppendLine($"                return new object[] {{ new Start{firstRejectionStep}Command(WorkflowId) }};");
            }
            else
            {
                sb.AppendLine($"                return new Start{firstRejectionStep}Command(WorkflowId);");
            }
        }
        else
        {
            // No rejection steps - go directly to Failed
            sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine("                MarkCompleted();");
            if (!isVoidHandler)
            {
                sb.AppendLine("                return null;");
            }
        }
    }
}
