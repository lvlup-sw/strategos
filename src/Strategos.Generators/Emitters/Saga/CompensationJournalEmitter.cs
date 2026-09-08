// -----------------------------------------------------------------------
// <copyright file="CompensationJournalEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Models;

using Microsoft.CodeAnalysis;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits durable completion-journal writes at forward completion sites.
/// </summary>
internal static class CompensationJournalEmitter
{
    /// <summary>Emits the broad rollback guard when an occurrence is selected dynamically.</summary>
    public static void EmitForwardCompletionGuard(
        StringBuilder sb,
        WorkflowModel model,
        string exitStatement,
        string indent = "        ")
    {
        if (!CompensationTopology.UsesDerivedRuntime(model))
        {
            return;
        }

        sb.AppendLine($"{indent}// An active or terminal rollback rejects late forward results before state mutation.");
        sb.AppendLine($"{indent}if (ShouldIgnoreForwardCompletion(null, null, evt.StepExecutionId))");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    {exitStatement}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the monotonic rollback-claim guard before a forward completion can
    /// mutate saga state or dispatch a successor.
    /// </summary>
    public static void EmitForwardCompletionGuard(
        StringBuilder sb,
        WorkflowModel model,
        string phaseName,
        PathRoutingKey? pathKey,
        string exitStatement,
        string indent = "        ")
    {
        if (!CompensationTopology.UsesDerivedRuntime(model))
        {
            return;
        }

        var topology = CompensationTopology.Build(model);
        if (!topology.TryResolve(phaseName, pathKey, out var occurrence))
        {
            return;
        }

        EmitForwardCompletionGuard(sb, occurrence, exitStatement, indent);
    }

    /// <summary>Emits the guard for a pre-resolved occurrence.</summary>
    public static void EmitForwardCompletionGuard(
        StringBuilder sb,
        CompensationOccurrence? occurrence,
        string exitStatement,
        string indent = "        ")
    {
        if (occurrence is null)
        {
            return;
        }

        sb.AppendLine($"{indent}// Once rollback claims this occurrence, a late forward result cannot resurrect it.");
        sb.AppendLine($"{indent}if (ShouldIgnoreForwardCompletion(");
        sb.AppendLine($"{indent}    {Literal(occurrence.StableKey)},");
        sb.AppendLine($"{indent}    ResolveCompensationScopeInstance({Literal(occurrence.Scope.TemplateKey)}),");
        sb.AppendLine($"{indent}    evt.StepExecutionId))");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    {exitStatement}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a journal write for every completed occurrence in a derived program.
    /// </summary>
    /// <param name="sb">The generated saga source.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="phaseName">The completed phase.</param>
    /// <param name="pathKey">The structural path identity, when applicable.</param>
    /// <param name="indent">The generated-code indentation.</param>
    public static void EmitRecordCompletion(
        StringBuilder sb,
        WorkflowModel model,
        string phaseName,
        PathRoutingKey? pathKey = null,
        string indent = "        ")
    {
        if (!CompensationTopology.UsesDerivedRuntime(model))
        {
            return;
        }

        var topology = CompensationTopology.Build(model);
        if (!topology.TryResolve(phaseName, pathKey, out var occurrence))
        {
            return;
        }

        EmitRecordCompletion(sb, occurrence, indent);
    }

    /// <summary>
    /// Emits a journal write for a pre-resolved occurrence.
    /// </summary>
    public static void EmitRecordCompletion(
        StringBuilder sb,
        CompensationOccurrence occurrence,
        string indent = "        ")
    {
        var stableKey = Literal(occurrence.StableKey);
        var scopeTemplate = Literal(occurrence.Scope.TemplateKey);
        var scopeKind = Literal(occurrence.Scope.Kind.ToString());
        var scopeOrdinal = occurrence.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var laneKey = NullableLiteral(occurrence.Scope.LaneKey);
        var forkId = NullableLiteral(occurrence.Scope.ForkId);
        var forkPathIndex = occurrence.Scope.ForkPathIndex?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        var forwardStep = Literal(occurrence.Step.StepName);
        var inverseStep = NullableLiteral(occurrence.InverseStepName);
        var forwardAction = NullableLiteral(occurrence.ForwardActionIdentity);
        var inverseAction = NullableLiteral(occurrence.InverseActionIdentity);
        var usesIdentityInverse = occurrence.UsesIdentityInverse ? "true" : "false";
        var inverseTimeoutTicks = (occurrence.Step.Compensation?.Timeout?.Ticks
            ?? SagaCompensationComponentEmitter.DefaultTimeoutTicks)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        sb.AppendLine($"{indent}// Persist the completed forward occurrence before its successor is dispatched.");
        sb.AppendLine($"{indent}RecordCompensationCompletion(");
        sb.AppendLine($"{indent}    evt.StepExecutionId,");
        sb.AppendLine($"{indent}    {stableKey},");
        sb.AppendLine($"{indent}    ResolveCompensationScopeInstance({scopeTemplate}),");
        sb.AppendLine($"{indent}    {scopeKind},");
        sb.AppendLine($"{indent}    {scopeOrdinal},");
        sb.AppendLine($"{indent}    {laneKey},");
        sb.AppendLine($"{indent}    {forkId},");
        sb.AppendLine($"{indent}    {forkPathIndex},");
        sb.AppendLine($"{indent}    {forwardStep},");
        sb.AppendLine($"{indent}    {inverseStep},");
        sb.AppendLine($"{indent}    {forwardAction},");
        sb.AppendLine($"{indent}    {inverseAction},");
        sb.AppendLine($"{indent}    {usesIdentityInverse},");
        sb.AppendLine($"{indent}    {inverseTimeoutTicks}L,");
        sb.AppendLine($"{indent}    evt.UpdatedState);");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the derived rollback trigger for a reducer-driven failure that is
    /// observed only after the forward occurrence was durably journaled.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the derived runtime owns the failure route;
    /// otherwise <see langword="false"/> so the caller can emit the legacy route.
    /// </returns>
    public static bool EmitFailureAfterForwardCompletion(
        StringBuilder sb,
        WorkflowModel model,
        CompensationOccurrence? occurrence,
        string indent = "            ")
    {
        if (!CompensationTopology.UsesDerivedRuntime(model))
        {
            return false;
        }

        if (occurrence is null)
        {
            sb.AppendLine($"{indent}CompensationFailureMessage = \"The completed failure boundary is absent from the compiled compensation topology; saga retained.\";");
            sb.AppendLine($"{indent}Phase = {model.PhaseEnumName}.Failed;");
            sb.AppendLine($"{indent}yield break;");
            return true;
        }

        sb.AppendLine($"{indent}logger.LogWarning(");
        sb.AppendLine($"{indent}    \"Forward occurrence {{OccurrenceKey}} completed into Failed phase for workflow {{WorkflowId}}; starting derived rollback\",");
        sb.AppendLine($"{indent}    {Literal(occurrence.StableKey)},");
        sb.AppendLine($"{indent}    WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"{indent}if (!TryMintPostCompletionFailureClaim(");
        sb.AppendLine($"{indent}        {Literal(occurrence.StableKey)},");
        sb.AppendLine($"{indent}        ResolveCompensationScopeInstance({Literal(occurrence.Scope.TemplateKey)}),");
        sb.AppendLine($"{indent}        {Literal(occurrence.Scope.Kind.ToString())},");
        sb.AppendLine($"{indent}        {NullableLiteral(occurrence.Scope.LaneKey)},");
        sb.AppendLine($"{indent}        {NullableLiteral(occurrence.Scope.ForkId)},");
        sb.AppendLine($"{indent}        {NullableIntLiteral(occurrence.Scope.ForkPathIndex)},");
        sb.AppendLine($"{indent}        {Literal(occurrence.Step.StepName)},");
        sb.AppendLine($"{indent}        \"StateTransitionFailure\",");
        sb.AppendLine($"{indent}        out var postCompletionFailureClaim))");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    yield break;");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}yield return new Trigger{model.PascalName}FailureHandlerCommand(");
        sb.AppendLine($"{indent}    WorkflowId,");
        sb.AppendLine($"{indent}    {Literal(occurrence.Step.StepName)},");
        sb.AppendLine($"{indent}    \"Workflow state entered Failed after the forward occurrence completed.\",");
        sb.AppendLine($"{indent}    \"StateTransitionFailure\",");
        sb.AppendLine($"{indent}    null)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    ForwardOccurrenceKey = {Literal(occurrence.StableKey)},");
        sb.AppendLine($"{indent}    CompensationScopeKey = ResolveCompensationScopeInstance({Literal(occurrence.Scope.TemplateKey)}),");
        sb.AppendLine($"{indent}    CompensationScopeKind = {Literal(occurrence.Scope.Kind.ToString())},");
        sb.AppendLine($"{indent}    CompensationLaneKey = {NullableLiteral(occurrence.Scope.LaneKey)},");
        sb.AppendLine($"{indent}    CompensationForkId = {NullableLiteral(occurrence.Scope.ForkId)},");
        sb.AppendLine($"{indent}    CompensationForkPathIndex = {NullableIntLiteral(occurrence.Scope.ForkPathIndex)},");
        sb.AppendLine($"{indent}    CompensationJournalSequenceAtDispatch = postCompletionFailureClaim.JournalSequenceAtDispatch,");
        sb.AppendLine($"{indent}    FailedForwardExecutionId = postCompletionFailureClaim.ForwardExecutionId,");
        sb.AppendLine($"{indent}    FailureOccurredAfterForwardCompletion = true,");
        sb.AppendLine($"{indent}}};");
        sb.AppendLine($"{indent}yield break;");
        return true;
    }

    /// <summary>
    /// Stops an in-flight fork lane at a completion boundary when another lane has
    /// already selected rollback. The completed work is journaled by the caller,
    /// but no forward successor is dispatched.
    /// </summary>
    public static void EmitPendingForkQuiescenceGuard(
        StringBuilder sb,
        WorkflowModel model,
        CompensationOccurrence? occurrence,
        string indent = "        ")
    {
        if (!CompensationTopology.UsesDerivedRuntime(model)
            || occurrence?.Scope.ForkId is not { } forkId
            || occurrence.Scope.ForkPathIndex is not { } pathIndex)
        {
            return;
        }

        var forkIdLiteral = Literal(forkId);
        var sanitizedId = forkId.Replace("-", "_");
        sb.AppendLine($"{indent}// A sibling failed: stop this lane at its durable completion boundary.");
        sb.AppendLine($"{indent}if (string.Equals(PendingCompensationForkId, {forkIdLiteral}, StringComparison.Ordinal))");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    Fork_{sanitizedId}_Path{pathIndex}CompensationQuiesced = true;");
        sb.AppendLine($"{indent}    Fork_{sanitizedId}_Path{pathIndex}Status = Strategos.Definitions.ForkPathStatus.Success;");
        sb.AppendLine($"{indent}    if (CheckJoinReady_{sanitizedId}() && PendingCompensationScopeKey is not null)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        foreach (var rollbackMessage in BeginCompensationScope(PendingCompensationScopeKey, logger))");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            yield return rollbackMessage;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    yield break;");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static string NullableLiteral(string? value) => value is null ? "null" : Literal(value);

    private static string NullableIntLiteral(int? value) => value?.ToString(
        System.Globalization.CultureInfo.InvariantCulture) ?? "null";
}
