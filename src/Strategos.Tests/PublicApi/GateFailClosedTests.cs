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
/// These tests mutate tracked files in place, so the class is <see cref="NotInParallelAttribute"/>
/// and every test restores the original bytes in a finally regardless of outcome.
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

    /// <summary>
    /// An Ontology member line guaranteed present in that package's unshipped baseline
    /// (its shipped baseline is empty until the first Ontology release roll); dropping
    /// it is the synthetic drift for the second gated project.
    /// </summary>
    private const string OntologyDriftLineToRemove =
        "Strategos.Ontology.OntologyGraphBuilder.Build() -> Strategos.Ontology.OntologyGraph!";

    private static string ShippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Shipped.txt");

    private static string UnshippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Unshipped.txt");

    private static string OntologyUnshippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos.Ontology", "PublicAPI.Unshipped.txt");

    private static string GateScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "check-builder-api-stability.sh");

    private static string PlacementScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "check-unshipped-against-tag.sh");

    private const string StrategosProject = "src/Strategos/Strategos.csproj";

    private const string OntologyProject = "src/Strategos.Ontology/Strategos.Ontology.csproj";

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

            var (driftExit, driftOutput) = await RunGateAsync(StrategosProject);

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
        var (cleanExit, cleanOutput) = await RunGateAsync(StrategosProject);

        await Assert.That(cleanExit).IsEqualTo(0);
        await Assert.That(cleanOutput).Contains("Builder public API stable against baseline.");
    }

    /// <summary>
    /// The gate accepts several projects and stops at the first failure. The Ontology
    /// package publishes its own baseline that the exarchos mirror also consumes, so
    /// the same real-build proof runs against it: drop one tracked Ontology member
    /// from <c>PublicAPI.Unshipped.txt</c> and require the gate to fail closed with the
    /// verbatim remediation, then pass once the baseline is restored.
    /// </summary>
    [Test]
    public async Task Gate_FailsClosed_OnOntologyBaselineDrift_ThenPassesOnRestoredBaseline()
    {
        var original = await File.ReadAllTextAsync(OntologyUnshippedBaselinePath);

        await Assert.That(original)
            .Contains(OntologyDriftLineToRemove);

        try
        {
            var drifted = string.Join(
                '\n',
                original
                    .Split('\n')
                    .Where(line => line.Trim() != OntologyDriftLineToRemove));
            await File.WriteAllTextAsync(OntologyUnshippedBaselinePath, drifted);

            var (driftExit, driftOutput) = await RunGateAsync(OntologyProject);

            await Assert.That(driftExit).IsNotEqualTo(0);
            await Assert.That(driftOutput).Contains("RS0016");
            await Assert.That(driftOutput).Contains(Remediation);
        }
        finally
        {
            await File.WriteAllTextAsync(OntologyUnshippedBaselinePath, original);
        }

        var (cleanExit, cleanOutput) = await RunGateAsync(OntologyProject);

        await Assert.That(cleanExit).IsEqualTo(0);
        await Assert.That(cleanOutput).Contains("Builder public API stable against baseline (" + OntologyProject + ").");
    }

    /// <summary>
    /// Placement gate self-test. <c>PublicAPI.Shipped.txt</c> is defined as "present in
    /// the last v* release" and <c>PublicAPI.Unshipped.txt</c> as "added since"; the
    /// PublicApiAnalyzers accept a member in either file, so only
    /// <c>scripts/check-unshipped-against-tag.sh</c> keeps the two apart. Inject a line
    /// that shipped long ago into the unshipped baseline and require the script to fail
    /// naming that line; then require the committed baseline to pass.
    /// </summary>
    [Test]
    public async Task PlacementGate_FailsClosed_WhenAShippedMemberIsListedAsUnshipped_ThenPassesOnCommittedBaseline()
    {
        var original = await File.ReadAllTextAsync(UnshippedBaselinePath);
        var shipped = await File.ReadAllTextAsync(ShippedBaselinePath);

        // Guard: the injected line must really be shipped, else the failure would be a no-op.
        await Assert.That(shipped)
            .Contains(DriftLineToRemove);
        await Assert.That(original)
            .DoesNotContain(DriftLineToRemove);

        try
        {
            await File.WriteAllTextAsync(
                UnshippedBaselinePath,
                original.TrimEnd('\n') + '\n' + DriftLineToRemove + '\n');

            var (injectedExit, injectedOutput) = await RunScriptAsync(PlacementScriptPath);

            await Assert.That(injectedExit).IsEqualTo(1);
            await Assert.That(injectedOutput).Contains("already shipped at");
            await Assert.That(injectedOutput).Contains(DriftLineToRemove);
        }
        finally
        {
            await File.WriteAllTextAsync(UnshippedBaselinePath, original);
        }

        var (cleanExit, cleanOutput) = await RunScriptAsync(PlacementScriptPath);

        await Assert.That(cleanExit).IsEqualTo(0);
        await Assert.That(cleanOutput).Contains("holds only members added since");
    }

    private static Task<(int ExitCode, string Output)> RunGateAsync(params string[] projects)
    {
        return RunScriptAsync(GateScriptPath, projects);
    }

    private static async Task<(int ExitCode, string Output)> RunScriptAsync(string scriptPath, params string[] arguments)
    {
        var psi = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FixturePaths.RepoRoot,
        };
        psi.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        // Bound the wait so a wedged gate (e.g. a hung `dotnet build`) fails the
        // test deterministically instead of hanging CI. 5 min comfortably covers
        // a cold restore+build while still being a hard ceiling.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            proc.Kill(entireProcessTree: true);
            var timedOutStdout = await stdoutTask;
            var timedOutStderr = await stderrTask;
            return (-1, $"TIMEOUT: gate did not exit within 5 minutes.\n{timedOutStdout}{timedOutStderr}");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (proc.ExitCode, stdout + stderr);
    }
}
