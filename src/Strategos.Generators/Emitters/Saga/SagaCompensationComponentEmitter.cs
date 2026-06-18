// -----------------------------------------------------------------------
// <copyright file="SagaCompensationComponentEmitter.cs" company="Levelup Software">
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
/// Component emitter that lowers a step's <c>.Compensate&lt;T&gt;()</c> rollback
/// into a runnable saga compensation chain (DR-3).
/// </summary>
/// <remarks>
/// <para>
/// This closes the long-standing dead path: the worker handler's generated
/// <c>Configure(HandlerChain)</c> chain now PUBLISHES the
/// <c>Trigger{Pascal}FailureHandlerCommand</c> on terminal step failure
/// (see <see cref="WorkerHandlerEmitter"/>); this emitter is what RECEIVES that
/// command in the saga and actually runs the compensation step. It emits, nested
/// in the generated saga:
/// <list type="bullet">
///   <item><description>
///     A <c>Handle(Trigger{Pascal}FailureHandlerCommand)</c> that stores the
///     failure context, transitions to the <c>Compensating</c> phase, and
///     dispatches the compensation step's <c>Execute{Comp}WorkerCommand</c>. The
///     compensation step's worker handler is produced by the normal main-flow
///     path because the compensation step type is folded into <c>model.Steps</c>,
///     so the proven worker dispatch is reused (it RUNS the rollback step).
///   </description></item>
///   <item><description>
///     A <c>Handle({Comp}Completed)</c> that folds the rollback's returned state
///     (INV-7: the compensation step returns new state; the saga never mutates the
///     input), then transitions the saga to its terminal <c>Failed</c> phase and
///     calls <c>MarkCompleted()</c>.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <b>Mutual exclusion with <see cref="SagaFailureHandlerComponentEmitter"/>:</b>
/// both emitters would produce a <c>Handle(Trigger{Pascal}FailureHandlerCommand)</c>
/// overload. To avoid a duplicate-method (CS0111) collision this emitter is a NO-OP
/// when the workflow ALSO declares an <c>OnFailure</c> block
/// (<c>model.HasFailureHandlers</c>); in that case the <c>OnFailure</c> path owns the
/// trigger handler. Compensation + OnFailure interop is a separate, larger concern
/// tracked outside this vertical.
/// </para>
/// <para>
/// <b>Single-compensation-step scope:</b> when multiple distinct compensation step
/// types are declared, the trigger handler routes to the correct rollback worker on
/// the <c>FailedStepName</c> carried by the trigger command.
/// </para>
/// <para>
/// <b>Reuse of a main-flow step as a compensation target:</b> a compensation step
/// TYPE may also appear as a normal main-flow step (rolling back to a step the happy
/// path also runs). The main-flow completed handler (<see cref="SagaStepHandlersEmitter"/>)
/// already declares that step's <c>Handle({Comp}Completed)</c> overload, so this
/// emitter SKIPS the duplicate to avoid a CS0111 collision; the trigger/dispatch
/// path is unaffected because the rollback's worker command is the same one the
/// folded main-flow step model already produced.
/// </para>
/// </remarks>
internal sealed class SagaCompensationComponentEmitter : ISagaComponentEmitter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        if (!model.HasCompensation)
        {
            return;
        }

        // The OnFailure path (SagaFailureHandlerComponentEmitter) owns the trigger
        // handler when present; emitting ours too would collide (CS0111).
        if (model.HasFailureHandlers)
        {
            return;
        }

        var compensatedSteps = model.CompensationSteps;

        sb.AppendLine();
        EmitTriggerHandler(sb, model, compensatedSteps);

        // A compensation step TYPE may also be a normal main-flow step (it is valid
        // to roll back to a step the happy path already runs). In that case the
        // main-flow completed handler (SagaStepHandlersEmitter) already declares the
        // Handle({Comp}Completed) overload; emitting ours too would collide (CS0111).
        // Mirror the HasFailureHandlers no-op above: skip the duplicate handler and
        // let the existing main-flow one cover it. The trigger/dispatch path needs no
        // change — it dispatches the rollback's worker command, which the folded
        // step model already produced (no duplicate member there).
        var mainFlowStepNames = new HashSet<string>(model.StepNames, StringComparer.Ordinal);

        // Deduplicate by compensation step type: two steps rolling back to the same
        // compensation type share a single {Comp}Completed handler.
        var emittedCompletedHandlers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in compensatedSteps)
        {
            var compStepName = NamingHelper.GetSimpleTypeName(step.Compensation!.CompensationStepTypeName);

            // Skip if the main flow already declares this step's completed handler.
            if (mainFlowStepNames.Contains(compStepName))
            {
                continue;
            }

            if (emittedCompletedHandlers.Add(compStepName))
            {
                sb.AppendLine();
                EmitCompensationCompletedHandler(sb, model, compStepName);
            }
        }
    }

    /// <summary>
    /// Emits the saga handler for the trigger failure-handler command: stores
    /// failure context, transitions to <c>Compensating</c>, and dispatches the
    /// compensation step's worker command (routing on the failed step name when
    /// more than one compensation step is declared).
    /// </summary>
    private static void EmitTriggerHandler(
        StringBuilder sb,
        WorkflowModel model,
        IReadOnlyList<StepModel> compensatedSteps)
    {
        var triggerCommandName = $"Trigger{model.PascalName}FailureHandlerCommand";
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var single = compensatedSteps.Count == 1;

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Handles the compensation trigger command (DR-3) - stores failure context");
        sb.AppendLine("    /// and dispatches the failed step's compensation (rollback) worker so the");
        sb.AppendLine("    /// compensation step actually runs.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"cmd\">The trigger command carrying the failure context.</param>");
        sb.AppendLine("    /// <param name=\"logger\">The injected logger.</param>");
        sb.AppendLine("    /// <returns>The compensation worker command(s) to dispatch.</returns>");
        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {triggerCommandName} cmd,");
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine("        FailedStepName = cmd.FailedStepName;");
        sb.AppendLine("        FailureExceptionMessage = cmd.ExceptionMessage;");
        sb.AppendLine("        FailureExceptionType = cmd.ExceptionType;");
        sb.AppendLine("        FailureStackTrace = cmd.StackTrace;");
        sb.AppendLine("        FailureTimestamp = DateTimeOffset.UtcNow;");
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Compensating;");
        sb.AppendLine();
        sb.AppendLine("        logger.LogWarning(");
        sb.AppendLine("            \"Step {FailedStepName} failed for workflow {WorkflowId}; running compensation\",");
        sb.AppendLine("            cmd.FailedStepName,");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();

        foreach (var step in compensatedSteps)
        {
            var compStepName = NamingHelper.GetSimpleTypeName(step.Compensation!.CompensationStepTypeName);
            var workerCommandName = NamingHelper.GetWorkerCommandName(compStepName);

            if (single)
            {
                sb.AppendLine($"        yield return new {workerCommandName}(WorkflowId, Guid.NewGuid(), State);");
            }
            else
            {
                // Route on the failed step name so the correct rollback runs.
                sb.AppendLine($"        if (cmd.FailedStepName == \"{step.StepName}\")");
                sb.AppendLine("        {");
                sb.AppendLine($"            yield return new {workerCommandName}(WorkflowId, Guid.NewGuid(), State);");
                sb.AppendLine("            yield break;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits the saga handler for a compensation step's completed event: folds the
    /// rollback's returned state, transitions to the terminal <c>Failed</c> phase,
    /// and marks the saga completed.
    /// </summary>
    private static void EmitCompensationCompletedHandler(
        StringBuilder sb,
        WorkflowModel model,
        string compStepName)
    {
        var completedEventName = NamingHelper.GetCompletedEventName(compStepName);
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {completedEventName} event (DR-3) - folds the rollback's");
        sb.AppendLine("    /// returned state and transitions the saga to its terminal Failed phase.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {compStepName} compensation completed event.</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);
        sb.AppendLine("    /// <param name=\"logger\">The injected logger.</param>");
        sb.AppendLine("    public void Handle(");
        sb.AppendLine($"        {completedEventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        // INV-7: the compensation step returns NEW state; the saga only folds it,
        // never mutates the input. This is the standard reducer application.
        StateApplicationHelper.EmitStateApplication(sb, model);

        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine();
        sb.AppendLine("        logger.LogInformation(");
        sb.AppendLine("            \"Compensation completed for workflow {WorkflowId}; workflow Failed\",");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine("        MarkCompleted();");
        sb.AppendLine("    }");
    }
}
