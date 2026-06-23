// -----------------------------------------------------------------------
// <copyright file="SagaTimeoutComponentEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Component emitter that lowers a step's <c>.WithTimeout(t)</c> into a Wolverine
/// saga deadline race (DR-4).
/// </summary>
/// <remarks>
/// <para>
/// For each step that declares a timeout, this component emits, nested inside the
/// generated saga:
/// <list type="bullet">
///   <item><description>
///     A <c>{Phase}Timeout</c> record deriving from <see cref="global::Wolverine.TimeoutMessage"/>.
///     The base type carries a <see cref="System.TimeSpan"/> and tells Wolverine to
///     auto-schedule delayed delivery of the message and to silently ignore it if
///     the saga no longer exists (so the no-op race needs no NotFound handler).
///   </description></item>
///   <item><description>
///     A <c>Handle({Phase}Timeout)</c> method that routes to the failure path only
///     when the step's phase has not advanced — an idempotent race guard. If the
///     step already completed, the saga's <c>Phase</c> moved on and the timeout is a
///     no-op (the step's <c>Completed</c> event won the race).
///   </description></item>
/// </list>
/// </para>
/// <para>
/// The cascade of the timeout message (so the deadline race starts when the step
/// starts) is emitted by <see cref="StepStartHandlerEmitter"/> alongside the worker
/// command. This mirrors the existing approval-timeout shape
/// (<see cref="SagaApprovalHandlersEmitter.EmitTimeoutHandler"/>), differing only in
/// that Wolverine's <c>TimeoutMessage</c> base auto-schedules delivery rather than an
/// explicit <c>context.ScheduleAsync(...)</c>.
/// </para>
/// </remarks>
internal sealed class SagaTimeoutComponentEmitter : ISagaComponentEmitter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var context = SagaEmissionContext.Create(model);

        foreach (var phaseName in model.StepNames)
        {
            if (!context.StepsByName.TryGetValue(phaseName, out var stepModel))
            {
                continue;
            }

            if (stepModel.Timeout is null)
            {
                continue;
            }

            sb.AppendLine();
            EmitTimeoutMessageRecord(sb, phaseName, stepModel.Timeout);

            sb.AppendLine();
            EmitTimeoutHandler(sb, model, phaseName);
        }
    }

    /// <summary>
    /// Emits the timeout message record that derives from Wolverine's
    /// <c>TimeoutMessage</c> base. Wolverine auto-schedules its delivery from the
    /// base constructor's <see cref="System.TimeSpan"/>.
    /// </summary>
    private static void EmitTimeoutMessageRecord(StringBuilder sb, string phaseName, TimeoutModel timeout)
    {
        // Reconstruct the configured TimeSpan deterministically from ticks so the
        // emitted literal is exact regardless of how the duration was expressed in
        // the DSL (seconds, minutes, milliseconds, ...).
        var ticks = timeout.Timeout.Ticks.ToString(CultureInfo.InvariantCulture);

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Saga timeout message for the {phaseName} step. Derives from");
        sb.AppendLine("    /// <see cref=\"Wolverine.TimeoutMessage\"/> so Wolverine auto-schedules its");
        sb.AppendLine("    /// delayed delivery and ignores it if the saga has already completed.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public sealed record {phaseName}Timeout(");
        sb.AppendLine("        [property: Wolverine.Persistence.Sagas.SagaIdentity] Guid WorkflowId)");
        sb.AppendLine($"        : TimeoutMessage(System.TimeSpan.FromTicks({ticks}L));");
    }

    /// <summary>
    /// Emits the saga handler for a step's timeout message. The handler routes to the
    /// failure path only when the step's phase has not advanced (idempotent guard).
    /// </summary>
    private static void EmitTimeoutHandler(StringBuilder sb, WorkflowModel model, string phaseName)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {phaseName} step timeout - routes to the failure path");
        sb.AppendLine("    /// only if the step has not already advanced past its phase.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"t\">The {phaseName} timeout message.</param>");
        sb.AppendLine("    /// <param name=\"logger\">The injected logger.</param>");
        sb.AppendLine("    public void Handle(");
        sb.AppendLine($"        {phaseName}Timeout t,");
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(t, nameof(t));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine("        // Race guard: if the step already completed, the saga's Phase has");
        sb.AppendLine("        // advanced past this step and the timeout is a no-op (the Completed");
        sb.AppendLine("        // event won the race).");
        sb.AppendLine($"        if (Phase != {model.PhaseEnumName}.{phaseName})");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        logger.LogWarning(");
        sb.AppendLine("            \"Step {StepName} timed out for workflow {WorkflowId}; routing to failure path\",");
        sb.AppendLine($"            \"{phaseName}\",");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("        MarkCompleted();");
        sb.AppendLine("    }");
    }
}
