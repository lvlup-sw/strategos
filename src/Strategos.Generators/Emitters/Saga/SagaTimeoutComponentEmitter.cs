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
///     A <c>Handle({Phase}Timeout)</c> method with an idempotent race guard. Legacy
///     programs retain their phase guard. Typed programs use the durable completion
///     journal plus the timeout's compiled structural identity, because one global
///     phase cannot distinguish concurrent fork lanes or loop iterations.
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
        var topology = CompensationTopology.UsesDerivedRuntime(model)
            ? CompensationTopology.Build(model)
            : null;

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
            EmitTimeoutMessageRecord(sb, model, phaseName, stepModel.Timeout);

            sb.AppendLine();
            EmitTimeoutHandler(
                sb,
                model,
                phaseName,
                topology?.Occurrences
                    .Where(occurrence => string.Equals(
                        occurrence.PhaseName,
                        phaseName,
                        StringComparison.Ordinal))
                    .OrderBy(static occurrence => occurrence.StableKey, StringComparer.Ordinal)
                    .ToList() ?? []);
        }
    }

    /// <summary>
    /// Emits the timeout message record that derives from Wolverine's
    /// <c>TimeoutMessage</c> base. Wolverine auto-schedules its delivery from the
    /// base constructor's <see cref="System.TimeSpan"/>.
    /// </summary>
    private static void EmitTimeoutMessageRecord(
        StringBuilder sb,
        WorkflowModel model,
        string phaseName,
        TimeoutModel timeout)
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
        if (!CompensationTopology.UsesDerivedRuntime(model))
        {
            sb.AppendLine($"        : TimeoutMessage(System.TimeSpan.FromTicks({ticks}L));");
            return;
        }

        sb.AppendLine($"        : TimeoutMessage(System.TimeSpan.FromTicks({ticks}L))");
        sb.AppendLine("    {");
        sb.AppendLine("        public string? ForwardOccurrenceKey { get; init; }");
        sb.AppendLine();
        sb.AppendLine("        public string? CompensationScopeKey { get; init; }");
        sb.AppendLine();
        sb.AppendLine("        public string? CompensationScopeKind { get; init; }");
        sb.AppendLine();
        sb.AppendLine("        public string? CompensationLaneKey { get; init; }");
        sb.AppendLine();
        sb.AppendLine("        public string? CompensationForkId { get; init; }");
        sb.AppendLine();
        sb.AppendLine("        public int? CompensationForkPathIndex { get; init; }");
        sb.AppendLine();
        sb.AppendLine("        public long? CompensationJournalSequenceAtDispatch { get; init; }");
        sb.AppendLine();
        sb.AppendLine("        public Guid? ForwardExecutionId { get; init; }");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits the saga handler for a step's timeout message. Legacy handlers retain
    /// their phase guard; typed handlers use journal and topology identity.
    /// </summary>
    private static void EmitTimeoutHandler(
        StringBuilder sb,
        WorkflowModel model,
        string phaseName,
        IReadOnlyList<CompensationOccurrence> occurrences)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var usesDerivedRuntime = CompensationTopology.UsesDerivedRuntime(model);

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {phaseName} step timeout and routes a live deadline");
        sb.AppendLine("    /// to the failure path while ignoring a completion that won the race.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"t\">The {phaseName} timeout message.</param>");
        if (usesDerivedRuntime)
        {
            StateApplicationHelper.EmitSessionParameterDoc(sb, model);
        }

        sb.AppendLine("    /// <param name=\"logger\">The injected logger.</param>");
        sb.AppendLine(usesDerivedRuntime
            ? "    public IEnumerable<object> Handle("
            : "    public void Handle(");
        sb.AppendLine($"        {phaseName}Timeout t,");
        if (usesDerivedRuntime)
        {
            StateApplicationHelper.EmitSessionParameter(sb, model);
        }

        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(t, nameof(t));");
        if (usesDerivedRuntime)
        {
            StateApplicationHelper.EmitSessionGuard(sb, model);
        }

        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        if (!usesDerivedRuntime)
        {
            sb.AppendLine("        // Race guard: if the step already completed, the saga's Phase has");
            sb.AppendLine("        // advanced past this step and the timeout is a no-op (the Completed");
            sb.AppendLine("        // event won the race).");
            sb.AppendLine($"        if (Phase != {model.PhaseEnumName}.{phaseName})");
            sb.AppendLine("        {");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("        // Phase is global saga state and cannot identify one concurrent fork");
            sb.AppendLine("        // lane. The per-dispatch journal high-water and structural identity");
            sb.AppendLine("        // decide the race without suppressing a live sibling-lane timeout.");
            sb.AppendLine($"        if (HasCompleted{phaseName}AfterTimeoutDispatch(t))");
            sb.AppendLine("        {");
            sb.AppendLine("            yield break;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // A loop can re-enter the same phase before an earlier iteration's");
            sb.AppendLine("        // timeout is delivered. Only the currently bound scope may fail.");
            sb.AppendLine($"        if (!MatchesCurrent{phaseName}TimeoutTopology(t))");
            sb.AppendLine("        {");
            sb.AppendLine("            logger.LogWarning(");
            sb.AppendLine("                \"Ignoring stale or invalid timeout for step {StepName} in workflow {WorkflowId}\",");
            sb.AppendLine($"                {Literal(phaseName)},");
            sb.AppendLine("                WorkflowId);");
            sb.AppendLine("            yield break;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        logger.LogWarning(");
        sb.AppendLine("            \"Step {StepName} timed out for workflow {WorkflowId}; routing to failure path\",");
        sb.AppendLine($"            \"{phaseName}\",");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        if (usesDerivedRuntime)
        {
            sb.AppendLine($"        var failure = new Trigger{model.PascalName}FailureHandlerCommand(");
            sb.AppendLine("            WorkflowId,");
            sb.AppendLine($"            Resolve{phaseName}TimeoutStepName(t.ForwardOccurrenceKey),");
            sb.AppendLine("            \"Step deadline elapsed.\",");
            sb.AppendLine("            nameof(TimeoutException),");
            sb.AppendLine("            null)");
            sb.AppendLine("        {");
            sb.AppendLine("            ForwardOccurrenceKey = t.ForwardOccurrenceKey,");
            sb.AppendLine("            CompensationScopeKey = t.CompensationScopeKey,");
            sb.AppendLine("            CompensationScopeKind = t.CompensationScopeKind,");
            sb.AppendLine("            CompensationLaneKey = t.CompensationLaneKey,");
            sb.AppendLine("            CompensationForkId = t.CompensationForkId,");
            sb.AppendLine("            CompensationForkPathIndex = t.CompensationForkPathIndex,");
            sb.AppendLine("            CompensationJournalSequenceAtDispatch = t.CompensationJournalSequenceAtDispatch,");
            sb.AppendLine("            FailedForwardExecutionId = t.ForwardExecutionId,");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        foreach (var message in Handle(");
            sb.AppendLine("            failure,");
            if (model.IsEventSourced)
            {
                sb.AppendLine("            session,");
            }

            sb.AppendLine("            logger))");
            sb.AppendLine("        {");
            sb.AppendLine("            yield return message;");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine("        MarkCompleted();");
        }

        sb.AppendLine("    }");

        if (usesDerivedRuntime)
        {
            sb.AppendLine();
            EmitCompletedAfterTimeoutDispatchMatcher(sb, phaseName);
            sb.AppendLine();
            EmitTimeoutStepNameResolver(sb, phaseName, occurrences);
            sb.AppendLine();
            EmitCurrentTimeoutTopologyMatcher(sb, phaseName, occurrences);
        }
    }

    private static void EmitTimeoutStepNameResolver(
        StringBuilder sb,
        string phaseName,
        IReadOnlyList<CompensationOccurrence> occurrences)
    {
        sb.AppendLine($"    private static string Resolve{phaseName}TimeoutStepName(string? occurrenceKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        return occurrenceKey switch");
        sb.AppendLine("        {");
        foreach (var occurrence in occurrences)
        {
            sb.AppendLine($"            {Literal(occurrence.StableKey)} => {Literal(occurrence.Step.StepName)},");
        }

        sb.AppendLine("            _ => \"unresolved\",");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void EmitCompletedAfterTimeoutDispatchMatcher(
        StringBuilder sb,
        string phaseName)
    {
        sb.AppendLine($"    private bool HasCompleted{phaseName}AfterTimeoutDispatch({phaseName}Timeout timeout)");
        sb.AppendLine("    {");
        sb.AppendLine("        return CompensationJournal is not null");
        sb.AppendLine("            && timeout.CompensationJournalSequenceAtDispatch is long journalHighWater");
        sb.AppendLine("            && CompensationJournal.Any(entry =>");
        sb.AppendLine("                MatchesCompensationJournalTopology(entry)");
        sb.AppendLine("                && entry.Sequence > journalHighWater");
        sb.AppendLine("                && string.Equals(entry.OccurrenceKey, timeout.ForwardOccurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(entry.ScopeKey, timeout.CompensationScopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(entry.LaneKey, timeout.CompensationLaneKey, StringComparison.Ordinal));");
        sb.AppendLine("    }");
    }

    private static void EmitCurrentTimeoutTopologyMatcher(
        StringBuilder sb,
        string phaseName,
        IReadOnlyList<CompensationOccurrence> occurrences)
    {
        sb.AppendLine($"    private bool MatchesCurrent{phaseName}TimeoutTopology({phaseName}Timeout timeout)");
        sb.AppendLine("    {");
        sb.AppendLine("        return timeout.ForwardOccurrenceKey switch");
        sb.AppendLine("        {");
        foreach (var occurrence in occurrences)
        {
            sb.AppendLine($"            {Literal(occurrence.StableKey)} =>");
            sb.AppendLine($"                string.Equals(timeout.CompensationScopeKey, ResolveCompensationScopeInstance({Literal(occurrence.Scope.TemplateKey)}), StringComparison.Ordinal)");
            sb.AppendLine($"                && string.Equals(timeout.CompensationScopeKind, {Literal(occurrence.Scope.Kind.ToString())}, StringComparison.Ordinal)");
            sb.AppendLine($"                && {NullableStringMatch("timeout.CompensationLaneKey", occurrence.Scope.LaneKey)}");
            sb.AppendLine($"                && {NullableStringMatch("timeout.CompensationForkId", occurrence.Scope.ForkId)}");
            sb.AppendLine($"                && timeout.CompensationForkPathIndex == {NullableIntLiteral(occurrence.Scope.ForkPathIndex)}");
            sb.AppendLine("                && timeout.CompensationJournalSequenceAtDispatch is not null");
            sb.AppendLine("                && timeout.ForwardExecutionId is not null,");
        }

        sb.AppendLine("            _ => false,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static string NullableStringMatch(string expression, string? value) => value is null
        ? $"{expression} is null"
        : $"string.Equals({expression}, {Literal(value)}, StringComparison.Ordinal)";

    private static string NullableIntLiteral(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    private static string Literal(string value) =>
        SymbolDisplay.FormatLiteral(value, quote: true);
}
