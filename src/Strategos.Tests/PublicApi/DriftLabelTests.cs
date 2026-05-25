// =============================================================================
// <copyright file="DriftLabelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;

using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #107 drift-issue label fail-open. On a fresh exarchos environment the
/// <c>cross-product:strategos</c> label may not exist yet, which would make the
/// <c>gh issue create --label</c> call fail. <c>build-drift-issue.sh</c> must
/// therefore create the label idempotently and fail-open (<c>|| true</c>)
/// <em>before</em> creating the issue. This test mounts a <c>gh</c> shim that
/// logs every invocation, succeeds for both <c>label create</c> and
/// <c>issue create</c>, and asserts the label call is recorded first, targets
/// the right label, and the script exits 0.
/// </summary>
public sealed class DriftLabelTests
{
    private static string PayloadScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "build-drift-issue.sh");

    [Test]
    public async Task BuildDriftIssue_CreatesLabelIdempotently_BeforeIssueCreate()
    {
        var tempDir = Directory.CreateTempSubdirectory("strategos-gh-label-");
        try
        {
            // A gh shim that logs every invocation and succeeds unconditionally,
            // so both the label-create and issue-create calls are reached and
            // recorded in the call log.
            var shimPath = Path.Combine(tempDir.FullName, "gh");
            var callLogPath = Path.Combine(tempDir.FullName, "gh-calls.log");
            await File.WriteAllTextAsync(shimPath, GhShimScript);
            MakeExecutable(shimPath);

            var pathSep = OperatingSystem.IsWindows() ? ';' : ':';
            var augmentedPath = tempDir.FullName + pathSep + Environment.GetEnvironmentVariable("PATH");

            var psi = new ProcessStartInfo("bash")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = FixturePaths.RepoRoot,
            };
            psi.ArgumentList.Add(PayloadScriptPath);
            psi.ArgumentList.Add("--diff-url");
            psi.ArgumentList.Add("https://github.com/lvlup-sw/strategos/compare/v0.0.0...deadbeef");
            psi.Environment["PATH"] = augmentedPath;
            psi.Environment["EXARCHOS_ISSUES_PAT"] = "label-test-fake-token";
            psi.Environment["GH_CALL_LOG"] = callLogPath;

            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            await Assert.That(proc.ExitCode).IsEqualTo(0);

            var log = await File.ReadAllTextAsync(callLogPath);
            var labelIdx = log.IndexOf("label create", StringComparison.Ordinal);
            var issueIdx = log.IndexOf("issue create", StringComparison.Ordinal);

            // (1) a label-create call must be recorded.
            await Assert.That(labelIdx).IsGreaterThanOrEqualTo(0);
            // (2) an issue-create call must be recorded.
            await Assert.That(issueIdx).IsGreaterThanOrEqualTo(0);
            // (3) the label call must come BEFORE the issue call.
            await Assert.That(labelIdx).IsLessThan(issueIdx);
            // (4) the label call targeted cross-product:strategos.
            var labelLine = log
                .Split('\n')
                .First(l => l.Contains("label create", StringComparison.Ordinal));
            await Assert.That(labelLine).Contains("cross-product:strategos");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var chmod = Process.Start(new ProcessStartInfo("chmod", $"+x \"{path}\"")
        {
            UseShellExecute = false,
        })!;
        chmod.WaitForExit();
    }

    // Logs each invocation to $GH_CALL_LOG (one line per call: "$1 $2" then the
    // full "$*"), and succeeds for both `label create` and `issue create` so
    // both calls are reached and recorded.
    private const string GhShimScript = """
        #!/usr/bin/env bash
        set -uo pipefail
        printf '%s %s | %s\n' "${1:-}" "${2:-}" "$*" >> "${GH_CALL_LOG}"
        exit 0
        """;
}
