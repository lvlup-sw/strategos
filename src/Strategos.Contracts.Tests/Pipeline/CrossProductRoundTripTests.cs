// =============================================================================
// <copyright file="CrossProductRoundTripTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using NJsonSchema;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T31 — the cross-product round-trip harness (design §Resilience item 2,
/// exarchos#1247). Two directions, both run OFFLINE against OUR OWN emitted
/// artifacts:
/// <list type="number">
///   <item>
///     <b>fixture → Zod:</b> our emitted workflow-IR fixtures must parse against
///     a Zod schema generated from our own bundled JSON Schema (the zod-smoke
///     pipeline proves the JSON Schema → Zod step). This stands in, offline, for
///     "a fixture must parse against Exarchos's generated Zod".
///   </item>
///   <item>
///     <b>IR → our C# schema:</b> a representative IR JSON validates against our
///     C#-side (NJsonSchema) workflow schema — standing in, offline, for "an
///     Exarchos-emitted IR must validate against our schema".
///   </item>
/// </list>
/// <para>
/// EXTERNAL-COORDINATION SEAM (exarchos#1247): the PRODUCTION cross-product gate
/// must pin Exarchos's *published* Zod snapshot and run our fixtures against
/// THAT (not against Zod we re-derive from our own schema). Pinning that
/// snapshot is coordinated in exarchos#1247 and is OUT OF SCOPE here — this
/// harness derives Zod from our own schema so it stays runnable offline and in a
/// single repo. The seam is the <c>--zod-source</c> input of the harness
/// (currently "self"); production swaps it for the pinned Exarchos barrel. See
/// also <c>src/Strategos.Contracts/scripts/cross-product-roundtrip.mjs</c> and
/// the README "Cross-product round-trip" note.
/// </para>
/// </summary>
[Property("Category", "Pipeline")]
[NotInParallel("tsp-compile")]
public class CrossProductRoundTripTests
{
    /// <summary>
    /// Drives the offline harness: generate Zod from our bundled workflow schema,
    /// parse every emitted fixture against it, AND validate a representative IR
    /// against our C#-side NJsonSchema. Both directions must pass.
    /// </summary>
    [Test]
    public async Task CrossProduct_FixtureValidatesAgainstZod_AndZodIrValidatesHere()
    {
        // Direction 1 — fixture → Zod (offline, our own schema-derived Zod).
        var harness = Path.Combine(
            RepoLayout.ContractsProjectDir, "scripts", "cross-product-roundtrip.mjs");
        await Assert.That(File.Exists(harness)).IsTrue()
            .Because($"expected the cross-product round-trip harness at {harness}");

        var fixturesDir = RepoLayout.BuilderFixturesDir;
        await Assert.That(Directory.Exists(fixturesDir)).IsTrue()
            .Because($"expected exported fixtures at {fixturesDir} (run the #53 fixture export first)");

        var run = await Cli.RunAsync(
            "node",
            $"\"{harness}\" --fixtures \"{fixturesDir}\" --zod-source self",
            RepoLayout.ContractsProjectDir);

        await Assert.That(run.ExitCode).IsEqualTo(0)
            .Because($"every emitted fixture must parse against schema-derived Zod (offline):\n{run.Output}");
        await Assert.That(run.Output).Contains("fixtures parsed")
            .Because(run.Output);

        // Direction 2 — a representative IR validates against our C#-side schema.
        var schemaJson = await File.ReadAllTextAsync(RepoLayout.WorkflowSchemaPath);
        var schema = await JsonSchema.FromJsonAsync(schemaJson);

        var representative = await File.ReadAllTextAsync(FirstFixture(fixturesDir));
        var errors = schema.Validate(representative);

        await Assert.That(errors.Count).IsEqualTo(0)
            .Because("a representative IR must validate against our C# (NJsonSchema) schema:\n"
                + string.Join("\n", errors.Select(e => e.ToString())));
    }

    private static string FirstFixture(string fixturesDir)
    {
        return Directory.EnumerateFiles(fixturesDir, "*.json", SearchOption.AllDirectories)
            .First(p => !p.EndsWith("index.json", StringComparison.Ordinal));
    }
}
