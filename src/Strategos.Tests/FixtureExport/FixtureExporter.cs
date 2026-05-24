// =============================================================================
// <copyright file="FixtureExporter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using Strategos.Contracts;

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// Exports the #53 builder-fixture corpus: each <see cref="WorkflowCorpus.Case"/>
/// is projected via <c>ToContract()</c> and serialized through the contracts
/// canonical serializer (<see cref="ContractsJson"/>), written to
/// <c>artifacts/builder-fixtures/&lt;tag&gt;/&lt;name&gt;.json</c> with an
/// <c>index.json</c> manifest carrying per-fixture combinator-coverage tags.
/// </summary>
/// <remarks>
/// The export is <b>atomic</b> (T24): every fixture is written into a sibling
/// temp directory first; only on full success is the destination replaced by a
/// single move. A failure part-way through leaves no half-written
/// <c>artifacts/</c> directory.
/// </remarks>
internal static class FixtureExporter
{
    /// <summary>One entry in the fixture manifest.</summary>
    public sealed record ManifestEntry(string Tag, string Name, string Path, int StepCount);

    /// <summary>The fixture manifest written as <c>index.json</c>.</summary>
    public sealed record Manifest(
        int Count,
        IReadOnlyList<string> Tags,
        IReadOnlyDictionary<string, int> CountByTag,
        IReadOnlyList<ManifestEntry> Fixtures);

    // The manifest is tooling metadata (not a wire contract): camelCase keys so
    // index.json reads naturally for the CI gate and humans.
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Exports every corpus case to <paramref name="destination"/> atomically.
    /// </summary>
    /// <param name="cases">The corpus cases to export.</param>
    /// <param name="destination">The target <c>builder-fixtures</c> directory.</param>
    /// <param name="caseTransform">
    /// Optional hook (test seam) applied to each case before serialization; used
    /// by the atomicity test to inject a mid-export failure.
    /// </param>
    /// <returns>The written manifest.</returns>
    public static Manifest Export(
        IReadOnlyList<WorkflowCorpus.Case> cases,
        string destination,
        Action<WorkflowCorpus.Case>? caseTransform = null)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(destination))!;
        Directory.CreateDirectory(parent);
        var temp = Path.Combine(parent, ".builder-fixtures.tmp-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(temp);

            var entries = new List<ManifestEntry>();
            foreach (var c in cases)
            {
                caseTransform?.Invoke(c);

                var v1 = c.Workflow.ToContract();
                var json = ContractsJson.Serialize(v1);

                var relPath = Path.Combine(c.Tag, c.Name + ".json");
                var fullPath = Path.Combine(temp, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, json);

                entries.Add(new ManifestEntry(c.Tag, c.Name, relPath.Replace('\\', '/'), v1.Steps.Count));
            }

            var manifest = new Manifest(
                Count: entries.Count,
                Tags: WorkflowCorpus.Tags,
                CountByTag: entries.GroupBy(e => e.Tag).ToDictionary(g => g.Key, g => g.Count()),
                Fixtures: entries);

            File.WriteAllText(
                Path.Combine(temp, "index.json"),
                JsonSerializer.Serialize(manifest, ManifestOptions));

            // Atomic publish: replace the destination in a single move (T24).
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            Directory.Move(temp, destination);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }
}
