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
    /// DR-18 posture for a new closed-enum or union member.</summary>
    Notice = 1,

    /// <summary>A change that invalidates previously-valid documents or removes a
    /// guarantee a consumer relied on. The release gate requires a minor version
    /// increment before 1.0 and a major increment after 1.0.</summary>
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
    /// <see cref="ChangeSeverity.Notice"/>.</summary>
    public bool HasNotices => Changes.Any(c => c.Severity == ChangeSeverity.Notice);

    /// <summary>Gets the highest severity, or
    /// <see cref="ChangeSeverity.NonBreaking"/> when there are no changes.</summary>
    public ChangeSeverity Severity =>
        Changes.Count == 0 ? ChangeSeverity.NonBreaking : Changes.Max(c => c.Severity);
}

/// <summary>
/// Recursively compares the emitted JSON Schema subset used by Strategos
/// Contracts. A change not proven compatible is classified as breaking.
/// </summary>
/// <remarks>
/// The classifier understands nested properties and required members, declared
/// types, enums, constants, references, minimum string/collection sizes,
/// patterns, item schemas, discriminators, composition keywords, and the
/// conditional applicators (<c>if</c> / <c>then</c> / <c>else</c> /
/// <c>dependentSchemas</c> / <c>dependentRequired</c>), and recurses per entry
/// through the <c>$defs</c> / <c>definitions</c> schema
/// containers. Unknown validation keywords fail closed when their values change. Schema annotations
/// such as descriptions and vendor extensions do not affect compatibility.
/// </remarks>
public static class JsonSchemaDiff
{
    private static readonly HashSet<string> HandledKeywords = new(StringComparer.Ordinal)
    {
        "properties",
        "required",
        "type",
        "enum",
        "const",
        "$id",
        "$schema",
        "$ref",
        "minLength",
        "minItems",
        "minProperties",
        "pattern",
        "items",
        "anyOf",
        "oneOf",
        "allOf",
        "discriminator",
        "$defs",
        "definitions",
        "if",
        "then",
        "else",
        "dependentSchemas",
        "dependentRequired",
    };

    private static readonly HashSet<string> AnnotationKeywords = new(StringComparer.Ordinal)
    {
        "$comment",
        "title",
        "description",
        "default",
        "examples",
        "deprecated",
        "readOnly",
        "writeOnly",
    };

    /// <summary>Gets the validation keywords the classifier has a compatibility rule
    /// for. The Node gate (<c>scripts/contracts-schema-diff.mjs</c>) must carry the
    /// same list; a test compares the two.</summary>
    internal static IReadOnlySet<string> SupportedKeywords => HandledKeywords;

    /// <summary>Gets the keywords treated as annotations (no compatibility effect).
    /// The Node gate must carry the same list; a test compares the two.</summary>
    internal static IReadOnlySet<string> AnnotationOnlyKeywords => AnnotationKeywords;

    /// <summary>Compares two JSON Schema documents given as JSON text.</summary>
    /// <param name="previousJson">The previous (baseline) schema document.</param>
    /// <param name="nextJson">The next (candidate) schema document.</param>
    /// <returns>The classified diff result.</returns>
    public static SchemaDiffResult Compare(string previousJson, string nextJson)
    {
        ArgumentNullException.ThrowIfNull(previousJson);
        ArgumentNullException.ThrowIfNull(nextJson);

        using var previous = JsonDocument.Parse(previousJson);
        using var next = JsonDocument.Parse(nextJson);
        return Compare(previous.RootElement, next.RootElement);
    }

    /// <summary>Compares two parsed JSON Schema documents.</summary>
    /// <param name="previous">The previous (baseline) schema root element.</param>
    /// <param name="next">The next (candidate) schema root element.</param>
    /// <returns>The classified diff result.</returns>
    public static SchemaDiffResult Compare(JsonElement previous, JsonElement next)
    {
        var changes = new List<SchemaChange>();
        DiffSchema(previous, next, "$", changes);
        return new SchemaDiffResult(changes);
    }

    private static void DiffSchema(
        JsonElement previous,
        JsonElement next,
        string path,
        List<SchemaChange> changes)
    {
        if (JsonElement.DeepEquals(previous, next))
        {
            return;
        }

        if (previous.ValueKind != JsonValueKind.Object || next.ValueKind != JsonValueKind.Object)
        {
            var relaxation = previous.ValueKind == JsonValueKind.False
                || next.ValueKind == JsonValueKind.True;
            AddChange(
                changes,
                relaxation ? ChangeSeverity.NonBreaking : ChangeSeverity.Breaking,
                path,
                "boolean or non-object schema changed");
            return;
        }

        DiffProperties(previous, next, path, changes);
        DiffType(previous, next, path, changes);
        DiffEnum(previous, next, path, changes);
        DiffExactConstraint(previous, next, "$id", path, removalIsBreaking: true, changes);
        DiffExactConstraint(previous, next, "$schema", path, removalIsBreaking: true, changes);
        DiffExactConstraint(previous, next, "$ref", path, removalIsBreaking: true, changes);
        DiffExactConstraint(previous, next, "const", path, removalIsBreaking: true, changes);
        DiffExactConstraint(previous, next, "pattern", path, removalIsBreaking: false, changes);
        DiffExactConstraint(previous, next, "discriminator", path, removalIsBreaking: true, changes);
        DiffMinimum(previous, next, "minLength", path, changes);
        DiffMinimum(previous, next, "minItems", path, changes);
        DiffMinimum(previous, next, "minProperties", path, changes);
        DiffItems(previous, next, path, changes);
        DiffUnion(previous, next, "anyOf", path, changes);
        DiffUnion(previous, next, "oneOf", path, changes);
        DiffUnion(previous, next, "allOf", path, changes);
        DiffDefinitions(previous, next, "$defs", path, changes);
        DiffDefinitions(previous, next, "definitions", path, changes);
        DiffConditional(previous, next, "if", path, changes);
        DiffConditional(previous, next, "then", path, changes);
        DiffConditional(previous, next, "else", path, changes);
        DiffConditional(previous, next, "dependentSchemas", path, changes);
        DiffConditional(previous, next, "dependentRequired", path, changes);
        DiffUnhandledKeywords(previous, next, path, changes);
    }

    private static void DiffProperties(
        JsonElement previous,
        JsonElement next,
        string path,
        List<SchemaChange> changes)
    {
        var previousProperties = ReadProperties(previous);
        var nextProperties = ReadProperties(next);
        var previousRequired = ReadRequired(previous);
        var nextRequired = ReadRequired(next);

        foreach (var name in previousProperties.Keys)
        {
            if (!nextProperties.ContainsKey(name))
            {
                AddChange(changes, ChangeSeverity.Breaking, path, $"property '{name}' was removed");
            }
        }

        foreach (var name in nextProperties.Keys)
        {
            if (!previousProperties.ContainsKey(name))
            {
                var nowRequired = nextRequired.Contains(name);
                AddChange(
                    changes,
                    nowRequired ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                    path,
                    nowRequired
                        ? $"property '{name}' was added as required"
                        : $"optional property '{name}' was added");
            }
        }

        foreach (var (name, previousProperty) in previousProperties)
        {
            if (nextProperties.TryGetValue(name, out var nextProperty))
            {
                DiffSchema(
                    previousProperty,
                    nextProperty,
                    $"{path}.properties['{name}']",
                    changes);
            }
        }

        foreach (var name in nextRequired)
        {
            var reportedAsNewRequiredProperty = !previousProperties.ContainsKey(name)
                && nextProperties.ContainsKey(name);
            if (!previousRequired.Contains(name) && !reportedAsNewRequiredProperty)
            {
                AddChange(changes, ChangeSeverity.Breaking, path, $"property '{name}' became required");
            }
        }

        foreach (var name in previousRequired)
        {
            if (nextProperties.ContainsKey(name) && !nextRequired.Contains(name))
            {
                AddChange(
                    changes,
                    ChangeSeverity.NonBreaking,
                    path,
                    $"property '{name}' is no longer required");
            }
        }
    }

    private static void DiffType(
        JsonElement previous,
        JsonElement next,
        string path,
        List<SchemaChange> changes)
    {
        var hadType = TryGetKeyword(previous, "type", out var previousType);
        var hasType = TryGetKeyword(next, "type", out var nextType);
        if (!hadType && !hasType)
        {
            return;
        }

        if (hadType && !hasType)
        {
            AddChange(changes, ChangeSeverity.NonBreaking, path, "declared type constraint was removed");
        }
        else if (!hadType && hasType)
        {
            AddChange(changes, ChangeSeverity.Breaking, path, "declared type constraint was added");
        }
        else if (!JsonElement.DeepEquals(previousType, nextType))
        {
            AddChange(
                changes,
                ChangeSeverity.Breaking,
                path,
                $"declared type changed from {previousType.GetRawText()} to {nextType.GetRawText()}");
        }
    }

    private static void DiffEnum(
        JsonElement previous,
        JsonElement next,
        string path,
        List<SchemaChange> changes)
    {
        var hadEnum = TryGetKeyword(previous, "enum", out var previousEnum);
        var hasEnum = TryGetKeyword(next, "enum", out var nextEnum);
        if (!hadEnum && !hasEnum)
        {
            return;
        }

        if (!hadEnum || !hasEnum)
        {
            AddChange(
                changes,
                ChangeSeverity.Breaking,
                path,
                "enum constraint was added or removed");
            return;
        }

        if ((hadEnum && previousEnum.ValueKind != JsonValueKind.Array)
            || (hasEnum && nextEnum.ValueKind != JsonValueKind.Array))
        {
            if (!hadEnum || !hasEnum || !JsonElement.DeepEquals(previousEnum, nextEnum))
            {
                AddChange(changes, ChangeSeverity.Breaking, path, "enum constraint changed shape");
            }

            return;
        }

        var previousMembers = previousEnum.EnumerateArray().ToArray();
        var nextMembers = nextEnum.EnumerateArray().ToArray();

        foreach (var member in previousMembers)
        {
            if (!ContainsEquivalent(nextMembers, member))
            {
                AddChange(
                    changes,
                    ChangeSeverity.Breaking,
                    path,
                    $"enum member {member.GetRawText()} was removed");
            }
        }

        foreach (var member in nextMembers)
        {
            if (!ContainsEquivalent(previousMembers, member))
            {
                AddChange(
                    changes,
                    ChangeSeverity.Notice,
                    path,
                    $"enum member {member.GetRawText()} was added");
            }
        }
    }

    private static void DiffExactConstraint(
        JsonElement previous,
        JsonElement next,
        string keyword,
        string path,
        bool removalIsBreaking,
        List<SchemaChange> changes)
    {
        var hadConstraint = TryGetKeyword(previous, keyword, out var previousConstraint);
        var hasConstraint = TryGetKeyword(next, keyword, out var nextConstraint);
        if (!hadConstraint && !hasConstraint)
        {
            return;
        }

        if (hadConstraint
            && hasConstraint
            && JsonElement.DeepEquals(previousConstraint, nextConstraint))
        {
            return;
        }

        if (hadConstraint && !hasConstraint && !removalIsBreaking)
        {
            AddChange(
                changes,
                ChangeSeverity.NonBreaking,
                path,
                $"'{keyword}' constraint was removed");
            return;
        }

        AddChange(changes, ChangeSeverity.Breaking, path, $"'{keyword}' constraint changed");
    }

    private static void DiffMinimum(
        JsonElement previous,
        JsonElement next,
        string keyword,
        string path,
        List<SchemaChange> changes)
    {
        var hadMinimum = TryGetKeyword(previous, keyword, out var previousMinimum);
        var hasMinimum = TryGetKeyword(next, keyword, out var nextMinimum);
        if (!hadMinimum && !hasMinimum)
        {
            return;
        }

        var validPrevious = !hadMinimum
            || (previousMinimum.ValueKind == JsonValueKind.Number
                && previousMinimum.TryGetInt64(out var previousValue)
                && previousValue >= 0);
        var validNext = !hasMinimum
            || (nextMinimum.ValueKind == JsonValueKind.Number
                && nextMinimum.TryGetInt64(out var nextValue)
                && nextValue >= 0);
        if (!validPrevious || !validNext)
        {
            if (!hadMinimum
                || !hasMinimum
                || !JsonElement.DeepEquals(previousMinimum, nextMinimum))
            {
                AddChange(
                    changes,
                    ChangeSeverity.Breaking,
                    path,
                    $"'{keyword}' constraint changed shape");
            }

            return;
        }

        var previousResolved = hadMinimum ? previousMinimum.GetInt64() : 0;
        var nextResolved = hasMinimum ? nextMinimum.GetInt64() : 0;
        if (nextResolved > previousResolved)
        {
            AddChange(
                changes,
                ChangeSeverity.Breaking,
                path,
                $"'{keyword}' increased from {previousResolved} to {nextResolved}");
        }
        else if (nextResolved < previousResolved)
        {
            AddChange(
                changes,
                ChangeSeverity.NonBreaking,
                path,
                $"'{keyword}' decreased from {previousResolved} to {nextResolved}");
        }
    }

    private static void DiffItems(
        JsonElement previous,
        JsonElement next,
        string path,
        List<SchemaChange> changes)
    {
        var hadItems = TryGetKeyword(previous, "items", out var previousItems);
        var hasItems = TryGetKeyword(next, "items", out var nextItems);
        if (!hadItems && !hasItems)
        {
            return;
        }

        if (!hadItems || !hasItems)
        {
            AddChange(
                changes,
                ChangeSeverity.Breaking,
                path,
                "'items' schema was added or removed");
            return;
        }

        DiffSchema(previousItems, nextItems, $"{path}.items", changes);
    }

    private static void DiffUnion(
        JsonElement previous,
        JsonElement next,
        string keyword,
        string path,
        List<SchemaChange> changes)
    {
        var hadUnion = TryGetKeyword(previous, keyword, out var previousUnion);
        var hasUnion = TryGetKeyword(next, keyword, out var nextUnion);
        if (!hadUnion && !hasUnion)
        {
            return;
        }

        if (!hadUnion
            || !hasUnion
            || previousUnion.ValueKind != JsonValueKind.Array
            || nextUnion.ValueKind != JsonValueKind.Array)
        {
            if (!hadUnion || !hasUnion || !JsonElement.DeepEquals(previousUnion, nextUnion))
            {
                AddChange(
                    changes,
                    ChangeSeverity.Breaking,
                    path,
                    $"'{keyword}' union changed shape");
            }

            return;
        }

        var previousArms = previousUnion.EnumerateArray().ToArray();
        var nextArms = nextUnion.EnumerateArray().ToArray();
        foreach (var arm in previousArms)
        {
            if (!ContainsEquivalent(nextArms, arm))
            {
                AddChange(
                    changes,
                    ChangeSeverity.Breaking,
                    path,
                    $"'{keyword}' union arm {arm.GetRawText()} was removed or narrowed");
            }
        }

        foreach (var arm in nextArms)
        {
            if (!ContainsEquivalent(previousArms, arm))
            {
                AddChange(
                    changes,
                    keyword == "anyOf" ? ChangeSeverity.Notice : ChangeSeverity.Breaking,
                    path,
                    $"'{keyword}' union arm {arm.GetRawText()} was added");
            }
        }
    }

    /// <summary>
    /// <c>$defs</c> (2019-09+) and <c>definitions</c> (draft-07) are schema
    /// containers, not validation keywords: each named entry is a schema in its own
    /// right, so the classifier recurses per entry. An added definition is
    /// additive; a removed definition is breaking; a changed definition is
    /// classified by what changed inside it.
    /// </summary>
    private static void DiffDefinitions(
        JsonElement previous,
        JsonElement next,
        string keyword,
        string path,
        List<SchemaChange> changes)
    {
        var hadDefinitions = TryGetKeyword(previous, keyword, out var previousDefinitions);
        var hasDefinitions = TryGetKeyword(next, keyword, out var nextDefinitions);
        if (!hadDefinitions && !hasDefinitions)
        {
            return;
        }

        if ((hadDefinitions && previousDefinitions.ValueKind != JsonValueKind.Object)
            || (hasDefinitions && nextDefinitions.ValueKind != JsonValueKind.Object))
        {
            if (!hadDefinitions
                || !hasDefinitions
                || !JsonElement.DeepEquals(previousDefinitions, nextDefinitions))
            {
                AddChange(
                    changes,
                    ChangeSeverity.Breaking,
                    path,
                    $"'{keyword}' container changed shape");
            }

            return;
        }

        var previousEntries = ReadObjectMembers(previousDefinitions);
        var nextEntries = ReadObjectMembers(nextDefinitions);

        foreach (var name in previousEntries.Keys)
        {
            if (!nextEntries.ContainsKey(name))
            {
                AddChange(changes, ChangeSeverity.Breaking, path, $"definition '{name}' was removed");
            }
        }

        foreach (var name in nextEntries.Keys)
        {
            if (!previousEntries.ContainsKey(name))
            {
                AddChange(changes, ChangeSeverity.NonBreaking, path, $"definition '{name}' was added");
            }
        }

        foreach (var (name, previousDefinition) in previousEntries)
        {
            if (nextEntries.TryGetValue(name, out var nextDefinition))
            {
                DiffSchema(
                    previousDefinition,
                    nextDefinition,
                    $"{path}.{keyword}['{name}']",
                    changes);
            }
        }
    }

    /// <summary>
    /// <c>if</c> / <c>then</c> / <c>else</c> / <c>dependentSchemas</c> /
    /// <c>dependentRequired</c> are the applicator keywords that make a document's
    /// validity depend on its own shape. Adding one, or changing one, can only
    /// reject documents the previous schema accepted, so it is a narrowing
    /// (breaking). Removing one only widens the accepted set (non-breaking). The
    /// Node gate (<c>scripts/contracts-schema-diff.mjs</c>) carries the same rule.
    /// </summary>
    private static void DiffConditional(
        JsonElement previous,
        JsonElement next,
        string keyword,
        string path,
        List<SchemaChange> changes)
    {
        var hadConditional = TryGetKeyword(previous, keyword, out var previousConditional);
        var hasConditional = TryGetKeyword(next, keyword, out var nextConditional);
        if (!hadConditional && !hasConditional)
        {
            return;
        }

        if (hadConditional
            && hasConditional
            && JsonElement.DeepEquals(previousConditional, nextConditional))
        {
            return;
        }

        if (hadConditional && !hasConditional)
        {
            AddChange(
                changes,
                ChangeSeverity.NonBreaking,
                path,
                $"'{keyword}' conditional was removed");
            return;
        }

        AddChange(
            changes,
            ChangeSeverity.Breaking,
            path,
            $"'{keyword}' conditional was added or narrowed");
    }

    private static void DiffUnhandledKeywords(
        JsonElement previous,
        JsonElement next,
        string path,
        List<SchemaChange> changes)
    {
        var previousKeywords = ReadObjectMembers(previous);
        var nextKeywords = ReadObjectMembers(next);
        var keywords = previousKeywords.Keys
            .Concat(nextKeywords.Keys)
            .Distinct(StringComparer.Ordinal);

        foreach (var keyword in keywords)
        {
            if (HandledKeywords.Contains(keyword) || IsAnnotation(keyword))
            {
                continue;
            }

            var hadKeyword = previousKeywords.TryGetValue(keyword, out var previousValue);
            var hasKeyword = nextKeywords.TryGetValue(keyword, out var nextValue);
            if (!hadKeyword || !hasKeyword || !JsonElement.DeepEquals(previousValue, nextValue))
            {
                AddChange(
                    changes,
                    ChangeSeverity.Breaking,
                    path,
                    $"unsupported compatibility keyword '{keyword}' changed; safety cannot be proven");
            }
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadProperties(JsonElement schema)
    {
        if (TryGetKeyword(schema, "properties", out var properties)
            && properties.ValueKind == JsonValueKind.Object)
        {
            return ReadObjectMembers(properties);
        }

        return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> ReadRequired(JsonElement schema)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (TryGetKeyword(schema, "required", out var required)
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

    private static IReadOnlyDictionary<string, JsonElement> ReadObjectMembers(JsonElement value)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                result[property.Name] = property.Value;
            }
        }

        return result;
    }

    private static bool TryGetKeyword(
        JsonElement schema,
        string keyword,
        out JsonElement value)
    {
        if (schema.ValueKind == JsonValueKind.Object)
        {
            return schema.TryGetProperty(keyword, out value);
        }

        value = default;
        return false;
    }

    private static bool ContainsEquivalent(IEnumerable<JsonElement> values, JsonElement expected) =>
        values.Any(value => JsonElement.DeepEquals(value, expected));

    private static bool IsAnnotation(string keyword) =>
        AnnotationKeywords.Contains(keyword)
        || keyword.StartsWith("x-", StringComparison.Ordinal);

    private static void AddChange(
        List<SchemaChange> changes,
        ChangeSeverity severity,
        string path,
        string description) =>
        changes.Add(new SchemaChange(severity, $"{path}: {description}"));
}
