// =============================================================================
// <copyright file="AgwfCatalogSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T2 — the AGWF catalog TypeSpec source emits, after <c>tsp compile</c>, one
/// JSON Schema per ground-truth diagnostic code carrying machine-readable
/// <c>const</c> metadata (R-lit representation; DR-1). Asserts the catalog
/// enumerates exactly the 10 defined codes — <c>AGWF001, 002, 003, 004, 009,
/// 010, 012, 014, 015, 016</c> — with gaps preserved as gaps (INV-5: no
/// renumber), each carrying <c>id</c>/<c>severity</c>/<c>summary</c>/
/// <c>remediation</c>/<c>since</c>.
/// </summary>
[Property("Category", "Diagnostics")]
[NotInParallel("tsp-compile")]
public class AgwfCatalogSchemaTests
{
    /// <summary>The 10 ground-truth AGWF codes (INV-5: gaps stay gaps, no renumber).</summary>
    private static readonly string[] GroundTruthCodes =
    [
        "AGWF001", "AGWF002", "AGWF003", "AGWF004", "AGWF009",
        "AGWF010", "AGWF012", "AGWF014", "AGWF015", "AGWF016",
    ];

    /// <summary>
    /// Compiles the TypeSpec sources, then asserts each AGWF entry schema carries
    /// the five metadata fields as <c>const</c> literals and the union of their
    /// <c>id</c> consts equals exactly the 10 ground-truth codes.
    /// </summary>
    [Test]
    public async Task AgwfCatalogSchema_TenCodes_EmittedWithMetadata()
    {
        var compile = await TspToolchain.CompileAsync();
        await Assert.That(compile.ExitCode).IsEqualTo(0).Because(compile.Output);

        var schemaDir = Path.Combine(RepoLayout.ContractsProjectDir, "schemas", "json-schema");
        var entryFiles = Directory.GetFiles(schemaDir, "AgwfEntry*.json");

        await Assert.That(entryFiles.Length).IsEqualTo(GroundTruthCodes.Length)
            .Because($"exactly {GroundTruthCodes.Length} AGWF entry schemas must emit (INV-5).");

        var ids = new List<string>();
        foreach (var file in entryFiles)
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
            var props = doc.RootElement.GetProperty("properties");

            foreach (var field in new[] { "id", "severity", "summary", "remediation", "since" })
            {
                await Assert.That(props.TryGetProperty(field, out var fieldEl)).IsTrue()
                    .Because($"{Path.GetFileName(file)} must carry a '{field}' field.");
                await Assert.That(fieldEl.TryGetProperty("const", out _)).IsTrue()
                    .Because($"{Path.GetFileName(file)} '{field}' must be a const literal (R-lit).");
            }

            ids.Add(props.GetProperty("id").GetProperty("const").GetString()!);
        }

        await Assert.That(ids.OrderBy(x => x, StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(GroundTruthCodes.OrderBy(x => x, StringComparer.Ordinal).ToArray())
            .Because("the catalog must enumerate exactly the 10 ground-truth codes (INV-5: gaps stay gaps).");
    }
}
