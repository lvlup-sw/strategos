// =============================================================================
// <copyright file="BuilderFixtures.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

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

    /// <summary>
    /// Whether the corpus on disk is complete enough to reuse.
    /// </summary>
    /// <remarks>
    /// "Any file is present" is not the question. A directory left by an older build
    /// can hold plenty of valid fixtures and still not be the corpus this run needs,
    /// and a conformance gate that passed against it would be reporting on a corpus
    /// nobody asked for. The manifest is the authority: its <c>count</c> must match
    /// what is on disk, and every fixture it lists must exist.
    /// </remarks>
    private static bool HasFixtures()
    {
        var dir = RepoLayout.BuilderFixturesDir;
        var manifestPath = Path.Combine(dir, "index.json");
        if (!Directory.Exists(dir) || !File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            if (!root.TryGetProperty("count", out var count)
                || !root.TryGetProperty("fixtures", out var fixtures))
            {
                return false;
            }

            var onDisk = Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                .Count(path => !path.EndsWith("index.json", StringComparison.Ordinal));
            if (onDisk != count.GetInt32())
            {
                return false;
            }

            return fixtures.EnumerateArray().All(entry =>
                entry.TryGetProperty("path", out var relative)
                && relative.GetString() is { } value
                && File.Exists(Path.Combine(dir, value)));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
