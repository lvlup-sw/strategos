// =============================================================================
// <copyright file="ContractsJson.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts;

/// <summary>
/// The single canonical JSON serialization path for the cross-product contracts.
/// </summary>
/// <remarks>
/// <para>
/// Every place that turns a generated contract into JSON — the #53 fixture
/// export, cross-product round-trip harnesses, and any consumer-side helper —
/// must route through <see cref="Options"/> / <see cref="Serialize{T}"/> rather
/// than constructing its own <see cref="JsonSerializerOptions"/>. A single
/// canonical path is what makes the wire form deterministic across producers,
/// so a fixture emitted here is byte-comparable with one emitted elsewhere and
/// the JSON Schema validates exactly what ships.
/// </para>
/// <para>
/// The options mirror the generated records' contract: enums serialize to their
/// string wire value (the generated enums carry
/// <c>[JsonConverter(typeof(JsonStringEnumConverter&lt;T&gt;))]</c>), the
/// discriminated <c>StepDefinition</c> union round-trips on its <c>kind</c>
/// discriminator via <c>[JsonPolymorphic]</c>, and <c>null</c> members (optional
/// wire fields that were absent on the builder IR) are omitted.
/// </para>
/// </remarks>
public static class ContractsJson
{
    /// <summary>
    /// Gets the canonical serializer options for the contracts wire form:
    /// camel-case-free (the generated records already carry explicit
    /// <c>[JsonPropertyName]</c>), indented for human-readable fixtures, and
    /// null-omitting.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes a contract value to its canonical JSON wire form.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The canonical JSON string.</returns>
    /// <remarks>
    /// Uses reflection-based <see cref="JsonSerializer"/>; the requirement is
    /// declared so AOT/trim callers opt in explicitly. The fixture-export and
    /// round-trip tooling that consumes this is not itself AOT-constrained. A
    /// future source-generated <c>JsonSerializerContext</c> can replace this
    /// without changing the canonical-path contract.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Canonical contract serialization uses reflection-based System.Text.Json.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
        "Canonical contract serialization uses reflection-based System.Text.Json.")]
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
