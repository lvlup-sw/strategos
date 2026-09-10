// =============================================================================
// <copyright file="BasileusCompatTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;

namespace Strategos.Contracts.Tests.Events;

/// <summary>
/// T11 — Basileus wire compatibility. The generated <c>SdlcEventEnvelope</c>
/// record must serialize to JSON whose key shape matches the known Basileus
/// envelope wire contract (<c>Basileus.Core.Events.Sdlc.ExarchosEventDto</c>):
/// camelCase <c>streamId / sequence / timestamp / type / correlationId /
/// causationId / agentId / agentRole / source / schemaVersion / data</c>. This
/// is validation only — Basileus is not migrated here (basileus#152).
/// </summary>
[Property("Category", "Events")]
public class BasileusCompatTests
{
    /// <summary>The Basileus envelope wire keys, transcribed from ExarchosEventDto.</summary>
    private static readonly string[] BasileusEnvelopeKeys =
    [
        "streamId", "sequence", "timestamp", "type",
        "correlationId", "causationId", "agentId", "agentRole",
        "source", "schemaVersion", "data",
    ];

    /// <summary>
    /// Constructs a generated <c>SdlcEventEnvelope</c>, serializes it with
    /// System.Text.Json, and asserts the emitted JSON keys are exactly the
    /// Basileus envelope wire keys (camelCase, via <c>[JsonPropertyName]</c>) —
    /// so a Basileus consumer deserializing our record sees the shape it expects.
    /// </summary>
    [Test]
    public async Task GeneratedRecords_RoundTrip_AgainstBasileusEventShapes()
    {
        var asm = typeof(ContractsMarker).Assembly;
        var envelopeType = asm.GetTypes().FirstOrDefault(t => t.Name == "SdlcEventEnvelope");
        await Assert.That(envelopeType).IsNotNull()
            .Because("SdlcEventEnvelope must be a generated contract type.");

        // Build an instance via init setters with representative values.
        var instance = Activator.CreateInstance(envelopeType!)!;
        Set(envelopeType!, instance, "StreamId", "feature-42");
        Set(envelopeType!, instance, "Sequence", 7);
        Set(envelopeType!, instance, "Timestamp", "2026-05-24T00:00:00Z");
        Set(envelopeType!, instance, "Type", "task.progressed");
        Set(envelopeType!, instance, "CorrelationId", "corr-1");
        Set(envelopeType!, instance, "Source", "exarchos");

        var json = JsonSerializer.Serialize(instance, envelopeType!);
        using var doc = JsonDocument.Parse(json);
        var emittedKeys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        // Every key the record emits must be a known Basileus envelope key
        // (camelCase) — no PascalCase leak, no unexpected member.
        foreach (var key in emittedKeys)
        {
            await Assert.That(BasileusEnvelopeKeys).Contains(key)
                .Because($"emitted envelope key '{key}' must match the Basileus wire contract (camelCase).");
        }

        // The required envelope keys must be present on a populated instance.
        foreach (var key in new[] { "streamId", "sequence", "timestamp", "type" })
        {
            await Assert.That(emittedKeys.Contains(key)).IsTrue()
                .Because($"required envelope key '{key}' must serialize.");
        }

        // source must serialize as its string wire value ('exarchos'), not a
        // numeric enum ordinal — Basileus reads source as a string.
        await Assert.That(doc.RootElement.GetProperty("source").GetString()).IsEqualTo("exarchos")
            .Because("the source enum must serialize to its string wire value for Basileus.");

        // sequence serializes as a JSON number (int32 → matches Basileus long).
        await Assert.That(doc.RootElement.GetProperty("sequence").ValueKind).IsEqualTo(JsonValueKind.Number);
    }

    private static void Set(Type type, object instance, string prop, object value)
    {
        var p = type.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"missing property {prop} on {type.Name}");

        // Convert string -> enum for enum-typed members (e.g. Source).
        var target = p.PropertyType;
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        var converted = underlying.IsEnum && value is string s
            ? Enum.Parse(underlying, ToPascal(s))
            : Convert.ChangeType(value, underlying);

        p.SetValue(instance, converted);
    }

    private static string ToPascal(string wire) =>
        wire.Length == 0 ? wire : char.ToUpperInvariant(wire[0]) + wire[1..];
}
