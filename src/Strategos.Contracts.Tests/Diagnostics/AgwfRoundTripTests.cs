// =============================================================================
// <copyright file="AgwfRoundTripTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T8 — the cross-product round-trip-by-NAME contract (exarchos T6 mirrors this).
/// Every <c>agwf-catalog.json</c> entry's <c>id</c> maps to an <c>AgwfCode</c>
/// enum member <strong>by member name</strong> (NOT ordinal — the INV-5
/// condition), and the enum member's wire value maps back to that <c>id</c>.
/// This is the exact relation Exarchos's generated TypeScript enum must satisfy.
/// </summary>
[Property("Category", "Diagnostics")]
public class AgwfRoundTripTests
{
    /// <summary>
    /// For each catalog entry: (1) its <c>name</c> is a real <c>AgwfCode</c>
    /// member name; (2) serializing that member yields the entry's <c>id</c>;
    /// (3) deserializing the <c>id</c> yields back the same member name. The map
    /// is total and stable, asserted on names, never ordinals.
    /// </summary>
    [Test]
    public async Task AgwfCatalog_RoundTripsByName_AgainstGeneratedEnum()
    {
        var enumType = typeof(ContractsMarker).Assembly
            .GetTypes()
            .First(t => t.IsEnum
                && t.Namespace == "Strategos.Contracts.Generated"
                && t.Name == "AgwfCode");

        var memberNames = Enum.GetNames(enumType).ToHashSet(StringComparer.Ordinal);
        var options = Strategos.Contracts.ContractsJson.Options;

        var catalogPath = Path.Combine(
            RepoLayout.ContractsProjectDir, "Generated", "agwf-catalog.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(catalogPath));
        var entries = doc.RootElement.GetProperty("entries");

        var matchedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries.EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString()!;
            var id = entry.GetProperty("id").GetString()!;

            // (1) name is a real enum member NAME (not an ordinal lookup).
            await Assert.That(memberNames).Contains(name)
                .Because($"catalog entry name '{name}' must be an AgwfCode member name (by name, not ordinal).");

            var value = Enum.Parse(enumType, name);

            // (2) member serializes to the catalog id wire value.
            var json = JsonSerializer.Serialize(value, enumType, options);
            await Assert.That(json).IsEqualTo($"\"{id}\"")
                .Because($"AgwfCode.{name} must serialize to the catalog id \"{id}\".");

            // (3) the id deserializes back to the same member by name.
            var back = JsonSerializer.Deserialize($"\"{id}\"", enumType, options);
            await Assert.That(back!.ToString()).IsEqualTo(name)
                .Because($"catalog id \"{id}\" must round-trip back to AgwfCode.{name} by name.");

            matchedNames.Add(name);
        }

        // The map is total: every enum member is covered by a catalog entry.
        await Assert.That(matchedNames.Count).IsEqualTo(memberNames.Count)
            .Because("the name<->id map must be total (every AgwfCode member has a catalog entry).");
    }
}
