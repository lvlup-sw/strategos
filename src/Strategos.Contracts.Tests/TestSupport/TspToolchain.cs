// =============================================================================
// <copyright file="TspToolchain.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests;

/// <summary>
/// Drives the TypeSpec Node toolchain (npm restore + <c>tsp compile</c>) for the
/// contracts project from tests. Restore is performed once per test process.
/// </summary>
internal static class TspToolchain
{
    private static readonly SemaphoreSlim RestoreGate = new(1, 1);
    private static bool restored;

    /// <summary>
    /// Ensures <c>node_modules</c> is restored, then runs <c>tsp compile</c> in
    /// the contracts project directory, emitting into <c>schemas/</c> and
    /// <c>Generated/</c> per <c>tspconfig.yaml</c>.
    /// </summary>
    public static async Task<CliResult> CompileAsync()
    {
        await EnsureRestoredAsync();
        return await Cli.RunAsync(
            "npx", "tsp compile .", RepoLayout.ContractsProjectDir);
    }

    private static async Task EnsureRestoredAsync()
    {
        if (restored)
        {
            return;
        }

        await RestoreGate.WaitAsync();
        try
        {
            if (restored)
            {
                return;
            }

            var nodeModules = Path.Combine(RepoLayout.ContractsProjectDir, "node_modules", "@typespec");
            if (!Directory.Exists(nodeModules))
            {
                var install = await Cli.RunAsync(
                    "npm", "install --no-audit --no-fund", RepoLayout.ContractsProjectDir);
                if (install.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "npm install failed for the contracts toolchain:\n" + install.Output);
                }
            }

            restored = true;
        }
        finally
        {
            RestoreGate.Release();
        }
    }
}
