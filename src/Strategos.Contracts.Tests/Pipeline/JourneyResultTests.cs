// =============================================================================
// <copyright file="JourneyResultTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// S1 (issue #63) — the <c>JourneyResult</c> response envelope. It composes the
/// SMQ <c>_meta</c>/<c>_perf</c> blocks, carries the overall <c>outcome</c>, a
/// per-journey <c>journeyOutcomes</c> list of <c>JourneyOutcome</c> records, the
/// <c>budgetConsumed</c> token/cost record, a <c>provenanceRef</c>, and the shared
/// <c>nextActions</c> union list.
/// </summary>
[Property("Category", "Pipeline")]
public sealed class JourneyResultTests
{
    /// <summary>
    /// Asserts the result envelope carries the SMQ <c>_meta</c>/<c>_perf</c> blocks,
    /// the <c>outcome</c> enum, a read-only <c>journeyOutcomes</c> list typed as
    /// <c>JourneyOutcome</c> (NOT <c>WorkflowRef</c>), the <c>budgetConsumed</c>
    /// record, a <c>provenanceRef</c> string, and the <c>nextActions</c> union list.
    /// </summary>
    [Test]
    public async Task JourneyResult_CarriesFullRequiredSurface()
    {
        var result = IntentEnvelopeTests.ResolveGenerated("JourneyResult");
        await Assert.That(result).IsNotNull()
            .Because("the JourneyResult envelope must be generated.");
        await Assert.That(result!.IsSealed).IsTrue();

        await MergeGateDecisionTests.AssertHasBlock(result, "MergeQueueMetaV1", "_meta");
        await MergeGateDecisionTests.AssertHasBlock(result, "PerfMetaV1", "_perf");

        // outcome → JourneyOutcomeStatus enum.
        var outcome = result.GetProperty("Outcome");
        await Assert.That(outcome).IsNotNull();
        await Assert.That(outcome!.PropertyType.Name).IsEqualTo("JourneyOutcomeStatus")
            .Because("outcome is typed as the JourneyOutcomeStatus enum.");

        // journeyOutcomes → IReadOnlyList<JourneyOutcome> (was the WorkflowRef[] bug).
        var outcomes = result.GetProperty("JourneyOutcomes");
        await Assert.That(outcomes).IsNotNull();
        await Assert.That(outcomes!.PropertyType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(outcomes.PropertyType.GetGenericArguments()[0].Name).IsEqualTo("JourneyOutcome")
            .Because("journey outcomes are JourneyOutcome records, not WorkflowRef (the #63 bug).");

        // budgetConsumed → BudgetConsumedV1.
        var budget = result.GetProperty("BudgetConsumed");
        await Assert.That(budget).IsNotNull();
        await Assert.That(budget!.PropertyType.Name).IsEqualTo("BudgetConsumedV1");

        // provenanceRef → string.
        var provenance = result.GetProperty("ProvenanceRef");
        await Assert.That(provenance).IsNotNull();
        await Assert.That(provenance!.PropertyType).IsEqualTo(typeof(string));

        // nextActions → IReadOnlyList<NextAction>.
        var nextActions = result.GetProperty("NextActions");
        await Assert.That(nextActions).IsNotNull();
        await Assert.That(nextActions!.PropertyType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(nextActions.PropertyType.GetGenericArguments()[0].Name).IsEqualTo("NextAction");
    }

    /// <summary>
    /// Asserts the <c>JourneyOutcome</c> record (#63) carries its four fields —
    /// <c>workflowId</c>, <c>catalogVersion</c>, the <c>outcome</c>
    /// <c>JourneyOutcomeStatus</c> enum, and an <c>evidenceRef</c> — and that the
    /// <c>JourneyOutcomeStatus</c> enum has its canonical members.
    /// </summary>
    [Test]
    public async Task JourneyOutcome_CarriesFourFields_AndStatusEnum()
    {
        var outcome = IntentEnvelopeTests.ResolveGenerated("JourneyOutcome");
        await Assert.That(outcome).IsNotNull()
            .Because("the JourneyOutcome record must be generated.");
        await Assert.That(outcome!.IsSealed).IsTrue();

        var workflowId = outcome.GetProperty("WorkflowId");
        await Assert.That(workflowId).IsNotNull();
        await Assert.That(workflowId!.PropertyType).IsEqualTo(typeof(string));

        var catalogVersion = outcome.GetProperty("CatalogVersion");
        await Assert.That(catalogVersion).IsNotNull();
        await Assert.That(catalogVersion!.PropertyType).IsEqualTo(typeof(string));

        var status = outcome.GetProperty("Outcome");
        await Assert.That(status).IsNotNull();
        await Assert.That(status!.PropertyType.Name).IsEqualTo("JourneyOutcomeStatus");

        var evidence = outcome.GetProperty("EvidenceRef");
        await Assert.That(evidence).IsNotNull();
        await Assert.That(evidence!.PropertyType).IsEqualTo(typeof(string));

        var statusEnum = IntentEnvelopeTests.ResolveGenerated("JourneyOutcomeStatus");
        await Assert.That(statusEnum).IsNotNull();
        await Assert.That(statusEnum!.IsEnum).IsTrue();
        foreach (var m in new[] { "AllPassed", "Partial", "AllFailed", "NotExecuted" })
        {
            await Assert.That(Enum.GetNames(statusEnum)).Contains(m);
        }
    }

    /// <summary>
    /// Asserts the <c>BudgetConsumedV1</c> token/cost record (#63) carries the
    /// non-nullable <c>inputTokens</c>/<c>outputTokens</c>/<c>cacheReadTokens</c>
    /// int counters and an optional <c>costUsd</c> double.
    /// </summary>
    [Test]
    public async Task BudgetConsumedV1_CarriesTokenAndCostFields()
    {
        var budget = IntentEnvelopeTests.ResolveGenerated("BudgetConsumedV1");
        await Assert.That(budget).IsNotNull()
            .Because("the BudgetConsumedV1 record must be generated.");
        await Assert.That(budget!.IsSealed).IsTrue();

        foreach (var fieldName in new[] { "InputTokens", "OutputTokens", "CacheReadTokens" })
        {
            var field = budget.GetProperty(fieldName);
            await Assert.That(field).IsNotNull()
                .Because($"BudgetConsumedV1 must carry {fieldName}.");
            await Assert.That(field!.PropertyType).IsEqualTo(typeof(int));
        }

        var cost = budget.GetProperty("CostUsd");
        await Assert.That(cost).IsNotNull();
        await Assert.That(Nullable.GetUnderlyingType(cost!.PropertyType)).IsEqualTo(typeof(double))
            .Because("costUsd is an optional double.");
    }
}
