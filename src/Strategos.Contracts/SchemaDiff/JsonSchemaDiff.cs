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

    /// <summary>A change that invalidates previously-valid documents or removes a
    /// guarantee a consumer relied on (requires a major version bump).</summary>
    Breaking = 1,
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

    /// <summary>Gets the overall severity: <see cref="ChangeSeverity.Breaking"/> if
    /// any single change is breaking, otherwise <see cref="ChangeSeverity.NonBreaking"/>.</summary>
    public ChangeSeverity Severity =>
        HasBreakingChanges ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking;
}

/// <summary>
/// A small, dependency-free structural diff over two JSON Schema (draft 2020-12)
/// object schemas. It is deliberately conservative: it classifies the change
/// classes the cross-product versioning contract cares about (design §Resilience
/// item 3) and treats anything it cannot prove safe as breaking.
/// </summary>
/// <remarks>
/// Scope (intentionally narrow — this gate guards the cross-product wire contract,
/// not arbitrary JSON Schema): top-level <c>properties</c> + <c>required</c> and
/// each property's declared <c>type</c>. Rules:
/// <list type="bullet">
///   <item>Removed property ⇒ BREAKING.</item>
///   <item>Property newly added to <c>required</c> (existing or new) ⇒ BREAKING.</item>
///   <item>Property's declared <c>type</c> changed ⇒ BREAKING (type narrowing/swap).</item>
///   <item>Added optional property ⇒ NON-BREAKING.</item>
///   <item>Property removed from <c>required</c> (relaxed) ⇒ NON-BREAKING.</item>
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
        }

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
}
