// =============================================================================
// <copyright file="FixtureExportTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// T22 — the #53 fixture export. Runs the builder corpus through
/// <c>ToContract()</c> + the contracts canonical serializer and writes
/// <c>artifacts/builder-fixtures/&lt;tag&gt;/&lt;name&gt;.json</c> plus an
/// <c>index.json</c> manifest. The exported artifacts are the input to the T23
/// equivalence gate.
/// </summary>
[Property("Category", "FixtureExport")]
[NotInParallel("fixture-export")]
public class FixtureExportTests
{
    /// <summary>
    /// Exports the corpus and asserts ≥100 fixtures across all eight combinator
    /// tags, each from a real builder invocation, written to disk with a
    /// manifest.
    /// </summary>
    [Test]
    public async Task FixtureExport_Produces100PlusFixtures_AcrossAll8CombinatorTags()
    {
        var cases = WorkflowCorpus.All();

        var manifest = FixtureExporter.Export(cases, FixturePaths.BuilderFixturesDir);

        // ≥100 fixtures.
        await Assert.That(manifest.Count).IsGreaterThanOrEqualTo(100)
            .Because("the corpus must yield at least 100 fixtures.");

        // All eight combinator tags are covered, each non-empty.
        await Assert.That(manifest.Tags.Count).IsEqualTo(8);
        foreach (var tag in WorkflowCorpus.Tags)
        {
            await Assert.That(manifest.CountByTag.TryGetValue(tag, out var count) && count > 0).IsTrue()
                .Because($"tag {tag} must have at least one fixture.");
        }

        // The artifacts are actually on disk: every manifest entry maps to a file.
        foreach (var entry in manifest.Fixtures)
        {
            var path = Path.Combine(FixturePaths.BuilderFixturesDir, entry.Path);
            await Assert.That(File.Exists(path)).IsTrue()
                .Because($"fixture {entry.Name} must be written to {path}.");
        }

        // The index.json manifest is present and parseable.
        var indexPath = Path.Combine(FixturePaths.BuilderFixturesDir, "index.json");
        await Assert.That(File.Exists(indexPath)).IsTrue();
        using var index = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
        await Assert.That(index.RootElement.GetProperty("count").GetInt32())
            .IsEqualTo(manifest.Count);

        // A spot-checked fixture is valid wire JSON pinned to schemaVersion 1.0.
        var sample = manifest.Fixtures[0];
        using var fixtureDoc = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(FixturePaths.BuilderFixturesDir, sample.Path)));
        await Assert.That(fixtureDoc.RootElement.GetProperty("schemaVersion").GetString())
            .IsEqualTo("1.0");
    }
}
