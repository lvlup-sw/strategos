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
public class IntentEnvelopeTests
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
    /// Asserts the <c>DegradedReason</c> closed enum round-trips BY NAME (not
    /// ordinal): the wire monikers carry over JSON, covering at least
    /// <c>DownstreamUnreachable</c>, <c>JudgeTimeout</c>, <c>MalformedOutput</c>.
    /// </summary>
    [Test]
    public async Task DegradedReason_Enum_RoundTripsByName()
    {
        var reason = ResolveGenerated("DegradedReason");
        await Assert.That(reason).IsNotNull()
            .Because("the DegradedReason closed enum must be generated.");
        await Assert.That(reason!.IsEnum).IsTrue();

        var names = Enum.GetNames(reason);
        await Assert.That(names).Contains("DownstreamUnreachable");
        await Assert.That(names).Contains("JudgeTimeout");
        await Assert.That(names).Contains("MalformedOutput");

        // BY NAME, not ordinal: each member serializes to its kebab wire moniker
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
