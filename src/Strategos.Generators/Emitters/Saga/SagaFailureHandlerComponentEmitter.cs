// -----------------------------------------------------------------------
// <copyright file="SagaFailureHandlerComponentEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Text;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Component emitter that generates failure handler saga methods.
/// </summary>
/// <remarks>
/// <para>
/// This component emits handlers for failure handler commands. It generates:
/// <list type="bullet">
///   <item><description>Trigger handler - stores exception context and starts first step</description></item>
///   <item><description>Start handlers - transitions phase and dispatches to worker</description></item>
///   <item><description>Completed handlers - chains to next step or marks Failed</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SagaFailureHandlerComponentEmitter : ISagaComponentEmitter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        if (!model.HasFailureHandlers)
        {
            return;
        }

        // Get the first handler to determine the first step to chain to
        var firstHandler = model.FailureHandlers!.First();
        var firstStepName = firstHandler.FirstStepName;

        // Emit the trigger handler
        sb.AppendLine();
        EmitTriggerHandler(sb, model, firstHandler);

        // Emit handlers for each failure handler step
        foreach (var handler in model.FailureHandlers!)
        {
            var sanitizedId = handler.HandlerId.Replace("-", "_");
            var stepNames = handler.StepNames.ToList();

            for (int i = 0; i < stepNames.Count; i++)
            {
                var stepName = stepNames[i];
                var isLastStep = i == stepNames.Count - 1;
                var nextStepName = isLastStep ? null : stepNames[i + 1];

                // Start handler
                sb.AppendLine();
                EmitStartHandler(sb, model, handler, stepName, sanitizedId);

                // Completed handler
                sb.AppendLine();
                EmitCompletedHandler(sb, model, handler, stepName, sanitizedId, isLastStep, nextStepName);
            }
        }
    }

    private static void EmitTriggerHandler(
        StringBuilder sb,
        WorkflowModel model,
        FailureHandlerModel handler)
    {
        var triggerCommandName = $"Trigger{model.PascalName}FailureHandlerCommand";
        var sanitizedId = handler.HandlerId.Replace("-", "_");
        var firstStepName = handler.FirstStepName;
        var startCommandName = $"StartFailureHandler_{sanitizedId}_{firstStepName}Command";

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the failure handler trigger command - stores exception context and starts first step.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"cmd\">The trigger command containing failure information.</param>");
        sb.AppendLine($"    /// <returns>The command to start the first failure handler step.</returns>");
        sb.AppendLine($"    public {startCommandName} Handle({triggerCommandName} cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine();
        sb.AppendLine("        FailedStepName = cmd.FailedStepName;");
        sb.AppendLine("        FailureExceptionMessage = cmd.ExceptionMessage;");
        sb.AppendLine("        FailureExceptionType = cmd.ExceptionType;");
        sb.AppendLine("        FailureStackTrace = cmd.StackTrace;");
        sb.AppendLine("        FailureTimestamp = DateTimeOffset.UtcNow;");
        sb.AppendLine();
        sb.AppendLine($"        return new {startCommandName}(WorkflowId);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits the parameter list for failure handler completed methods.
    /// </summary>
    private static void EmitFailureHandlerParams(
        StringBuilder sb,
        WorkflowModel model,
        string completedEventName)
    {
        if (model.IsEventSourced)
        {
            sb.AppendLine($"        {completedEventName} evt,");
            sb.AppendLine("        IDocumentSession session)");
        }
        else
        {
            sb.AppendLine($"        {completedEventName} evt)");
        }
    }

    private static void EmitStartHandler(
        StringBuilder sb,
        WorkflowModel model,
        FailureHandlerModel handler,
        string stepName,
        string sanitizedId)
    {
        var startCommandName = $"StartFailureHandler_{sanitizedId}_{stepName}Command";
        var workerCommandName = $"ExecuteFailureHandler_{sanitizedId}_{stepName}WorkerCommand";
        var phaseName = $"FailureHandler_{sanitizedId}_{stepName}";

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {startCommandName} - transitions phase and dispatches to worker.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"cmd\">The start command.</param>");
        sb.AppendLine($"    /// <returns>The command to execute the worker.</returns>");
        sb.AppendLine($"    public {workerCommandName} Handle(");
        sb.AppendLine($"        {startCommandName} cmd)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine();
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.{phaseName};");
        sb.AppendLine();
        sb.AppendLine($"        return new {workerCommandName}(");
        sb.AppendLine("            WorkflowId,");
        sb.AppendLine("            Guid.NewGuid(),");
        sb.AppendLine("            State,");
        sb.AppendLine("            FailedStepName!,");
        sb.AppendLine("            FailureExceptionMessage,");
        sb.AppendLine("            FailureExceptionType,");
        sb.AppendLine("            FailureStackTrace);");
        sb.AppendLine("    }");
    }

    private static void EmitCompletedHandler(
        StringBuilder sb,
        WorkflowModel model,
        FailureHandlerModel handler,
        string stepName,
        string sanitizedId,
        bool isLastStep,
        string? nextStepName)
    {
        var completedEventName = $"FailureHandler_{sanitizedId}_{stepName}Completed";

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {completedEventName} event.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The completed event.</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);

        if (isLastStep && handler.IsTerminal)
        {
            // Final step of a terminal handler - mark as Failed and complete
            sb.AppendLine("    public void Handle(");
            EmitFailureHandlerParams(sb, model, completedEventName);
            sb.AppendLine("    {");
            sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
            StateApplicationHelper.EmitSessionGuard(sb, model);
            sb.AppendLine();
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine("        MarkCompleted();");
            sb.AppendLine("    }");
        }
        else if (isLastStep)
        {
            // Final step of a non-terminal handler - just update state
            sb.AppendLine("    public void Handle(");
            EmitFailureHandlerParams(sb, model, completedEventName);
            sb.AppendLine("    {");
            sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
            StateApplicationHelper.EmitSessionGuard(sb, model);
            sb.AppendLine();
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine("    }");
        }
        else
        {
            // Chain to next step
            var nextStartCommandName = $"StartFailureHandler_{sanitizedId}_{nextStepName}Command";
            sb.AppendLine($"    /// <returns>The command to start the next failure handler step.</returns>");
            sb.AppendLine($"    public {nextStartCommandName} Handle(");
            EmitFailureHandlerParams(sb, model, completedEventName);
            sb.AppendLine("    {");
            sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
            StateApplicationHelper.EmitSessionGuard(sb, model);
            sb.AppendLine();
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine($"        return new {nextStartCommandName}(WorkflowId);");
            sb.AppendLine("    }");
        }
    }
}
