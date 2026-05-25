// =============================================================================
// <copyright file="IntentEnvelopeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// S2 (issue #64) — the shared response-envelope <c>_meta</c> block. The S-series
/// response envelopes (<c>MergeGateDecision</c>, <c>JourneyResult</c>) carry a
/// <c>ResponseMetaV1</c> degraded-flag block and a <c>PerfMetaV1</c> perf block.
/// <c>degraded == true</c> is the observability seam: downstream consumers must
/// short-circuit any "use the decision" logic — fallback is never silent.
/// </summary>
[Property("Category", "Pipeline")]
public sealed class IntentEnvelopeTests
{
    /// <summary>
    /// Asserts the generated <c>ResponseMetaV1</c> base block is a
    /// <c>sealed record</c> exposing <c>bool Degraded</c> and a nullable
    /// <c>DegradedReason? DegradedReason</c>, both <c>{ get; init; }</c> (INV-6/7).
    /// </summary>
    [Test]
    public async Task IntentEnvelopeMeta_BaseBlock_CarriesDegradedFields()
    {
        var meta = ResolveGenerated("ResponseMetaV1");
        await Assert.That(meta).IsNotNull()
            .Because("the S2 response-meta base block ResponseMetaV1 must be generated.");

        await Assert.That(meta!.IsSealed).IsTrue()
            .Because("ResponseMetaV1 must be a sealed record (INV-6).");

        var degraded = meta.GetProperty("Degraded");
        await Assert.That(degraded).IsNotNull()
            .Because("ResponseMetaV1 must expose a Degraded flag.");
        await Assert.That(degraded!.PropertyType).IsEqualTo(typeof(bool))
            .Because("Degraded is a non-nullable boolean.");
        await Assert.That(IsInitOnly(degraded)).IsTrue()
            .Because("ResponseMetaV1.Degraded must be { get; init; } (INV-7).");

        var reason = meta.GetProperty("DegradedReason");
        await Assert.That(reason).IsNotNull()
            .Because("ResponseMetaV1 must expose a nullable DegradedReason.");
        var reasonType = reason!.PropertyType;
        var underlying = Nullable.GetUnderlyingType(reasonType);
        await Assert.That(underlying).IsNotNull()
            .Because("DegradedReason must be a nullable value type (DegradedReason?).");
        await Assert.That(underlying!.Name).IsEqualTo("DegradedReason")
            .Because("the nullable wraps the DegradedReason enum.");
        await Assert.That(IsInitOnly(reason)).IsTrue()
            .Because("ResponseMetaV1.DegradedReason must be { get; init; } (INV-7).");
    }

    /// <summary>
    /// Asserts the <c>DegradedReason</c> closed enum (#64) carries the 8 canonical
    /// members and round-trips BY NAME (not ordinal): each snake_case wire moniker
    /// carries over JSON. The canonical set is
    /// <c>model_timeout</c>, <c>model_5xx</c>, <c>rate_limit</c>,
    /// <c>malformed_output</c>, <c>low_confidence</c>, <c>judge_unavailable</c>,
    /// <c>budget_exhausted</c>, <c>sandbox_unavailable</c>.
    /// </summary>
    [Test]
    public async Task DegradedReason_Enum_RoundTripsByName()
    {
        var reason = ResolveGenerated("DegradedReason");
        await Assert.That(reason).IsNotNull()
            .Because("the DegradedReason closed enum must be generated.");
        await Assert.That(reason!.IsEnum).IsTrue();

        var names = Enum.GetNames(reason);
        string[] canonical =
        [
            "ModelTimeout", "Model5xx", "RateLimit", "MalformedOutput",
            "LowConfidence", "JudgeUnavailable", "BudgetExhausted", "SandboxUnavailable",
        ];
        foreach (var member in canonical)
        {
            await Assert.That(names).Contains(member)
                .Because($"DegradedReason must carry the canonical member {member} (#64).");
        }

        await Assert.That(names.Length).IsEqualTo(canonical.Length)
            .Because("DegradedReason is the exact 8-member canonical set (no extras).");

        // BY NAME, not ordinal: each member serializes to its snake_case wire moniker
        // and deserializes back to the same member regardless of declaration order.
        foreach (var name in names)
        {
            var value = Enum.Parse(reason, name);
            var json = JsonSerializer.Serialize(value, reason, ContractsJson.Options);
            await Assert.That(json).IsNotEqualTo(((int)value).ToString())
                .Because($"{name} must serialize by NAME (wire moniker), not its ordinal.");

            var back = JsonSerializer.Deserialize(json, reason, ContractsJson.Options);
            await Assert.That(back).IsEqualTo(value)
                .Because($"{name} must round-trip by name through JSON: {json}");
        }

        // The exact snake_case wire values cross-product consumers match on.
        var modelTimeout = Enum.Parse(reason, "ModelTimeout");
        await Assert.That(JsonSerializer.Serialize(modelTimeout, reason, ContractsJson.Options))
            .IsEqualTo("\"model_timeout\"");
        var model5xx = Enum.Parse(reason, "Model5xx");
        await Assert.That(JsonSerializer.Serialize(model5xx, reason, ContractsJson.Options))
            .IsEqualTo("\"model_5xx\"");
    }

    /// <summary>
    /// Asserts the <c>PerfMetaV1</c> perf block (#63) exposes the merge-context
    /// telemetry counters: <c>ms</c>, <c>inputTokens</c>, <c>outputTokens</c>,
    /// <c>cacheReadTokens</c> — all non-nullable <c>int</c>, all init-only.
    /// </summary>
    [Test]
    public async Task PerfMetaV1_CarriesTokenCounters()
    {
        var perf = ResolveGenerated("PerfMetaV1");
        await Assert.That(perf).IsNotNull()
            .Because("the PerfMetaV1 perf block must be generated.");
        await Assert.That(perf!.IsSealed).IsTrue()
            .Because("PerfMetaV1 must be a sealed record (INV-6).");

        foreach (var fieldName in new[] { "Ms", "InputTokens", "OutputTokens", "CacheReadTokens" })
        {
            var field = perf.GetProperty(fieldName);
            await Assert.That(field).IsNotNull()
                .Because($"PerfMetaV1 must expose {fieldName}.");
            await Assert.That(field!.PropertyType).IsEqualTo(typeof(int))
                .Because($"{fieldName} is a non-nullable int32 counter.");
            await Assert.That(IsInitOnly(field)).IsTrue()
                .Because($"PerfMetaV1.{fieldName} must be {{ get; init; }} (INV-7).");
        }
    }

    /// <summary>
    /// Asserts the <c>MergeQueueMetaV1</c> SMQ <c>_meta</c> block (#63) spreads the
    /// universal <c>ResponseMetaV1</c> base (carrying <c>Degraded</c> +
    /// <c>DegradedReason</c>) and adds the merge-context fields
    /// <c>HeadSha</c>, <c>BaseSha</c>, <c>MergeGroupId</c>, <c>EvaluatorTier</c>.
    /// </summary>
    [Test]
    public async Task MergeQueueMetaV1_SpreadsBaseAndCarriesMergeContext()
    {
        var meta = ResolveGenerated("MergeQueueMetaV1");
        await Assert.That(meta).IsNotNull()
            .Because("the SMQ _meta block MergeQueueMetaV1 must be generated.");
        await Assert.That(meta!.IsSealed).IsTrue()
            .Because("MergeQueueMetaV1 must be a sealed record (INV-6).");

        // Spreads the base degraded seam.
        var degraded = meta.GetProperty("Degraded");
        await Assert.That(degraded).IsNotNull()
            .Because("MergeQueueMetaV1 spreads ResponseMetaV1.Degraded.");
        await Assert.That(degraded!.PropertyType).IsEqualTo(typeof(bool));

        var reason = meta.GetProperty("DegradedReason");
        await Assert.That(reason).IsNotNull()
            .Because("MergeQueueMetaV1 spreads ResponseMetaV1.DegradedReason.");
        await Assert.That(Nullable.GetUnderlyingType(reason!.PropertyType)!.Name)
            .IsEqualTo("DegradedReason");

        // Merge-context fields.
        foreach (var fieldName in new[] { "HeadSha", "BaseSha", "MergeGroupId", "EvaluatorTier" })
        {
            var field = meta.GetProperty(fieldName);
            await Assert.That(field).IsNotNull()
                .Because($"MergeQueueMetaV1 must carry {fieldName}.");
            await Assert.That(field!.PropertyType).IsEqualTo(typeof(string))
                .Because($"{fieldName} is a string moniker.");
            await Assert.That(IsInitOnly(field)).IsTrue()
                .Because($"MergeQueueMetaV1.{fieldName} must be {{ get; init; }} (INV-7).");
        }
    }

    internal static Type? ResolveGenerated(string simpleName) =>
        typeof(ContractsMarker).Assembly
            .GetType($"Strategos.Contracts.Generated.{simpleName}");

    internal static bool IsInitOnly(PropertyInfo prop)
    {
        var setter = prop.SetMethod;
        return setter is not null
            && setter.ReturnParameter.GetRequiredCustomModifiers().Any(m => m == typeof(IsExternalInit));
    }
}
