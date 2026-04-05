// -----------------------------------------------------------------------
// <copyright file="ForkJoinHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits handler methods for fork path completion and join synchronization in a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates three types of handlers:
/// <list type="bullet">
///   <item><description>
///     Path completed handler - Updates path status and state, checks join readiness
///   </description></item>
///   <item><description>
///     Join readiness method - Checks if all paths have reached terminal status
///   </description></item>
///   <item><description>
///     Join handler - Builds ForkContext and dispatches join step
///   </description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class ForkJoinHandlerEmitter
{
    /// <summary>
    /// Emits a handler for when a fork path's last step completes.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the last step in the path.</param>
    /// <param name="fork">The fork model.</param>
    /// <param name="path">The specific path that completed.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// This handler uses a void return with explicit <c>context.SendAsync()</c> rather than
    /// returning the join command. This is critical because Wolverine does not persist saga
    /// state when handlers return null. With the previous nullable return pattern, fork path
    /// status changes were lost when paths completed before others, causing the join condition
    /// to never become true.
    /// </para>
    /// </remarks>
    public void EmitPathCompletedHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        ForkModel fork,
        ForkPathModel path)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(fork, nameof(fork));
        ThrowHelper.ThrowIfNull(path, nameof(path));

        // Worker handlers generate unprefixed events (e.g., ValidateThesisStepCompleted) because they
        // are generated per step TYPE, not per phase. Extract the base step name from the prefixed
        // phase name (e.g., "TargetLoop_ValidateThesisStep" -> "ValidateThesisStep").
        var baseStepName = ExtractBaseStepName(stepName);
        var eventName = $"{baseStepName}Completed";
        var sanitizedId = fork.ForkId.Replace("-", "_");
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {eventName} event - updates path status and checks join readiness.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {stepName} completed event.</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);
        sb.AppendLine("    /// <param name=\"logger\">The logger for diagnostic output.</param>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// Uses IEnumerable return to conditionally yield join command when all paths complete.");
        sb.AppendLine("    /// Wolverine persists saga state after handler completes, ensuring path status is saved");
        sb.AppendLine("    /// even when not all paths are ready for join.");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <returns>The join command if all paths complete, empty otherwise.</returns>");

        // IEnumerable<object> return allows conditional yield return for join command
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        // Apply state change and store path state if state type is specified
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine($"        Fork_{sanitizedId}_Path{path.PathIndex}State = evt.UpdatedState;");
            sb.AppendLine();
        }

        // Update path status to Success
        sb.AppendLine($"        // Mark path {path.PathIndex} as completed");
        sb.AppendLine($"        Fork_{sanitizedId}_Path{path.PathIndex}Status = Strategos.Definitions.ForkPathStatus.Success;");
        sb.AppendLine();

        // Log fork path completion
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Fork path {{PathIndex}} completed for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            {path.PathIndex},");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();

        // Check join readiness and yield join command if ready
        sb.AppendLine("        // Check if all paths are complete - yield join command if ready");
        sb.AppendLine($"        if (CheckJoinReady_{sanitizedId}())");
        sb.AppendLine("        {");
        sb.AppendLine($"            logger.LogDebug(");
        sb.AppendLine($"                \"All fork paths complete, joining for workflow {{WorkflowId}}\",");
        sb.AppendLine("                WorkflowId);");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Joining_{sanitizedId};");
        sb.AppendLine($"            yield return new JoinFork_{sanitizedId}_Command(WorkflowId);");
        sb.AppendLine("        }");
        sb.AppendLine("        // IEnumerable return ensures Wolverine always persists saga state after handler");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits the join readiness check method for a fork.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="fork">The fork model.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public void EmitJoinReadinessMethod(
        StringBuilder sb,
        ForkModel fork)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(fork, nameof(fork));

        var sanitizedId = fork.ForkId.Replace("-", "_");

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Checks if all paths in fork {fork.ForkId} have reached terminal status.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>True if all paths are complete, false otherwise.</returns>");
        sb.AppendLine($"    private bool CheckJoinReady_{sanitizedId}()");
        sb.AppendLine("    {");

        // Check each path's status is terminal (Success, Failed, or FailedWithRecovery)
        sb.Append("        return ");
        for (var i = 0; i < fork.Paths.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
                sb.Append("            && ");
            }

            sb.Append($"(Fork_{sanitizedId}_Path{i}Status is Strategos.Definitions.ForkPathStatus.Success ");
            sb.Append($"or Strategos.Definitions.ForkPathStatus.Failed ");
            sb.Append($"or Strategos.Definitions.ForkPathStatus.FailedWithRecovery)");
        }

        sb.AppendLine(";");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits the join handler that executes the join step.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="fork">The fork model.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public void EmitJoinHandler(
        StringBuilder sb,
        WorkflowModel model,
        ForkModel fork)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(fork, nameof(fork));

        var sanitizedId = fork.ForkId.Replace("-", "_");
        var joinStepCommand = $"Start{fork.JoinStepName}Command";
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the join command - dispatches {fork.JoinStepName} step.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"cmd\">The join command.</param>");
        sb.AppendLine("    /// <param name=\"logger\">The logger for diagnostic output.</param>");
        sb.AppendLine($"    /// <returns>The start command for {fork.JoinStepName}.</returns>");
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    public {joinStepCommand} Handle(");
        sb.AppendLine($"        JoinFork_{sanitizedId}_Command cmd,");
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Dispatching join step {{JoinStep}} for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            \"{fork.JoinStepName}\",");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.{fork.JoinStepName};");
        sb.AppendLine($"        return new {joinStepCommand}(WorkflowId);");
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
