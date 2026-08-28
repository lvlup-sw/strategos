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
///     <b>Permitted-trigger + per-trigger evidence guard</b> (the DR-8 occurrence-
///     completeness chokepoint) — the fork is admitted only for a permitted trigger whose
///     occurrence evidence map carries at least every field that trigger declared
///     (<see cref="PermittedForkTriggerModel.RequiredEvidenceFields"/>), each present and
///     non-empty (extra keys are ignored). A fork WITHOUT a permitted trigger, or WHOSE map
///     omits any of the fired trigger's declared fields, is refused — so a
///     <c>gate_contradiction</c> fork must
///     carry its own <c>leftGateId</c>/<c>rightGateId</c>, not ratification evidence, and
///     an unjustified occurrence cannot be born.
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
/// The fork count is PER EDGE, keyed by the sanitized compensation-seed moniker
/// (a <c>DiagnosticForkCount_{seed}</c> saga property per declared fork edge; same
/// '-' → '_' sanitizer as <c>Fork_{id}_Path{n}State</c>). Each edge enforces its
/// declared <c>maxForks</c> bound against its OWN tally, so a high-bound edge cannot
/// exhaust a shared pool and starve a low-bound edge (L3). Two edges that share a
/// seed are rejected (duplicate-compensation-seed diagnostic) rather than sharing a counter. 2.10.0 used
/// positional <c>DiagnosticForkCount_{i}</c>; 2.11.0 is a breaking saga-property
/// rename with no dual-read shim.
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
        sb.AppendLine("    /// permitted-trigger + per-trigger evidence-completeness guard, and the per-edge");
        sb.AppendLine("    /// maxForks bound; on a valid fork appends the WorkflowForked audit event");
        sb.AppendLine("    /// (event-sourced) and seeds compensation via the merged failure/compensation");
        sb.AppendLine("    /// trigger site.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"cmd\">The fork decision command carrying the occurrence (trigger + evidence map).</param>");
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

        EmitEvidenceCompletenessHelper(sb);
    }

    /// <summary>
    /// Emits the shared per-trigger evidence-completeness helper (DR-8): true only when the
    /// occurrence evidence map carries every one of the fired trigger's declared evidence
    /// field names with a present, non-empty value. Driving the guard off the declared
    /// fields is what makes each trigger require ITS OWN evidence rather than a single
    /// hardcoded ratification shape.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    private static void EmitEvidenceCompletenessHelper(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns true when the fork occurrence evidence map carries every one of the");
        sb.AppendLine("    /// fired trigger's declared evidence field names with a present, non-empty value");
        sb.AppendLine("    /// (the DR-8 per-trigger occurrence-completeness check).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    private static bool ForkEvidenceComplete(");
        sb.AppendLine("        System.Collections.Generic.IReadOnlyDictionary<string, string> evidence,");
        sb.AppendLine("        params string[] requiredFields)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (evidence is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var field in requiredFields)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!evidence.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))");
        sb.AppendLine("            {");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return true;");
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
        var countVar = fork.CountPropertyName;

        // Every moniker below is authored in user DSL string literals (anchors, evidence
        // field names, the compensation seed), so it may contain a double-quote or a
        // backslash. Emit each as a SymbolDisplay.FormatLiteral quote-wrapped literal so a
        // hostile value cannot break out of the generated string literal (M5) - the same
        // pattern StepStartHandlerEmitter uses for the validation message.
        var seedLiteral = SymbolDisplay.FormatLiteral(fork.CompensationSeedMoniker, quote: true);

        sb.AppendLine($"        // Diagnostic fork edge {edgeIndex} - admissible at anchor(s): {string.Join(", ", fork.AnchorStepMonikers)}.");
        sb.Append("        if (");
        for (var a = 0; a < fork.AnchorStepMonikers.Count; a++)
        {
            if (a > 0)
            {
                sb.AppendLine();
                sb.Append("            || ");
            }

            var anchorLiteral = SymbolDisplay.FormatLiteral(fork.AnchorStepMonikers[a], quote: true);
            sb.Append($"cmd.Anchor == {anchorLiteral}");
        }

        sb.AppendLine(")");
        sb.AppendLine("        {");

        // Permitted-trigger + per-trigger evidence-completeness guard (DR-8 occurrence
        // chokepoint). The permitted check is the disjunction of the edge's triggers; the
        // evidence check is a per-trigger switch requiring every one of the fired trigger's
        // declared RequiredEvidenceFields, so each trigger requires its own evidence.
        sb.AppendLine("            // Occurrence-completeness guard (DR-8): admit only a permitted trigger whose");
        sb.AppendLine("            // occurrence evidence map carries the fields THAT trigger declared; a fork");
        sb.AppendLine("            // without a permitted trigger or missing its declared evidence is refused.");
        sb.Append($"            var {permittedVar} =");
        for (var t = 0; t < fork.PermittedTriggers.Count; t++)
        {
            var wireLiteral = SymbolDisplay.FormatLiteral(PascalToSnake(fork.PermittedTriggers[t].TriggerName), quote: true);
            if (t > 0)
            {
                sb.AppendLine();
                sb.Append($"                || cmd.Trigger == {wireLiteral}");
            }
            else
            {
                sb.Append($" cmd.Trigger == {wireLiteral}");
            }
        }

        sb.AppendLine(";");
        sb.AppendLine($"            var {evidenceVar} = cmd.Trigger switch");
        sb.AppendLine("            {");
        for (var t = 0; t < fork.PermittedTriggers.Count; t++)
        {
            var trigger = fork.PermittedTriggers[t];
            var wireLiteral = SymbolDisplay.FormatLiteral(PascalToSnake(trigger.TriggerName), quote: true);
            var fieldArgs = string.Join(
                ", ",
                trigger.RequiredEvidenceFields.Select(f => SymbolDisplay.FormatLiteral(f, quote: true)));
            sb.AppendLine($"                {wireLiteral} => ForkEvidenceComplete(cmd.Evidence, {fieldArgs}),");
        }

        sb.AppendLine("                _ => false,");
        sb.AppendLine("            };");
        sb.AppendLine($"            if (!{permittedVar} || !{evidenceVar})");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogWarning(");
        sb.AppendLine("                    \"Refusing diagnostic fork for workflow {WorkflowId}: trigger {Trigger} is not permitted or its declared evidence is incomplete\",");
        sb.AppendLine("                    WorkflowId,");
        sb.AppendLine("                    cmd.Trigger);");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();

        // Per-edge maxForks bound (L3; the loop MaxIterations forced-exit precedent). Each
        // edge counts against its OWN tally so a high-bound edge cannot starve a low-bound
        // one out of a shared pool.
        sb.AppendLine("            // maxForks bound (per edge; the loop MaxIterations forced-exit precedent): once");
        sb.AppendLine("            // THIS edge's bound is reached, an overflowing fork routes to the blocked terminal.");
        sb.AppendLine($"            if ({countVar} >= {fork.MaxForks})");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogWarning(");
        sb.AppendLine("                    \"Diagnostic fork bound {Bound} reached for workflow {WorkflowId}; routing to blocked terminal for human escalation\",");
        sb.AppendLine($"                    {fork.MaxForks},");
        sb.AppendLine("                    WorkflowId);");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.ForkBlocked;");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            {countVar}++;");

        if (model.IsEventSourced)
        {
            sb.AppendLine();
            sb.AppendLine("            // Append the WorkflowForked audit stream event at the single decision site,");
            sb.AppendLine("            // carrying the fired trigger's own evidence map (DR-8 occurrence payload).");
            sb.AppendLine("            session.Events.Append(");
            sb.AppendLine("                WorkflowId,");
            sb.AppendLine($"                new {eventName}(");
            sb.AppendLine("                    WorkflowId,");
            sb.AppendLine($"                    \"{SchemaVersionLiteral}\",");
            sb.AppendLine("                    cmd.Trigger,");
            sb.AppendLine("                    cmd.Evidence,");
            sb.AppendLine("                    DateTimeOffset.UtcNow));");
        }

        sb.AppendLine();
        sb.AppendLine("            logger.LogInformation(");
        sb.AppendLine("                \"Diagnostic fork admitted for workflow {WorkflowId} on trigger {Trigger}; seeding compensation to {Seed}\",");
        sb.AppendLine("                WorkflowId,");
        sb.AppendLine("                cmd.Trigger,");
        sb.AppendLine($"                {seedLiteral});");

        if (canSeedCompensation)
        {
            sb.AppendLine();
            sb.AppendLine("            // Seed compensation via the merged Compensate/OnFailure trigger site (#140):");
            sb.AppendLine("            // the fork routes rollback to its declared compensation seed.");
            sb.AppendLine($"            yield return new {triggerCommandName}(");
            sb.AppendLine("                WorkflowId,");
            sb.AppendLine($"                {seedLiteral},");
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
