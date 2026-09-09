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

        // Completed event: unique-type and linear steps stay {StepType}Completed.
        // Shared-type fork instances bind ForkPathCompletedNaming.StemFor so colliding
        // unnamed paths get {PathId}_{PhaseName}Completed rather than CS0111.
        var stepModel = context.StepModel;
        var baseStepName = stepModel?.StepName ?? ExtractBaseStepName(stepName);
        var eventName = PathEndTypeCollisionFinder.CompletedEventName(
            model, stepName, baseStepName, context.IsForkPathStep, context.ForkPathKey);
        CompensationOccurrence? compensationOccurrence = null;
        if (CompensationTopology.UsesDerivedRuntime(model))
        {
            var topology = CompensationTopology.Build(model);
            _ = topology.TryResolve(stepName, context.ForkPathKey, out compensationOccurrence!);
        }

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
            EmitApprovalWaitingHandler(
                sb,
                model,
                stepName,
                eventName,
                context.ApprovalAtStep,
                compensationOccurrence);
        }
        else if (confidence?.OnLowConfidenceHandlerStep is not null)
        {
            EmitConfidenceGatedHandler(
                sb,
                model,
                stepName,
                eventName,
                confidence,
                context,
                compensationOccurrence);
        }
        else if (context.IsTerminalStep || context.IsLastStep)
        {
            EmitFinalStepHandler(sb, model, stepName, eventName, compensationOccurrence);
        }
        else
        {
            EmitNonFinalStepHandler(
                sb,
                model,
                stepName,
                eventName,
                context.NextStepName!,
                compensationOccurrence);
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
        string stepName,
        string eventName,
        ConfidenceModel confidence,
        HandlerContext context,
        CompensationOccurrence? compensationOccurrence)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var handlerStepName = confidence.OnLowConfidenceHandlerStep!.StepName;
        var lowConfidenceCommand = $"Start{handlerStepName}Command";
        var thresholdLiteral = confidence.Threshold.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var proceedsToCompletion = context.IsTerminalStep || context.IsLastStep;

        // #138 G-5: the gated step's own name (the step whose low-confidence result
        // drove the route) for the LowConfidenceRouted audit event. Derive it from
        // the completed event name (strip the trailing "Completed") so it is robust
        // even when StepModel is unavailable.
        var gatedStepName = eventName.EndsWith("Completed", System.StringComparison.Ordinal)
            ? eventName.Substring(0, eventName.Length - "Completed".Length)
            : eventName;

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

        CompensationJournalEmitter.EmitForwardCompletionGuard(
            sb,
            compensationOccurrence,
            "yield break;");

        StateApplicationHelper.EmitStateApplication(sb, model);

        if (compensationOccurrence is not null)
        {
            CompensationJournalEmitter.EmitRecordCompletion(sb, compensationOccurrence);
        }

        CompensationJournalEmitter.EmitPendingForkQuiescenceGuard(
            sb,
            model,
            compensationOccurrence);

        // Failure-phase sync + route (F1): a confidence-gated step can ALSO drive
        // the saga into the Failed phase via its reducer/state application. The
        // confidence comparison must not bypass failure handling, so an OnFailure
        // chain or typed derived compensation activates the same phase-aware route.
        // Sync Phase from reduced state and emit the Phase == Failed guard BEFORE
        // the confidence comparison. The semantic state-property inspection is the
        // authority; type-name suffixes do not decide whether a Phase member exists.
        EmitReducedPhaseSync(sb, model);

        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            sb.AppendLine();
        }

        if (NeedsReducedFailureRouting(model))
        {
            sb.AppendLine($"        if (Phase == {model.PhaseEnumName}.Failed)");
            sb.AppendLine("        {");
            EmitPostCompletionFailureRoute(
                sb,
                model,
                compensationOccurrence,
                stepName);

            sb.AppendLine("        }");
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

        // #138 G-5: append the LowConfidenceRouted audit STREAM event when
        // event-sourced. This is the single site where a confidence-gated step
        // actually routes below-threshold, so it is where the named event belongs.
        // The handler already receives IDocumentSession session in EventSourced mode.
        if (model.IsEventSourced)
        {
            sb.AppendLine();
            sb.AppendLine($"            session.Events.Append(");
            sb.AppendLine("                WorkflowId,");
            sb.AppendLine($"                new {model.PascalName}LowConfidenceRouted(");
            sb.AppendLine("                    WorkflowId,");
            sb.AppendLine($"                    \"{gatedStepName}\",");
            sb.AppendLine("                    confidenceScore,");
            sb.AppendLine($"                    {thresholdLiteral},");
            sb.AppendLine("                    DateTimeOffset.UtcNow));");
        }

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
        string stepName,
        string eventName,
        CompensationOccurrence? compensationOccurrence)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var needsReducedFailureRouting = NeedsReducedFailureRouting(model);

        // Final step - apply state change, then MarkCompleted. Failure-aware
        // programs use an iterator because a reducer-driven Failed state must first
        // enter OnFailure or emit its authenticated rollback trigger instead of
        // completing the saga.
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine(needsReducedFailureRouting
            ? "    public IEnumerable<object> Handle("
            : "    public void Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        CompensationJournalEmitter.EmitForwardCompletionGuard(
            sb,
            compensationOccurrence,
            needsReducedFailureRouting ? "yield break;" : "return;");

        StateApplicationHelper.EmitStateApplication(sb, model);

        if (compensationOccurrence is not null)
        {
            CompensationJournalEmitter.EmitRecordCompletion(sb, compensationOccurrence);
        }

        CompensationJournalEmitter.EmitPendingForkQuiescenceGuard(
            sb,
            model,
            compensationOccurrence);

        if (needsReducedFailureRouting)
        {
            EmitReducedPhaseSync(sb, model);
            EmitReducedFailureGuard(sb, model, compensationOccurrence, stepName);
        }

        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Completed;");
        sb.AppendLine();
        sb.AppendLine("        logger.LogInformation(");
        sb.AppendLine("            \"Workflow {WorkflowId} completed\",");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine("        MarkCompleted();");
        if (needsReducedFailureRouting)
        {
            sb.AppendLine("        yield break;");
        }

        sb.AppendLine("    }");
    }

    private static void EmitNonFinalStepHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        string eventName,
        string nextStepName,
        CompensationOccurrence? compensationOccurrence)
    {
        // Non-final step - apply reducer, returns StartNextStepCommand
        var nextStartCommand = $"Start{nextStepName}Command";

        // OnFailure and typed derived compensation both need phase-aware routing:
        // - Return type must be `IEnumerable<object>` to support polymorphic return
        // - After reducer, check if Phase == Failed and emit the unified failure trigger
        if (NeedsReducedFailureRouting(model))
        {
            EmitPhaseAwareNonFinalStepHandler(
                sb,
                model,
                eventName,
                stepName,
                nextStepName,
                nextStartCommand,
                compensationOccurrence);
        }
        else
        {
            EmitSimpleNonFinalStepHandler(
                sb,
                model,
                eventName,
                nextStartCommand,
                compensationOccurrence);
        }
    }

    private static void EmitSimpleNonFinalStepHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName,
        string nextStartCommand,
        CompensationOccurrence? compensationOccurrence)
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

        CompensationJournalEmitter.EmitForwardCompletionGuard(
            sb,
            compensationOccurrence,
            "yield break;");

        StateApplicationHelper.EmitStateApplication(sb, model);

        if (compensationOccurrence is not null)
        {
            CompensationJournalEmitter.EmitRecordCompletion(sb, compensationOccurrence);
        }

        CompensationJournalEmitter.EmitPendingForkQuiescenceGuard(
            sb,
            model,
            compensationOccurrence);

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
        string stepName,
        string nextStepName,
        string nextStartCommand,
        CompensationOccurrence? compensationOccurrence)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

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

        CompensationJournalEmitter.EmitForwardCompletionGuard(
            sb,
            compensationOccurrence,
            "yield break;");

        // Apply state change
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            StateApplicationHelper.EmitStateApplication(sb, model);

            if (compensationOccurrence is not null)
            {
                CompensationJournalEmitter.EmitRecordCompletion(sb, compensationOccurrence);
            }

            CompensationJournalEmitter.EmitPendingForkQuiescenceGuard(
                sb,
                model,
                compensationOccurrence);

            // Sync saga Phase from state ONLY for state types that actually expose a
            // Phase property (mechanically detected via StateHasPhaseProperty). State
            // types tracking phase at the saga level only have no Phase member, so
            // emitting State.Phase would not compile.
            EmitReducedPhaseSync(sb, model);

            sb.AppendLine();
        }

        EmitReducedFailureGuard(sb, model, compensationOccurrence, stepName);
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine("            \"Step completed, chaining to {NextStep} for workflow {WorkflowId}\",");
        sb.AppendLine($"            nameof({nextStartCommand}),");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        yield return new {nextStartCommand}(WorkflowId);");
        sb.AppendLine("    }");
    }

    private static bool NeedsReducedFailureRouting(WorkflowModel model) =>
        model.HasFailureHandlers || CompensationTopology.UsesDerivedRuntime(model);

    private static void EmitReducedPhaseSync(StringBuilder sb, WorkflowModel model)
    {
        if (model.StateHasPhaseProperty
            && !string.IsNullOrEmpty(model.StateTypeName))
        {
            sb.AppendLine($"        Phase = State.Phase;");
        }
    }

    private static void EmitReducedFailureGuard(
        StringBuilder sb,
        WorkflowModel model,
        CompensationOccurrence? compensationOccurrence,
        string failedStepName)
    {
        sb.AppendLine($"        if (Phase == {model.PhaseEnumName}.Failed)");
        sb.AppendLine("        {");
        EmitPostCompletionFailureRoute(
            sb,
            model,
            compensationOccurrence,
            failedStepName);

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the unified reducer-failure route after a forward occurrence has
    /// completed. Typed derived compensation carries the saga-minted occurrence
    /// claim into the trigger; legacy <c>OnFailure</c> retains the trigger's
    /// ordinary failure audit metadata.
    /// </summary>
    internal static void EmitPostCompletionFailureRoute(
        StringBuilder sb,
        WorkflowModel model,
        CompensationOccurrence? compensationOccurrence,
        string failedStepName,
        string indent = "            ")
    {
        if (!CompensationJournalEmitter.EmitFailureAfterForwardCompletion(
                sb,
                model,
                compensationOccurrence,
                indent))
        {
            EmitLegacyFailureTrigger(sb, model, failedStepName, indent);
        }
    }

    private static void EmitLegacyFailureTrigger(
        StringBuilder sb,
        WorkflowModel model,
        string failedStepName,
        string indent = "            ")
    {
        var triggerCommand = $"Trigger{model.PascalName}FailureHandlerCommand";
        sb.AppendLine($"{indent}logger.LogWarning(");
        sb.AppendLine($"{indent}    \"Workflow {{WorkflowId}} entered Failed phase, routing to failure handler\",");
        sb.AppendLine($"{indent}    WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"{indent}yield return new {triggerCommand}(");
        sb.AppendLine($"{indent}    WorkflowId,");
        sb.AppendLine($"{indent}    \"{failedStepName}\",");
        sb.AppendLine($"{indent}    \"Workflow state entered Failed after the forward occurrence completed.\",");
        sb.AppendLine($"{indent}    \"StateTransitionFailure\",");
        sb.AppendLine($"{indent}    null);");
        sb.AppendLine($"{indent}yield break;");
    }

    private static void EmitApprovalWaitingHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        string eventName,
        ApprovalModel approval,
        CompensationOccurrence? compensationOccurrence)
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

        CompensationJournalEmitter.EmitForwardCompletionGuard(
            sb,
            compensationOccurrence,
            "yield break;");

        // Apply state change
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine();
        }

        if (compensationOccurrence is not null)
        {
            CompensationJournalEmitter.EmitRecordCompletion(sb, compensationOccurrence);
        }

        CompensationJournalEmitter.EmitPendingForkQuiescenceGuard(
            sb,
            model,
            compensationOccurrence);

        if (NeedsReducedFailureRouting(model))
        {
            EmitReducedPhaseSync(sb, model);
            EmitReducedFailureGuard(sb, model, compensationOccurrence, stepName);
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
