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
    /// An Ontology member line guaranteed present in that package's shipped baseline
    /// (rolled from Unshipped for 3.0.0-rc.1, the first Ontology release roll, and shipped in 3.0.0); dropping
    /// it is the synthetic drift for the second gated project.
    /// </summary>
    private const string OntologyDriftLineToRemove =
        "Strategos.Ontology.OntologyGraphBuilder.Build() -> Strategos.Ontology.OntologyGraph!";

    private static string ShippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Shipped.txt");

    private static string UnshippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Unshipped.txt");

    private static string OntologyShippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos.Ontology", "PublicAPI.Shipped.txt");

    private static string GateScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "check-builder-api-stability.sh");

    private static string PlacementScriptPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "scripts", "check-unshipped-against-tag.sh");

    private const string StrategosProject = "src/Strategos/Strategos.csproj";

    private const string OntologyProject = "src/Strategos.Ontology/Strategos.Ontology.csproj";

    /// <summary>The v* tag carried by the throwaway repo the placement-gate proofs run against.</summary>
    private const string FixtureTag = "v1.0.0";

    /// <summary>The header a PublicAPI baseline opens with; the gate skips comment lines.</summary>
    private const string FixtureBaselineHeader = "#nullable enable";

    /// <summary>A member the fixture tag's <c>PublicAPI.Shipped.txt</c> already lists.</summary>
    private const string FixtureTagShippedLine =
        "Strategos.Builders.IBranchBuilder<TState>.Complete() -> void";

    /// <summary>
    /// One of the three sources the gate reads out of the tag, as it stood at that tag.
    /// Declares a property and a two-parameter factory so both line shapes the gate
    /// reconstructs — accessor by member name, method by name plus parameter types —
    /// have something real to match against.
    /// </summary>
    private const string FixtureScopedSource = """
        namespace Strategos.Steps;

        public sealed record StepContext
        {
            public string? IdempotencyKey { get; init; }

            public static StepContext Create(Guid workflowId, string stepName) => new();
        }
        """;

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
    /// from <c>PublicAPI.Shipped.txt</c> and require the gate to fail closed with the
    /// verbatim remediation, then pass once the baseline is restored.
    /// </summary>
    [Test]
    public async Task Gate_FailsClosed_OnOntologyBaselineDrift_ThenPassesOnRestoredBaseline()
    {
        var original = await File.ReadAllTextAsync(OntologyShippedBaselinePath);

        await Assert.That(original)
            .Contains(OntologyDriftLineToRemove);

        try
        {
            var drifted = string.Join(
                '\n',
                original
                    .Split('\n')
                    .Where(line => line.Trim() != OntologyDriftLineToRemove));
            await File.WriteAllTextAsync(OntologyShippedBaselinePath, drifted);

            var (driftExit, driftOutput) = await RunGateAsync(OntologyProject);

            await Assert.That(driftExit).IsNotEqualTo(0);
            await Assert.That(driftOutput).Contains("RS0016");
            await Assert.That(driftOutput).Contains(Remediation);
        }
        finally
        {
            await File.WriteAllTextAsync(OntologyShippedBaselinePath, original);
        }

        var (cleanExit, cleanOutput) = await RunGateAsync(OntologyProject);

        await Assert.That(cleanExit).IsEqualTo(0);
        await Assert.That(cleanOutput).Contains("Builder public API stable against baseline (" + OntologyProject + ").");
    }

    /// <summary>
    /// Placement gate self-test. <c>PublicAPI.Shipped.txt</c> is defined as "present in
    /// the last v* release" and <c>PublicAPI.Unshipped.txt</c> as "added since"; the
    /// PublicApiAnalyzers accept a member in either file, so only
    /// <c>scripts/check-unshipped-against-tag.sh</c> keeps the two apart.
    /// <para>
    /// The gate reads the baselines out of the last v* tag with <c>git show</c>, so a
    /// proof anchored on this checkout only runs where that tag and its blobs are
    /// present. The shared build-and-test workflow checks out at depth 1 with no tags,
    /// where the gate is correctly indeterminate (exit 2) rather than fail-closed, so
    /// the logic proof runs against a purpose-built fixture repo instead: a throwaway
    /// git repo carrying both baselines, one scoped source, and a v* tag. All three of
    /// the gate's answers — misplaced by the tag's baseline, misplaced by the tag's
    /// source, and clean — are then provable in any checkout.
    /// </para>
    /// </summary>
    [Test]
    public async Task PlacementGate_FailsClosed_WhenAnUnshippedLineIsAlreadyInTheTagsShippedBaseline()
    {
        using var fixture = await GitFixture.CreateAsync(tagged: true);

        await fixture.WriteUnshippedAsync(FixtureBaselineHeader, FixtureTagShippedLine);

        var (exitCode, output) = await RunScriptAsync(fixture.Root, PlacementScriptPath);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains($"already shipped at {FixtureTag} (present in that tag's PublicAPI.Shipped.txt)");
        await Assert.That(output).Contains(FixtureTagShippedLine);
        await Assert.That(output).Contains("Move each flagged line to PublicAPI.Shipped.txt");
    }

    /// <summary>
    /// The second misplacement route: a member the tag's <c>PublicAPI.Shipped.txt</c>
    /// could not list because the .editorconfig re-enable block only brought its file
    /// into analyzer scope after that release. Both line shapes the gate reconstructs
    /// are exercised — a property accessor (matched on member name) and a method
    /// (matched on name plus the parameter type list).
    /// </summary>
    [Test]
    public async Task PlacementGate_FailsClosed_WhenAnUnshippedMemberIsDeclaredInTheTagsScopedSource()
    {
        using var fixture = await GitFixture.CreateAsync(tagged: true);

        const string propertyLine = "Strategos.Steps.StepContext.IdempotencyKey.get -> string?";
        const string methodLine =
            "static Strategos.Steps.StepContext.Create(System.Guid workflowId, string! stepName) -> Strategos.Steps.StepContext!";

        await fixture.WriteUnshippedAsync(FixtureBaselineHeader, propertyLine, methodLine);

        var (exitCode, output) = await RunScriptAsync(fixture.Root, PlacementScriptPath);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains($"already shipped at {FixtureTag} (declared in that tag's source): {propertyLine}");
        await Assert.That(output).Contains($"already shipped at {FixtureTag} (declared in that tag's source): {methodLine}");
    }

    /// <summary>
    /// Kill fixture for the two fail-closed proofs: an unshipped baseline holding only
    /// genuinely new members must pass. Covers both skip routes — a member of a type the
    /// gate does not inspect, and a member of an in-scope type that the tag's source does
    /// not declare — so a gate that flagged everything would fail here.
    /// </summary>
    [Test]
    public async Task PlacementGate_Passes_WhenEveryUnshippedLineIsGenuinelyNew()
    {
        using var fixture = await GitFixture.CreateAsync(tagged: true);

        await fixture.WriteUnshippedAsync(
            FixtureBaselineHeader,
            "Strategos.Builders.IBranchBuilder<TState>.Abandon() -> void",
            "Strategos.Steps.StepContext.CompensationDeadline.get -> System.TimeSpan?",
            "static Strategos.Steps.StepContext.Create(System.Guid workflowId, string! stepName, string! currentPhase) -> Strategos.Steps.StepContext!");

        var (exitCode, output) = await RunScriptAsync(fixture.Root, PlacementScriptPath);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains($"holds only members added since {FixtureTag}");
    }

    /// <summary>
    /// A checkout with no reachable v* tag — the depth-1, tagless checkout the shared
    /// build-and-test workflow performs — must report indeterminate (exit 2), never a
    /// clean pass. Pins the distinction the placement gate depends on: enforcement is
    /// the <c>builder-api-stability</c> CI job, which checks out at depth 0 with tags.
    /// </summary>
    [Test]
    public async Task PlacementGate_IsIndeterminate_WhenNoVersionTagIsReachable()
    {
        using var fixture = await GitFixture.CreateAsync(tagged: false);

        await fixture.WriteUnshippedAsync(FixtureBaselineHeader, FixtureTagShippedLine);

        var (exitCode, output) = await RunScriptAsync(fixture.Root, PlacementScriptPath);

        await Assert.That(exitCode).IsEqualTo(2);
        await Assert.That(output).Contains("no v* tag reachable from HEAD; result indeterminate");
    }

    /// <summary>
    /// The committed baselines run through the same gate. Where this checkout can see the
    /// last v* tag the gate must pass; where it cannot the gate must say so rather than
    /// report a tree it never inspected. Either way a misplaced committed line (exit 1)
    /// fails this test.
    /// </summary>
    [Test]
    public async Task PlacementGate_OnCommittedBaseline_PassesOrReportsIndeterminate_ButNeverMisplacement()
    {
        var (exitCode, output) = await RunScriptAsync(FixturePaths.RepoRoot, PlacementScriptPath);

        if (exitCode == 2)
        {
            await Assert.That(output).Contains("indeterminate");
            return;
        }

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("holds only members added since");
    }

    private static Task<(int ExitCode, string Output)> RunGateAsync(params string[] projects)
    {
        return RunScriptAsync(FixturePaths.RepoRoot, GateScriptPath, projects);
    }

    private static async Task<(int ExitCode, string Output)> RunScriptAsync(
        string workingDirectory,
        string scriptPath,
        params string[] arguments)
    {
        var psi = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
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

    /// <summary>
    /// A throwaway git repo shaped like the paths the placement gate reads — both
    /// PublicAPI baselines and one scoped source, committed and optionally tagged.
    /// The gate resolves its comparison baseline with <c>git show &lt;tag&gt;:&lt;path&gt;</c>,
    /// so proving its logic against a fixture keeps the proof independent of how deep
    /// the surrounding checkout is and whether it carries tags at all.
    /// </summary>
    private sealed class GitFixture : IDisposable
    {
        private GitFixture(string root) => Root = root;

        /// <summary>The fixture repo's working-tree root; the gate runs with this as its cwd.</summary>
        public string Root { get; }

        public static async Task<GitFixture> CreateAsync(bool tagged)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "strategos-placement-gate-" + Guid.NewGuid().ToString("N"));
            var fixture = new GitFixture(root);

            Directory.CreateDirectory(Path.Combine(root, "src", "Strategos", "PublicAPI"));
            Directory.CreateDirectory(Path.Combine(root, "src", "Strategos", "Steps"));

            await File.WriteAllTextAsync(
                Path.Combine(root, "src", "Strategos", "PublicAPI", "PublicAPI.Shipped.txt"),
                FixtureBaselineHeader + "\n" + FixtureTagShippedLine + "\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "src", "Strategos", "Steps", "StepContext.cs"),
                FixtureScopedSource + "\n");
            await fixture.WriteUnshippedAsync(FixtureBaselineHeader);

            await RunGitAsync(root, "init", "--quiet", "--initial-branch=main");
            await RunGitAsync(root, "add", "--all");
            await RunGitAsync(root, "commit", "--quiet", "--no-verify", "-m", "fixture baseline");

            if (tagged)
            {
                await RunGitAsync(root, "tag", FixtureTag);
            }

            return fixture;
        }

        /// <summary>Rewrites the working-tree unshipped baseline the gate reads line by line.</summary>
        public Task WriteUnshippedAsync(params string[] lines)
        {
            return File.WriteAllTextAsync(
                Path.Combine(Root, "src", "Strategos", "PublicAPI", "PublicAPI.Unshipped.txt"),
                string.Join('\n', lines) + "\n");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A temp tree that outlives the test is not a test result.
            }
            catch (UnauthorizedAccessException)
            {
                // Same: cleanup is best-effort, the assertions already ran.
            }
        }

        private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
        {
            var psi = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            // Ignore ambient git configuration so the fixture is byte-identical on a
            // developer machine and on a runner: no hooks, no signing, no template dir,
            // and an identity that does not depend on the machine having one.
            psi.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
            psi.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
            psi.Environment["GIT_AUTHOR_NAME"] = "Strategos Tests";
            psi.Environment["GIT_AUTHOR_EMAIL"] = "tests@strategos.invalid";
            psi.Environment["GIT_COMMITTER_NAME"] = "Strategos Tests";
            psi.Environment["GIT_COMMITTER_EMAIL"] = "tests@strategos.invalid";

            using var proc = Process.Start(psi)!;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"git {string.Join(' ', arguments)} exited {proc.ExitCode}: {stdout}{stderr}");
            }
        }
    }
}
