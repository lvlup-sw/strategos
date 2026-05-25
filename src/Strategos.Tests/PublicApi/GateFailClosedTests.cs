// =============================================================================
// <copyright file="GateFailClosedTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;

using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #51 builder API-stability gate (PR-B), DIM-6. Proves the gate FAILS CLOSED on
/// real builder API drift, and PASSES on the unmodified baseline.
/// <para>
/// Approach: a REAL build-level proof (not a stubbed log fixture). The test
/// removes one tracked member line from the committed
/// <c>src/Strategos/PublicAPI/PublicAPI.Shipped.txt</c> (a synthetic drift),
/// drives <c>scripts/check-builder-api-stability.sh</c> end-to-end — which runs
/// <c>dotnet build … /warnaserror</c> with the PublicApiAnalyzers wired exactly
/// as CI does — and asserts the script exits NON-ZERO and emits the VERBATIM
/// remediation message. It then restores the baseline and asserts the
/// unmodified gate exits ZERO. This exercises the analyzer, the .editorconfig
/// scoping, and the script's branch logic together, so it would catch a neutered
/// script, a broken analyzer wiring, OR a baseline that stopped being enforced.
/// </para>
/// <para>
/// Chosen over a stubbed-log fixture because the incremental Strategos build is
/// ~1s on a warm tree, so a real proof is affordable and strictly stronger: a
/// stub would only test the grep branch, not that the analyzer actually raises
/// RS0016 for a dropped member.
/// </para>
/// <para>
/// This test mutates a tracked file in place, so it is <see cref="NotInParallelAttribute"/>
/// and restores the original bytes in a finally regardless of outcome.
/// </para>
/// </summary>
[NotInParallel("PublicAPI.Shipped.txt-mutation")]
public sealed class GateFailClosedTests
{
    /// <summary>
    /// The verbatim remediation protocol the gate must print on drift. Mirrors
    /// the constant pinned in <see cref="ApiDriftRemediationMessageTests"/> and
    /// the gate script itself — the exarchos strategos-api-mirror.test.ts
    /// consumer depends on this exact string.
    /// </summary>
    private const string Remediation =
        "Update PublicAPI.Unshipped.txt and add a CHANGELOG entry under Cross-product breaking changes.";

    /// <summary>A tracked member line guaranteed present in the baseline; dropping it is the synthetic drift.</summary>
    private const string DriftLineToRemove =
        "Strategos.Builders.IBranchBuilder<TState>.Complete() -> void";

    private static string ShippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Shipped.txt");

    private static string GateScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "check-builder-api-stability.sh");

    [Test]
    public async Task Gate_FailsClosedWithVerbatimRemediation_OnDrift_ThenPassesOnRestoredBaseline()
    {
        var original = await File.ReadAllTextAsync(ShippedBaselinePath);

        // Guard: the synthetic-drift line must really be in the baseline, else
        // the "drift" would be a no-op and the test would prove nothing.
        await Assert.That(original)
            .Contains(DriftLineToRemove);

        try
        {
            // --- DRIFT: drop one tracked member from the shipped baseline. ---
            var drifted = string.Join(
                '\n',
                original
                    .Split('\n')
                    .Where(line => line.Trim() != DriftLineToRemove));
            await File.WriteAllTextAsync(ShippedBaselinePath, drifted);

            var (driftExit, driftOutput) = await RunGateAsync();

            // Fails closed: non-zero exit.
            await Assert.That(driftExit).IsNotEqualTo(0);

            // Emits the analyzer drift diagnostic and the VERBATIM remediation.
            await Assert.That(driftOutput).Contains("RS0016");
            await Assert.That(driftOutput).Contains(Remediation);
        }
        finally
        {
            // Always restore the committed baseline, even if an assertion threw.
            await File.WriteAllTextAsync(ShippedBaselinePath, original);
        }

        // --- BASELINE: unmodified baseline must pass (exit zero). ---
        var (cleanExit, cleanOutput) = await RunGateAsync();

        await Assert.That(cleanExit).IsEqualTo(0);
        await Assert.That(cleanOutput).Contains("Builder public API stable against baseline.");
    }

    private static async Task<(int ExitCode, string Output)> RunGateAsync()
    {
        var psi = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FixturePaths.RepoRoot,
        };
        psi.ArgumentList.Add(GateScriptPath);

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (proc.ExitCode, stdout + stderr);
    }
}
