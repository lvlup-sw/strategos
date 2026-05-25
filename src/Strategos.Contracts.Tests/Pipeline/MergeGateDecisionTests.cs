// =============================================================================
// <copyright file="MergeGateDecisionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json.Serialization;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// S1 (issue #63) — the <c>MergeGateDecision</c> response envelope. It composes
/// the shared S2 base blocks (<c>ResponseMetaV1</c> <c>_meta</c>,
/// <c>PerfMetaV1</c> <c>_perf</c>), types its suggested journeys as
/// <c>WorkflowRef</c>, and carries a discriminated <c>NextAction</c> list.
/// </summary>
[Property("Category", "Pipeline")]
public class MergeGateDecisionTests
{
    /// <summary>
    /// Asserts the decision envelope carries the <c>_meta</c>/<c>_perf</c> base
    /// blocks (typed as the shared S2 records) and a read-only
    /// <c>SuggestedJourneys</c> list typed as <c>WorkflowRef</c>.
    /// </summary>
    [Test]
    public async Task MergeGateDecision_ExtendsBaseMeta_AndTypesSuggestedJourneys()
    {
        var decision = IntentEnvelopeTests.ResolveGenerated("MergeGateDecision");
        await Assert.That(decision).IsNotNull()
            .Because("the MergeGateDecision envelope must be generated.");
        await Assert.That(decision!.IsSealed).IsTrue();

        await AssertHasBlock(decision, "ResponseMetaV1", "_meta");
        await AssertHasBlock(decision, "PerfMetaV1", "_perf");

        var suggested = decision.GetProperty("SuggestedJourneys");
        await Assert.That(suggested).IsNotNull()
            .Because("the decision must type its suggested journeys.");
        await Assert.That(suggested!.PropertyType.IsGenericType
                && suggested.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .IsTrue()
            .Because("SuggestedJourneys must be IReadOnlyList<T> (INV-7).");
        await Assert.That(suggested.PropertyType.GetGenericArguments()[0].Name).IsEqualTo("WorkflowRef")
            .Because("suggested journeys are typed as WorkflowRef, not strings (INV-8).");
    }

    /// <summary>
    /// Asserts <c>NextAction</c> is a discriminated union of TYPED branches (no
    /// string-typed indirection): a <c>run-journey</c> branch carrying a
    /// <c>WorkflowRef</c> and a <c>block</c> branch carrying a string reason.
    /// </summary>
    [Test]
    public async Task NextActions_DiscriminatedUnion_TypedBranches()
    {
        var baseType = IntentEnvelopeTests.ResolveGenerated("NextAction");
        await Assert.That(baseType).IsNotNull()
            .Because("the NextAction discriminated-union base must be generated.");
        await Assert.That(baseType!.IsAbstract).IsTrue()
            .Because("the union base is an abstract polymorphic record (INV-6).");

        var run = IntentEnvelopeTests.ResolveGenerated("RunJourneyAction");
        await Assert.That(run).IsNotNull()
            .Because("the run-journey branch RunJourneyAction must be generated.");
        await Assert.That(baseType.IsAssignableFrom(run!)).IsTrue();
        var journey = run!.GetProperty("Journey");
        await Assert.That(journey).IsNotNull()
            .Because("the run-journey branch must carry a typed WorkflowRef (no string indirection).");
        await Assert.That(journey!.PropertyType.Name).IsEqualTo("WorkflowRef");

        var block = IntentEnvelopeTests.ResolveGenerated("BlockAction");
        await Assert.That(block).IsNotNull()
            .Because("the block branch BlockAction must be generated.");
        await Assert.That(baseType.IsAssignableFrom(block!)).IsTrue();
        var reason = block!.GetProperty("Reason");
        await Assert.That(reason).IsNotNull();
        await Assert.That(reason!.PropertyType).IsEqualTo(typeof(string));

        // Both decision and result carry the NextAction list.
        foreach (var envelopeName in new[] { "MergeGateDecision", "JourneyResult" })
        {
            var envelope = IntentEnvelopeTests.ResolveGenerated(envelopeName);
            var nextActions = envelope!.GetProperty("NextActions");
            await Assert.That(nextActions).IsNotNull()
                .Because($"{envelopeName} must carry a NextActions list.");
            await Assert.That(nextActions!.PropertyType.GetGenericTypeDefinition())
                .IsEqualTo(typeof(IReadOnlyList<>));
            await Assert.That(nextActions.PropertyType.GetGenericArguments()[0].Name)
                .IsEqualTo("NextAction");
        }
    }

    internal static async Task AssertHasBlock(Type envelope, string blockTypeName, string wireName)
    {
        var prop = envelope.GetProperties()
            .FirstOrDefault(p => p.PropertyType.Name == blockTypeName);
        await Assert.That(prop).IsNotNull()
            .Because($"{envelope.Name} must carry a {blockTypeName} block.");
        await Assert.That(IntentEnvelopeTests.IsInitOnly(prop!)).IsTrue()
            .Because($"{envelope.Name}.{prop!.Name} must be init-only (INV-7).");

        var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        await Assert.That(jsonName).IsEqualTo(wireName)
            .Because($"{envelope.Name}'s {blockTypeName} block must serialize as \"{wireName}\".");
    }
}
