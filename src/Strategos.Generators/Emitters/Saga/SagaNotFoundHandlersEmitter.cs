// -----------------------------------------------------------------------
// <copyright file="SagaNotFoundHandlersEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits the NotFound handlers for a Wolverine saga class.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates static NotFound handlers that are invoked when
/// a saga message arrives but the saga instance no longer exists (either
/// because it completed or was never created).
/// </para>
/// <para>
/// The generated handlers:
/// <list type="bullet">
///   <item><description>A NotFound handler for the start workflow command</description></item>
///   <item><description>A NotFound handler for each step's completed event</description></item>
/// </list>
/// </para>
/// <para>
/// Each handler logs a warning with the workflow ID to aid in debugging
/// orphaned or late-arriving messages.
/// </para>
/// </remarks>
internal sealed class SagaNotFoundHandlersEmitter : ISagaComponentEmitter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var sagaClassName = $"{model.PascalName}Saga";
        var naming = ForkPathCompletedNaming.For(model);

        // NotFound for StartCommand
        EmitStartCommandNotFoundHandler(sb, model, sagaClassName);

        // NotFound for each emitted completed event. Shared-type fork-path instances use
        // ForkPathCompletedNaming stems; keep the unqualified type event only when the
        // same type also has a linear/non-fork occurrence that EventsEmitter emits.
        var emittedStepEvents = new HashSet<string>(StringComparer.Ordinal);
        if (model.Steps is not null)
        {
            foreach (var step in model.Steps)
            {
                if (naming.HasUnqualifiedUse(step.StepName, model)
                    && emittedStepEvents.Add(step.StepName))
                {
                    sb.AppendLine();
                    EmitStepCompletedNotFoundHandler(sb, step.StepName, sagaClassName);
                }
            }
        }
        else
        {
            // Fallback for models without Step collection - extract base step names from phase names
            foreach (var phaseName in model.StepNames)
            {
                if (naming.IsQualifiedPhase(phaseName))
                {
                    continue;
                }

                var baseStepName = ExtractBaseStepName(phaseName);
                if (emittedStepEvents.Add(baseStepName))
                {
                    sb.AppendLine();
                    EmitStepCompletedNotFoundHandler(sb, baseStepName, sagaClassName);
                }
            }
        }

        if (model.Forks is not null)
        {
            foreach (var fork in model.Forks)
            {
                foreach (var path in fork.Paths)
                {
                    foreach (var step in path.Steps)
                    {
                        var key = PathRoutingKey.ForFork(fork.ForkId, path.PathIndex, step.PhaseName);
                        var stem = naming.StemFor(key, step.StepName);
                        if (emittedStepEvents.Add(stem))
                        {
                            sb.AppendLine();
                            EmitStepCompletedNotFoundHandler(sb, stem, sagaClassName);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(fork.JoinStepName)
                    && emittedStepEvents.Add(fork.JoinStepName))
                {
                    sb.AppendLine();
                    EmitStepCompletedNotFoundHandler(sb, fork.JoinStepName, sagaClassName);
                }
            }
        }
    }

    private static void EmitStartCommandNotFoundHandler(
        StringBuilder sb,
        WorkflowModel model,
        string sagaClassName)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Handles start command when saga no longer exists.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static void NotFound(Start{model.PascalName}Command command, ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(command, nameof(command));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine("        logger.LogWarning(");
        sb.AppendLine($"            \"Received Start{model.PascalName}Command for completed/unknown workflow {{WorkflowId}}\",");
        sb.AppendLine("            command.WorkflowId);");
        sb.AppendLine("    }");
    }

    private static void EmitStepCompletedNotFoundHandler(
        StringBuilder sb,
        string stepName,
        string sagaClassName)
    {
        var eventName = $"{stepName}Completed";

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles {eventName} event when saga no longer exists.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static void NotFound({eventName} evt, ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine("        logger.LogWarning(");
        sb.AppendLine($"            \"Received {eventName} for completed/unknown workflow {{WorkflowId}}\",");
        sb.AppendLine("            evt.WorkflowId);");
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
