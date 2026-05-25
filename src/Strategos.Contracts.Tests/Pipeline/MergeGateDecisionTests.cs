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
/// the SMQ <c>_meta</c> block (<c>MergeQueueMetaV1</c>) and the <c>_perf</c> block
/// (<c>PerfMetaV1</c>), carries the decision/confidence/rationale fields, types
/// its suggested journeys as <c>WorkflowRef</c>, and carries a discriminated
/// <c>NextAction</c> list verb-keyed on <c>verb</c>.
/// </summary>
[Property("Category", "Pipeline")]
public sealed class MergeGateDecisionTests
{
    /// <summary>
    /// Asserts every required <c>MergeGateDecision</c> field exists with the right
    /// type: the <c>schemaVersion</c> literal, the <c>decision</c> enum, scalar
    /// <c>confidence</c>/<c>rationale</c>, the <c>diffClassification</c> enum,
    /// <c>riskSignals</c>/<c>suggestedJourneys</c> collections, the optional
    /// <c>fallbackReason</c>, the <c>promptId</c>/<c>modelId</c> strings, the
    /// <c>_meta</c>/<c>_perf</c> blocks, and the <c>nextActions</c> union list.
    /// </summary>
    [Test]
    public async Task MergeGateDecision_CarriesFullRequiredSurface()
    {
        var decision = IntentEnvelopeTests.ResolveGenerated("MergeGateDecision");
        await Assert.That(decision).IsNotNull()
            .Because("the MergeGateDecision envelope must be generated.");
        await Assert.That(decision!.IsSealed).IsTrue();

        // The SMQ meta block is MergeQueueMetaV1 (NOT the universal ResponseMetaV1).
        await AssertHasBlock(decision, "MergeQueueMetaV1", "_meta");
        await AssertHasBlock(decision, "PerfMetaV1", "_perf");

        // schemaVersion literal.
        var schemaVersion = decision.GetProperty("SchemaVersion");
        await Assert.That(schemaVersion).IsNotNull()
            .Because("MergeGateDecision must carry the schemaVersion literal.");
        await Assert.That(schemaVersion!.PropertyType).IsEqualTo(typeof(string));

        // decision → MergeDecision enum.
        var decisionProp = decision.GetProperty("Decision");
        await Assert.That(decisionProp).IsNotNull();
        await Assert.That(decisionProp!.PropertyType.Name).IsEqualTo("MergeDecision")
            .Because("decision is typed as the MergeDecision enum.");

        // confidence → double.
        var confidence = decision.GetProperty("Confidence");
        await Assert.That(confidence).IsNotNull();
        await Assert.That(confidence!.PropertyType).IsEqualTo(typeof(double));

        // rationale → string.
        var rationale = decision.GetProperty("Rationale");
        await Assert.That(rationale).IsNotNull();
        await Assert.That(rationale!.PropertyType).IsEqualTo(typeof(string));

        // diffClassification → DiffClassification enum.
        var diff = decision.GetProperty("DiffClassification");
        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!.PropertyType.Name).IsEqualTo("DiffClassification");

        // riskSignals → IReadOnlyList<string>.
        var risk = decision.GetProperty("RiskSignals");
        await Assert.That(risk).IsNotNull();
        await Assert.That(risk!.PropertyType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(risk.PropertyType.GetGenericArguments()[0]).IsEqualTo(typeof(string));

        // suggestedJourneys → IReadOnlyList<WorkflowRef>.
        var suggested = decision.GetProperty("SuggestedJourneys");
        await Assert.That(suggested).IsNotNull();
        await Assert.That(suggested!.PropertyType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(suggested.PropertyType.GetGenericArguments()[0].Name).IsEqualTo("WorkflowRef")
            .Because("suggested journeys are typed as WorkflowRef, not strings (INV-8).");

        // fallbackReason → nullable FallbackReason.
        var fallback = decision.GetProperty("FallbackReason");
        await Assert.That(fallback).IsNotNull();
        var fallbackUnderlying = Nullable.GetUnderlyingType(fallback!.PropertyType);
        await Assert.That(fallbackUnderlying).IsNotNull()
            .Because("fallbackReason is optional (nullable value type).");
        await Assert.That(fallbackUnderlying!.Name).IsEqualTo("FallbackReason");

        // promptId / modelId → string.
        var promptId = decision.GetProperty("PromptId");
        await Assert.That(promptId).IsNotNull();
        await Assert.That(promptId!.PropertyType).IsEqualTo(typeof(string));
        var modelId = decision.GetProperty("ModelId");
        await Assert.That(modelId).IsNotNull();
        await Assert.That(modelId!.PropertyType).IsEqualTo(typeof(string));

        // nextActions → IReadOnlyList<NextAction>.
        var nextActions = decision.GetProperty("NextActions");
        await Assert.That(nextActions).IsNotNull();
        await Assert.That(nextActions!.PropertyType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(nextActions.PropertyType.GetGenericArguments()[0].Name).IsEqualTo("NextAction");
    }

    /// <summary>
    /// Asserts the <c>MergeDecision</c>, <c>DiffClassification</c> and
    /// <c>FallbackReason</c> enums (#63) are generated with their canonical members.
    /// </summary>
    [Test]
    public async Task MergeGate_Enums_CarryCanonicalMembers()
    {
        var decision = IntentEnvelopeTests.ResolveGenerated("MergeDecision");
        await Assert.That(decision).IsNotNull();
        await Assert.That(decision!.IsEnum).IsTrue();
        foreach (var m in new[] { "Skip", "RunE2e", "RunE2eFocused", "EscalateHuman" })
        {
            await Assert.That(Enum.GetNames(decision)).Contains(m);
        }

        var diff = IntentEnvelopeTests.ResolveGenerated("DiffClassification");
        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!.IsEnum).IsTrue();
        foreach (var m in new[] { "Docs", "TestOnly", "Refactor", "Feature", "Infra", "Schema", "Config", "Mixed" })
        {
            await Assert.That(Enum.GetNames(diff)).Contains(m);
        }

        var fallback = IntentEnvelopeTests.ResolveGenerated("FallbackReason");
        await Assert.That(fallback).IsNotNull();
        await Assert.That(fallback!.IsEnum).IsTrue();
        foreach (var m in new[] { "ModelTimeout", "Model5xx", "RateLimit", "MalformedOutput", "LowConfidence", "JudgeUnavailable" })
        {
            await Assert.That(Enum.GetNames(fallback)).Contains(m);
        }
    }

    /// <summary>
    /// Asserts <c>NextAction</c> is a discriminated union of TYPED branches keyed
    /// on <c>verb</c> (#63): a <c>run_buildkite_pipeline</c> branch carrying
    /// <c>BuildkitePipelineParams</c> (a typed <c>WorkflowRef[]</c>), and an
    /// <c>escalate_human</c> branch carrying a string reason.
    /// </summary>
    [Test]
    public async Task NextActions_DiscriminatedUnion_VerbBranches()
    {
        var baseType = IntentEnvelopeTests.ResolveGenerated("NextAction");
        await Assert.That(baseType).IsNotNull()
            .Because("the NextAction discriminated-union base must be generated.");
        await Assert.That(baseType!.IsAbstract).IsTrue()
            .Because("the union base is an abstract polymorphic record (INV-6).");

        var run = IntentEnvelopeTests.ResolveGenerated("RunBuildkitePipelineAction");
        await Assert.That(run).IsNotNull()
            .Because("the run_buildkite_pipeline branch RunBuildkitePipelineAction must be generated.");
        await Assert.That(baseType.IsAssignableFrom(run!)).IsTrue();
        var prms = run!.GetProperty("Params");
        await Assert.That(prms).IsNotNull()
            .Because("the run_buildkite_pipeline branch must carry typed params.");
        await Assert.That(prms!.PropertyType.Name).IsEqualTo("BuildkitePipelineParams");

        var paramsType = IntentEnvelopeTests.ResolveGenerated("BuildkitePipelineParams");
        await Assert.That(paramsType).IsNotNull();
        var journeys = paramsType!.GetProperty("Journeys");
        await Assert.That(journeys).IsNotNull();
        await Assert.That(journeys!.PropertyType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(journeys.PropertyType.GetGenericArguments()[0].Name).IsEqualTo("WorkflowRef");

        var escalate = IntentEnvelopeTests.ResolveGenerated("EscalateHumanAction");
        await Assert.That(escalate).IsNotNull()
            .Because("the escalate_human branch EscalateHumanAction must be generated.");
        await Assert.That(baseType.IsAssignableFrom(escalate!)).IsTrue();
        var reason = escalate!.GetProperty("Reason");
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
