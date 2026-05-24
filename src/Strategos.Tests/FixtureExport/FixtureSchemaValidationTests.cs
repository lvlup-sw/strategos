// =============================================================================
// <copyright file="FixtureSchemaValidationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using NJsonSchema;

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// T23 — the equivalence gate (the design's projection-drift surface). Every
/// fixture emitted by <c>ToContract()</c> must validate against the bundled
/// <c>workflow-definition-v1.schema.json</c>. A schema/projection disagreement
/// fails here before either cross-product consumer sees it.
/// </summary>
[Property("Category", "FixtureExport")]
[NotInParallel("fixture-export")]
public class FixtureSchemaValidationTests
{
    /// <summary>
    /// Exports the corpus, then validates every fixture against the generated
    /// workflow IR schema; any validation error fails the gate.
    /// </summary>
    [Test]
    public async Task EveryFixture_ValidatesAgainst_WorkflowDefinitionV1Schema()
    {
        // Export fresh so the gate is self-contained (no cross-test ordering).
        var manifest = FixtureExporter.Export(WorkflowCorpus.All(), FixturePaths.BuilderFixturesDir);
        await Assert.That(manifest.Count).IsGreaterThanOrEqualTo(100);

        var schemaJson = await File.ReadAllTextAsync(FixturePaths.WorkflowSchemaPath);
        var schema = await JsonSchema.FromJsonAsync(schemaJson);

        var failures = new List<string>();
        foreach (var entry in manifest.Fixtures)
        {
            var fixturePath = Path.Combine(FixturePaths.BuilderFixturesDir, entry.Path);
            var json = await File.ReadAllTextAsync(fixturePath);

            var errors = schema.Validate(json);
            if (errors.Count > 0)
            {
                failures.Add($"{entry.Name}: {string.Join("; ", errors.Select(e => e.ToString()))}");
            }
        }

        await Assert.That(failures.Count).IsEqualTo(0)
            .Because("every projected fixture must validate against the wire schema:\n"
                + string.Join("\n", failures.Take(10)));
    }
}
