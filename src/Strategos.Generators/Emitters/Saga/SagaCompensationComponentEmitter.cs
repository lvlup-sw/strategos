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
        sb.AppendLine("        if (CompensationJournal.Any(entry =>");
        sb.AppendLine("            entry.ForwardExecutionId == forwardExecutionId");
        sb.AppendLine("            && string.Equals(entry.OccurrenceKey, occurrenceKey, StringComparison.Ordinal)))");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        CompensationJournalSequence++;");
        sb.AppendLine("        CompensationJournal.Add(new CompensationJournalEntry");
        sb.AppendLine("        {");
        sb.AppendLine("            Sequence = CompensationJournalSequence,");
        sb.AppendLine("            ForwardExecutionId = forwardExecutionId,");
        sb.AppendLine("            RollbackId = forwardExecutionId,");
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

        sb.AppendLine("        if (CompensationJournalSchemaVersion != 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Typed rollback journal was not initialized by the #169 runtime; saga retained for migration.\";");
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
        sb.AppendLine("            || cmd.CompensationJournalSequenceAtDispatch is not long journalHighWater)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Typed rollback trigger is missing #169 topology metadata; migrate the publisher and retain the saga.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var scopeKey = cmd.CompensationScopeKey;");
        sb.AppendLine("        var scopeKind = cmd.CompensationScopeKind;");
        sb.AppendLine("        if (journalHighWater < 0");
        sb.AppendLine("            || CompensationJournalSequence < journalHighWater");
        sb.AppendLine("            || !HasCompleteCompensationJournalThrough(journalHighWater))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal high-water mark is missing or corrupt for a typed rollback program; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var expectedPrefixCount = ExpectedCompletedPrefixCount(cmd.ForwardOccurrenceKey);");
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
        sb.AppendLine("        if (recordedPrefixCount < expectedPrefixCount)");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal is missing history for a typed rollback program; saga retained.\";");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.Failed;");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var isForkFailure = string.Equals(scopeKind, \"Fork\", StringComparison.Ordinal)");
        sb.AppendLine("            && cmd.CompensationForkId is not null;");
        sb.AppendLine("        if (isForkFailure)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Every failing lane must become terminal before the selected fork can unwind.");
        sb.AppendLine("            MarkFailedForkPath(cmd.CompensationForkId!, cmd.CompensationForkPathIndex);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (ActiveCompensationScopeKey is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!string.Equals(ActiveCompensationScopeKey, scopeKey, StringComparison.Ordinal))");
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
        sb.AppendLine("        if (isForkFailure)");
        sb.AppendLine("        {");
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
    }

    private static void EmitExpectedPrefixCount(StringBuilder sb, CompensationTopology topology)
    {
        sb.AppendLine("    private static int ExpectedCompletedPrefixCount(string occurrenceKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        return occurrenceKey switch");
        sb.AppendLine("        {");

        foreach (var occurrence in topology.Occurrences)
        {
            var prefixCount = topology.Occurrences.Count(candidate =>
                string.Equals(candidate.Scope.TemplateKey, occurrence.Scope.TemplateKey, StringComparison.Ordinal)
                && string.Equals(candidate.Scope.LaneKey, occurrence.Scope.LaneKey, StringComparison.Ordinal)
                && candidate.Ordinal < occurrence.Ordinal);
            sb.AppendLine($"            {Literal(occurrence.StableKey)} => {prefixCount.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
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
                    sb.AppendLine($"                    && Fork_{sanitizedId}_Path{path.PathIndex}Status == Strategos.Definitions.ForkPathStatus.Failed)");
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
        sb.AppendLine("        if (!HasCompleteCompensationJournalThrough(CompensationJournalSequence))");
        sb.AppendLine("        {");
        sb.AppendLine("            CompensationFailureMessage = \"Completion journal became non-contiguous before rollback planning; saga retained.\";");
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
        sb.AppendLine("        var entry = CompensationJournal.FirstOrDefault(candidate =>");
        sb.AppendLine("            candidate.Sequence == evt.JournalSequence");
        sb.AppendLine("            && candidate.RollbackId == evt.RollbackId);");
        sb.AppendLine("        if (entry is null || !string.Equals(entry.Status, \"InProgress\", StringComparison.Ordinal))");
        sb.AppendLine("        {");
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
        sb.AppendLine($"{indent}ActiveCompensationScopeKey = null;");
        sb.AppendLine($"{indent}Phase = {model.PhaseEnumName}.Failed;");

        if (model.HasFailureHandlers)
        {
            var firstHandler = model.FailureHandlers!.First();
            var command = $"StartFailureHandler_{Sanitize(firstHandler.HandlerId)}_{firstHandler.FirstStepName}Command";
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
        sb.AppendLine("        var entry = CompensationJournal.FirstOrDefault(candidate =>");
        sb.AppendLine("            candidate.Sequence == evt.JournalSequence");
        sb.AppendLine("            && candidate.RollbackId == evt.RollbackId);");
        sb.AppendLine("        if (entry is null)");
        sb.AppendLine("        {");
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
        sb.AppendLine("        if (entry.Status is \"RolledBack\" or \"Failed\" or \"OutcomeUnknown\")");
        sb.AppendLine("        {");
        sb.AppendLine("            // Idempotent redelivery of a terminal inverse outcome.");
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
        sb.AppendLine("        var entry = CompensationJournal.FirstOrDefault(candidate =>");
        sb.AppendLine("            candidate.Sequence == timeout.JournalSequence");
        sb.AppendLine("            && candidate.RollbackId == timeout.RollbackId);");
        sb.AppendLine("        if (entry is null || !string.Equals(entry.Status, \"InProgress\", StringComparison.Ordinal))");
        sb.AppendLine("        {");
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
            var command = $"StartFailureHandler_{Sanitize(firstHandler.HandlerId)}_{firstHandler.FirstStepName}Command";
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

    private static string NullableLiteral(string? value) => value is null ? "null" : Literal(value);

    private static string Sanitize(string value) => value.Replace("-", "_");
}
