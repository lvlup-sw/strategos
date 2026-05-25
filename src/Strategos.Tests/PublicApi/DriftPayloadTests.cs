// =============================================================================
// <copyright file="DriftPayloadTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;

using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #51 builder API-stability gate (PR-B), task T13. The cross-repo auto-issue
/// Action (<c>public-api-drift.yml</c>) builds a <c>gh issue create</c>
/// invocation when the shipped builder baseline diverges from the previous tag.
/// This suite tests the payload-builder script (<c>build-drift-issue.sh</c>) in
/// isolation: given a baseline diff it must emit a command targeting
/// <c>--repo lvlup-sw/exarchos --label cross-product:strategos</c> with a diff
/// link in the body — independent of any live token (the script prints the
/// command in <c>--dry-run</c> mode rather than executing it).
/// </summary>
public sealed class DriftPayloadTests
{
    private static string PayloadScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "build-drift-issue.sh");

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunPayloadScriptAsync(
        IReadOnlyDictionary<string, string>? env = null,
        params string[] args)
    {
        var psi = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FixturePaths.RepoRoot,
        };
        psi.ArgumentList.Add(PayloadScriptPath);
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        if (env is not null)
        {
            foreach (var (k, v) in env)
            {
                psi.Environment[k] = v;
            }
        }

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stdout, stderr);
    }

    [Test]
    public async Task ShippedDiverges_ProducesIssuePayload_WithRepoLabelAndDiffLink()
    {
        // --dry-run prints the gh invocation it WOULD run instead of executing
        // it, so no live token is needed.
        var (exit, stdout, _) = await RunPayloadScriptAsync(
            env: null,
            "--dry-run",
            "--diff-url",
            "https://github.com/lvlup-sw/strategos/compare/v2.7.0...abc1234");

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(stdout).Contains("gh issue create");
        await Assert.That(stdout).Contains("--repo lvlup-sw/exarchos");
        await Assert.That(stdout).Contains("--label cross-product:strategos");
        await Assert.That(stdout)
            .Contains("https://github.com/lvlup-sw/strategos/compare/v2.7.0...abc1234");
    }

    [Test]
    public async Task PatAbsent_WarnsAndSkips_NeverFails()
    {
        // No EXARCHOS_ISSUES_PAT in the environment → fail-soft: warn + exit 0.
        var (exit, stdout, stderr) = await RunPayloadScriptAsync(
            env: new Dictionary<string, string> { ["EXARCHOS_ISSUES_PAT"] = string.Empty },
            "--diff-url",
            "https://github.com/lvlup-sw/strategos/compare/v2.7.0...abc1234");

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(stdout + stderr).Contains("PAT");
    }
}
