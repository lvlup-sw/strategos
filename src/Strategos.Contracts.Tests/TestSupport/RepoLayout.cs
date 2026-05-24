// =============================================================================
// <copyright file="RepoLayout.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests;

/// <summary>
/// Resolves well-known paths in the Strategos repository layout relative to the
/// running test assembly. Walks up from the build output directory to the repo
/// root (the directory containing <c>src/strategos.sln</c>).
/// </summary>
internal static class RepoLayout
{
    /// <summary>Gets the repository root (the directory containing <c>src/strategos.sln</c>).</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>Gets the absolute path to <c>src/Strategos.Contracts</c>.</summary>
    public static string ContractsProjectDir { get; } =
        Path.Combine(RepoRoot, "src", "Strategos.Contracts");

    /// <summary>Gets the absolute path to the exported <c>artifacts/builder-fixtures</c>
    /// directory (the #53 fixture corpus; produced by the fixture-export tests).</summary>
    public static string BuilderFixturesDir { get; } =
        Path.Combine(RepoRoot, "artifacts", "builder-fixtures");

    /// <summary>Gets the absolute path to the bundled workflow IR JSON Schema
    /// (<c>schemas/workflow-definition-v1.schema.json</c>) — the equivalence-gate
    /// target every fixture validates against.</summary>
    public static string WorkflowSchemaPath { get; } =
        Path.Combine(ContractsProjectDir, "schemas", "workflow-definition-v1.schema.json");

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
            "Could not locate repo root (no src/strategos.sln found walking up from "
            + AppContext.BaseDirectory + ").");
    }
}
