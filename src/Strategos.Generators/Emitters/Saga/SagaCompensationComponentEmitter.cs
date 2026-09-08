// -----------------------------------------------------------------------
// <copyright file="SagaCompensationComponentEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits durable, topology-aware inverse execution for a generated saga.
/// </summary>
/// <remarks>
/// Completed forward occurrences are journaled on the persisted saga. A failure
/// unwinds only its concrete innermost scope. Sequential scopes run in reverse
/// order; a fork first quiesces, preserves its structural lanes, and conservatively
/// folds their inverses through the shared state one at a time.
/// Inverse messages have distinct completion/failure routes and a stable rollback id,
/// so they cannot advance forward flow or recursively enter compensation.
/// </remarks>
internal sealed class SagaCompensationComponentEmitter : ISagaComponentEmitter
{
    internal const long DefaultTimeoutTicks = 3_000_000_000L;

    /// <inheritdoc />
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        if (!model.HasCompensation)
        {
            return;
        }

        if (!CompensationTopology.UsesDerivedRuntime(model))
        {
            EmitLegacyCompensation(sb, model);
            return;
        }

        var topology = CompensationTopology.Build(model);
        var inverseStepNames = model.CompensationSteps
            .Select(step => NamingHelper.GetSimpleTypeName(step.Compensation!.CompensationStepTypeName))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        sb.AppendLine();
        EmitJournalTypes(sb, model);
        sb.AppendLine();
        EmitScopeInstanceResolver(sb, model);
        sb.AppendLine();
        EmitScopeMembership(sb);
        sb.AppendLine();
        EmitJournalWriter(sb, model);
        sb.AppendLine();
        EmitTopologyValidators(sb, model, topology);
        sb.AppendLine();
        EmitForwardDispatchClaimHelpers(sb, model);
        sb.AppendLine();
        EmitFailureTriggerClaimHelpers(sb, model);
        sb.AppendLine();
        EmitTriggerHandler(sb, model, topology);
        sb.AppendLine();
        EmitRollbackPlanner(sb, model, inverseStepNames);

        foreach (var inverseStepName in inverseStepNames)
        {
            sb.AppendLine();
            EmitRollbackCompletedHandler(sb, model, inverseStepName);
        }

        sb.AppendLine();
        EmitRollbackFailedHandler(sb, model);
        sb.AppendLine();
        EmitRollbackTimeoutHandler(sb, model);
    }

    private static void EmitJournalTypes(StringBuilder sb, WorkflowModel model)
    {
        var stateType = model.StateTypeName ?? "object";

        sb.AppendLine("    /// <summary>Durable state of one completed forward occurrence.</summary>");
        sb.AppendLine("    public sealed class CompensationJournalEntry");
        sb.AppendLine("    {");
        EmitRequiredProperty(sb, "long", "Sequence");
        EmitRequiredProperty(sb, "Guid", "ForwardExecutionId");
        EmitRequiredProperty(sb, "Guid", "RollbackId");
        EmitRequiredProperty(sb, "string", "OccurrenceKey");
        EmitRequiredProperty(sb, "string", "ScopeKey");
        EmitRequiredProperty(sb, "string", "ScopeKind");
        EmitRequiredProperty(sb, "int", "ScopeOrdinal");
        EmitNullableProperty(sb, "string", "LaneKey");
        EmitNullableProperty(sb, "string", "ForkId");
        EmitNullableProperty(sb, "int", "ForkPathIndex");
        EmitRequiredProperty(sb, "string", "ForwardStepName");
        EmitNullableProperty(sb, "string", "InverseStepName");
        EmitNullableProperty(sb, "string", "ForwardActionIdentity");
        EmitNullableProperty(sb, "string", "InverseActionIdentity");
        EmitRequiredProperty(sb, "bool", "UsesIdentityInverse");
        EmitRequiredProperty(sb, "long", "InverseTimeoutTicks");
        EmitRequiredProperty(sb, stateType, "RollbackState");
        sb.AppendLine("        /// <summary>Gets or sets the durable execution status.</summary>");
        sb.AppendLine("        public string Status { get; set; } = \"Completed\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Durable authority for one dispatched forward occurrence.</summary>");
        sb.AppendLine("    public sealed class ForwardDispatchClaim");
        sb.AppendLine("    {");
        EmitRequiredProperty(sb, "Guid", "ForwardExecutionId");
        EmitRequiredProperty(sb, "string", "OccurrenceKey");
        EmitRequiredProperty(sb, "string", "ScopeKey");
        EmitRequiredProperty(sb, "string", "ScopeKind");
        EmitNullableProperty(sb, "string", "LaneKey");
        EmitNullableProperty(sb, "string", "ForkId");
        EmitNullableProperty(sb, "int", "ForkPathIndex");
        EmitRequiredProperty(sb, "string", "ForwardStepName");
        EmitRequiredProperty(sb, "long", "JournalSequenceAtDispatch");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Durable authority for one accepted failure trigger.</summary>");
        sb.AppendLine("    public sealed class FailureTriggerClaim");
        sb.AppendLine("    {");
        EmitRequiredProperty(sb, "Guid", "ForwardExecutionId");
        EmitRequiredProperty(sb, "string", "OccurrenceKey");
        EmitRequiredProperty(sb, "string", "ScopeKey");
        EmitRequiredProperty(sb, "string", "ScopeKind");
        EmitNullableProperty(sb, "string", "LaneKey");
        EmitNullableProperty(sb, "string", "ForkId");
        EmitNullableProperty(sb, "int", "ForkPathIndex");
        EmitRequiredProperty(sb, "string", "ForwardStepName");
        EmitRequiredProperty(sb, "long", "JournalSequenceAtDispatch");
        EmitRequiredProperty(sb, "string", "FailureKind");
        EmitRequiredProperty(sb, "bool", "FailureOccurredAfterForwardCompletion");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Fail-safe deadline for one inverse execution.</summary>");
        sb.AppendLine("    public sealed record CompensationRollbackTimeout(");
        sb.AppendLine("        [property: Wolverine.Persistence.Sagas.SagaIdentity] Guid WorkflowId,");
        sb.AppendLine("        Guid RollbackId,");
        sb.AppendLine("        long JournalSequence,");
        sb.AppendLine("        long TimeoutTicks)");
        sb.AppendLine("        : TimeoutMessage(TimeSpan.FromTicks(TimeoutTicks));");
    }

    private static void EmitRequiredProperty(StringBuilder sb, string type, string name)
    {
        sb.AppendLine($"        /// <summary>Gets or sets {name}.</summary>");
        sb.AppendLine($"        public required {type} {name} {{ get; set; }}");
    }

    private static void EmitNullableProperty(StringBuilder sb, string type, string name)
    {
        sb.AppendLine($"        /// <summary>Gets or sets {name}, when applicable.</summary>");
        sb.AppendLine($"        public {type}? {name} {{ get; set; }}");
    }

    private static void EmitScopeInstanceResolver(StringBuilder sb, WorkflowModel model)
    {
        sb.AppendLine("    private string ResolveCompensationScopeInstance(string template)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(template, nameof(template));");
        sb.AppendLine("        var resolved = template;");

        if (model.Loops is not null)
        {
            foreach (var loop in model.Loops
                .GroupBy(static value => value.IterationPropertyName, StringComparer.Ordinal)
                .Select(static values => values.First()))
            {
                sb.AppendLine("        resolved = resolved.Replace(");
                sb.AppendLine($"            {Literal($"{{{loop.IterationPropertyName}}}")},");
                sb.AppendLine($"            {loop.IterationPropertyName}.ToString(System.Globalization.CultureInfo.InvariantCulture),");
                sb.AppendLine("            StringComparison.Ordinal);");
            }
        }

        sb.AppendLine("        return resolved;");
        sb.AppendLine("    }");
    }

    private static void EmitJournalWriter(StringBuilder sb, WorkflowModel model)
    {
        var stateType = model.StateTypeName ?? "object";

        sb.AppendLine("    private void RecordCompensationCompletion(");
        sb.AppendLine("        Guid forwardExecutionId,");
        sb.AppendLine("        string occurrenceKey,");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine("        string scopeKind,");
        sb.AppendLine("        int scopeOrdinal,");
        sb.AppendLine("        string? laneKey,");
        sb.AppendLine("        string? forkId,");
        sb.AppendLine("        int? forkPathIndex,");
        sb.AppendLine("        string forwardStepName,");
        sb.AppendLine("        string? inverseStepName,");
        sb.AppendLine("        string? forwardActionIdentity,");
        sb.AppendLine("        string? inverseActionIdentity,");
        sb.AppendLine("        bool usesIdentityInverse,");
        sb.AppendLine("        long inverseTimeoutTicks,");
        sb.AppendLine($"        {stateType} rollbackState)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (CompensationJournal is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal is missing from persisted compensation state; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (forwardExecutionId == Guid.Empty)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Forward completion has an empty execution identity; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal changed or became corrupt before a forward completion; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var dispatchClaim = ForwardDispatchClaims.FirstOrDefault(claim =>");
        sb.AppendLine("            claim.ForwardExecutionId == forwardExecutionId);");
        sb.AppendLine("        if (dispatchClaim is null");
        sb.AppendLine("            || !string.Equals(dispatchClaim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("            || !string.Equals(dispatchClaim.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("            || !string.Equals(dispatchClaim.ScopeKind, scopeKind, StringComparison.Ordinal)");
        sb.AppendLine("            || !string.Equals(dispatchClaim.LaneKey, laneKey, StringComparison.Ordinal)");
        sb.AppendLine("            || !string.Equals(dispatchClaim.ForkId, forkId, StringComparison.Ordinal)");
        sb.AppendLine("            || dispatchClaim.ForkPathIndex != forkPathIndex");
        sb.AppendLine("            || !string.Equals(dispatchClaim.ForwardStepName, forwardStepName, StringComparison.Ordinal))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Forward completion lacks its exact durable dispatch authority; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var existing = CompensationJournal.FirstOrDefault(entry =>");
        sb.AppendLine("            entry.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("            || (string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(entry.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)));");
        sb.AppendLine("        if (existing is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (existing.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("                && string.Equals(existing.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(existing.OccurrenceKey, occurrenceKey, StringComparison.Ordinal))");
        sb.AppendLine("            {");
        sb.AppendLine("                // Idempotent redelivery of the same durable forward completion.");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            CompensationFailureMessage = \"Forward completion identity contradicts the durable compensation journal; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        CompensationJournalSequence++;");
        sb.AppendLine("        CompensationJournal.Add(new CompensationJournalEntry");
        sb.AppendLine("        {");
        sb.AppendLine("            Sequence = CompensationJournalSequence,");
        sb.AppendLine("            ForwardExecutionId = forwardExecutionId,");
        sb.AppendLine("            RollbackId = CreateCompensationRollbackId(forwardExecutionId),");
        sb.AppendLine("            OccurrenceKey = occurrenceKey,");
        sb.AppendLine("            ScopeKey = scopeKey,");
        sb.AppendLine("            ScopeKind = scopeKind,");
        sb.AppendLine("            ScopeOrdinal = scopeOrdinal,");
        sb.AppendLine("            LaneKey = laneKey,");
        sb.AppendLine("            ForkId = forkId,");
        sb.AppendLine("            ForkPathIndex = forkPathIndex,");
        sb.AppendLine("            ForwardStepName = forwardStepName,");
        sb.AppendLine("            InverseStepName = inverseStepName,");
        sb.AppendLine("            ForwardActionIdentity = forwardActionIdentity,");
        sb.AppendLine("            InverseActionIdentity = inverseActionIdentity,");
        sb.AppendLine("            UsesIdentityInverse = usesIdentityInverse,");
        sb.AppendLine("            InverseTimeoutTicks = inverseTimeoutTicks,");
        sb.AppendLine("            RollbackState = rollbackState,");
        sb.AppendLine("        });");
        sb.AppendLine("        _ = ForwardDispatchClaims.Remove(dispatchClaim);");
        sb.AppendLine("    }");
    }

    private static void EmitScopeMembership(StringBuilder sb)
    {
        sb.AppendLine("    private static bool IsWithinCompensationScope(");
        sb.AppendLine("        string candidateScopeKey,");
        sb.AppendLine("        string selectedScopeKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        return string.Equals(candidateScopeKey, selectedScopeKey, StringComparison.Ordinal)");
        sb.AppendLine("            || candidateScopeKey.StartsWith(selectedScopeKey + \"/\", StringComparison.Ordinal);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static Guid CreateCompensationRollbackId(Guid forwardExecutionId)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Cycle the non-empty GUID space by one. This is a stable, collision-free");
        sb.AppendLine("        // permutation whose rollback identity always differs from its forward identity.");
        sb.AppendLine("        var bytes = forwardExecutionId.ToByteArray();");
        sb.AppendLine("        if (bytes.All(value => value == byte.MaxValue))");
        sb.AppendLine("        {");
        sb.AppendLine("            Array.Clear(bytes, 0, bytes.Length);");
        sb.AppendLine("            bytes[0] = 1;");
        sb.AppendLine("            return new Guid(bytes);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        for (var index = 0; index < bytes.Length; index++)");
        sb.AppendLine("        {");
        sb.AppendLine("            bytes[index]++;");
        sb.AppendLine("            if (bytes[index] != 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                break;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return new Guid(bytes);");
        sb.AppendLine("    }");
    }

    private static void EmitTopologyValidators(
        StringBuilder sb,
        WorkflowModel model,
        CompensationTopology topology)
    {
        sb.AppendLine("    private bool MatchesCompensationTriggerTopology(");
        sb.AppendLine("        string occurrenceKey,");
        sb.AppendLine("        string failedStepName,");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine("        string scopeKind,");
        sb.AppendLine("        string? laneKey,");
        sb.AppendLine("        string? forkId,");
        sb.AppendLine("        int? forkPathIndex)");
        sb.AppendLine("    {");
        sb.AppendLine("        return occurrenceKey switch");
        sb.AppendLine("        {");
        foreach (var occurrence in topology.Occurrences)
        {
            var loopBounds = LoopBoundsArguments(model, occurrence.Scope.TemplateKey);
            sb.AppendLine($"            {Literal(occurrence.StableKey)} =>");
            sb.AppendLine($"                string.Equals(failedStepName, {Literal(occurrence.Step.StepName)}, StringComparison.Ordinal)");
            sb.AppendLine($"                && string.Equals(scopeKey, ResolveCompensationScopeInstance({Literal(occurrence.Scope.TemplateKey)}), StringComparison.Ordinal)");
            sb.AppendLine($"                && MatchesCompensationScopeTemplate(scopeKey, {Literal(occurrence.Scope.TemplateKey)}{loopBounds})");
            sb.AppendLine($"                && string.Equals(scopeKind, {Literal(occurrence.Scope.Kind.ToString())}, StringComparison.Ordinal)");
            sb.AppendLine($"                && {NullableStringMatch("laneKey", occurrence.Scope.LaneKey)}");
            sb.AppendLine($"                && {NullableStringMatch("forkId", occurrence.Scope.ForkId)}");
            sb.AppendLine($"                && forkPathIndex == {NullableIntLiteral(occurrence.Scope.ForkPathIndex)},");
        }

        sb.AppendLine("            _ => false,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool IsKnownCompensationJournalStatus(string status)");
        sb.AppendLine("    {");
        sb.AppendLine("        return status is \"Completed\" or \"InProgress\" or \"RolledBack\" or \"Failed\" or \"OutcomeUnknown\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool MatchesCompensationScopeTemplate(");
        sb.AppendLine("        string candidate,");
        sb.AppendLine("        string template,");
        sb.AppendLine("        params int[] inclusiveLoopBounds)");
        sb.AppendLine("    {");
        sb.AppendLine("        var candidateIndex = 0;");
        sb.AppendLine("        var templateIndex = 0;");
        sb.AppendLine("        var loopBoundIndex = 0;");
        sb.AppendLine("        while (templateIndex < template.Length)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (template[templateIndex] != '{')");
        sb.AppendLine("            {");
        sb.AppendLine("                if (candidateIndex >= candidate.Length");
        sb.AppendLine("                    || candidate[candidateIndex] != template[templateIndex])");
        sb.AppendLine("                {");
        sb.AppendLine("                    return false;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                candidateIndex++;");
        sb.AppendLine("                templateIndex++;");
        sb.AppendLine("                continue;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var closeBrace = template.IndexOf('}', templateIndex + 1);");
        sb.AppendLine("            if (closeBrace < 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var digitStart = candidateIndex;");
        sb.AppendLine("            var value = 0;");
        sb.AppendLine("            while (candidateIndex < candidate.Length");
        sb.AppendLine("                && candidate[candidateIndex] >= '0'");
        sb.AppendLine("                && candidate[candidateIndex] <= '9')");
        sb.AppendLine("            {");
        sb.AppendLine("                var digit = candidate[candidateIndex] - '0';");
        sb.AppendLine("                if (value > (int.MaxValue - digit) / 10)");
        sb.AppendLine("                {");
        sb.AppendLine("                    return false;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                value = (value * 10) + digit;");
        sb.AppendLine("                candidateIndex++;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (candidateIndex == digitStart");
        sb.AppendLine("                || (candidateIndex - digitStart > 1 && candidate[digitStart] == '0')");
        sb.AppendLine("                || loopBoundIndex >= inclusiveLoopBounds.Length");
        sb.AppendLine("                || value > inclusiveLoopBounds[loopBoundIndex])");
        sb.AppendLine("            {");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            loopBoundIndex++;");
        sb.AppendLine("            templateIndex = closeBrace + 1;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return candidateIndex == candidate.Length");
        sb.AppendLine("            && loopBoundIndex == inclusiveLoopBounds.Length;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool MatchesCompensationJournalTopology(CompensationJournalEntry? entry)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (entry is null");
        sb.AppendLine("            || string.IsNullOrEmpty(entry.OccurrenceKey)");
        sb.AppendLine("            || string.IsNullOrEmpty(entry.ScopeKey)");
        sb.AppendLine("            || string.IsNullOrEmpty(entry.ScopeKind)");
        sb.AppendLine("            || string.IsNullOrEmpty(entry.ForwardStepName)");
        sb.AppendLine("            || string.IsNullOrEmpty(entry.Status)");
        sb.AppendLine("            || entry.Sequence <= 0");
        sb.AppendLine("            || entry.ForwardExecutionId == Guid.Empty");
        sb.AppendLine("            || entry.RollbackId == Guid.Empty");
        sb.AppendLine("            || entry.RollbackId != CreateCompensationRollbackId(entry.ForwardExecutionId)");
        sb.AppendLine("            || entry.InverseTimeoutTicks <= 0");
        sb.AppendLine("            || !IsKnownCompensationJournalStatus(entry.Status))");
        sb.AppendLine("        {");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return entry.OccurrenceKey switch");
        sb.AppendLine("        {");
        foreach (var occurrence in topology.Occurrences)
        {
            var inverseTimeoutTicks = occurrence.Step.Compensation?.Timeout?.Ticks
                ?? DefaultTimeoutTicks;
            var loopBounds = LoopBoundsArguments(model, occurrence.Scope.TemplateKey);
            sb.AppendLine($"            {Literal(occurrence.StableKey)} =>");
            sb.AppendLine($"                MatchesCompensationScopeTemplate(entry.ScopeKey, {Literal(occurrence.Scope.TemplateKey)}{loopBounds})");
            sb.AppendLine($"                && string.Equals(entry.ScopeKind, {Literal(occurrence.Scope.Kind.ToString())}, StringComparison.Ordinal)");
            sb.AppendLine($"                && entry.ScopeOrdinal == {occurrence.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine($"                && {NullableStringMatch("entry.LaneKey", occurrence.Scope.LaneKey)}");
            sb.AppendLine($"                && {NullableStringMatch("entry.ForkId", occurrence.Scope.ForkId)}");
            sb.AppendLine($"                && entry.ForkPathIndex == {NullableIntLiteral(occurrence.Scope.ForkPathIndex)}");
            sb.AppendLine($"                && string.Equals(entry.ForwardStepName, {Literal(occurrence.Step.StepName)}, StringComparison.Ordinal)");
            sb.AppendLine($"                && {NullableStringMatch("entry.InverseStepName", occurrence.InverseStepName)}");
            sb.AppendLine($"                && {NullableStringMatch("entry.ForwardActionIdentity", occurrence.ForwardActionIdentity)}");
            sb.AppendLine($"                && {NullableStringMatch("entry.InverseActionIdentity", occurrence.InverseActionIdentity)}");
            sb.AppendLine($"                && entry.UsesIdentityInverse == {(occurrence.UsesIdentityInverse ? "true" : "false")}");
            sb.AppendLine($"                && entry.InverseTimeoutTicks == {inverseTimeoutTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)}L,");
        }

        sb.AppendLine("            _ => false,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private string? ExpectedCompensationScopeKey(string occurrenceKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        return occurrenceKey switch");
        sb.AppendLine("        {");
        foreach (var occurrence in topology.Occurrences)
        {
            sb.AppendLine($"            {Literal(occurrence.StableKey)} => ResolveCompensationScopeInstance({Literal(occurrence.Scope.TemplateKey)}),");
        }

        sb.AppendLine("            _ => null,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static string? ExpectedCompensationForkId(string occurrenceKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        return occurrenceKey switch");
        sb.AppendLine("        {");
        foreach (var occurrence in topology.Occurrences)
        {
            sb.AppendLine($"            {Literal(occurrence.StableKey)} => {NullableLiteral(occurrence.Scope.ForkId)},");
        }

        sb.AppendLine("            _ => null,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool HasCoherentActiveCompensation()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (FailedCompensationOccurrenceKey is null");
        sb.AppendLine("            || ActiveCompensationScopeKey is null");
        sb.AppendLine("            || !string.Equals(");
        sb.AppendLine("                ActiveCompensationScopeKey,");
        sb.AppendLine("                ExpectedCompensationScopeKey(FailedCompensationOccurrenceKey),");
        sb.AppendLine("                StringComparison.Ordinal))");
        sb.AppendLine("        {");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var inProgress = CompensationJournal");
        sb.AppendLine("            .Where(entry => string.Equals(entry.Status, \"InProgress\", StringComparison.Ordinal))");
        sb.AppendLine("            .ToList();");
        sb.AppendLine("        return inProgress.Count == 1");
        sb.AppendLine("            && IsWithinCompensationScope(inProgress[0].ScopeKey, ActiveCompensationScopeKey);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool IsOnFailedCompensationForkPath(string occurrenceKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        return occurrenceKey switch");
        sb.AppendLine("        {");
        foreach (var occurrence in topology.Occurrences.Where(static occurrence =>
            occurrence.Scope.ForkId is not null
            && occurrence.Scope.ForkPathIndex is not null))
        {
            var forkId = Sanitize(occurrence.Scope.ForkId!);
            var pathIndex = occurrence.Scope.ForkPathIndex!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"            {Literal(occurrence.StableKey)} => Fork_{forkId}_Path{pathIndex}Status == Strategos.Definitions.ForkPathStatus.Failed,");
        }

        sb.AppendLine("            _ => false,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool ShouldIgnoreForwardCompletion(");
        sb.AppendLine("        string? occurrenceKey,");
        sb.AppendLine("        string? scopeKey,");
        sb.AppendLine("        Guid forwardExecutionId)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (CompensationRollbackFinished");
        sb.AppendLine("            || CompensationOutcomeUnknown");
        sb.AppendLine("            || CompensationFailureMessage is not null");
        sb.AppendLine("            || ActiveCompensationScopeKey is not null");
        sb.AppendLine("            || CompensationJournal?.Any(entry => entry?.Status is \"Failed\" or \"OutcomeUnknown\") == true)");
        sb.AppendLine("        {");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (CompensationJournal is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal is missing from persisted compensation state; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal changed or became corrupt before a forward result; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (forwardExecutionId == Guid.Empty)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Forward completion has an empty execution identity; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var completedExecution = CompensationJournal.FirstOrDefault(entry =>");
        sb.AppendLine("            entry.ForwardExecutionId == forwardExecutionId);");
        sb.AppendLine("        if (completedExecution is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (occurrenceKey is not null");
        sb.AppendLine("                && !string.Equals(completedExecution.OccurrenceKey, occurrenceKey, StringComparison.Ordinal))");
        sb.AppendLine("            {");
        sb.AppendLine("                CompensationFailureMessage = \"Forward completion claim contradicts the durable compensation journal; saga retained.\";");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("                return true;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // The persisted execution identity is authoritative. In a loop, the mutable");
        sb.AppendLine("            // counter may now resolve the same occurrence to a later concrete scope instance.");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var occupiedOccurrence = occurrenceKey is null || scopeKey is null");
        sb.AppendLine("            ? null");
        sb.AppendLine("            : CompensationJournal.FirstOrDefault(entry =>");
        sb.AppendLine("                string.Equals(entry.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal));");
        sb.AppendLine("        if (occupiedOccurrence is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Forward completion claim contradicts the durable compensation journal; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (occurrenceKey is not null");
        sb.AppendLine("            && PendingCompensationForkId is not null");
        sb.AppendLine("            && (string.Equals(FailedCompensationOccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("                || IsOnFailedCompensationForkPath(occurrenceKey)))");
        sb.AppendLine("        {");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var dispatchClaim = ForwardDispatchClaims.FirstOrDefault(claim =>");
        sb.AppendLine("            claim.ForwardExecutionId == forwardExecutionId);");
        sb.AppendLine("        if (dispatchClaim is null");
        sb.AppendLine("            || (occurrenceKey is not null");
        sb.AppendLine("                && (!string.Equals(dispatchClaim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("                    || !string.Equals(dispatchClaim.ScopeKey, scopeKey, StringComparison.Ordinal))))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Forward completion lacks its exact durable dispatch authority; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return false;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool ShouldIgnoreForwardStart(string occurrenceKey, string scopeKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        var compensationJournal = CompensationJournal;");
        sb.AppendLine("        if (CompensationRollbackFinished");
        sb.AppendLine("            || CompensationOutcomeUnknown");
        sb.AppendLine("            || CompensationFailureMessage is not null");
        sb.AppendLine("            || ActiveCompensationScopeKey is not null");
        sb.AppendLine("            || compensationJournal?.Any(entry => entry?.Status is \"Failed\" or \"OutcomeUnknown\") == true)");
        sb.AppendLine("        {");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (compensationJournal is null");
        sb.AppendLine("            || !HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal changed or became corrupt before a forward start; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return ForwardDispatchClaims.Any(claim =>");
        sb.AppendLine("                string.Equals(claim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(claim.ScopeKey, scopeKey, StringComparison.Ordinal))");
        sb.AppendLine("            || compensationJournal.Any(entry =>");
        sb.AppendLine("                string.Equals(entry.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal));");
        sb.AppendLine("    }");
    }

    private static void EmitTriggerHandler(
        StringBuilder sb,
        WorkflowModel model,
        CompensationTopology topology)
    {
        var triggerName = $"Trigger{model.PascalName}FailureHandlerCommand";

        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {triggerName} cmd,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine("        if (CompensationJournal is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal is missing from persisted compensation state; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (CompensationJournalSchemaVersion != 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Typed rollback journal was not initialized by the #169 runtime; saga retained for migration.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal or failure-authority ledger changed or became corrupt before rollback; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (CompensationRollbackFinished");
        sb.AppendLine("            || CompensationOutcomeUnknown");
        sb.AppendLine("            || CompensationFailureMessage is not null");
        sb.AppendLine("            || CompensationJournal.Any(entry => entry.Status is \"Failed\" or \"OutcomeUnknown\"))");
        sb.AppendLine("        {");
        sb.AppendLine("            // A terminal rollback result is monotonic under trigger redelivery.");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();

        if (CompensationTopology.GetProgramKind(model) == CompensationProgramKind.Mixed)
        {
            sb.AppendLine("        CompensationFailureMessage = \"Compensation declarations are mixed, dynamic, or unresolved and cannot form one derived rollback program; saga retained.\";");
            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine("        yield break;");
            sb.AppendLine();
        }

        sb.AppendLine("        if (string.IsNullOrEmpty(cmd.ForwardOccurrenceKey)");
        sb.AppendLine("            || string.IsNullOrEmpty(cmd.CompensationScopeKey)");
        sb.AppendLine("            || string.IsNullOrEmpty(cmd.CompensationScopeKind)");
        sb.AppendLine("            || string.IsNullOrEmpty(cmd.ExceptionType)");
        sb.AppendLine("            || cmd.CompensationJournalSequenceAtDispatch is not long journalHighWater)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Typed rollback trigger is missing #169 topology metadata; migrate the publisher and retain the saga.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var scopeKey = cmd.CompensationScopeKey;");
        sb.AppendLine("        var scopeKind = cmd.CompensationScopeKind;");
        sb.AppendLine("        if (!MatchesCompensationTriggerTopology(");
        sb.AppendLine("                cmd.ForwardOccurrenceKey,");
        sb.AppendLine("                cmd.FailedStepName,");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                scopeKind,");
        sb.AppendLine("                cmd.CompensationLaneKey,");
        sb.AppendLine("                cmd.CompensationForkId,");
        sb.AppendLine("                cmd.CompensationForkPathIndex))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Typed rollback trigger contradicts the compiled compensation topology; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (cmd.FailedForwardExecutionId is not Guid failedForwardExecutionId)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Typed rollback trigger lacks a durable failure execution identity; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        ForwardDispatchClaim? failureDispatchClaim = null;");
        sb.AppendLine("        FailureTriggerClaim? pendingPostCompletionClaim = null;");
        sb.AppendLine("        FailureTriggerClaim? consumedFailureClaim;");
        sb.AppendLine("        if (!cmd.FailureOccurredAfterForwardCompletion)");
        sb.AppendLine("        {");
        sb.AppendLine("            failureDispatchClaim = FindForwardDispatchClaim(");
        sb.AppendLine("                failedForwardExecutionId,");
        sb.AppendLine("                cmd.ForwardOccurrenceKey,");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                scopeKind,");
        sb.AppendLine("                cmd.CompensationLaneKey,");
        sb.AppendLine("                cmd.CompensationForkId,");
        sb.AppendLine("                cmd.CompensationForkPathIndex,");
        sb.AppendLine("                cmd.FailedStepName,");
        sb.AppendLine("                journalHighWater);");
        sb.AppendLine("            consumedFailureClaim = FindFailureTriggerClaim(");
        sb.AppendLine("                ConsumedFailureTriggerClaims,");
        sb.AppendLine("                failedForwardExecutionId,");
        sb.AppendLine("                cmd.ForwardOccurrenceKey,");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                scopeKind,");
        sb.AppendLine("                cmd.CompensationLaneKey,");
        sb.AppendLine("                cmd.CompensationForkId,");
        sb.AppendLine("                cmd.CompensationForkPathIndex,");
        sb.AppendLine("                cmd.FailedStepName,");
        sb.AppendLine("                journalHighWater,");
        sb.AppendLine("                cmd.ExceptionType,");
        sb.AppendLine("                failureOccurredAfterForwardCompletion: false);");
        sb.AppendLine("            if (failureDispatchClaim is null && consumedFailureClaim is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                CompensationFailureMessage = \"Typed rollback trigger does not match an active durable forward dispatch; saga retained.\";");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            pendingPostCompletionClaim = FindFailureTriggerClaim(");
        sb.AppendLine("                PendingPostCompletionFailureClaims,");
        sb.AppendLine("                failedForwardExecutionId,");
        sb.AppendLine("                cmd.ForwardOccurrenceKey,");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                scopeKind,");
        sb.AppendLine("                cmd.CompensationLaneKey,");
        sb.AppendLine("                cmd.CompensationForkId,");
        sb.AppendLine("                cmd.CompensationForkPathIndex,");
        sb.AppendLine("                cmd.FailedStepName,");
        sb.AppendLine("                journalHighWater,");
        sb.AppendLine("                cmd.ExceptionType,");
        sb.AppendLine("                failureOccurredAfterForwardCompletion: true);");
        sb.AppendLine("            consumedFailureClaim = FindFailureTriggerClaim(");
        sb.AppendLine("                ConsumedFailureTriggerClaims,");
        sb.AppendLine("                failedForwardExecutionId,");
        sb.AppendLine("                cmd.ForwardOccurrenceKey,");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                scopeKind,");
        sb.AppendLine("                cmd.CompensationLaneKey,");
        sb.AppendLine("                cmd.CompensationForkId,");
        sb.AppendLine("                cmd.CompensationForkPathIndex,");
        sb.AppendLine("                cmd.FailedStepName,");
        sb.AppendLine("                journalHighWater,");
        sb.AppendLine("                cmd.ExceptionType,");
        sb.AppendLine("                failureOccurredAfterForwardCompletion: true);");
        sb.AppendLine("            if (pendingPostCompletionClaim is null && consumedFailureClaim is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                CompensationFailureMessage = \"Post-completion rollback trigger lacks its exact saga-minted capability; saga retained.\";");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (journalHighWater < 0");
        sb.AppendLine("            || CompensationJournalSequence < journalHighWater");
        sb.AppendLine("            || !HasCompleteCompensationJournalThrough(journalHighWater))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal high-water mark is missing or corrupt for a typed rollback program; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var expectedPrefixCount = ExpectedCompletedPrefixCount(");
        sb.AppendLine("            cmd.ForwardOccurrenceKey,");
        sb.AppendLine("            cmd.FailureOccurredAfterForwardCompletion);");
        sb.AppendLine("        if (expectedPrefixCount < 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback occurrence is absent from the closed topology; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var recordedPrefixCount = CompensationJournal.Count(entry =>");
        sb.AppendLine("            string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.LaneKey, cmd.CompensationLaneKey, StringComparison.Ordinal));");
        sb.AppendLine("        if (recordedPrefixCount != expectedPrefixCount)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal is missing history for a typed rollback program; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var isForkFailure = string.Equals(scopeKind, \"Fork\", StringComparison.Ordinal)");
        sb.AppendLine("            && cmd.CompensationForkId is not null;");
        sb.AppendLine("        if (ActiveCompensationScopeKey is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!string.Equals(ActiveCompensationScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                || !HasCoherentActiveCompensation()");
        sb.AppendLine("                || failureDispatchClaim is not null");
        sb.AppendLine("                || pendingPostCompletionClaim is not null");
        sb.AppendLine("                || consumedFailureClaim is null");
        sb.AppendLine("                || FailedCompensationOccurrenceKey is null");
        sb.AppendLine("                || (!isForkFailure && !string.Equals(");
        sb.AppendLine("                    FailedCompensationOccurrenceKey,");
        sb.AppendLine("                    cmd.ForwardOccurrenceKey,");
        sb.AppendLine("                    StringComparison.Ordinal))");
        sb.AppendLine("                || (isForkFailure && !string.Equals(");
        sb.AppendLine("                    cmd.CompensationForkId,");
        sb.AppendLine("                    ExpectedCompensationForkId(FailedCompensationOccurrenceKey),");
        sb.AppendLine("                    StringComparison.Ordinal)))");
        sb.AppendLine("            {");
        sb.AppendLine("                CompensationFailureMessage = \"A second rollback scope was requested while compensation was active; saga retained.\";");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Idempotent trigger redelivery never dispatches an inverse twice.");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (PendingCompensationScopeKey is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!string.Equals(PendingCompensationScopeKey, scopeKey, StringComparison.Ordinal))");
        sb.AppendLine("            {");
        sb.AppendLine("                CompensationFailureMessage = \"A second rollback scope was requested while fork quiescence was pending; saga retained.\";");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (!isForkFailure");
        sb.AppendLine("                || PendingCompensationForkId is null");
        sb.AppendLine("                || !string.Equals(PendingCompensationForkId, cmd.CompensationForkId, StringComparison.Ordinal)");
        sb.AppendLine("                || FailedCompensationOccurrenceKey is null");
        sb.AppendLine("                || !string.Equals(");
        sb.AppendLine("                    scopeKey,");
        sb.AppendLine("                    ExpectedCompensationScopeKey(FailedCompensationOccurrenceKey),");
        sb.AppendLine("                    StringComparison.Ordinal)");
        sb.AppendLine("                || !string.Equals(");
        sb.AppendLine("                    PendingCompensationForkId,");
        sb.AppendLine("                    ExpectedCompensationForkId(FailedCompensationOccurrenceKey),");
        sb.AppendLine("                    StringComparison.Ordinal))");
        sb.AppendLine("            {");
        sb.AppendLine("                CompensationFailureMessage = \"Pending rollback state does not match the compiled fork failure; saga retained.\";");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (failureDispatchClaim is not null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ConsumeForwardFailureClaim(failureDispatchClaim, cmd.ExceptionType);");
        sb.AppendLine("            }");
        sb.AppendLine("            else if (pendingPostCompletionClaim is not null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ConsumePostCompletionFailureClaim(pendingPostCompletionClaim);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // A distinct failing lane joins the same durable fork rollback claim.");
        sb.AppendLine("            MarkFailedForkPath(cmd.CompensationForkId!, cmd.CompensationForkPathIndex);");
        sb.AppendLine();
        sb.AppendLine("            if (PendingCompensationForkId is null");
        sb.AppendLine("                || !IsCompensationForkQuiescent(PendingCompensationForkId))");
        sb.AppendLine("            {");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            foreach (var message in BeginCompensationScope(scopeKey, logger))");
        sb.AppendLine("            {");
        sb.AppendLine("                yield return message;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (FailedCompensationOccurrenceKey is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback claim metadata is inconsistent with the active compensation state; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (failureDispatchClaim is null && pendingPostCompletionClaim is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Initial rollback claim lacks unconsumed durable failure authority; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (failureDispatchClaim is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            ConsumeForwardFailureClaim(failureDispatchClaim, cmd.ExceptionType);");
        sb.AppendLine("        }");
        sb.AppendLine("        else if (pendingPostCompletionClaim is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            ConsumePostCompletionFailureClaim(pendingPostCompletionClaim);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        FailedCompensationOccurrenceKey = cmd.ForwardOccurrenceKey;");
        sb.AppendLine("        FailedStepName = cmd.FailedStepName;");
        sb.AppendLine("        FailureExceptionMessage = cmd.ExceptionMessage;");
        sb.AppendLine("        FailureExceptionType = cmd.ExceptionType;");
        sb.AppendLine("        FailureStackTrace = cmd.StackTrace;");
        sb.AppendLine("        FailureTimestamp = DateTimeOffset.UtcNow;");
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Compensating;");

        if (model.IsEventSourced)
        {
            sb.AppendLine("        session.Events.Append(");
            sb.AppendLine("            WorkflowId,");
            sb.AppendLine($"            new {model.PascalName}StepFailed(");
            sb.AppendLine("                WorkflowId,");
            sb.AppendLine("                cmd.FailedStepName,");
            sb.AppendLine("                cmd.ExceptionType,");
            sb.AppendLine("                cmd.ExceptionMessage,");
            sb.AppendLine("                FailureTimestamp.Value));");
        }

        sb.AppendLine();
        sb.AppendLine("        if (isForkFailure)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Every failing lane must become terminal before the selected fork can unwind.");
        sb.AppendLine("            MarkFailedForkPath(cmd.CompensationForkId!, cmd.CompensationForkPathIndex);");
        sb.AppendLine("            PendingCompensationForkId = cmd.CompensationForkId;");
        sb.AppendLine("            PendingCompensationScopeKey = scopeKey;");
        sb.AppendLine("            if (!IsCompensationForkQuiescent(cmd.CompensationForkId!))");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogWarning(");
        sb.AppendLine("                    \"Fork {ForkId} failed for workflow {WorkflowId}; waiting for path quiescence\",");
        sb.AppendLine("                    cmd.CompensationForkId,");
        sb.AppendLine("                    WorkflowId);");
        sb.AppendLine("                yield break;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var message in BeginCompensationScope(scopeKey, logger))");
        sb.AppendLine("        {");
        sb.AppendLine("            yield return message;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        EmitExpectedPrefixCount(sb, topology);
        sb.AppendLine();
        EmitJournalContinuityHelper(sb);
        sb.AppendLine();
        EmitForkQuiescenceHelpers(sb, model);
        sb.AppendLine();
        EmitForkJournalValidationHelpers(sb, model, topology);
    }

    private static void EmitForwardDispatchClaimHelpers(StringBuilder sb, WorkflowModel model)
    {
        sb.AppendLine("    private bool HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("    {");
        sb.AppendLine("        return ForwardDispatchClaims is not null");
        sb.AppendLine("            && ForwardDispatchClaims.All(claim =>");
        sb.AppendLine("                claim is not null");
        sb.AppendLine("                && claim.ForwardExecutionId != Guid.Empty");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.OccurrenceKey)");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.ScopeKey)");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.ScopeKind)");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.ForwardStepName)");
        sb.AppendLine("                && claim.JournalSequenceAtDispatch >= 0");
        sb.AppendLine("                && claim.JournalSequenceAtDispatch <= CompensationJournalSequence");
        sb.AppendLine("                && MatchesCompensationTriggerTopology(");
        sb.AppendLine("                    claim.OccurrenceKey,");
        sb.AppendLine("                    claim.ForwardStepName,");
        sb.AppendLine("                    claim.ScopeKey,");
        sb.AppendLine("                    claim.ScopeKind,");
        sb.AppendLine("                    claim.LaneKey,");
        sb.AppendLine("                    claim.ForkId,");
        sb.AppendLine("                    claim.ForkPathIndex))");
        sb.AppendLine("            && ForwardDispatchClaims.Select(claim => claim.ForwardExecutionId).Distinct().Count() == ForwardDispatchClaims.Count");
        sb.AppendLine("            && ForwardDispatchClaims");
        sb.AppendLine("                .GroupBy(");
        sb.AppendLine("                    claim => claim.ScopeKey + \"\\u001f\" + claim.OccurrenceKey,");
        sb.AppendLine("                    StringComparer.Ordinal)");
        sb.AppendLine("                .All(group => group.Count() == 1)");
        sb.AppendLine("            && !ForwardDispatchClaims.Any(claim => CompensationJournal.Any(entry =>");
        sb.AppendLine("                string.Equals(entry.ScopeKey, claim.ScopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(entry.OccurrenceKey, claim.OccurrenceKey, StringComparison.Ordinal)));");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private ForwardDispatchClaim? FindForwardDispatchClaim(");
        sb.AppendLine("        Guid forwardExecutionId,");
        sb.AppendLine("        string occurrenceKey,");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine("        string scopeKind,");
        sb.AppendLine("        string? laneKey,");
        sb.AppendLine("        string? forkId,");
        sb.AppendLine("        int? forkPathIndex,");
        sb.AppendLine("        string forwardStepName,");
        sb.AppendLine("        long journalSequenceAtDispatch)");
        sb.AppendLine("    {");
        sb.AppendLine("        return ForwardDispatchClaims.FirstOrDefault(claim =>");
        sb.AppendLine("            claim.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("            && string.Equals(claim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.ScopeKind, scopeKind, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.LaneKey, laneKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.ForkId, forkId, StringComparison.Ordinal)");
        sb.AppendLine("            && claim.ForkPathIndex == forkPathIndex");
        sb.AppendLine("            && string.Equals(claim.ForwardStepName, forwardStepName, StringComparison.Ordinal)");
        sb.AppendLine("            && claim.JournalSequenceAtDispatch == journalSequenceAtDispatch);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool TryRecordForwardDispatch(");
        sb.AppendLine("        Guid forwardExecutionId,");
        sb.AppendLine("        string occurrenceKey,");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine("        string scopeKind,");
        sb.AppendLine("        string? laneKey,");
        sb.AppendLine("        string? forkId,");
        sb.AppendLine("        int? forkPathIndex,");
        sb.AppendLine("        string forwardStepName)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims()");
        sb.AppendLine("            || forwardExecutionId == Guid.Empty");
        sb.AppendLine("            || !MatchesCompensationTriggerTopology(");
        sb.AppendLine("                occurrenceKey,");
        sb.AppendLine("                forwardStepName,");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                scopeKind,");
        sb.AppendLine("                laneKey,");
        sb.AppendLine("                forkId,");
        sb.AppendLine("                forkPathIndex)");
        sb.AppendLine("            || ForwardDispatchClaims.Any(claim =>");
        sb.AppendLine("                claim.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("                || (string.Equals(claim.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                    && string.Equals(claim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)))");
        sb.AppendLine("            || CompensationJournal.Any(entry =>");
        sb.AppendLine("                string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(entry.OccurrenceKey, occurrenceKey, StringComparison.Ordinal))");
        sb.AppendLine("            || PendingPostCompletionFailureClaims.Any(claim =>");
        sb.AppendLine("                claim.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("                || (string.Equals(claim.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                    && string.Equals(claim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)))");
        sb.AppendLine("            || ConsumedFailureTriggerClaims.Any(claim =>");
        sb.AppendLine("                claim.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("                || (string.Equals(claim.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                    && string.Equals(claim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal))))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Forward dispatch could not establish a unique durable authority claim; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        ForwardDispatchClaims.Add(new ForwardDispatchClaim");
        sb.AppendLine("        {");
        sb.AppendLine("            ForwardExecutionId = forwardExecutionId,");
        sb.AppendLine("            OccurrenceKey = occurrenceKey,");
        sb.AppendLine("            ScopeKey = scopeKey,");
        sb.AppendLine("            ScopeKind = scopeKind,");
        sb.AppendLine("            LaneKey = laneKey,");
        sb.AppendLine("            ForkId = forkId,");
        sb.AppendLine("            ForkPathIndex = forkPathIndex,");
        sb.AppendLine("            ForwardStepName = forwardStepName,");
        sb.AppendLine("            JournalSequenceAtDispatch = CompensationJournalSequence,");
        sb.AppendLine("        });");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
    }

    private static void EmitFailureTriggerClaimHelpers(StringBuilder sb, WorkflowModel model)
    {
        sb.AppendLine("    private bool HasStructurallyValidFailureTriggerClaims()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (PendingPostCompletionFailureClaims is null");
        sb.AppendLine("            || ConsumedFailureTriggerClaims is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var allClaims = PendingPostCompletionFailureClaims");
        sb.AppendLine("            .Concat(ConsumedFailureTriggerClaims)");
        sb.AppendLine("            .ToList();");
        sb.AppendLine("        var selectedScope = FailedCompensationOccurrenceKey is null");
        sb.AppendLine("            ? null");
        sb.AppendLine("            : ExpectedCompensationScopeKey(FailedCompensationOccurrenceKey);");
        sb.AppendLine("        return PendingPostCompletionFailureClaims.All(claim =>");
        sb.AppendLine("                claim is not null");
        sb.AppendLine("                && claim.FailureOccurredAfterForwardCompletion)");
        sb.AppendLine("            && allClaims.All(claim =>");
        sb.AppendLine("                claim is not null");
        sb.AppendLine("                && claim.ForwardExecutionId != Guid.Empty");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.OccurrenceKey)");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.ScopeKey)");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.ScopeKind)");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.ForwardStepName)");
        sb.AppendLine("                && !string.IsNullOrEmpty(claim.FailureKind)");
        sb.AppendLine("                && claim.JournalSequenceAtDispatch >= 0");
        sb.AppendLine("                && claim.JournalSequenceAtDispatch <= CompensationJournalSequence");
        sb.AppendLine("                && MatchesCompensationTriggerTopology(");
        sb.AppendLine("                    claim.OccurrenceKey,");
        sb.AppendLine("                    claim.ForwardStepName,");
        sb.AppendLine("                    claim.ScopeKey,");
        sb.AppendLine("                    claim.ScopeKind,");
        sb.AppendLine("                    claim.LaneKey,");
        sb.AppendLine("                    claim.ForkId,");
        sb.AppendLine("                    claim.ForkPathIndex)");
        sb.AppendLine("                && (claim.FailureOccurredAfterForwardCompletion");
        sb.AppendLine("                    ? CompensationJournal.Any(entry =>");
        sb.AppendLine("                        FailureClaimMatchesJournalEntry(claim, entry)");
        sb.AppendLine("                        && entry.Sequence <= claim.JournalSequenceAtDispatch)");
        sb.AppendLine("                    : !CompensationJournal.Any(entry =>");
        sb.AppendLine("                        entry.ForwardExecutionId == claim.ForwardExecutionId");
        sb.AppendLine("                        || (string.Equals(entry.ScopeKey, claim.ScopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                            && string.Equals(entry.OccurrenceKey, claim.OccurrenceKey, StringComparison.Ordinal)))))");
        sb.AppendLine("            && PendingPostCompletionFailureClaims.All(claim =>");
        sb.AppendLine("                CompensationJournal.Any(entry =>");
        sb.AppendLine("                    FailureClaimMatchesJournalEntry(claim, entry)");
        sb.AppendLine("                    && string.Equals(entry.Status, \"Completed\", StringComparison.Ordinal)))");
        sb.AppendLine("            && allClaims.Select(claim => claim.ForwardExecutionId).Distinct().Count() == allClaims.Count");
        sb.AppendLine("            && allClaims");
        sb.AppendLine("                .GroupBy(");
        sb.AppendLine("                    claim => claim.ScopeKey + \"\\u001f\" + claim.OccurrenceKey,");
        sb.AppendLine("                    StringComparer.Ordinal)");
        sb.AppendLine("                .All(group => group.Count() == 1)");
        sb.AppendLine("            && !ForwardDispatchClaims.Any(active => allClaims.Any(claim =>");
        sb.AppendLine("                active.ForwardExecutionId == claim.ForwardExecutionId");
        sb.AppendLine("                || (string.Equals(active.ScopeKey, claim.ScopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                    && string.Equals(active.OccurrenceKey, claim.OccurrenceKey, StringComparison.Ordinal))))");
        sb.AppendLine("            && ((FailedCompensationOccurrenceKey is null");
        sb.AppendLine("                    && ConsumedFailureTriggerClaims.Count == 0)");
        sb.AppendLine("                || (selectedScope is not null");
        sb.AppendLine("                    && ConsumedFailureTriggerClaims.All(claim =>");
        sb.AppendLine("                        IsWithinCompensationScope(claim.ScopeKey, selectedScope))));");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool FailureClaimMatchesJournalEntry(");
        sb.AppendLine("        FailureTriggerClaim claim,");
        sb.AppendLine("        CompensationJournalEntry entry)");
        sb.AppendLine("    {");
        sb.AppendLine("        return entry.ForwardExecutionId == claim.ForwardExecutionId");
        sb.AppendLine("            && string.Equals(entry.OccurrenceKey, claim.OccurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.ScopeKey, claim.ScopeKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.ScopeKind, claim.ScopeKind, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.LaneKey, claim.LaneKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.ForkId, claim.ForkId, StringComparison.Ordinal)");
        sb.AppendLine("            && entry.ForkPathIndex == claim.ForkPathIndex");
        sb.AppendLine("            && string.Equals(entry.ForwardStepName, claim.ForwardStepName, StringComparison.Ordinal);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static FailureTriggerClaim? FindFailureTriggerClaim(");
        sb.AppendLine("        IEnumerable<FailureTriggerClaim> claims,");
        sb.AppendLine("        Guid forwardExecutionId,");
        sb.AppendLine("        string occurrenceKey,");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine("        string scopeKind,");
        sb.AppendLine("        string? laneKey,");
        sb.AppendLine("        string? forkId,");
        sb.AppendLine("        int? forkPathIndex,");
        sb.AppendLine("        string forwardStepName,");
        sb.AppendLine("        long journalSequenceAtDispatch,");
        sb.AppendLine("        string failureKind,");
        sb.AppendLine("        bool failureOccurredAfterForwardCompletion)");
        sb.AppendLine("    {");
        sb.AppendLine("        return claims.FirstOrDefault(claim =>");
        sb.AppendLine("            claim.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("            && string.Equals(claim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.ScopeKind, scopeKind, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.LaneKey, laneKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(claim.ForkId, forkId, StringComparison.Ordinal)");
        sb.AppendLine("            && claim.ForkPathIndex == forkPathIndex");
        sb.AppendLine("            && string.Equals(claim.ForwardStepName, forwardStepName, StringComparison.Ordinal)");
        sb.AppendLine("            && claim.JournalSequenceAtDispatch == journalSequenceAtDispatch");
        sb.AppendLine("            && string.Equals(claim.FailureKind, failureKind, StringComparison.Ordinal)");
        sb.AppendLine("            && claim.FailureOccurredAfterForwardCompletion == failureOccurredAfterForwardCompletion);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool TryMintPostCompletionFailureClaim(");
        sb.AppendLine("        string occurrenceKey,");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine("        string scopeKind,");
        sb.AppendLine("        string? laneKey,");
        sb.AppendLine("        string? forkId,");
        sb.AppendLine("        int? forkPathIndex,");
        sb.AppendLine("        string forwardStepName,");
        sb.AppendLine("        string failureKind,");
        sb.AppendLine("        out FailureTriggerClaim failureClaim)");
        sb.AppendLine("    {");
        sb.AppendLine("        failureClaim = null!;");
        sb.AppendLine("        if (!HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims()");
        sb.AppendLine("            || string.IsNullOrEmpty(failureKind)");
        sb.AppendLine("            || !MatchesCompensationTriggerTopology(");
        sb.AppendLine("                occurrenceKey,");
        sb.AppendLine("                forwardStepName,");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                scopeKind,");
        sb.AppendLine("                laneKey,");
        sb.AppendLine("                forkId,");
        sb.AppendLine("                forkPathIndex))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Post-completion failure could not establish valid durable authority; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var existingClaim = PendingPostCompletionFailureClaims");
        sb.AppendLine("            .Concat(ConsumedFailureTriggerClaims)");
        sb.AppendLine("            .FirstOrDefault(claim =>");
        sb.AppendLine("                claim.FailureOccurredAfterForwardCompletion");
        sb.AppendLine("                && string.Equals(claim.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("                && string.Equals(claim.ScopeKey, scopeKey, StringComparison.Ordinal));");
        sb.AppendLine("        if (existingClaim is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!string.Equals(existingClaim.FailureKind, failureKind, StringComparison.Ordinal))");
        sb.AppendLine("            {");
        sb.AppendLine("                CompensationFailureMessage = \"Post-completion failure kind contradicts its durable authority; saga retained.\";");
        sb.AppendLine($"                Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            failureClaim = existingClaim;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var completedEntry = CompensationJournal.FirstOrDefault(entry =>");
        sb.AppendLine("            string.Equals(entry.Status, \"Completed\", StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.ScopeKind, scopeKind, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.LaneKey, laneKey, StringComparison.Ordinal)");
        sb.AppendLine("            && string.Equals(entry.ForkId, forkId, StringComparison.Ordinal)");
        sb.AppendLine("            && entry.ForkPathIndex == forkPathIndex");
        sb.AppendLine("            && string.Equals(entry.ForwardStepName, forwardStepName, StringComparison.Ordinal));");
        sb.AppendLine("        if (completedEntry is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Post-completion failure has no exact completed journal authority; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        failureClaim = new FailureTriggerClaim");
        sb.AppendLine("        {");
        sb.AppendLine("            ForwardExecutionId = completedEntry.ForwardExecutionId,");
        sb.AppendLine("            OccurrenceKey = occurrenceKey,");
        sb.AppendLine("            ScopeKey = scopeKey,");
        sb.AppendLine("            ScopeKind = scopeKind,");
        sb.AppendLine("            LaneKey = laneKey,");
        sb.AppendLine("            ForkId = forkId,");
        sb.AppendLine("            ForkPathIndex = forkPathIndex,");
        sb.AppendLine("            ForwardStepName = forwardStepName,");
        sb.AppendLine("            JournalSequenceAtDispatch = CompensationJournalSequence,");
        sb.AppendLine("            FailureKind = failureKind,");
        sb.AppendLine("            FailureOccurredAfterForwardCompletion = true,");
        sb.AppendLine("        };");
        sb.AppendLine("        PendingPostCompletionFailureClaims.Add(failureClaim);");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void ConsumeForwardFailureClaim(ForwardDispatchClaim claim, string failureKind)");
        sb.AppendLine("    {");
        sb.AppendLine("        ConsumedFailureTriggerClaims.Add(new FailureTriggerClaim");
        sb.AppendLine("        {");
        sb.AppendLine("            ForwardExecutionId = claim.ForwardExecutionId,");
        sb.AppendLine("            OccurrenceKey = claim.OccurrenceKey,");
        sb.AppendLine("            ScopeKey = claim.ScopeKey,");
        sb.AppendLine("            ScopeKind = claim.ScopeKind,");
        sb.AppendLine("            LaneKey = claim.LaneKey,");
        sb.AppendLine("            ForkId = claim.ForkId,");
        sb.AppendLine("            ForkPathIndex = claim.ForkPathIndex,");
        sb.AppendLine("            ForwardStepName = claim.ForwardStepName,");
        sb.AppendLine("            JournalSequenceAtDispatch = claim.JournalSequenceAtDispatch,");
        sb.AppendLine("            FailureKind = failureKind,");
        sb.AppendLine("            FailureOccurredAfterForwardCompletion = false,");
        sb.AppendLine("        });");
        sb.AppendLine("        _ = ForwardDispatchClaims.Remove(claim);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void ConsumePostCompletionFailureClaim(FailureTriggerClaim claim)");
        sb.AppendLine("    {");
        sb.AppendLine("        _ = PendingPostCompletionFailureClaims.Remove(claim);");
        sb.AppendLine("        ConsumedFailureTriggerClaims.Add(claim);");
        sb.AppendLine("    }");
    }

    private static void EmitJournalContinuityHelper(StringBuilder sb)
    {
        sb.AppendLine("    private bool HasCompleteCompensationJournalThrough(long highWater)");
        sb.AppendLine("    {");
        sb.AppendLine("        var sequences = CompensationJournal");
        sb.AppendLine("            .Where(entry => entry.Sequence <= highWater)");
        sb.AppendLine("            .Select(entry => entry.Sequence)");
        sb.AppendLine("            .OrderBy(sequence => sequence)");
        sb.AppendLine("            .ToList();");
        sb.AppendLine("        if ((long)sequences.Count != highWater)");
        sb.AppendLine("        {");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        for (var index = 0; index < sequences.Count; index++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (sequences[index] != index + 1L)");
        sb.AppendLine("            {");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool HasStructurallyValidCompensationJournal()");
        sb.AppendLine("    {");
        sb.AppendLine("        return CompensationJournal is not null");
        sb.AppendLine("            && CompensationJournalSchemaVersion == 1");
        sb.AppendLine("            && CompensationJournalSequence >= 0");
        sb.AppendLine("            && CompensationJournal.LongCount() == CompensationJournalSequence");
        sb.AppendLine("            && CompensationJournal.All(MatchesCompensationJournalTopology)");
        sb.AppendLine("            && HasCompleteCompensationJournalThrough(CompensationJournalSequence)");
        sb.AppendLine("            && HasCanonicalCompensationScopeHistories()");
        sb.AppendLine("            && HasUniqueCompensationJournalIdentities();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool HasUniqueCompensationJournalIdentities()");
        sb.AppendLine("    {");
        sb.AppendLine("        return CompensationJournal.Select(entry => entry.ForwardExecutionId).Distinct().Count() == CompensationJournal.Count");
        sb.AppendLine("            && CompensationJournal.Select(entry => entry.RollbackId).Distinct().Count() == CompensationJournal.Count");
        sb.AppendLine("            && CompensationJournal");
        sb.AppendLine("                .GroupBy(");
        sb.AppendLine("                    entry => entry.ScopeKey + \"\\u001f\" + entry.OccurrenceKey,");
        sb.AppendLine("                    StringComparer.Ordinal)");
        sb.AppendLine("                .All(group => group.Count() == 1);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool HasCanonicalCompensationScopeHistories()");
        sb.AppendLine("    {");
        sb.AppendLine("        var histories = CompensationJournal.GroupBy(");
        sb.AppendLine("            entry => entry.ScopeKey + \"\\u001f\" + (entry.LaneKey ?? string.Empty),");
        sb.AppendLine("            StringComparer.Ordinal);");
        sb.AppendLine("        foreach (var history in histories)");
        sb.AppendLine("        {");
        sb.AppendLine("            var ordinals = history");
        sb.AppendLine("                .OrderBy(entry => entry.Sequence)");
        sb.AppendLine("                .Select(entry => entry.ScopeOrdinal)");
        sb.AppendLine("                .ToList();");
        sb.AppendLine("            if (!IsContiguousScopeHistory(ordinals, expectedCompletedCount: null, allowEmpty: false))");
        sb.AppendLine("            {");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
    }

    private static void EmitExpectedPrefixCount(StringBuilder sb, CompensationTopology topology)
    {
        sb.AppendLine("    private static int ExpectedCompletedPrefixCount(");
        sb.AppendLine("        string occurrenceKey,");
        sb.AppendLine("        bool failureOccurredAfterForwardCompletion)");
        sb.AppendLine("    {");
        sb.AppendLine("        return occurrenceKey switch");
        sb.AppendLine("        {");

        foreach (var occurrence in topology.Occurrences)
        {
            var prefixCount = topology.Occurrences.Count(candidate =>
                string.Equals(candidate.Scope.TemplateKey, occurrence.Scope.TemplateKey, StringComparison.Ordinal)
                && string.Equals(candidate.Scope.LaneKey, occurrence.Scope.LaneKey, StringComparison.Ordinal)
                && candidate.Ordinal < occurrence.Ordinal);
            sb.AppendLine($"            {Literal(occurrence.StableKey)} => {prefixCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} + (failureOccurredAfterForwardCompletion ? 1 : 0),");
        }

        sb.AppendLine("            _ => -1,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void EmitForkQuiescenceHelpers(StringBuilder sb, WorkflowModel model)
    {
        sb.AppendLine("    private bool IsCompensationForkQuiescent(string forkId)");
        sb.AppendLine("    {");
        sb.AppendLine("        return forkId switch");
        sb.AppendLine("        {");
        if (model.Forks is not null)
        {
            foreach (var fork in model.Forks)
            {
                sb.AppendLine($"            {Literal(fork.ForkId)} => CheckJoinReady_{Sanitize(fork.ForkId)}(),");
            }
        }

        sb.AppendLine("            _ => false,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void MarkFailedForkPath(string forkId, int? pathIndex)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (forkId, pathIndex)");
        sb.AppendLine("        {");
        if (model.Forks is not null)
        {
            foreach (var fork in model.Forks)
            {
                foreach (var path in fork.Paths)
                {
                    sb.AppendLine($"            case ({Literal(fork.ForkId)}, {path.PathIndex}):");
                    sb.AppendLine($"                Fork_{Sanitize(fork.ForkId)}_Path{path.PathIndex}Status = Strategos.Definitions.ForkPathStatus.Failed;");
                    sb.AppendLine("                break;");
                }
            }
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                break;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void EmitForkJournalValidationHelpers(
        StringBuilder sb,
        WorkflowModel model,
        CompensationTopology topology)
    {
        sb.AppendLine("    private bool AreForkHistoriesComplete(");
        sb.AppendLine("        string selectedScopeKey,");
        sb.AppendLine("        string? pendingForkId)");
        sb.AppendLine("    {");

        if (model.Forks is not null)
        {
            for (var forkIndex = 0; forkIndex < model.Forks.Count; forkIndex++)
            {
                var fork = model.Forks[forkIndex];
                var scopeVariable = $"forkScopes{forkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var forkSegment = $"/fork:{fork.ForkId}";
                sb.AppendLine($"        var {scopeVariable} = CompensationJournal");
                sb.AppendLine("            .Where(entry => IsWithinCompensationScope(entry.ScopeKey, selectedScopeKey))");
                sb.AppendLine($"            .Select(entry => ExtractForkScopeKey(entry.ScopeKey, {Literal(forkSegment)}))");
                sb.AppendLine("            .Where(scopeKey => scopeKey is not null)");
                sb.AppendLine("            .Select(scopeKey => scopeKey!)");
                sb.AppendLine("            .Distinct(StringComparer.Ordinal)");
                sb.AppendLine("            .ToList();");
                sb.AppendLine($"        if (string.Equals(pendingForkId, {Literal(fork.ForkId)}, StringComparison.Ordinal)");
                sb.AppendLine($"            && ExtractForkScopeKey(selectedScopeKey, {Literal(forkSegment)}) is {{ }} pendingForkScope{forkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                sb.AppendLine($"            && !{scopeVariable}.Contains(pendingForkScope{forkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}, StringComparer.Ordinal))");
                sb.AppendLine("        {");
                sb.AppendLine($"            {scopeVariable}.Add(pendingForkScope{forkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        if ({scopeVariable}.Any(scopeKey => !ValidateForkJournal{forkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}(");
                sb.AppendLine("            scopeKey,");
                sb.AppendLine($"            string.Equals(pendingForkId, {Literal(fork.ForkId)}, StringComparison.Ordinal)");
                sb.AppendLine("                && string.Equals(scopeKey, selectedScopeKey, StringComparison.Ordinal))))");
                sb.AppendLine("        {");
                sb.AppendLine("            return false;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }

        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static string? ExtractForkScopeKey(");
        sb.AppendLine("        string candidateScopeKey,");
        sb.AppendLine("        string forkSegment)");
        sb.AppendLine("    {");
        sb.AppendLine("        var segmentIndex = candidateScopeKey.IndexOf(forkSegment, StringComparison.Ordinal);");
        sb.AppendLine("        if (segmentIndex < 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var scopeLength = segmentIndex + forkSegment.Length;");
        sb.AppendLine("        if (candidateScopeKey.Length > scopeLength && candidateScopeKey[scopeLength] != '/')");
        sb.AppendLine("        {");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return candidateScopeKey.Substring(0, scopeLength);");
        sb.AppendLine("    }");

        if (model.Forks is not null)
        {
            for (var forkIndex = 0; forkIndex < model.Forks.Count; forkIndex++)
            {
                var fork = model.Forks[forkIndex];
                var suffix = forkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var sanitizedId = Sanitize(fork.ForkId);
                sb.AppendLine();
                sb.AppendLine($"    private bool ValidateForkJournal{suffix}(");
                sb.AppendLine("        string scopeKey,");
                sb.AppendLine("        bool isPendingInstance)");
                sb.AppendLine("    {");
                sb.AppendLine("        return");
                for (var pathIndex = 0; pathIndex < fork.Paths.Count; pathIndex++)
                {
                    var path = fork.Paths[pathIndex];
                    var expectedCount = topology.Occurrences.Count(occurrence =>
                        occurrence.Scope.Kind == CompensationScopeKind.Fork
                        && string.Equals(occurrence.Scope.ForkId, fork.ForkId, StringComparison.Ordinal)
                        && occurrence.Scope.ForkPathIndex == path.PathIndex)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var prefix = pathIndex == 0 ? "            " : "            && ";
                    sb.AppendLine($"{prefix}(Fork_{sanitizedId}_Path{path.PathIndex}Status is Strategos.Definitions.ForkPathStatus.Success or Strategos.Definitions.ForkPathStatus.Failed)");
                    sb.AppendLine($"            && HasContiguousForkLaneHistory(");
                    sb.AppendLine("                scopeKey,");
                    sb.AppendLine($"                {path.PathIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                    sb.AppendLine("                isPendingInstance");
                    sb.AppendLine($"                    ? (Fork_{sanitizedId}_Path{path.PathIndex}Status == Strategos.Definitions.ForkPathStatus.Success");
                    sb.AppendLine($"                        && !Fork_{sanitizedId}_Path{path.PathIndex}CompensationQuiesced");
                    sb.AppendLine($"                            ? {expectedCount}");
                    sb.AppendLine("                            : (int?)null)");
                    sb.AppendLine($"                    : {expectedCount},");
                    sb.AppendLine($"                isPendingInstance");
                    sb.AppendLine($"                    && (Fork_{sanitizedId}_Path{path.PathIndex}Status == Strategos.Definitions.ForkPathStatus.Failed");
                    sb.AppendLine($"                        || Fork_{sanitizedId}_Path{path.PathIndex}CompensationQuiesced))");
                }

                sb.AppendLine("            && HasContiguousForkDescendantHistories(scopeKey)");
                sb.AppendLine("            ;");
                sb.AppendLine("    }");
            }
        }

        sb.AppendLine();
        sb.AppendLine("    private bool HasContiguousForkLaneHistory(");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine("        int pathIndex,");
        sb.AppendLine("        int? expectedCompletedCount,");
        sb.AppendLine("        bool allowEmpty)");
        sb.AppendLine("    {");
        sb.AppendLine("        var ordinals = CompensationJournal");
        sb.AppendLine("            .Where(entry => string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                && entry.ForkPathIndex == pathIndex)");
        sb.AppendLine("            .Select(entry => entry.ScopeOrdinal)");
        sb.AppendLine("            .OrderBy(ordinal => ordinal)");
        sb.AppendLine("            .ToList();");
        sb.AppendLine("        return IsContiguousScopeHistory(ordinals, expectedCompletedCount, allowEmpty);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool HasContiguousForkDescendantHistories(string forkScopeKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        var descendantGroups = CompensationJournal");
        sb.AppendLine("            .Where(entry => entry.ScopeKey.StartsWith(forkScopeKey + \"/\", StringComparison.Ordinal))");
        sb.AppendLine("            .GroupBy(");
        sb.AppendLine("                entry => entry.ScopeKey + \"\\u001f\" + (entry.LaneKey ?? string.Empty),");
        sb.AppendLine("                StringComparer.Ordinal);");
        sb.AppendLine("        return descendantGroups.All(group => IsContiguousScopeHistory(");
        sb.AppendLine("            group.Select(entry => entry.ScopeOrdinal).OrderBy(ordinal => ordinal).ToList(),");
        sb.AppendLine("            expectedCompletedCount: null,");
        sb.AppendLine("            allowEmpty: false));");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool IsContiguousScopeHistory(");
        sb.AppendLine("        IReadOnlyList<int> ordinals,");
        sb.AppendLine("        int? expectedCompletedCount,");
        sb.AppendLine("        bool allowEmpty)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (ordinals.Count == 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            return allowEmpty || expectedCompletedCount == 0;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (expectedCompletedCount is not null && ordinals.Count != expectedCompletedCount)");
        sb.AppendLine("        {");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        for (var index = 0; index < ordinals.Count; index++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (ordinals[index] != index)");
        sb.AppendLine("            {");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
    }

    private static void EmitRollbackPlanner(
        StringBuilder sb,
        WorkflowModel model,
        IReadOnlyList<string> inverseStepNames)
    {
        sb.AppendLine("    private IEnumerable<object> BeginCompensationScope(");
        sb.AppendLine("        string scopeKey,");
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        var pendingForkId = PendingCompensationForkId;");
        sb.AppendLine("        if (FailedCompensationOccurrenceKey is null");
        sb.AppendLine("            || !string.Equals(");
        sb.AppendLine("                scopeKey,");
        sb.AppendLine("                ExpectedCompensationScopeKey(FailedCompensationOccurrenceKey),");
        sb.AppendLine("                StringComparison.Ordinal)");
        sb.AppendLine("            || (pendingForkId is not null");
        sb.AppendLine("                && (!string.Equals(PendingCompensationScopeKey, scopeKey, StringComparison.Ordinal)");
        sb.AppendLine("                    || !string.Equals(");
        sb.AppendLine("                        pendingForkId,");
        sb.AppendLine("                        ExpectedCompensationForkId(FailedCompensationOccurrenceKey),");
        sb.AppendLine("                        StringComparison.Ordinal))))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback claim contradicts the compiled compensation scope; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims()");
        sb.AppendLine("            || ForwardDispatchClaims.Any(claim =>");
        sb.AppendLine("                IsWithinCompensationScope(claim.ScopeKey, scopeKey)))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback scope still contains an active or corrupt forward dispatch; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (CompensationJournal.Any(entry => !MatchesCompensationJournalTopology(entry)))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal contradicts the compiled compensation topology; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasCompleteCompensationJournalThrough(CompensationJournalSequence))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal became non-contiguous before rollback planning; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasCanonicalCompensationScopeHistories())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal does not contain canonical completed prefixes for every compensation scope; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasUniqueCompensationJournalIdentities())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal contains duplicate execution or occurrence identities; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (CompensationJournal.Any(entry =>");
        sb.AppendLine("            entry.Status is \"InProgress\" or \"Failed\" or \"OutcomeUnknown\"))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback planning found a pre-existing active or terminal inverse outcome; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!AreForkHistoriesComplete(scopeKey, pendingForkId))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"A quiescent fork has missing or non-contiguous completion journal history; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        ActiveCompensationScopeKey = scopeKey;");
        sb.AppendLine("        PendingCompensationForkId = null;");
        sb.AppendLine("        PendingCompensationScopeKey = null;");
        sb.AppendLine("        var pending = CompensationJournal");
        sb.AppendLine("            .Where(entry => IsWithinCompensationScope(entry.ScopeKey, scopeKey)");
        sb.AppendLine("                && string.Equals(entry.Status, \"Completed\", StringComparison.Ordinal))");
        sb.AppendLine("            .OrderByDescending(entry => entry.Sequence)");
        sb.AppendLine("            .ToList();");
        sb.AppendLine("        if (CompensationJournal.Any(entry =>");
        sb.AppendLine("            IsWithinCompensationScope(entry.ScopeKey, scopeKey)");
        sb.AppendLine("            && entry.Status is not \"Completed\" and not \"RolledBack\"))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Selected rollback scope contains an invalid or unfinished journal state; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine("        if (pending.Count == 0)");
        sb.AppendLine("        {");
        EmitFinishRollback(sb, model, "            ");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Compensability propagates upward: preflight the whole selected scope.");
        sb.AppendLine("        // A no-worker identity is safe only because the binding proof established an empty frame.");
        sb.AppendLine("        if (pending.Any(entry => !entry.UsesIdentityInverse");
        sb.AppendLine("            && (string.IsNullOrEmpty(entry.InverseStepName)");
        sb.AppendLine("                || string.IsNullOrEmpty(entry.InverseActionIdentity))))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Selected rollback scope contains a completed non-compensable occurrence.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var identityEntry in pending.Where(entry => entry.UsesIdentityInverse))");
        sb.AppendLine("        {");
        sb.AppendLine("            identityEntry.Status = \"RolledBack\";");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        pending = pending.Where(entry => !entry.UsesIdentityInverse).ToList();");
        sb.AppendLine("        if (pending.Count == 0)");
        sb.AppendLine("        {");
        EmitFinishRollback(sb, model, "            ");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var forkLaneHeads = pending");
        sb.AppendLine("            .Where(entry => string.Equals(entry.ScopeKind, \"Fork\", StringComparison.Ordinal))");
        sb.AppendLine("            .GroupBy(entry => entry.ScopeKey + \"\\u001f\" + (entry.LaneKey ?? string.Empty), StringComparer.Ordinal)");
        sb.AppendLine("            .Select(group => group.OrderByDescending(entry => entry.Sequence).First())");
        sb.AppendLine("            .ToList();");
        sb.AppendLine("        logger.LogDebug(");
        sb.AppendLine("            \"Rollback scope {ScopeKey} contains {ForkLaneCount} structural fork lanes\",");
        sb.AppendLine("            scopeKey,");
        sb.AppendLine("            forkLaneHeads.Count);");
        sb.AppendLine("        // The topology preserves parallel lanes, but generic workflow state has no sound merge.");
        sb.AppendLine("        // Serialize every inverse globally in reverse completion order.");
        sb.AppendLine("        foreach (var entry in pending.Take(1))");
        sb.AppendLine("        {");
        sb.AppendLine("            entry.RollbackState = State;");
        sb.AppendLine("            foreach (var message in DispatchCompensationEntry(entry, logger))");
        sb.AppendLine("            {");
        sb.AppendLine("                yield return message;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        EmitDispatchMethod(sb, model, inverseStepNames);
    }

    private static void EmitDispatchMethod(
        StringBuilder sb,
        WorkflowModel model,
        IReadOnlyList<string> inverseStepNames)
    {
        sb.AppendLine("    private IEnumerable<object> DispatchCompensationEntry(");
        sb.AppendLine("        CompensationJournalEntry entry,");
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        entry.Status = \"InProgress\";");
        sb.AppendLine("        logger.LogWarning(");
        sb.AppendLine("            \"Rolling back {ForwardStep} with {InverseStep}; rollback {RollbackId}\",");
        sb.AppendLine("            entry.ForwardStepName,");
        sb.AppendLine("            entry.InverseStepName,");
        sb.AppendLine("            entry.RollbackId);");
        sb.AppendLine("        object worker = entry.InverseStepName switch");
        sb.AppendLine("        {");

        foreach (var inverseStepName in inverseStepNames)
        {
            sb.AppendLine($"            {Literal(inverseStepName)} => new Execute{inverseStepName}WorkerCommand(WorkflowId, entry.RollbackId, entry.RollbackState)");
            sb.AppendLine("            {");
            sb.AppendLine("                RollbackId = entry.RollbackId,");
            sb.AppendLine("                RollbackJournalSequence = entry.Sequence,");
            sb.AppendLine("                IsCompensation = true,");
            sb.AppendLine("                ForwardOccurrenceKey = entry.OccurrenceKey,");
            sb.AppendLine("                CompensationScopeKey = entry.ScopeKey,");
            sb.AppendLine("                CompensationScopeKind = entry.ScopeKind,");
            sb.AppendLine("                CompensationLaneKey = entry.LaneKey,");
            sb.AppendLine("                CompensationForkId = entry.ForkId,");
            sb.AppendLine("                CompensationForkPathIndex = entry.ForkPathIndex,");
            sb.AppendLine("            },");
        }

        sb.AppendLine("            _ => throw new InvalidOperationException($\"No inverse worker for {entry.InverseStepName}.\"),");
        sb.AppendLine("        };");
        sb.AppendLine("        yield return worker;");
        sb.AppendLine("        yield return new CompensationRollbackTimeout(WorkflowId, entry.RollbackId, entry.Sequence, entry.InverseTimeoutTicks);");
        sb.AppendLine("    }");
    }

    private static void EmitRollbackCompletedHandler(
        StringBuilder sb,
        WorkflowModel model,
        string inverseStepName)
    {
        var eventName = $"{model.PascalName}{inverseStepName}RollbackCompleted";

        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine("        if (CompensationRollbackFinished");
        sb.AppendLine("            || CompensationOutcomeUnknown");
        sb.AppendLine("            || CompensationFailureMessage is not null");
        sb.AppendLine("            || CompensationJournal?.Any(candidate => candidate?.Status is \"Failed\" or \"OutcomeUnknown\") == true)");
        sb.AppendLine("        {");
        sb.AppendLine("            // The first terminal rollback outcome is monotonic under redelivery.");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (CompensationJournal is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal is missing from persisted compensation state; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal changed or became corrupt during rollback; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var entry = CompensationJournal.FirstOrDefault(candidate =>");
        sb.AppendLine("            candidate.Sequence == evt.JournalSequence");
        sb.AppendLine("            && candidate.RollbackId == evt.RollbackId);");
        sb.AppendLine("        if (entry is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = $\"Unmatched rollback completion {evt.RollbackId:N}/{evt.JournalSequence}; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            logger.LogError(");
        sb.AppendLine("                \"Unmatched rollback completion {RollbackId}/{JournalSequence} for workflow {WorkflowId}; saga retained\",");
        sb.AppendLine("                evt.RollbackId,");
        sb.AppendLine("                evt.JournalSequence,");
        sb.AppendLine("                WorkflowId);");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (string.Equals(entry.Status, \"RolledBack\", StringComparison.Ordinal))");
        sb.AppendLine("        {");
        sb.AppendLine("            // Idempotent redelivery of the same successful inverse outcome.");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        if (!string.Equals(entry.InverseStepName, {Literal(inverseStepName)}, StringComparison.Ordinal)");
        sb.AppendLine("            || !string.Equals(entry.Status, \"InProgress\", StringComparison.Ordinal)");
        sb.AppendLine("            || !HasCoherentActiveCompensation()");
        sb.AppendLine("            || ActiveCompensationScopeKey is null");
        sb.AppendLine("            || !IsWithinCompensationScope(entry.ScopeKey, ActiveCompensationScopeKey))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback completion contradicts the active journal entry; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        StateApplicationHelper.EmitStateApplication(sb, model);
        sb.AppendLine("        entry.RollbackState = State;");
        sb.AppendLine("        entry.Status = \"RolledBack\";");
        sb.AppendLine("        var next = ActiveCompensationScopeKey is null");
        sb.AppendLine("            ? null");
        sb.AppendLine("            : CompensationJournal");
        sb.AppendLine("                .Where(candidate => IsWithinCompensationScope(candidate.ScopeKey, ActiveCompensationScopeKey)");
        sb.AppendLine("                    && string.Equals(candidate.Status, \"Completed\", StringComparison.Ordinal))");
        sb.AppendLine("                .OrderByDescending(candidate => candidate.Sequence)");
        sb.AppendLine("                .FirstOrDefault();");
        sb.AppendLine("        if (next is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            next.RollbackState = State;");
        sb.AppendLine("            foreach (var message in DispatchCompensationEntry(next, logger))");
        sb.AppendLine("            {");
        sb.AppendLine("                yield return message;");
        sb.AppendLine("            }");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var scopeStillRunning = ActiveCompensationScopeKey is not null");
        sb.AppendLine("            && CompensationJournal.Any(candidate =>");
        sb.AppendLine("            IsWithinCompensationScope(candidate.ScopeKey, ActiveCompensationScopeKey)");
        sb.AppendLine("            && (string.Equals(candidate.Status, \"Completed\", StringComparison.Ordinal)");
        sb.AppendLine("                || string.Equals(candidate.Status, \"InProgress\", StringComparison.Ordinal)));");
        sb.AppendLine("        if (scopeStillRunning)");
        sb.AppendLine("        {");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        EmitFinishRollback(sb, model, "        ");
        sb.AppendLine("    }");
    }

    private static void EmitFinishRollback(StringBuilder sb, WorkflowModel model, string indent)
    {
        sb.AppendLine($"{indent}CompensationRollbackFinished = true;");
        sb.AppendLine($"{indent}ActiveCompensationScopeKey = null;");
        sb.AppendLine($"{indent}Phase = {model.PhaseEnumName}.Failed;");

        if (model.HasFailureHandlers)
        {
            var firstHandler = model.FailureHandlers!.First();
            var command = $"StartFailureHandler_{Sanitize(firstHandler.HandlerId)}_{firstHandler.FirstStepPhaseName}Command";
            sb.AppendLine($"{indent}yield return new {command}(WorkflowId);");
        }
        else
        {
            sb.AppendLine($"{indent}MarkCompleted();");
        }
    }

    private static void EmitRollbackFailedHandler(StringBuilder sb, WorkflowModel model)
    {
        sb.AppendLine("    public void Handle(");
        sb.AppendLine($"        {model.PascalName}RollbackFailed evt,");
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine("        var compensationJournal = CompensationJournal;");
        sb.AppendLine("        if (CompensationRollbackFinished");
        sb.AppendLine("            || CompensationOutcomeUnknown");
        sb.AppendLine("            || CompensationFailureMessage is not null");
        sb.AppendLine("            || compensationJournal?.Any(candidate => candidate?.Status is \"Failed\" or \"OutcomeUnknown\") == true)");
        sb.AppendLine("        {");
        sb.AppendLine("            // The first terminal rollback outcome is monotonic under redelivery.");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (compensationJournal is null");
        sb.AppendLine("            || !HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal changed or became corrupt during rollback; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var entry = compensationJournal.FirstOrDefault(candidate =>");
        sb.AppendLine("            candidate.Sequence == evt.JournalSequence");
        sb.AppendLine("            && candidate.RollbackId == evt.RollbackId);");
        sb.AppendLine("        if (entry is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = $\"Unmatched rollback failure {evt.RollbackId:N}/{evt.JournalSequence}: {evt.ExceptionMessage}\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            logger.LogError(");
        sb.AppendLine("                \"Unmatched rollback failure {RollbackId}/{JournalSequence} for workflow {WorkflowId}; saga retained\",");
        sb.AppendLine("                evt.RollbackId,");
        sb.AppendLine("                evt.JournalSequence,");
        sb.AppendLine("                WorkflowId);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (string.Equals(entry.Status, \"RolledBack\", StringComparison.Ordinal))");
        sb.AppendLine("        {");
        sb.AppendLine("            // A previously persisted success wins over a late failure delivery.");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!string.Equals(entry.Status, \"InProgress\", StringComparison.Ordinal)");
        sb.AppendLine("            || !HasCoherentActiveCompensation()");
        sb.AppendLine("            || ActiveCompensationScopeKey is null");
        sb.AppendLine("            || !IsWithinCompensationScope(entry.ScopeKey, ActiveCompensationScopeKey))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback failure contradicts the active journal entry; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        entry.Status = \"Failed\";");
        sb.AppendLine("        CompensationFailureMessage = evt.ExceptionMessage;");
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("        logger.LogError(");
        sb.AppendLine("            \"Inverse {InverseStep} failed for workflow {WorkflowId}; saga retained\",");
        sb.AppendLine("            entry.InverseStepName,");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine("        // Do not call MarkCompleted: reconciliation must retain this journal.");
        sb.AppendLine("    }");
    }

    private static void EmitRollbackTimeoutHandler(StringBuilder sb, WorkflowModel model)
    {
        sb.AppendLine("    public void Handle(");
        sb.AppendLine("        CompensationRollbackTimeout timeout,");
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(timeout, nameof(timeout));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine("        var compensationJournal = CompensationJournal;");
        sb.AppendLine("        if (CompensationRollbackFinished");
        sb.AppendLine("            || CompensationOutcomeUnknown");
        sb.AppendLine("            || CompensationFailureMessage is not null");
        sb.AppendLine("            || compensationJournal?.Any(candidate => candidate?.Status is \"Failed\" or \"OutcomeUnknown\") == true)");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (compensationJournal is null");
        sb.AppendLine("            || !HasStructurallyValidCompensationJournal()");
        sb.AppendLine("            || !HasStructurallyValidForwardDispatchClaims()");
        sb.AppendLine("            || !HasStructurallyValidFailureTriggerClaims())");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal changed or became corrupt during rollback; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var entry = compensationJournal.FirstOrDefault(candidate =>");
        sb.AppendLine("            candidate.Sequence == timeout.JournalSequence");
        sb.AppendLine("            && candidate.RollbackId == timeout.RollbackId);");
        sb.AppendLine("        if (entry is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = $\"Unmatched rollback timeout {timeout.RollbackId:N}/{timeout.JournalSequence}; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (string.Equals(entry.Status, \"RolledBack\", StringComparison.Ordinal))");
        sb.AppendLine("        {");
        sb.AppendLine("            // The inverse completed before its scheduled deadline.");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (timeout.TimeoutTicks != entry.InverseTimeoutTicks");
        sb.AppendLine("            || !string.Equals(entry.Status, \"InProgress\", StringComparison.Ordinal)");
        sb.AppendLine("            || !HasCoherentActiveCompensation()");
        sb.AppendLine("            || ActiveCompensationScopeKey is null");
        sb.AppendLine("            || !IsWithinCompensationScope(entry.ScopeKey, ActiveCompensationScopeKey))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationOutcomeUnknown = true;");
        sb.AppendLine("            CompensationFailureMessage = \"Rollback timeout contradicts the active journal entry; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        entry.Status = \"OutcomeUnknown\";");
        sb.AppendLine("        CompensationOutcomeUnknown = true;");
        sb.AppendLine("        CompensationFailureMessage = \"Inverse outcome unknown after timeout.\";");
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("        logger.LogError(");
        sb.AppendLine("            \"Inverse {InverseStep} outcome unknown for workflow {WorkflowId}; saga retained\",");
        sb.AppendLine("            entry.InverseStepName,");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine("        // Never assume an unknown inverse succeeded; retain the saga.");
        sb.AppendLine("    }");
    }

    private static void EmitLegacyCompensation(StringBuilder sb, WorkflowModel model)
    {
        var compensatedSteps = model.CompensationSteps;
        sb.AppendLine();
        sb.AppendLine("    public IEnumerable<object> Handle(");
        sb.AppendLine($"        Trigger{model.PascalName}FailureHandlerCommand cmd,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(cmd, nameof(cmd));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine("        FailedStepName = cmd.FailedStepName;");
        sb.AppendLine("        FailureExceptionMessage = cmd.ExceptionMessage;");
        sb.AppendLine("        FailureExceptionType = cmd.ExceptionType;");
        sb.AppendLine("        FailureStackTrace = cmd.StackTrace;");
        sb.AppendLine("        FailureTimestamp = DateTimeOffset.UtcNow;");
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Compensating;");

        if (model.IsEventSourced)
        {
            sb.AppendLine("        session.Events.Append(");
            sb.AppendLine("            WorkflowId,");
            sb.AppendLine($"            new {model.PascalName}StepFailed(");
            sb.AppendLine("                WorkflowId,");
            sb.AppendLine("                cmd.FailedStepName,");
            sb.AppendLine("                cmd.ExceptionType,");
            sb.AppendLine("                cmd.ExceptionMessage,");
            sb.AppendLine("                FailureTimestamp.Value));");
        }

        if (compensatedSteps.Count == 1)
        {
            var inverse = NamingHelper.GetSimpleTypeName(
                compensatedSteps[0].Compensation!.CompensationStepTypeName);
            sb.AppendLine($"        yield return new Execute{inverse}WorkerCommand(WorkflowId, Guid.NewGuid(), State);");
        }
        else
        {
            foreach (var step in compensatedSteps)
            {
                var inverse = NamingHelper.GetSimpleTypeName(step.Compensation!.CompensationStepTypeName);
                sb.AppendLine($"        if (cmd.FailedStepName == {Literal(step.StepName)})");
                sb.AppendLine("        {");
                sb.AppendLine($"            yield return new Execute{inverse}WorkerCommand(WorkflowId, Guid.NewGuid(), State);");
                sb.AppendLine("            yield break;");
                sb.AppendLine("        }");
            }

            sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine("        MarkCompleted();");
            sb.AppendLine("        yield break;");
        }

        sb.AppendLine("    }");

        var mainFlowNames = new HashSet<string>(model.StepNames, StringComparer.Ordinal);
        foreach (var inverse in compensatedSteps
            .Select(step => NamingHelper.GetSimpleTypeName(step.Compensation!.CompensationStepTypeName))
            .Distinct(StringComparer.Ordinal))
        {
            if (mainFlowNames.Contains(inverse))
            {
                continue;
            }

            sb.AppendLine();
            EmitLegacyCompletedHandler(sb, model, inverse);
        }
    }

    private static void EmitLegacyCompletedHandler(
        StringBuilder sb,
        WorkflowModel model,
        string inverseStepName)
    {
        if (model.HasFailureHandlers)
        {
            var firstHandler = model.FailureHandlers!.First();
            var command = $"StartFailureHandler_{Sanitize(firstHandler.HandlerId)}_{firstHandler.FirstStepPhaseName}Command";
            sb.AppendLine($"    public {command} Handle(");
            sb.AppendLine($"        {inverseStepName}Completed evt,");
            StateApplicationHelper.EmitSessionParameter(sb, model);
            sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
            sb.AppendLine("    {");
            sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
            StateApplicationHelper.EmitSessionGuard(sb, model);
            sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine($"        return new {command}(WorkflowId);");
            sb.AppendLine("    }");
            return;
        }

        sb.AppendLine("    public void Handle(");
        sb.AppendLine($"        {inverseStepName}Completed evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{model.SagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        StateApplicationHelper.EmitStateApplication(sb, model);
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("        MarkCompleted();");
        sb.AppendLine("    }");
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static string NullableStringMatch(string expression, string? expected) => expected is null
        ? expression + " is null"
        : $"string.Equals({expression}, {Literal(expected)}, StringComparison.Ordinal)";

    private static string NullableIntLiteral(int? value) => value?.ToString(
        System.Globalization.CultureInfo.InvariantCulture) ?? "null";

    private static string NullableLiteral(string? value) => value is null ? "null" : Literal(value);

    private static string LoopBoundsArguments(WorkflowModel model, string scopeTemplate)
    {
        if (model.Loops is null)
        {
            return string.Empty;
        }

        var bounds = model.Loops
            .Select(loop => new
            {
                Loop = loop,
                Index = scopeTemplate.IndexOf(
                    "{" + loop.IterationPropertyName + "}",
                    StringComparison.Ordinal),
            })
            .Where(static candidate => candidate.Index >= 0)
            .GroupBy(static candidate => candidate.Loop.IterationPropertyName, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static candidate => candidate.Index)
            .Select(static candidate => candidate.Loop.MaxIterations.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        return string.Concat(bounds.Select(static bound => ", " + bound));
    }

    private static string Sanitize(string value) => value.Replace("-", "_");
}
