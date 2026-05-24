// =============================================================================
// <copyright file="EnvelopeSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Events;

/// <summary>
/// T6 — the <c>SdlcEventEnvelope</c> contract. Compiles the canonical
/// <c>.tsp</c> and asserts the emitted JSON Schema carries the required
/// envelope fields (<c>streamId</c>, <c>sequence:int32</c>, <c>timestamp</c>,
/// <c>type</c> discriminator), the optional correlation/causation/agent fields,
/// a <c>source</c> constrained to <c>exarchos | basileus</c>, and a <c>data</c>
/// payload that admits unknown <c>type</c> values (forward-compat: logged, never
/// rejected).
/// </summary>
[Property("Category", "Events")]
[NotInParallel("tsp-compile")]
public class EnvelopeSchemaTests
{
    /// <summary>
    /// Compiles the contracts <c>.tsp</c> and asserts the
    /// <c>SdlcEventEnvelope</c> JSON Schema shape against the ADR §4 envelope
    /// field list, including the <c>source</c> two-value constraint and the
    /// forward-compatible unconstrained <c>data</c> payload.
    /// </summary>
    [Test]
    public async Task Envelope_Schema_HasRequiredFieldsAndSourceDiscriminator()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var root = await EventSchemas.LoadAsync("SdlcEventEnvelope");

        await Assert.That(root.TryGetProperty("properties", out var props)).IsTrue();

        // Required fields per the ADR §4 envelope.
        var required = root.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .ToHashSet();
        foreach (var name in new[] { "streamId", "sequence", "timestamp", "type" })
        {
            await Assert.That(required.Contains(name)).IsTrue()
                .Because($"{name} must be a required envelope field.");
        }

        // sequence is int32, not int64 (spike finding — avoid string coercion).
        var sequence = props.GetProperty("sequence");
        await Assert.That(sequence.GetProperty("type").GetString()).IsEqualTo("integer");
        await Assert.That(sequence.GetProperty("maximum").GetInt64()).IsEqualTo(2147483647L)
            .Because("sequence must be int32-bounded (not int64).");

        // Optional correlation/causation/agent fields are present but not required.
        foreach (var name in new[] { "correlationId", "causationId", "agentId", "agentRole", "schemaVersion" })
        {
            await Assert.That(props.TryGetProperty(name, out _)).IsTrue()
                .Because($"{name} must exist on the envelope.");
            await Assert.That(required.Contains(name)).IsFalse()
                .Because($"{name} must be optional.");
        }

        // source is constrained to exactly exarchos | basileus (enum ref).
        var source = props.GetProperty("source");
        var sourceValues = EventSchemas.EnumValues(source);
        await Assert.That(sourceValues).Contains("exarchos");
        await Assert.That(sourceValues).Contains("basileus");
        await Assert.That(sourceValues.Count).IsEqualTo(2)
            .Because("source must be exactly { exarchos, basileus }.");

        // data is the forward-compatible payload: an open object (unknown event
        // shapes representable, never rejected). TypeSpec emits Record<unknown>
        // as a $ref to an open-object document with no `required` constraint.
        var data = props.GetProperty("data");
        await Assert.That(data.TryGetProperty("$ref", out var dataRef)).IsTrue()
            .Because("data must reference the open-object payload schema.");
        var dataDoc = await EventSchemas.LoadAsync(
            Path.GetFileNameWithoutExtension(dataRef.GetString())!);
        await Assert.That(dataDoc.GetProperty("type").GetString()).IsEqualTo("object");
        await Assert.That(dataDoc.TryGetProperty("required", out _)).IsFalse()
            .Because("the data payload must accept unknown event shapes (no required keys).");
    }
}
