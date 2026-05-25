// =============================================================================
// <copyright file="AgwfMarkdownTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T5 — the generated <c>docs/diagnostics/agwf.md</c> reference page. After
/// codegen, the page carries a Markdown table with the columns
/// id/severity/summary/remediation/since and exactly 10 data rows, one per
/// ground-truth code, sorted by ID.
/// </summary>
[Property("Category", "Diagnostics")]
public class AgwfMarkdownTests
{
    private static readonly string[] GroundTruthCodes =
    [
        "AGWF001", "AGWF002", "AGWF003", "AGWF004", "AGWF009",
        "AGWF010", "AGWF012", "AGWF014", "AGWF015", "AGWF016",
    ];

    /// <summary>
    /// Asserts the generated reference page exists, declares the five-column
    /// header, and has one data row per code in ascending ID order.
    /// </summary>
    [Test]
    public async Task AgwfMarkdown_TenRows_MatchesCatalog()
    {
        var docPath = Path.Combine(
            RepoLayout.RepoRoot, "docs", "diagnostics", "agwf.md");

        await Assert.That(File.Exists(docPath)).IsTrue()
            .Because($"the codegen pipeline must emit {docPath}.");

        var lines = await File.ReadAllLinesAsync(docPath);

        // The header row carries the five columns.
        var header = lines.FirstOrDefault(l =>
            l.Contains("| id ", StringComparison.OrdinalIgnoreCase)
            || l.TrimStart().StartsWith("| id", StringComparison.OrdinalIgnoreCase));
        await Assert.That(header).IsNotNull()
            .Because("the page must declare a table header beginning with an 'id' column.");
        foreach (var col in new[] { "id", "severity", "summary", "remediation", "since" })
        {
            await Assert.That(header!.Contains($" {col} ", StringComparison.OrdinalIgnoreCase)
                || header.Contains($"| {col}", StringComparison.OrdinalIgnoreCase)).IsTrue()
                .Because($"the table header must declare a '{col}' column.");
        }

        // The data rows: each ground-truth code appears as a row, in order.
        var dataRows = lines
            .Where(l => GroundTruthCodes.Any(c => l.Contains(c, StringComparison.Ordinal)))
            .ToList();

        await Assert.That(dataRows.Count).IsEqualTo(GroundTruthCodes.Length)
            .Because("the table must have exactly 10 data rows, one per code.");

        var rowIds = dataRows
            .Select(r => GroundTruthCodes.First(c => r.Contains(c, StringComparison.Ordinal)))
            .ToArray();
        await Assert.That(string.Join(",", rowIds))
            .IsEqualTo(string.Join(",", GroundTruthCodes))
            .Because("rows must be sorted by ID (ascending).");
    }
}
