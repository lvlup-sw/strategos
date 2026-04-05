// -----------------------------------------------------------------------
// <copyright file="BranchHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

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
        var reducerTypeName = model.ReducerTypeName;
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

        // Add default if no otherwise case
        var hasOtherwise = branch.Cases.Any(c => c.CaseValueLiteral == "_" || c.CaseValueLiteral == "default");
        if (!hasOtherwise)
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
    /// Emits a handler for the end of a branch path.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the last step in the branch path.</param>
    /// <param name="branch">The branch model.</param>
    /// <param name="branchCase">The specific branch case.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public void EmitPathEndHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        BranchModel branch,
        BranchCaseModel branchCase)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(branch, nameof(branch));
        ThrowHelper.ThrowIfNull(branchCase, nameof(branchCase));

        // Branch path step names include the branch prefix (e.g., "Approved_Complete")
        // and should be used as-is for the event name - don't strip the prefix
        var eventName = $"{stepName}Completed";
        var reducerTypeName = model.ReducerTypeName;
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {eventName} event - completes branch path and routes to rejoin.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {stepName} completed event.</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);
        sb.AppendLine("    /// <param name=\"logger\">The logger for diagnostic output.</param>");

        if (branch.HasRejoinPoint)
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
            // No rejoin - this branch path ends the workflow
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
