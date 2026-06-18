// =============================================================================
// <copyright file="AgwfCatalogEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T3 — the canonical <c>agwf-catalog.json</c> data artifact. After the full
/// codegen pipeline runs, the catalog file exists under the contracts project,
/// carries a manifest (<c>catalog_version</c>), and enumerates exactly the 15
/// ground-truth entries ordered by ID, each with full metadata
/// (<c>name</c>/<c>id</c>/<c>severity</c>/<c>summary</c>/<c>remediation</c>/
/// <c>since</c>).
/// </summary>
[Property("Category", "Diagnostics")]
[NotInParallel("tsp-compile")]
public sealed class AgwfCatalogEmitterTests
{
    private static readonly string[] GroundTruthCodes =
    [
        "AGWF001", "AGWF002", "AGWF003", "AGWF004", "AGWF009",
        "AGWF010", "AGWF012", "AGWF014", "AGWF015", "AGWF016",
        "AGWF017", "AGWF018", "AGWF019", "AGWF020", "AGWF021",
    ];

    /// <summary>
    /// Regenerates from committed schemas and asserts the emitted catalog JSON
    /// has a <c>catalog_version</c> manifest field and exactly 15 entries ordered
    /// by ID, each carrying the full metadata set.
    /// </summary>
    [Test]
    public async Task AgwfCatalogEmitter_TenEntries_EmitsManifestAndEntries()
    {
        var catalogPath = Path.Combine(
            RepoLayout.ContractsProjectDir, "Generated", "agwf-catalog.json");

        await Assert.That(File.Exists(catalogPath)).IsTrue()
            .Because($"the codegen pipeline must emit {catalogPath}.");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(catalogPath));
        var root = doc.RootElement;

        await Assert.That(root.TryGetProperty("catalog_version", out _)).IsTrue()
            .Because("the catalog manifest must carry a catalog_version.");

        var entries = root.GetProperty("entries");
        await Assert.That(entries.GetArrayLength()).IsEqualTo(GroundTruthCodes.Length)
            .Because("the catalog must enumerate exactly the 15 ground-truth entries.");

        var ids = new List<string>();
        foreach (var entry in entries.EnumerateArray())
        {
            foreach (var field in new[] { "name", "id", "severity", "summary", "remediation", "since" })
            {
                await Assert.That(entry.TryGetProperty(field, out _)).IsTrue()
                    .Because($"each catalog entry must carry '{field}'.");
            }

            ids.Add(entry.GetProperty("id").GetString()!);
        }

        // GroundTruthCodes is already in ascending order; assert the emitted
        // sequence matches it positionally (entries ordered by ID).
        await Assert.That(string.Join(",", ids))
            .IsEqualTo(string.Join(",", GroundTruthCodes))
            .Because("entries must be ordered by ID (ascending).");
    }
}
