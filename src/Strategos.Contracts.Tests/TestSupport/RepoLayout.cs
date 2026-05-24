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
