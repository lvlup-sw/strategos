// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Lowers a workflow's declared diagnostic-fork edges (DR-9, #151) into the single
/// saga decision site — the <c>Handle(Fork{Pascal}Command)</c> occurrence chokepoint
/// where a diagnostic fork is born.
/// </summary>
/// <remarks>
/// <para>
/// The generated handler enforces, in order, the three guards the
/// <see cref="DiagnosticForkModel"/> declares:
/// <list type="number">
///   <item><description>
///     <b>Anchor guard</b> — the fork is admissible only at a declared anchor step
///     moniker (<see cref="DiagnosticForkModel.AnchorStepMonikers"/>); a fork whose
///     anchor is not declared is refused.
///   </description></item>
///   <item><description>
///     <b>Permitted-trigger + evidence guard</b> (the DR-8 occurrence-completeness
///     chokepoint) — the fork is admitted only for a permitted trigger whose
///     occurrence carries its required evidence (a non-empty provisional-stamp event id
///     and a non-empty taint set). A fork WITHOUT a permitted trigger or WITHOUT its
///     evidence is refused, so an unjustified occurrence cannot be born.
///   </description></item>
///   <item><description>
///     <b>maxForks bound</b> (<see cref="DiagnosticForkModel.MaxForks"/>, the loop
///     <c>MaxIterations</c> forced-exit precedent) — once the bound is reached an
///     overflowing fork routes to the blocked / human-escalation terminal phase
///     rather than spawning another remediation path.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// On a valid fork the handler appends the <c>{Pascal}WorkflowForked</c> audit stream
/// event (event-sourced mode only — mirroring the <c>EventsEmitter</c>
/// <c>IsEventSourced</c>-gated pattern) at this single decision site, then seeds
/// compensation by routing the fork's declared compensation seed into the merged
/// <c>Compensate</c>/<c>OnFailure</c> trigger site (#140) via the shared
/// <c>Trigger{Pascal}FailureHandlerCommand</c>.
/// </para>
/// <para>
/// The fork count is workflow-scoped (a single <c>DiagnosticForkCount</c> saga
/// property): the guard enforces the matched edge's bound against the total forks the
/// workflow has spawned.
/// </para>
/// </remarks>
internal sealed class DiagnosticForkHandlerEmitter
{
    private const string SchemaVersionLiteral = "fork.v1";

    /// <summary>
    /// Emits the single <c>Handle(Fork{Pascal}Command)</c> decision-site handler that
    /// lowers every diagnostic-fork edge declared on the workflow.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model carrying the diagnostic-fork edges.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public void EmitDecisionSiteHandler(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        if (!model.HasDiagnosticForks)
        {
            return;
        }

        var forks = model.DiagnosticForks!;
        var commandName = $"Fork{model.PascalName}Command";
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var eventName = $"{model.PascalName}WorkflowForked";
        var triggerCommandName = $"Trigger{model.PascalName}FailureHandlerCommand";

        // Only route into the merged trigger site when the workflow actually declares a
        // compensation / OnFailure path (that is what emits the Trigger command + its
        // handler). A fork edge always names a seed, but a workflow with no rollback
        // path has nowhere to route it, so the fork is still recorded/audited but seeds
        // nothing — keeping the generated handler compilable in every configuration.
        var canSeedCompensation = model.HasCompensation || model.HasFailureHandlers;

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Handles the diagnostic-fork decision command (DR-9) - the single occurrence");
        sb.AppendLine("    /// chokepoint where a diagnostic fork is born. Enforces the anchor guard, the");
        sb.AppendLine("    /// permitted-trigger + evidence-completeness guard, and the maxForks bound; on a");
        sb.AppendLine("    /// valid fork appends the WorkflowForked audit event (event-sourced) and seeds");
        sb.AppendLine("    /// compensation via the merged failure/compensation trigger site.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"cmd\">The fork decision command carrying the occurrence (trigger + evidence).</param>");
        StateApplicationHelper.EmitSessionParameterDoc(sb, model);
        sb.AppendLine("    /// <param name=\"logger\">The injected logger.</param>");
        sb.AppendLine("    /// <returns>The compensation trigger command when the fork is admitted; empty otherwise.</returns>");
        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {commandName} cmd,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        for (var i = 0; i < forks.Count; i++)
        {
            EmitEdgeBlock(sb, model, forks[i], i, eventName, triggerCommandName, canSeedCompensation);
        }

        // Anchor guard fall-through: a fork whose anchor is not a declared fork anchor
        // is refused (it reaches no edge block above).
        sb.AppendLine("        logger.LogWarning(");
        sb.AppendLine("            \"Refusing diagnostic fork for workflow {WorkflowId}: anchor {Anchor} is not a declared fork anchor\",");
        sb.AppendLine("            WorkflowId,");
        sb.AppendLine("            cmd.Anchor);");
        sb.AppendLine("    }");
    }

    private static void EmitEdgeBlock(
        StringBuilder sb,
        WorkflowModel model,
        DiagnosticForkModel fork,
        int edgeIndex,
        string eventName,
        string triggerCommandName,
        bool canSeedCompensation)
    {
        var permittedVar = $"edge{edgeIndex}Permitted";
        var evidenceVar = $"edge{edgeIndex}EvidencePresent";
        var seed = fork.CompensationSeedMoniker;

        sb.AppendLine($"        // Diagnostic fork edge {edgeIndex} - admissible at anchor(s): {string.Join(", ", fork.AnchorStepMonikers)}.");
        sb.Append("        if (");
        for (var a = 0; a < fork.AnchorStepMonikers.Count; a++)
        {
            if (a > 0)
            {
                sb.AppendLine();
                sb.Append("            || ");
            }

            sb.Append($"cmd.Anchor == \"{fork.AnchorStepMonikers[a]}\"");
        }

        sb.AppendLine(")");
        sb.AppendLine("        {");

        // Permitted-trigger + evidence-completeness guard (DR-8 occurrence chokepoint).
        sb.AppendLine("            // Occurrence-completeness guard (DR-8): admit only a permitted trigger whose");
        sb.AppendLine("            // occurrence carries its required evidence; a fork without them is refused.");
        sb.Append($"            var {permittedVar} =");
        for (var t = 0; t < fork.PermittedTriggers.Count; t++)
        {
            var wireValue = PascalToSnake(fork.PermittedTriggers[t].TriggerName);
            if (t > 0)
            {
                sb.AppendLine();
                sb.Append($"                || cmd.Trigger == \"{wireValue}\"");
            }
            else
            {
                sb.Append($" cmd.Trigger == \"{wireValue}\"");
            }
        }

        sb.AppendLine(";");
        sb.AppendLine($"            var {evidenceVar} =");
        sb.AppendLine("                !string.IsNullOrWhiteSpace(cmd.ProvisionalStampEventId)");
        sb.AppendLine("                && cmd.Taints is not null");
        sb.AppendLine("                && cmd.Taints.Count > 0;");
        sb.AppendLine($"            if (!{permittedVar} || !{evidenceVar})");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogWarning(");
        sb.AppendLine("                    \"Refusing diagnostic fork for workflow {WorkflowId}: trigger {Trigger} is not permitted or its evidence is incomplete\",");
        sb.AppendLine("                    WorkflowId,");
        sb.AppendLine("                    cmd.Trigger);");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();

        // maxForks bound (the loop MaxIterations forced-exit precedent).
        sb.AppendLine("            // maxForks bound (the loop MaxIterations forced-exit precedent): once the bound");
        sb.AppendLine("            // is reached, an overflowing fork routes to the blocked / human-escalation terminal.");
        sb.AppendLine($"            if (DiagnosticForkCount >= {fork.MaxForks})");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogWarning(");
        sb.AppendLine("                    \"Diagnostic fork bound {Bound} reached for workflow {WorkflowId}; routing to blocked terminal for human escalation\",");
        sb.AppendLine($"                    {fork.MaxForks},");
        sb.AppendLine("                    WorkflowId);");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.ForkBlocked;");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            DiagnosticForkCount++;");

        if (model.IsEventSourced)
        {
            sb.AppendLine();
            sb.AppendLine("            // Append the WorkflowForked audit stream event at the single decision site.");
            sb.AppendLine("            session.Events.Append(");
            sb.AppendLine("                WorkflowId,");
            sb.AppendLine($"                new {eventName}(");
            sb.AppendLine("                    WorkflowId,");
            sb.AppendLine($"                    \"{SchemaVersionLiteral}\",");
            sb.AppendLine("                    cmd.Trigger,");
            sb.AppendLine("                    cmd.ProvisionalStampEventId,");
            sb.AppendLine("                    cmd.Taints,");
            sb.AppendLine("                    DateTimeOffset.UtcNow));");
        }

        sb.AppendLine();
        sb.AppendLine("            logger.LogInformation(");
        sb.AppendLine("                \"Diagnostic fork admitted for workflow {WorkflowId} on trigger {Trigger}; seeding compensation to {Seed}\",");
        sb.AppendLine("                WorkflowId,");
        sb.AppendLine("                cmd.Trigger,");
        sb.AppendLine($"                \"{seed}\");");

        if (canSeedCompensation)
        {
            sb.AppendLine();
            sb.AppendLine("            // Seed compensation via the merged Compensate/OnFailure trigger site (#140):");
            sb.AppendLine("            // the fork routes rollback to its declared compensation seed.");
            sb.AppendLine($"            yield return new {triggerCommandName}(");
            sb.AppendLine("                WorkflowId,");
            sb.AppendLine($"                \"{seed}\",");
            sb.AppendLine("                \"Diagnostic fork remediation seeded by a permitted fork trigger.\",");
            sb.AppendLine("                \"DiagnosticFork\",");
            sb.AppendLine("                null);");
        }

        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Maps a closed-trigger enum member name (PascalCase, carried verbatim in the IR) to
    /// its snake_case wire value — the cross-repo identity token consumers match on. The
    /// wire-vocabulary mapping is applied here at lowering, not in the generator IR.
    /// </summary>
    /// <param name="pascalName">The enum member name (e.g. <c>RatificationFailure</c>).</param>
    /// <returns>The snake_case wire value (e.g. <c>ratification_failure</c>).</returns>
    private static string PascalToSnake(string pascalName)
    {
        var sb = new StringBuilder(pascalName.Length + 4);
        for (var i = 0; i < pascalName.Length; i++)
        {
            var c = pascalName[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
