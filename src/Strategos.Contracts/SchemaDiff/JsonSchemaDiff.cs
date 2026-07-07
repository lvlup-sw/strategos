// =============================================================================
// <copyright file="JsonSchemaDiff.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.SchemaDiff;

/// <summary>
/// Severity of a single structural change between two JSON Schema documents.
/// </summary>
public enum ChangeSeverity
{
    /// <summary>An additive, backward-compatible change (additive-only minor).</summary>
    NonBreaking = 0,

    /// <summary>An additive change that is compatible on the wire but demands a
    /// consumer-notice release-notes line before producers may exercise it — the
    /// DR-18 posture for a new closed-enum member. Permitted on a minor bump; does
    /// not block the CI gate, but is surfaced (flagged) rather than silent, because
    /// strict converters reject unknown members until consumers upgrade.</summary>
    Notice = 1,

    /// <summary>A change that invalidates previously-valid documents or removes a
    /// guarantee a consumer relied on (requires a major version bump).</summary>
    Breaking = 2,
}

/// <summary>A single structural change detected between a previous and a next schema.</summary>
/// <param name="Severity">Whether the change is breaking.</param>
/// <param name="Description">Human-readable description naming the affected member.</param>
public sealed record SchemaChange(ChangeSeverity Severity, string Description);

/// <summary>The result of diffing two JSON Schema documents.</summary>
/// <param name="Changes">All detected changes (empty when the schemas are equivalent).</param>
public sealed record SchemaDiffResult(IReadOnlyList<SchemaChange> Changes)
{
    /// <summary>Gets a value indicating whether any change is breaking.</summary>
    public bool HasBreakingChanges => Changes.Any(c => c.Severity == ChangeSeverity.Breaking);

    /// <summary>Gets a value indicating whether any change is a flagged
    /// <see cref="ChangeSeverity.Notice"/> (e.g. an added enum member) — non-breaking
    /// but requiring a consumer-notice release-notes line under DR-18.</summary>
    public bool HasNotices => Changes.Any(c => c.Severity == ChangeSeverity.Notice);

    /// <summary>Gets the overall severity: the highest severity of any single change
    /// (<see cref="ChangeSeverity.Breaking"/> &gt; <see cref="ChangeSeverity.Notice"/>
    /// &gt; <see cref="ChangeSeverity.NonBreaking"/>), or
    /// <see cref="ChangeSeverity.NonBreaking"/> when there are no changes.</summary>
    public ChangeSeverity Severity =>
        Changes.Count == 0 ? ChangeSeverity.NonBreaking : Changes.Max(c => c.Severity);
}

/// <summary>
/// A small, dependency-free structural diff over two JSON Schema (draft 2020-12)
/// object schemas. It is deliberately conservative: it classifies the change
/// classes the cross-product versioning contract cares about (design §Resilience
/// item 3) and treats anything it cannot prove safe as breaking.
/// </summary>
/// <remarks>
/// Scope (intentionally narrow — this gate guards the cross-product wire contract,
/// not arbitrary JSON Schema): top-level <c>properties</c> + <c>required</c>, each
/// property's declared <c>type</c>, and closed-enum member lists (the schema itself,
/// or a property's inline <c>enum</c>). Rules:
/// <list type="bullet">
///   <item>Removed property ⇒ BREAKING.</item>
///   <item>Property newly added to <c>required</c> (existing or new) ⇒ BREAKING.</item>
///   <item>Property's declared <c>type</c> changed ⇒ BREAKING (type narrowing/swap).</item>
///   <item>Enum member removed ⇒ BREAKING (a rename is a removal + an add, so it is
///   BREAKING too — DR-18 enum-evolution policy).</item>
///   <item>Added optional property ⇒ NON-BREAKING.</item>
///   <item>Property removed from <c>required</c> (relaxed) ⇒ NON-BREAKING.</item>
///   <item>Enum member added ⇒ NOTICE (additive on a minor, but flagged: strict
///   converters reject unknown members, so consumers must upgrade before producers
///   emit the new member).</item>
/// </list>
/// CI compares the previous published tag's <c>schemas/json-schema/*.json</c>
/// against the working tree's; the tests compare in-test fixtures so they stay
/// deterministic and offline.
/// </remarks>
public static class JsonSchemaDiff
{
    /// <summary>Compares two JSON Schema documents given as JSON text.</summary>
    /// <param name="previousJson">The previous (baseline) schema document.</param>
    /// <param name="nextJson">The next (candidate) schema document.</param>
    /// <returns>The classified diff result.</returns>
    public static SchemaDiffResult Compare(string previousJson, string nextJson)
    {
        ArgumentNullException.ThrowIfNull(previousJson);
        ArgumentNullException.ThrowIfNull(nextJson);

        using var prev = JsonDocument.Parse(previousJson);
        using var next = JsonDocument.Parse(nextJson);
        return Compare(prev.RootElement, next.RootElement);
    }

    /// <summary>Compares two parsed JSON Schema documents.</summary>
    /// <param name="previous">The previous (baseline) schema root element.</param>
    /// <param name="next">The next (candidate) schema root element.</param>
    /// <returns>The classified diff result.</returns>
    public static SchemaDiffResult Compare(JsonElement previous, JsonElement next)
    {
        var changes = new List<SchemaChange>();

        var prevProps = ReadProperties(previous);
        var nextProps = ReadProperties(next);
        var prevRequired = ReadRequired(previous);
        var nextRequired = ReadRequired(next);

        // Removed properties — breaking.
        foreach (var name in prevProps.Keys)
        {
            if (!nextProps.ContainsKey(name))
            {
                changes.Add(new SchemaChange(
                    ChangeSeverity.Breaking,
                    $"property '{name}' was removed"));
            }
        }

        // Added properties — non-breaking unless they land in `required`.
        foreach (var name in nextProps.Keys)
        {
            if (!prevProps.ContainsKey(name))
            {
                var nowRequired = nextRequired.Contains(name);
                changes.Add(new SchemaChange(
                    nowRequired ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                    nowRequired
                        ? $"property '{name}' was added as required"
                        : $"optional property '{name}' was added"));
            }
        }

        // Type narrowing/swap on retained properties — breaking.
        foreach (var (name, prevSchema) in prevProps)
        {
            if (!nextProps.TryGetValue(name, out var nextSchema))
            {
                continue;
            }

            var prevType = ReadType(prevSchema);
            var nextType = ReadType(nextSchema);
            if (prevType is not null && nextType is not null && prevType != nextType)
            {
                changes.Add(new SchemaChange(
                    ChangeSeverity.Breaking,
                    $"property '{name}' changed type from '{prevType}' to '{nextType}'"));
            }

            // Inline enum member evolution on a retained property.
            DiffEnumMembers(ReadEnumValues(prevSchema), ReadEnumValues(nextSchema), name, changes);
        }

        // Enum member evolution (DR-18) — the schema itself may BE an enum: TypeSpec
        // closed enums emit as a top-level `{ "type": "string", "enum": [...] }`
        // referenced by $ref, so diff the root enum member list too.
        DiffEnumMembers(ReadEnumValues(previous), ReadEnumValues(next), propertyName: null, changes);

        // Newly-required existing properties — breaking.
        foreach (var name in nextRequired)
        {
            if (prevProps.ContainsKey(name) && !prevRequired.Contains(name))
            {
                changes.Add(new SchemaChange(
                    ChangeSeverity.Breaking,
                    $"property '{name}' became required"));
            }
        }

        // Relaxed-required (was required, now optional) — non-breaking.
        foreach (var name in prevRequired)
        {
            if (!nextRequired.Contains(name) && nextProps.ContainsKey(name))
            {
                changes.Add(new SchemaChange(
                    ChangeSeverity.NonBreaking,
                    $"property '{name}' is no longer required"));
            }
        }

        return new SchemaDiffResult(changes);
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadProperties(JsonElement schema)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (schema.ValueKind == JsonValueKind.Object
            && schema.TryGetProperty("properties", out var props)
            && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                result[prop.Name] = prop.Value;
            }
        }

        return result;
    }

    private static IReadOnlySet<string> ReadRequired(JsonElement schema)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (schema.ValueKind == JsonValueKind.Object
            && schema.TryGetProperty("required", out var required)
            && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    result.Add(item.GetString()!);
                }
            }
        }

        return result;
    }

    private static string? ReadType(JsonElement propertySchema)
    {
        if (propertySchema.ValueKind == JsonValueKind.Object
            && propertySchema.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String)
        {
            return type.GetString();
        }

        return null;
    }

    private static IReadOnlyList<string> ReadEnumValues(JsonElement schema)
    {
        var result = new List<string>();
        if (schema.ValueKind == JsonValueKind.Object
            && schema.TryGetProperty("enum", out var members)
            && members.ValueKind == JsonValueKind.Array)
        {
            foreach (var member in members.EnumerateArray())
            {
                // Only string members are wire tokens for a closed enum; anything
                // else is outside this contract's scope and is ignored.
                if (member.ValueKind == JsonValueKind.String)
                {
                    result.Add(member.GetString()!);
                }
            }
        }

        return result;
    }

    private static void DiffEnumMembers(
        IReadOnlyList<string> previous,
        IReadOnlyList<string> next,
        string? propertyName,
        List<SchemaChange> changes)
    {
        if (previous.Count == 0 && next.Count == 0)
        {
            return;
        }

        var previousSet = new HashSet<string>(previous, StringComparer.Ordinal);
        var nextSet = new HashSet<string>(next, StringComparer.Ordinal);
        var prefix = propertyName is null ? string.Empty : $"property '{propertyName}' ";

        // Removed (or renamed-away) member ⇒ BREAKING: a producer may still emit it
        // and a consumer may still switch on it, yet it is gone from the closed set.
        // (A rename surfaces as a removal + an add, so the removal makes it BREAKING.)
        foreach (var member in previous)
        {
            if (!nextSet.Contains(member))
            {
                changes.Add(new SchemaChange(
                    ChangeSeverity.Breaking,
                    $"{prefix}enum member '{member}' was removed"));
            }
        }

        // Added member ⇒ NOTICE: additive on a minor, but flagged — strict converters
        // reject unknown members, so consumers must upgrade before producers emit it.
        foreach (var member in next)
        {
            if (!previousSet.Contains(member))
            {
                changes.Add(new SchemaChange(
                    ChangeSeverity.Notice,
                    $"{prefix}enum member '{member}' was added"));
            }
        }
    }
}
