// -----------------------------------------------------------------------
// <copyright file="StepCompletedHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits handler methods for step completed events in a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates handlers that process StepCompleted events.
/// The behavior differs based on the step's context:
/// <list type="bullet">
///   <item><description>
///     Step with approval: Applies reducer, sets approval waiting phase, returns void
///   </description></item>
///   <item><description>
///     Non-final step: Applies reducer (if state exists) and returns StartNextStepCommand
///   </description></item>
///   <item><description>
///     Final step: Applies reducer, sets Completed phase, and calls MarkCompleted()
///   </description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class StepCompletedHandlerEmitter
{
    /// <summary>
    /// Emits a handler method for a step completed event.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="context">The handler context with step information.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public void EmitHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        HandlerContext context)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Determine event name:
        // Worker handlers generate unprefixed events (e.g., ValidateThesisStepCompleted) because they
        // are generated per step TYPE, not per phase. Fork path steps use the same handlers, so they
        // receive the same unprefixed events.
        // When stepModel is available, use its StepName directly.
        // When stepModel is null (semantic resolution failed), extract base step name from phase name.
        var stepModel = context.StepModel;
        var baseStepName = stepModel?.StepName ?? ExtractBaseStepName(stepName);
        var eventName = $"{baseStepName}Completed";

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {eventName} event - applies state change and chains to next step.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {stepName} completed event.</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);

        // Priority: Approval → Confidence gate → Terminal/Final → Non-Final
        // Terminal steps (CompleteStep, FailedStep, TerminateStep, AutoFailStep) should always
        // call MarkCompleted() regardless of their position in the workflow.
        //
        // The confidence gate (DR-5) takes precedence over the terminal/non-final split because
        // it must compare the completed event's confidence to the threshold before deciding the
        // route — either to the low-confidence handler step (a Wolverine cascade, INV-1) or down
        // the normal path. It only applies when the step declared
        // .RequireConfidence(t).OnLowConfidence(alt => alt.Then<H>()), where H was lowered into its
        // own start command by the generator.
        var confidence = context.StepModel?.Confidence;
        if (context.ApprovalAtStep is not null)
        {
            EmitApprovalWaitingHandler(sb, model, eventName, context.ApprovalAtStep);
        }
        else if (confidence?.OnLowConfidenceHandlerStep is not null)
        {
            EmitConfidenceGatedHandler(sb, model, eventName, confidence, context);
        }
        else if (context.IsTerminalStep || context.IsLastStep)
        {
            EmitFinalStepHandler(sb, model, eventName);
        }
        else
        {
            EmitNonFinalStepHandler(sb, model, eventName, context.NextStepName!);
        }
    }

    /// <summary>
    /// Emits a confidence-gated completed handler (DR-5). After applying the
    /// reducer, it compares the completed event's <c>Confidence</c> to the
    /// configured threshold: when below, it cascades the low-confidence handler
    /// step's start command (routing the saga to the OnLowConfidence branch);
    /// when at or above (or when the result carried no confidence), it proceeds
    /// down the normal path — to the next step, or to completion if this step is
    /// terminal/last.
    /// </summary>
    private static void EmitConfidenceGatedHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName,
        ConfidenceModel confidence,
        HandlerContext context)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var handlerStepName = confidence.OnLowConfidenceHandlerStep!.StepName;
        var lowConfidenceCommand = $"Start{handlerStepName}Command";
        var thresholdLiteral = confidence.Threshold.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var proceedsToCompletion = context.IsTerminalStep || context.IsLastStep;

        sb.AppendLine($"    /// <returns>The low-confidence handler start command when below the");
        sb.AppendLine($"    /// confidence threshold; otherwise the normal next-step command.</returns>");
        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        StateApplicationHelper.EmitStateApplication(sb, model);

        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            sb.AppendLine();
        }

        // Confidence gate: route to the low-confidence handler when the step's
        // result confidence is present and below the configured threshold.
        sb.AppendLine($"        if (evt.Confidence is double confidenceScore && confidenceScore < {thresholdLiteral})");
        sb.AppendLine("        {");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.{handlerStepName};");
        sb.AppendLine();
        sb.AppendLine("            logger.LogWarning(");
        sb.AppendLine("                \"Step confidence {Confidence} below threshold {Threshold} for workflow {WorkflowId}, routing to {Handler}\",");
        sb.AppendLine("                confidenceScore,");
        sb.AppendLine($"                {thresholdLiteral},");
        sb.AppendLine("                WorkflowId,");
        sb.AppendLine($"                nameof({lowConfidenceCommand}));");
        sb.AppendLine();
        sb.AppendLine($"            yield return new {lowConfidenceCommand}(WorkflowId);");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();

        if (proceedsToCompletion)
        {
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Completed;");
            sb.AppendLine();
            sb.AppendLine("        logger.LogInformation(");
            sb.AppendLine("            \"Workflow {WorkflowId} completed\",");
            sb.AppendLine("            WorkflowId);");
            sb.AppendLine();
            sb.AppendLine("        MarkCompleted();");
            sb.AppendLine("        yield break;");
        }
        else
        {
            var nextStartCommand = $"Start{context.NextStepName}Command";
            sb.AppendLine("        logger.LogDebug(");
            sb.AppendLine("            \"Step confidence acceptable, chaining to {NextStep} for workflow {WorkflowId}\",");
            sb.AppendLine($"            nameof({nextStartCommand}),");
            sb.AppendLine("            WorkflowId);");
            sb.AppendLine();
            sb.AppendLine($"        yield return new {nextStartCommand}(WorkflowId);");
        }

        sb.AppendLine("    }");
    }

    private static void EmitFinalStepHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // Final step - apply state change, then MarkCompleted
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine("    public void Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        StateApplicationHelper.EmitStateApplication(sb, model);

        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Completed;");
        sb.AppendLine();
        sb.AppendLine("        logger.LogInformation(");
        sb.AppendLine("            \"Workflow {WorkflowId} completed\",");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine("        MarkCompleted();");
        sb.AppendLine("    }");
    }

    private static void EmitNonFinalStepHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName,
        string nextStepName)
    {
        // Non-final step - apply reducer, returns StartNextStepCommand
        var nextStartCommand = $"Start{nextStepName}Command";

        // When workflow has failure handlers, we need phase-aware routing:
        // - Return type must be `object` to support polymorphic return
        // - After reducer, check if Phase == Failed and route to FailedStep
        if (model.HasFailureHandlers)
        {
            EmitPhaseAwareNonFinalStepHandler(sb, model, eventName, nextStepName, nextStartCommand);
        }
        else
        {
            EmitSimpleNonFinalStepHandler(sb, model, eventName, nextStartCommand);
        }
    }

    private static void EmitSimpleNonFinalStepHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName,
        string nextStartCommand)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // Use IEnumerable<object> pattern for explicit cascading message support
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    /// <returns>The command to start the next step.</returns>");
        sb.AppendLine($"    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        StateApplicationHelper.EmitStateApplication(sb, model);

        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            sb.AppendLine();
        }

        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine("            \"Step completed, chaining to {NextStep} for workflow {WorkflowId}\",");
        sb.AppendLine($"            nameof({nextStartCommand}),");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        yield return new {nextStartCommand}(WorkflowId);");
        sb.AppendLine("    }");
    }

    private static void EmitPhaseAwareNonFinalStepHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName,
        string nextStepName,
        string nextStartCommand)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // Get the failure step command name from the workflow's failure handlers
        var failedStepCommand = GetFailedStepCommandName(model);

        // Use IEnumerable<object> pattern for phase-aware routing
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    /// <returns>The command to start the next step ({nextStepName}) or failure handler if phase is Failed.</returns>");
        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        // Apply state change
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            StateApplicationHelper.EmitStateApplication(sb, model);

            // Sync saga Phase from state for state types that have Phase property
            // State types ending in "WorkflowState" typically don't have Phase property
            // (e.g., OrchestratorWorkflowState uses saga-level phase tracking only)
            if (!model.StateTypeName.EndsWith("WorkflowState", StringComparison.Ordinal))
            {
                sb.AppendLine($"        Phase = State.Phase;");
            }

            sb.AppendLine();
        }

        // Phase-aware routing: check if Phase is Failed after reducer application
        sb.AppendLine($"        if (Phase == {model.PhaseEnumName}.Failed)");
        sb.AppendLine("        {");
        sb.AppendLine("            logger.LogWarning(");
        sb.AppendLine("                \"Workflow {WorkflowId} entered Failed phase, routing to failure handler\",");
        sb.AppendLine("                WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"            yield return new {failedStepCommand}(WorkflowId);");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine("            \"Step completed, chaining to {NextStep} for workflow {WorkflowId}\",");
        sb.AppendLine($"            nameof({nextStartCommand}),");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        yield return new {nextStartCommand}(WorkflowId);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Gets the command name for routing to the failure handler.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <returns>The command name to start the failure handler step.</returns>
    private static string GetFailedStepCommandName(WorkflowModel model)
    {
        // Find the workflow-scoped failure handler's first step
        var workflowFailureHandler = model.FailureHandlers?
            .FirstOrDefault(fh => fh.Scope == Models.FailureHandlerScope.Workflow);

        if (workflowFailureHandler is not null)
        {
            return $"Start{workflowFailureHandler.FirstStepName}Command";
        }

        // Fallback: look for any failure handler
        var anyFailureHandler = model.FailureHandlers?.FirstOrDefault();
        if (anyFailureHandler is not null)
        {
            return $"Start{anyFailureHandler.FirstStepName}Command";
        }

        // Last resort fallback
        return "StartFailedStepCommand";
    }

    private static void EmitApprovalWaitingHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName,
        ApprovalModel approval)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var requestEventName = $"Request{approval.ApprovalPointName}ApprovalEvent";

        // Step with approval - apply state change, set approval waiting phase, yield RequestApprovalEvent
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    /// <returns>The request approval event to initiate the approval flow.</returns>");
        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        // Apply state change
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine();
        }

        sb.AppendLine($"        Phase = {model.PhaseEnumName}.{approval.PhaseName};");
        sb.AppendLine();
        sb.AppendLine("        logger.LogInformation(");
        sb.AppendLine($"            \"Requesting approval '{{ApprovalPoint}}' for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            \"{approval.ApprovalPointName}\",");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        yield return new {requestEventName}(");
        sb.AppendLine("            WorkflowId,");
        sb.AppendLine($"            \"{approval.ApprovalPointName}\",");
        sb.AppendLine("            \"Approval requested\",");
        sb.AppendLine("            TimeSpan.FromHours(4),");
        sb.AppendLine("            null);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Extracts the base step name from a phase name.
    /// </summary>
    /// <param name="phaseName">The phase name (e.g., "LoopName_StepName" or "StepName").</param>
    /// <returns>The base step name (the part after the last underscore, or the whole string if no underscore).</returns>
    /// <remarks>
    /// Phase names for loop steps follow the pattern "{LoopName}_{StepName}" (e.g., "SpecialistExecution_SelectSpecialistStep").
    /// For nested loops, the pattern is "{OuterLoop}_{InnerLoop}_{StepName}".
    /// This method extracts the step name by taking the part after the last underscore.
    /// </remarks>
    private static string ExtractBaseStepName(string phaseName)
    {
        var lastUnderscoreIndex = phaseName.LastIndexOf('_');
        return lastUnderscoreIndex >= 0
            ? phaseName.Substring(lastUnderscoreIndex + 1)
            : phaseName;
    }
}
