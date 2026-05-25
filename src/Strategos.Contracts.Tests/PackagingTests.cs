// =============================================================================
// <copyright file="PackagingTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.IO.Compression;

namespace Strategos.Contracts.Tests;

/// <summary>
/// Packaging-level tests for the <c>LevelUp.Strategos.Contracts</c> NuGet package:
/// the assembly identity (T1) and the JSON Schema content-file wiring (T4).
/// </summary>
[Property("Category", "Unit")]
public class PackagingTests
{
    /// <summary>
    /// Verifies the contracts assembly is identifiable as the project that backs
    /// the <c>LevelUp.Strategos.Contracts</c> package. The assembly name is the
    /// in-repo project name (<c>Strategos.Contracts</c>); the NuGet PackageId is
    /// <c>LevelUp.Strategos.Contracts</c> (asserted at pack time in T4).
    /// </summary>
    [Test]
    public async Task Package_Identity_IsLevelUpStrategosContracts()
    {
        var assembly = typeof(Strategos.Contracts.ContractsMarker).Assembly;

        await Assert.That(assembly.GetName().Name).IsEqualTo("Strategos.Contracts");
    }

    /// <summary>
    /// Packs the contracts project and asserts the emitted JSON Schema files are
    /// embedded under <c>contentFiles/any/any/schemas/</c> so language-neutral
    /// consumers (Exarchos) receive them with the NuGet package.
    /// </summary>
    [Test]
    [Property("Category", "Pack")]
    public async Task Nupkg_Contains_SchemasUnderContentFiles()
    {
        var projectDir = RepoLayout.ContractsProjectDir;
        var outputDir = Directory.CreateTempSubdirectory("contracts-pack-").FullName;

        try
        {
            var pack = await Cli.RunAsync(
                "dotnet",
                $"pack \"{Path.Combine(projectDir, "Strategos.Contracts.csproj")}\" -c Release -o \"{outputDir}\"");

            await Assert.That(pack.ExitCode).IsEqualTo(0).Because(pack.Output);

            var nupkg = Directory.GetFiles(outputDir, "LevelUp.Strategos.Contracts.*.nupkg")
                .FirstOrDefault(p => !p.EndsWith(".symbols.nupkg", StringComparison.Ordinal));

            await Assert.That(nupkg).IsNotNull();

            using var archive = ZipFile.OpenRead(nupkg!);
            var schemaEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("contentFiles/any/any/schemas/", StringComparison.Ordinal)
                    && e.FullName.EndsWith(".json", StringComparison.Ordinal))
                .ToList();

            await Assert.That(schemaEntries).IsNotEmpty();
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    /// <summary>
    /// T32 — the 0.2.0 release package. Packs the project and asserts:
    /// the version is 0.2.0; all three schema families (events / workflow /
    /// diagnostics) are embedded under <c>contentFiles/any/any/schemas/</c>; the
    /// #53 builder fixtures are embedded under
    /// <c>contentFiles/any/any/fixtures/</c> (so Exarchos can extract them); and
    /// the compiled contracts assembly ships under <c>lib/</c>.
    /// </summary>
    [Test]
    [Property("Category", "Pack")]
    public async Task Package_Version_Is_0_2_0_WithEventsIrAndDiagnosticsContent()
    {
        // The fixtures are content (T32): ensure they exist on disk first — the
        // #53 export writes them under artifacts/builder-fixtures/.
        await EnsureFixturesExportedAsync();

        var projectDir = RepoLayout.ContractsProjectDir;
        var outputDir = Directory.CreateTempSubdirectory("contracts-pack-020-").FullName;

        try
        {
            var pack = await Cli.RunAsync(
                "dotnet",
                $"pack \"{Path.Combine(projectDir, "Strategos.Contracts.csproj")}\" -c Release -o \"{outputDir}\"");

            await Assert.That(pack.ExitCode).IsEqualTo(0).Because(pack.Output);

            var nupkg = Directory.GetFiles(outputDir, "LevelUp.Strategos.Contracts.*.nupkg")
                .FirstOrDefault(p => !p.EndsWith(".symbols.nupkg", StringComparison.Ordinal));
            await Assert.That(nupkg).IsNotNull();

            // Version 0.2.0 — read from the file name (the canonical packed version).
            var fileName = Path.GetFileName(nupkg!);
            await Assert.That(fileName).IsEqualTo("LevelUp.Strategos.Contracts.0.2.0.nupkg")
                .Because($"the package must version at exactly 0.2.0; got {fileName}");

            using var archive = ZipFile.OpenRead(nupkg!);

            // The .nuspec also pins 0.2.0.
            var nuspec = archive.Entries.First(e =>
                e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
            using (var reader = new StreamReader(nuspec.Open()))
            {
                var nuspecXml = await reader.ReadToEndAsync();
                await Assert.That(nuspecXml).Contains("<version>0.2.0</version>")
                    .Because("the .nuspec must declare version 0.2.0.");
            }

            string[] entries = [.. archive.Entries.Select(e => e.FullName)];

            // All three families' schemas embedded under contentFiles/.../schemas/.
            const string schemaPath = "contentFiles/any/any/schemas/";
            await Assert.That(entries).Contains(e =>
                e.StartsWith(schemaPath, StringComparison.Ordinal)
                && e.EndsWith("SdlcEventEnvelope.json", StringComparison.Ordinal))
                .Because("the events family schema must be embedded.");
            await Assert.That(entries).Contains(e =>
                e.StartsWith(schemaPath, StringComparison.Ordinal)
                && e.EndsWith("WorkflowDefinitionV1.json", StringComparison.Ordinal))
                .Because("the workflow-IR family schema must be embedded.");
            await Assert.That(entries).Contains(e =>
                e.StartsWith(schemaPath, StringComparison.Ordinal)
                && e.EndsWith("InvariantEntry.json", StringComparison.Ordinal))
                .Because("the diagnostics (invariant) family schema must be embedded.");

            // The #53 builder fixtures embedded under contentFiles/.../fixtures/.
            var fixtureEntries = entries
                .Where(e => e.StartsWith("contentFiles/any/any/fixtures/", StringComparison.Ordinal)
                    && e.EndsWith(".json", StringComparison.Ordinal))
                .ToList();
            await Assert.That(fixtureEntries.Count).IsGreaterThanOrEqualTo(100)
                .Because("Exarchos extracts the ≥100 builder fixtures from the package.");

            // The compiled contracts assembly ships under lib/.
            await Assert.That(entries).Contains(e =>
                e.StartsWith("lib/", StringComparison.Ordinal)
                && e.EndsWith("Strategos.Contracts.dll", StringComparison.Ordinal))
                .Because("the compiled contracts assembly must ship in the package.");
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    /// <summary>
    /// S1–S4 (#63–#66) — the SMQ schemas are embedded as NuGet content. Packs the
    /// project and asserts the four new top-level schemas (and the union arm
    /// schemas) land under <c>contentFiles/any/any/schemas/</c> so language-neutral
    /// consumers (Exarchos) receive them with the package and can derive Zod.
    /// </summary>
    [Test]
    [Property("Category", "Pack")]
    public async Task Packaging_SmqSchemas_EmbeddedAsContent()
    {
        var projectDir = RepoLayout.ContractsProjectDir;
        var outputDir = Directory.CreateTempSubdirectory("contracts-pack-smq-").FullName;

        try
        {
            var pack = await Cli.RunAsync(
                "dotnet",
                $"pack \"{Path.Combine(projectDir, "Strategos.Contracts.csproj")}\" -c Release -o \"{outputDir}\"");

            await Assert.That(pack.ExitCode).IsEqualTo(0).Because(pack.Output);

            var nupkg = Directory.GetFiles(outputDir, "LevelUp.Strategos.Contracts.*.nupkg")
                .FirstOrDefault(p => !p.EndsWith(".symbols.nupkg", StringComparison.Ordinal));
            await Assert.That(nupkg).IsNotNull();

            using var archive = ZipFile.OpenRead(nupkg!);
            string[] entries = [.. archive.Entries.Select(e => e.FullName)];

            const string schemaPath = "contentFiles/any/any/schemas/";
            string[] required =
            [
                "MergeGateDecision.json",
                "JourneyResult.json",
                "WorkflowCatalog.json",
                "WorkflowRef.json",
                "ResponseMetaV1.json",
                "DegradedReason.json",
                "NextAction.json",
                "RunBuildkitePipelineAction.json",
                "EscalateHumanAction.json",
                "BuildkitePipelineParams.json",
                "CatalogWorkflowRef.json",
                "AuthoredWorkflowRef.json",
                "MergeQueueMetaV1.json",
                "PerfMetaV1.json",
                "JourneyOutcome.json",
                "BudgetConsumedV1.json",
                "MergeDecision.json",
                "DiffClassification.json",
                "FallbackReason.json",
                "JourneyOutcomeStatus.json",
            ];

            foreach (var schema in required)
            {
                await Assert.That(entries).Contains(e =>
                    e.StartsWith(schemaPath, StringComparison.Ordinal)
                    && e.EndsWith("/" + schema, StringComparison.Ordinal))
                    .Because($"the SMQ schema {schema} must be embedded under {schemaPath}.");
            }
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    /// <summary>
    /// Ensures the #53 builder fixtures are present on disk (the packaging step
    /// embeds them as content). Runs the fixture-export entry point if absent.
    /// </summary>
    private static async Task EnsureFixturesExportedAsync()
    {
        var fixturesDir = RepoLayout.BuilderFixturesDir;
        var hasFixtures = Directory.Exists(fixturesDir)
            && Directory.EnumerateFiles(fixturesDir, "*.json", SearchOption.AllDirectories)
                .Any(p => !p.EndsWith("index.json", StringComparison.Ordinal));
        if (hasFixtures)
        {
            return;
        }

        // The exporter lives in the Strategos.Tests project (it depends on the
        // builder). Drive it via its fixture-export test entry point so the
        // packaging test is self-contained when run in isolation.
        var testProj = Path.Combine(
            RepoLayout.RepoRoot, "src", "Strategos.Tests", "Strategos.Tests.csproj");
        var run = await Cli.RunAsync(
            "dotnet",
            $"run --project \"{testProj}\" -- --treenode-filter \"/*/*/FixtureExportTests/*\"");
        await Assert.That(run.ExitCode).IsEqualTo(0)
            .Because($"fixture export must succeed to populate package content:\n{run.Output}");
    }
}
