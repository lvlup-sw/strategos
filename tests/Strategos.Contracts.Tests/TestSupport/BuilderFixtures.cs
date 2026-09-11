// =============================================================================
// <copyright file="BuilderFixtures.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests;

/// <summary>
/// Resolves the #53 builder-fixture corpus for tests that consume it.
/// </summary>
/// <remarks>
/// The corpus is exported by a test in <c>Strategos.Tests</c> (it needs the
/// builder), lands in the gitignored <c>artifacts/builder-fixtures</c>, and is
/// consumed here by packaging (it is embedded as package content) and by the
/// Zod conformance gate (it is the corpus both arms must accept). Either test
/// may be the first to run, so the export is driven on demand rather than
/// assumed.
/// </remarks>
internal static class BuilderFixtures
{
    private static readonly SemaphoreSlim ExportGate = new(1, 1);

    /// <summary>Exports the corpus if it is not already on disk.</summary>
    /// <returns>The corpus directory.</returns>
    public static async Task<string> EnsureExportedAsync()
    {
        await ExportGate.WaitAsync();
        try
        {
            if (HasFixtures())
            {
                return RepoLayout.BuilderFixturesDir;
            }

            var testProject = Path.Combine(
                RepoLayout.RepoRoot, "tests", "Strategos.Tests", "Strategos.Tests.csproj");
            var run = await Cli.RunAsync(
                "dotnet",
                $"run --project \"{testProject}\" -- --treenode-filter \"/*/*/FixtureExportTests/*\"");
            if (run.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "fixture export failed; the builder corpus is unavailable:\n" + run.Output);
            }

            return RepoLayout.BuilderFixturesDir;
        }
        finally
        {
            ExportGate.Release();
        }
    }

    private static bool HasFixtures()
    {
        var dir = RepoLayout.BuilderFixturesDir;
        return Directory.Exists(dir)
            && Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                .Any(path => !path.EndsWith("index.json", StringComparison.Ordinal));
    }
}
