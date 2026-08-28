// -----------------------------------------------------------------------
// <copyright file="BranchHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Linq;
using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits handler methods for branch routing and path completion in a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates two types of handlers for branching workflows:
/// <list type="bullet">
///   <item><description>
///     Routing handler - Uses a switch expression on the discriminator property
///     to route to the appropriate branch path
///   </description></item>
///   <item><description>
///     Path end handler - Handles completion of a branch path, either rejoining
///     the main workflow or completing the workflow entirely
///   </description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class BranchHandlerEmitter
{
    /// <summary>
    /// Emits a routing handler that dispatches to branch paths based on a discriminator.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the step before the branch.</param>
    /// <param name="branch">The branch model.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public void EmitRoutingHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        BranchModel branch)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(branch, nameof(branch));

        // Use unprefixed step type name for completed event (workers return per-type events)
        var baseStepName = ExtractBaseStepName(stepName);
        var eventName = $"{baseStepName}Completed";
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // Method discriminators are called with State as argument; property discriminators are accessed on State
        var discriminatorAccess = branch.IsMethodDiscriminator
            ? $"{branch.DiscriminatorPropertyPath}(State)"
            : $"State.{branch.DiscriminatorPropertyPath}";

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {eventName} event - routes to appropriate branch path.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {stepName} completed event.</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);
        sb.AppendLine("    /// <param name=\"logger\">The logger for diagnostic output.</param>");
        sb.AppendLine("    /// <returns>The start command for the selected branch path.</returns>");

        // Return type is object since we can return different command types
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    public object Handle(");
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

        // Log branch routing decision
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Branch routing for workflow {{WorkflowId}}, discriminator: {{Discriminator}}\",");
        sb.AppendLine($"            WorkflowId,");
        sb.AppendLine($"            {discriminatorAccess});");
        sb.AppendLine();

        // Emit switch/case based on discriminator
        sb.AppendLine($"        // Branch routing based on {branch.DiscriminatorPropertyPath}");
        sb.Append("        return ");
        EmitSwitchExpression(sb, branch, "        ");
        sb.AppendLine(";");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits a switch expression for branch routing, with support for nested switches
    /// when consecutive branches exist.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="branch">The branch model to emit.</param>
    /// <param name="baseIndent">The base indentation for the switch expression.</param>
    /// <remarks>
    /// <para>
    /// This method recursively handles consecutive branches by nesting switch expressions.
    /// For example, if Branch1 has NextConsecutiveBranch = Branch2, which has NextConsecutiveBranch = Branch3,
    /// the generated code will be:
    /// <code>
    /// State.Cond1() switch
    /// {
    ///     true => new StartStep1Command(WorkflowId),
    ///     _ => State.Cond2() switch
    ///     {
    ///         true => new StartStep2Command(WorkflowId),
    ///         _ => State.Cond3() switch
    ///         {
    ///             true => new StartStep3Command(WorkflowId),
    ///             _ => new StartRejoinStepCommand(WorkflowId)
    ///         }
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    private static void EmitSwitchExpression(StringBuilder sb, BranchModel branch, string baseIndent)
    {
        // Method discriminators are called with State as argument; property discriminators are accessed on State
        var discriminatorAccess = branch.IsMethodDiscriminator
            ? $"{branch.DiscriminatorPropertyPath}(State)"
            : $"State.{branch.DiscriminatorPropertyPath}";

        sb.AppendLine($"{discriminatorAccess} switch");
        sb.AppendLine($"{baseIndent}{{");

        // Emit case for each branch path
        foreach (var branchCase in branch.Cases)
        {
            // Apply loop prefix to step name if branch is inside a loop
            // This ensures the Start command matches the prefixed commands generated by CommandsEmitter
            var stepName = branch.IsInsideLoop
                ? $"{branch.LoopPrefix}_{branchCase.FirstStepName}"
                : branchCase.FirstStepName;
            var firstStepCommand = $"Start{stepName}Command";

            if (branchCase.CaseValueLiteral == "_" || branchCase.CaseValueLiteral == "default")
            {
                // Otherwise case (default)
                sb.AppendLine($"{baseIndent}    _ => new {firstStepCommand}(WorkflowId),");
            }
            else
            {
                sb.AppendLine($"{baseIndent}    {branchCase.CaseValueLiteral} => new {firstStepCommand}(WorkflowId),");
            }
        }

        // Add default if no otherwise case. A bool discriminator with both true and
        // false is already exhaustive — a leftover `_ =>` is CS8510 (#179).
        var hasOtherwise = branch.Cases.Any(c => c.CaseValueLiteral == "_" || c.CaseValueLiteral == "default");
        if (!hasOtherwise && !IsExhaustiveBoolDiscriminator(branch))
        {
            // Priority: consecutive branch → rejoin → throw
            if (branch.HasNextConsecutiveBranch)
            {
                // Emit nested switch for the next consecutive branch
                sb.Append($"{baseIndent}    _ => ");
                EmitSwitchExpression(sb, branch.NextConsecutiveBranch!, baseIndent + "    ");
                sb.AppendLine(",");
            }
            else if (branch.HasRejoinPoint)
            {
                // Route unhandled cases to rejoin (passthrough)
                var rejoinCommand = $"Start{branch.RejoinStepName}Command";
                sb.AppendLine($"{baseIndent}    _ => new {rejoinCommand}(WorkflowId),");
            }
            else
            {
                // No rejoin - throw for unexpected values
                sb.AppendLine($"{baseIndent}    _ => throw new InvalidOperationException($\"Unhandled branch value: {{{discriminatorAccess}}}\"),");
            }
        }

        sb.Append($"{baseIndent}}}");
    }

    /// <summary>
    /// Returns <see langword="true"/> when the discriminator is <see cref="bool"/> and both
    /// <see langword="true"/> and <see langword="false"/> arms are present, so a discarded
    /// default arm would be unreachable (CS8510).
    /// </summary>
    /// <param name="branch">The branch whose discriminator and cases to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when the default arm must be omitted; otherwise
    /// <see langword="false"/>.
    /// </returns>
    private static bool IsExhaustiveBoolDiscriminator(BranchModel branch)
    {
        if (!IsBoolDiscriminatorType(branch.DiscriminatorTypeName))
        {
            return false;
        }

        var hasTrue = false;
        var hasFalse = false;
        foreach (var branchCase in branch.Cases)
        {
            if (branchCase.CaseValueLiteral == "true")
            {
                hasTrue = true;
            }
            else if (branchCase.CaseValueLiteral == "false")
            {
                hasFalse = true;
            }
        }

        return hasTrue && hasFalse;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="typeName"/> names the
    /// <see cref="bool"/> discriminator type stored on <see cref="BranchModel"/>.
    /// </summary>
    /// <param name="typeName">The discriminator type name from the branch model.</param>
    /// <returns>
    /// <see langword="true"/> for <c>bool</c>, <c>Boolean</c>, or <c>System.Boolean</c>.
    /// </returns>
    private static bool IsBoolDiscriminatorType(string typeName)
        => typeName is "bool" or "Boolean" or "System.Boolean";

    /// <summary>
    /// Emits a handler for the end of a branch path.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the last step in the branch path.</param>
    /// <param name="branch">The branch model.</param>
    /// <param name="branchCase">The specific branch case.</param>
    /// <param name="confidence">
    /// The confidence policy declared on this last step, or null when the step declares none.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter other than <paramref name="confidence"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Whether the path rejoins or ends the workflow is decided per CASE, not per branch. A case
    /// that declared <c>.Complete()</c> completes the saga at its own last step even when a sibling
    /// case rejoins — in that mixed shape the branch-level rejoin flag is true, so reading it alone
    /// would send the ending case to the declared terminal (#175). The branch-level flag remains the
    /// fallback for a case that did not declare an ending of its own.
    /// </para>
    /// <para>
    /// A last step that declared <c>.RequireConfidence(t).OnLowConfidence(alt =&gt; ...)</c> is
    /// gated here, for BOTH case kinds. This handler intercepts the step, so a gate the generic
    /// completed handler would otherwise emit has nowhere else to land: without the prologue below
    /// the declared threshold and its handler chain never reach the saga at all.
    /// </para>
    /// </remarks>
    public void EmitPathEndHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        BranchModel branch,
        BranchCaseModel branchCase,
        ConfidenceModel? confidence = null)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(branch, nameof(branch));
        ThrowHelper.ThrowIfNull(branchCase, nameof(branchCase));

        // Branch path step names include the branch prefix (e.g., "Approved_Complete")
        // and should be used as-is for the event name - don't strip the prefix
        var eventName = $"{stepName}Completed";
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // The case's own declaration wins: a case that declared .Complete() ends the workflow here,
        // whichever way its siblings exit. Only a case that made no such declaration falls back to
        // the branch-level convergence point.
        var routesToRejoin = !branchCase.IsTerminal && branch.HasRejoinPoint;

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(routesToRejoin
            ? $"    /// Handles the {eventName} event - completes branch path and routes to rejoin."
            : $"    /// Handles the {eventName} event - completes branch path and completes the workflow.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {stepName} completed event.</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);
        sb.AppendLine("    /// <param name=\"logger\">The logger for diagnostic output.</param>");

        if (confidence?.OnLowConfidenceHandlerStep is not null)
        {
            EmitConfidenceGatedPathEndHandler(
                sb, model, eventName, branch, branchCase, routesToRejoin, confidence);
        }
        else if (routesToRejoin)
        {
            var rejoinStepCommand = $"Start{branch.RejoinStepName}Command";

            sb.AppendLine($"    /// <returns>The start command for the rejoin step ({branch.RejoinStepName}).</returns>");
            // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
            sb.AppendLine($"    public {rejoinStepCommand} Handle(");
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

            // Log branch path completion
            sb.AppendLine($"        logger.LogDebug(");
            sb.AppendLine($"            \"Branch path {{BranchPath}} completed for workflow {{WorkflowId}}, rejoining at {{RejoinStep}}\",");
            sb.AppendLine($"            \"{branchCase.BranchPathPrefix}\",");
            sb.AppendLine("            WorkflowId,");
            sb.AppendLine($"            \"{branch.RejoinStepName}\");");
            sb.AppendLine();

            sb.AppendLine($"        return new {rejoinStepCommand}(WorkflowId);");
            sb.AppendLine("    }");
        }
        else
        {
            // This branch path ends the workflow: either the case declared .Complete(), or the
            // branch has no convergence point at all.
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

            // Apply state change
            if (!string.IsNullOrEmpty(model.StateTypeName))
            {
                StateApplicationHelper.EmitStateApplication(sb, model);
                sb.AppendLine();
            }

            // Log branch path completion with workflow completion
            sb.AppendLine($"        logger.LogInformation(");
            sb.AppendLine($"            \"Branch path {{BranchPath}} completed workflow {{WorkflowId}}\",");
            sb.AppendLine($"            \"{branchCase.BranchPathPrefix}\",");
            sb.AppendLine("            WorkflowId);");
            sb.AppendLine();

            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Completed;");
            sb.AppendLine("        MarkCompleted();");
            sb.AppendLine("    }");
        }
    }

    /// <summary>
    /// Emits a branch path-end handler whose last step declared a confidence policy: the
    /// completed event's score is compared to the threshold before the path's ending is applied.
    /// Below the threshold the saga cascades the low-confidence handler chain's start command
    /// (INV-1) and neither rejoins nor completes; at or above it (or when the result carried no
    /// score) the path ends exactly as it would without the gate.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="eventName">The completed-event type name this handler accepts.</param>
    /// <param name="branch">The branch model.</param>
    /// <param name="branchCase">The specific branch case.</param>
    /// <param name="routesToRejoin">
    /// True when the path ends by rejoining the branch's convergence point; false when it ends
    /// the workflow.
    /// </param>
    /// <param name="confidence">The last step's confidence policy.</param>
    /// <remarks>
    /// The gated shape returns <c>IEnumerable&lt;object&gt;</c> because the below-threshold route
    /// and the ordinary ending are different messages. Unconfigured paths keep their original
    /// concrete return types, so output for a branch with no confidence policy is unchanged.
    /// </remarks>
    private static void EmitConfidenceGatedPathEndHandler(
        StringBuilder sb,
        WorkflowModel model,
        string eventName,
        BranchModel branch,
        BranchCaseModel branchCase,
        bool routesToRejoin,
        ConfidenceModel confidence)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var handlerStepName = confidence.OnLowConfidenceHandlerStep!.StepName;
        var lowConfidenceCommand = $"Start{handlerStepName}Command";
        var thresholdLiteral = confidence.Threshold.ToString("R", CultureInfo.InvariantCulture);

        // The gated step's own name, for the audit event. Derived from the completed event name so
        // it holds even when the branch case carries no resolved step model.
        var gatedStepName = eventName.EndsWith("Completed", StringComparison.Ordinal)
            ? eventName.Substring(0, eventName.Length - "Completed".Length)
            : eventName;

        sb.AppendLine("    /// <returns>The low-confidence handler start command when below the");
        sb.AppendLine("    /// confidence threshold; otherwise the path's ordinary ending.</returns>");
        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine();
        }

        sb.AppendLine("        // Confidence gate: route to the low-confidence handler when the branch-case");
        sb.AppendLine("        // step's result confidence is present and below the configured threshold.");
        sb.AppendLine($"        if (evt.Confidence is double confidenceScore && confidenceScore < {thresholdLiteral})");
        sb.AppendLine("        {");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.{handlerStepName};");
        sb.AppendLine();
        sb.AppendLine("            logger.LogWarning(");
        sb.AppendLine("                \"Branch-case step confidence {Confidence} below threshold {Threshold} for workflow {WorkflowId}, routing to {Handler}\",");
        sb.AppendLine("                confidenceScore,");
        sb.AppendLine($"                {thresholdLiteral},");
        sb.AppendLine("                WorkflowId,");
        sb.AppendLine($"                nameof({lowConfidenceCommand}));");

        if (model.IsEventSourced)
        {
            sb.AppendLine();
            sb.AppendLine("            session.Events.Append(");
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

        if (routesToRejoin)
        {
            sb.AppendLine("        logger.LogDebug(");
            sb.AppendLine("            \"Branch path {BranchPath} completed for workflow {WorkflowId}, rejoining at {RejoinStep}\",");
            sb.AppendLine($"            \"{branchCase.BranchPathPrefix}\",");
            sb.AppendLine("            WorkflowId,");
            sb.AppendLine($"            \"{branch.RejoinStepName}\");");
            sb.AppendLine();
            sb.AppendLine($"        yield return new Start{branch.RejoinStepName}Command(WorkflowId);");
        }
        else
        {
            sb.AppendLine("        logger.LogInformation(");
            sb.AppendLine("            \"Branch path {BranchPath} completed workflow {WorkflowId}\",");
            sb.AppendLine($"            \"{branchCase.BranchPathPrefix}\",");
            sb.AppendLine("            WorkflowId);");
            sb.AppendLine();
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Completed;");
            sb.AppendLine("        MarkCompleted();");
            sb.AppendLine("        yield break;");
        }

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
