// =============================================================================
// <copyright file="FixturePaths.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// Resolves the repository's <c>artifacts/builder-fixtures</c> directory by
/// walking up from the test assembly to the repo root (the directory holding
/// <c>src/strategos.sln</c>).
/// </summary>
internal static class FixturePaths
{
    /// <summary>Gets the repository root (directory containing <c>src/strategos.sln</c>).</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>Gets the canonical exported fixtures directory.</summary>
    public static string BuilderFixturesDir { get; } =
        Path.Combine(RepoRoot, "artifacts", "builder-fixtures");

    /// <summary>Gets the bundled workflow IR schema (the equivalence-gate target).</summary>
    public static string WorkflowSchemaPath { get; } = Path.Combine(
        RepoRoot, "src", "Strategos.Contracts", "schemas", "workflow-definition-v1.schema.json");

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "src", "strategos.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate repo root (no src/strategos.sln) from " + AppContext.BaseDirectory);
    }
}
