// =============================================================================
// <copyright file="Cli.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;
using System.Text;

namespace Strategos.Contracts.Tests;

/// <summary>Result of running an external process.</summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Output">Merged stdout + stderr.</param>
internal sealed record CliResult(int ExitCode, string Output);

/// <summary>
/// Thin wrapper for running external CLI tools (<c>dotnet</c>, <c>npm</c>,
/// <c>tsp</c>, <c>git</c>) from tests, capturing merged stdout/stderr.
/// </summary>
internal static class Cli
{
    /// <summary>Runs a command in the given working directory (defaults to the repo root).</summary>
    /// <param name="fileName">Executable name.</param>
    /// <param name="arguments">Command-line arguments string.</param>
    /// <param name="workingDirectory">Working directory; defaults to <see cref="RepoLayout.RepoRoot"/>.</param>
    public static async Task<CliResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? RepoLayout.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return new CliResult(process.ExitCode, output.ToString());
    }
}
