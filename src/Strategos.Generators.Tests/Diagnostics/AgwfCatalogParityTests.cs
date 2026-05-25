// -----------------------------------------------------------------------
// <copyright file="AgwfCatalogParityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Strategos.Generators.Diagnostics;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// T11 — the AGWF catalog metadata-parity gate (INV-5). The diagnostic code
/// <em>identity</em> (the <c>AGWF0xx</c> id) is single-sourced from TypeSpec
/// (<c>AgwfCatalog.tsp</c>), but each descriptor's severity / title /
/// messageFormat is hand-authored in <see cref="WorkflowDiagnostics"/> — so it
/// can silently drift from the generated catalog
/// (<c>Generated/agwf-catalog.json</c>). This test closes that gap: for every
/// catalog entry it asserts the live <see cref="DiagnosticDescriptor"/> agrees on
/// all three drift-prone fields, matched by the <c>id</c> string.
/// </summary>
[Property("Category", "Unit")]
public sealed class AgwfCatalogParityTests
{
    /// <summary>
    /// For each of the catalog's AGWF codes, asserts the generated entry's
    /// metadata (severity / summary / remediation) equals the live
    /// <see cref="WorkflowDiagnostics"/> descriptor's
    /// (<see cref="DiagnosticDescriptor.DefaultSeverity"/> /
    /// <see cref="DiagnosticDescriptor.Title"/> /
    /// <see cref="DiagnosticDescriptor.MessageFormat"/>). Table-driven over the
    /// reflection-built id → descriptor map so all 10 codes are covered without
    /// hand-listing them.
    /// </summary>
    [Test]
    public async Task AgwfCatalog_MetadataParity_MatchesLiveDescriptors()
    {
        var entries = LoadCatalogEntries();
        var descriptorsById = BuildDescriptorMap();

        var mismatches = new List<string>();

        foreach (var entry in entries)
        {
            if (!descriptorsById.TryGetValue(entry.Id, out var descriptor))
            {
                mismatches.Add($"{entry.Id}: no WorkflowDiagnostics descriptor exposes this id");
                continue;
            }

            var descriptorSeverity = SeverityToCatalogString(descriptor.DefaultSeverity);
            if (!string.Equals(entry.Severity, descriptorSeverity, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{entry.Id} severity: catalog '{entry.Severity}' != descriptor '{descriptorSeverity}'");
            }

            var descriptorTitle = descriptor.Title.ToString();
            if (!string.Equals(entry.Summary, descriptorTitle, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{entry.Id} summary/title: catalog '{entry.Summary}' != descriptor '{descriptorTitle}'");
            }

            var descriptorMessage = descriptor.MessageFormat.ToString();
            if (!string.Equals(entry.Remediation, descriptorMessage, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{entry.Id} remediation/messageFormat: catalog '{entry.Remediation}' != descriptor '{descriptorMessage}'");
            }
        }

        await Assert.That(mismatches).IsEmpty()
            .Because("the hand-authored WorkflowDiagnostics descriptors must stay in parity with the "
                + "single-sourced AGWF catalog (#105 / INV-5). Offenders: " + string.Join(" | ", mismatches));
    }

    private static string SeverityToCatalogString(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Info => "info",
        DiagnosticSeverity.Hidden => "hidden",
        _ => severity.ToString().ToLowerInvariant(),
    };

    private static IReadOnlyList<CatalogEntry> LoadCatalogEntries()
    {
        var srcRoot = FindSrcRoot();
        var path = Path.Combine(srcRoot, "Strategos.Contracts", "Generated", "agwf-catalog.json");
        var json = File.ReadAllText(path);

        using var doc = JsonDocument.Parse(json);
        var entries = new List<CatalogEntry>();
        foreach (var element in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            entries.Add(new CatalogEntry(
                Id: element.GetProperty("id").GetString()!,
                Severity: element.GetProperty("severity").GetString()!,
                Summary: element.GetProperty("summary").GetString()!,
                Remediation: element.GetProperty("remediation").GetString()!));
        }

        return entries;
    }

    private static IReadOnlyDictionary<string, DiagnosticDescriptor> BuildDescriptorMap()
    {
        var map = new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal);
        var fields = typeof(WorkflowDiagnostics)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor));

        foreach (var field in fields)
        {
            var descriptor = (DiagnosticDescriptor)field.GetValue(null)!;
            map[descriptor.Id] = descriptor;
        }

        return map;
    }

    private static string FindSrcRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var src = Path.Combine(dir, "src");
            if (File.Exists(Path.Combine(src, "strategos.sln")))
            {
                return src;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate src/ (no src/strategos.sln found walking up from "
            + AppContext.BaseDirectory + ").");
    }

    private sealed record CatalogEntry(string Id, string Severity, string Summary, string Remediation);
}
