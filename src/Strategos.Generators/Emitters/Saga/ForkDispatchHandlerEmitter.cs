// -----------------------------------------------------------------------
// <copyright file="ForkDispatchHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits handler methods for dispatching parallel fork paths in a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates the handler that runs when the step preceding a fork completes.
/// The handler:
/// <list type="bullet">
///   <item><description>Applies the reducer with the completed step's state</description></item>
///   <item><description>Sets the phase to Forking_{ForkId}</description></item>
///   <item><description>Sets all path statuses to InProgress</description></item>
///   <item><description>Uses OutgoingMessages to dispatch start commands for all paths</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class ForkDispatchHandlerEmitter
{
    /// <summary>
    /// Emits a dispatch handler that initiates all fork paths in parallel.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the step before the fork.</param>
    /// <param name="fork">The fork model.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public void EmitDispatchHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        ForkModel fork)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(fork, nameof(fork));

        // Use unprefixed step type name for completed event (workers return per-type events)
        var baseStepName = ExtractBaseStepName(stepName);
        var eventName = $"{baseStepName}Completed";
        var reducerTypeName = model.ReducerTypeName;
        var sanitizedId = fork.ForkId.Replace("-", "_");

        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {eventName} event - dispatches parallel fork paths.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {stepName} completed event.</param>");
        sb.AppendLine("    /// <param name=\"logger\">The logger for diagnostic output.</param>");
        sb.AppendLine("    /// <returns>The start commands for all fork paths.</returns>");

        // Handler returns IEnumerable<object> to dispatch multiple commands for parallel paths
        // This ensures saga state is persisted before path commands are processed
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
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

        // Set phase to forking
        sb.AppendLine($"        // Set phase to forking");
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Forking_{sanitizedId};");
        sb.AppendLine();

        // Log fork dispatch with path count
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Dispatching {{PathCount}} fork paths for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            {fork.Paths.Count},");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();

        // Set all path statuses to InProgress
        sb.AppendLine("        // Initialize all path statuses to InProgress");
        foreach (var path in fork.Paths)
        {
            sb.AppendLine($"        Fork_{sanitizedId}_Path{path.PathIndex}Status = Strategos.Definitions.ForkPathStatus.InProgress;");
        }

        sb.AppendLine();

        // Yield start commands for all paths
        sb.AppendLine("        // Dispatch parallel path start commands");
        foreach (var path in fork.Paths)
        {
            if (path.StepNames.Count > 0)
            {
                var firstStepName = path.StepNames[0];
                sb.AppendLine($"        yield return new Start{firstStepName}Command(WorkflowId);");
            }
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
