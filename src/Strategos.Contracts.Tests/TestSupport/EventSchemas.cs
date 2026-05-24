// =============================================================================
// <copyright file="EventSchemas.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests;

/// <summary>
/// Loads TypeSpec-emitted JSON Schema documents for the events family and
/// resolves the small subset of <c>$ref</c> / <c>enum</c> shapes the schema
/// assertions need. The emitter writes one <c>*.json</c> per model/enum into
/// <c>schemas/json-schema/</c>; an enum-typed property is emitted as a
/// <c>$ref</c> to its own document, so <see cref="EnumValues"/> follows that ref.
/// </summary>
internal static class EventSchemas
{
    /// <summary>Gets the directory the JSON Schema documents are emitted into.</summary>
    public static string SchemaDir { get; } =
        Path.Combine(RepoLayout.ContractsProjectDir, "schemas", "json-schema");

    /// <summary>Loads the root element of an emitted schema document by model name.</summary>
    /// <param name="modelName">The TypeSpec model/enum name (file name without extension).</param>
    public static async Task<JsonElement> LoadAsync(string modelName)
    {
        var path = Path.Combine(SchemaDir, modelName + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"expected emitted schema at {path} (did `tsp compile` run and emit {modelName}?)", path);
        }

        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>Returns true if a schema document for the given model name was emitted.</summary>
    public static bool Exists(string modelName) =>
        File.Exists(Path.Combine(SchemaDir, modelName + ".json"));

    /// <summary>Lists every emitted schema document name (file name without extension).</summary>
    public static IReadOnlyList<string> AllModelNames() =>
        Directory.Exists(SchemaDir)
            ? Directory.GetFiles(SchemaDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList()
            : Array.Empty<string>();

    /// <summary>
    /// Resolves the string enum values for a property element, whether the enum
    /// is inlined (<c>enum: [...]</c>) or referenced via <c>$ref</c> to a
    /// separate emitted enum document.
    /// </summary>
    public static IReadOnlyList<string> EnumValues(JsonElement property)
    {
        if (property.TryGetProperty("enum", out var inlined) && inlined.ValueKind == JsonValueKind.Array)
        {
            return ReadStrings(inlined);
        }

        if (property.TryGetProperty("$ref", out var refEl))
        {
            var refName = Path.GetFileNameWithoutExtension(refEl.GetString());
            if (refName is not null)
            {
                var refRoot = LoadAsync(refName).GetAwaiter().GetResult();
                if (refRoot.TryGetProperty("enum", out var refEnum) && refEnum.ValueKind == JsonValueKind.Array)
                {
                    return ReadStrings(refEnum);
                }
            }
        }

        // anyOf of const values (alternate TypeSpec encoding for a union).
        if (property.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
        {
            var vals = new List<string>();
            foreach (var arm in anyOf.EnumerateArray())
            {
                if (arm.TryGetProperty("const", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    vals.Add(c.GetString()!);
                }
            }

            if (vals.Count > 0)
            {
                return vals;
            }
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement array) =>
        array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
}
