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
}
