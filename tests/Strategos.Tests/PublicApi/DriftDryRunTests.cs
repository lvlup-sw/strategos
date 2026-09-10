// =============================================================================
// <copyright file="DriftDryRunTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;

using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #51 builder API-stability gate (PR-B), task T14. Proves the cross-repo
/// notify wiring end-to-end WITHOUT a live token: with a synthetic divergence,
/// a fake non-empty <c>EXARCHOS_ISSUES_PAT</c>, and a mocked <c>gh</c> shim on
/// <c>PATH</c>, <c>build-drift-issue.sh</c> must invoke
/// <c>gh issue create --repo lvlup-sw/exarchos --label cross-product:strategos</c>
/// with a diff link in the body. The shim asserts those args and never touches
/// the network — the same shim shape the <c>public-api-drift.yml</c>
/// mocked-gh-dry-run job uses.
/// </summary>
public sealed class DriftDryRunTests
{
    private static string PayloadScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "build-drift-issue.sh");

    [Test]
    public async Task MockedGh_AssertsRepoLabelBody_NoLiveToken()
    {
        var tempDir = Directory.CreateTempSubdirectory("strategos-gh-shim-");
        try
        {
            // A gh shim that asserts the invocation and prints a sentinel.
            var shimPath = Path.Combine(tempDir.FullName, "gh");
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
            // Non-empty fake PAT → script takes the gh branch; the shim never
            // authenticates, so no live token is involved.
            psi.Environment["EXARCHOS_ISSUES_PAT"] = "dry-run-fake-token";

            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            await Assert.That(proc.ExitCode).IsEqualTo(0);
            await Assert.That(stdout + stderr).Contains("MOCK gh OK");
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

    private const string GhShimScript = """
        #!/usr/bin/env bash
        set -euo pipefail
        all="$*"
        fail() { echo "MOCK gh FAIL: $1" >&2; exit 1; }
        case "$1 $2" in
          # Idempotent fail-open label create (#107) runs first; accept it
          # cleanly — it carries no --label/--body/diff flags to assert.
          "label create") echo "MOCK gh OK: label create accepted"; exit 0;;
          "issue create") ;;
          *) fail "expected 'issue create' or 'label create', got '$1 $2'";;
        esac
        [[ "$all" == *"--repo lvlup-sw/exarchos"* ]] || fail "missing --repo lvlup-sw/exarchos"
        [[ "$all" == *"--label cross-product:strategos"* ]] || fail "missing --label cross-product:strategos"
        [[ "$all" == *"--body"* ]] || fail "missing --body"
        [[ "$all" == *"compare/"* ]] || fail "body missing diff link"
        echo "MOCK gh OK: issue create asserted (--repo/--label/body/diff-link)"
        """;
}
