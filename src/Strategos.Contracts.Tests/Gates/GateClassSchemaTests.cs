// =============================================================================
// <copyright file="GateClassSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Strategos.Contracts;

namespace Strategos.Contracts.Tests.Gates;

/// <summary>
/// DR-1 (issue #150) — <c>GateClass</c>, the single typed gate identity shared
/// across the contract boundary. Asserts (a) the emitted JSON Schema is a CLOSED
/// string enum carrying the frozen snake_case wire vocabulary in order, and
/// (b) the generated C# enum round-trips each member to its snake_case wire value
/// via the <c>[JsonStringEnumMemberName]</c> + <c>JsonStringEnumConverter&lt;T&gt;</c>
/// path (#98 precedent) — by VALUE, never by ordinal (INV-8 polyglot identity).
/// </summary>
[Property("Category", "Gates")]
public sealed class GateClassSchemaTests
{
    // The frozen identity map: C# member name -> snake_case wire value, in the
    // exact declaration order of the closed enum (DR-1, #150). Order is part of
    // the contract: the emitted JSON Schema enum array must match it verbatim.
    private static readonly (string Name, string Wire)[] Frozen =
    [
        ("Typecheck", "typecheck"),
        ("Lint", "lint"),
        ("ScopedTest", "scoped_test"),
        ("FullSuite", "full_suite"),
        ("MutationAdequacy", "mutation_adequacy"),
        ("MergeGate", "merge_gate"),
        ("LlmJudge", "llm_judge"),
        ("Rules", "rules"),
    ];

    /// <summary>
    /// Asserts the emitted <c>GateClass.json</c> is a closed string enum whose
    /// values are EXACTLY the frozen snake_case tokens, in order — no more, no
    /// fewer. The bounded <c>enum</c> array (with no open extension) is the
    /// closed-vocabulary contract both runtimes derive from.
    /// </summary>
    [Test]
    public async Task GateClassSchema_IsClosedStringEnum_WithFrozenSnakeCaseValues()
    {
        await Assert.That(EventSchemas.Exists("GateClass")).IsTrue()
            .Because("`tsp compile` must emit a GateClass.json schema document (run scripts/contracts-codegen.sh).");

        var root = await EventSchemas.LoadAsync("GateClass");

        // A closed string enum: `type: string` + a bounded `enum` array.
        await Assert.That(root.TryGetProperty("type", out var type)).IsTrue()
            .Because("GateClass must be a scalar string enum.");
        await Assert.That(type.GetString()).IsEqualTo("string");

        var values = EventSchemas.EnumValues(root);
        await Assert.That(values.Count).IsEqualTo(Frozen.Length)
            .Because("GateClass is a CLOSED enum — exactly the frozen members, no more.");

        // Exact values, in exact order (the wire contract is order-sensitive for
        // cross-repo diffing; identity is by value).
        for (var i = 0; i < Frozen.Length; i++)
        {
            await Assert.That(values[i]).IsEqualTo(Frozen[i].Wire)
                .Because($"GateClass wire value #{i} must be \"{Frozen[i].Wire}\".");
        }
    }

    /// <summary>
    /// Reflects over the generated <c>Strategos.Contracts.Generated.GateClass</c>
    /// enum and asserts every member carries its snake_case
    /// <c>[JsonStringEnumMemberName]</c> and round-trips by VALUE (serialize →
    /// snake_case token, back → member) — the #98 emission path.
    /// </summary>
    [Test]
    public async Task GateClassEnum_CarriesSnakeCaseWireNames_AndRoundTripsByValue()
    {
        var enumType = typeof(ContractsMarker).Assembly
            .GetType("Strategos.Contracts.Generated.GateClass");

        await Assert.That(enumType).IsNotNull()
            .Because("the codegen must emit a Strategos.Contracts.Generated.GateClass enum.");
        await Assert.That(enumType!.IsEnum).IsTrue();

        var members = Enum.GetNames(enumType);
        await Assert.That(members.Length).IsEqualTo(Frozen.Length)
            .Because("GateClass must have exactly the frozen member set (closed enum).");

        var options = ContractsJson.Options;
        foreach (var (name, wire) in Frozen)
        {
            await Assert.That(members).Contains(name)
                .Because($"GateClass must carry the member '{name}'.");

            // The [JsonStringEnumMemberName] attribute pins the snake_case wire value.
            var field = enumType.GetField(name, BindingFlags.Public | BindingFlags.Static);
            await Assert.That(field).IsNotNull();
            var attr = field!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
            await Assert.That(attr).IsNotNull()
                .Because($"GateClass.{name} must carry a [JsonStringEnumMemberName] (snake_case wire identity).");
            await Assert.That(attr!.Name).IsEqualTo(wire)
                .Because($"GateClass.{name} must serialize as \"{wire}\".");

            var value = Enum.Parse(enumType, name);

            // Serialize → snake_case wire string.
            var json = JsonSerializer.Serialize(value, enumType, options);
            await Assert.That(json).IsEqualTo($"\"{wire}\"")
                .Because($"GateClass.{name} must serialize to \"{wire}\" (by value, not ordinal).");

            // Deserialize snake_case wire string → member.
            var back = JsonSerializer.Deserialize($"\"{wire}\"", enumType, options);
            await Assert.That(back!.ToString()).IsEqualTo(name)
                .Because($"\"{wire}\" must deserialize back to GateClass.{name}.");
        }
    }
}
