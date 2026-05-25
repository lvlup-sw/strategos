// =============================================================================
// <copyright file="CodegenGuardTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T5 — the codegen-guard. The emitted <c>Generated/*.g.cs</c> are emitter-owned;
/// a hand-edit must be mechanically detected (DIM-6), never trusted by
/// convention. These tests assert (a) the guard workflow exists and runs the
/// regenerate-then-<c>git diff --exit-code</c> contract, and (b) a hand-edit to a
/// generated file diverges from freshly-emitted output (i.e. the guard's diff is
/// non-empty), so CI fails.
/// </summary>
[Property("Category", "Pipeline")]
[NotInParallel("tsp-compile")]
public sealed class CodegenGuardTests
{
    /// <summary>
    /// Verifies the codegen-guard workflow exists and encodes the
    /// regenerate-then-diff contract over <c>Generated/</c> and <c>schemas/</c>.
    /// </summary>
    [Test]
    public async Task CodegenGuard_Workflow_RunsRegenerateThenDiff()
    {
        var workflow = Path.Combine(
            RepoLayout.RepoRoot, ".github", "workflows", "contracts-codegen-guard.yml");
        await Assert.That(File.Exists(workflow)).IsTrue()
            .Because($"expected guard workflow at {workflow}");

        var yaml = await File.ReadAllTextAsync(workflow);
        await Assert.That(yaml).Contains("contracts-codegen.sh");
        await Assert.That(yaml).Contains("git diff --exit-code");
        await Assert.That(yaml).Contains("Generated");
        await Assert.That(yaml).Contains("schemas");
    }

    /// <summary>
    /// Regenerates the C# records into a temp directory from the committed
    /// schemas, then asserts a hand-edited generated file does NOT match the
    /// freshly-emitted output — i.e. the guard's <c>git diff</c> would be
    /// non-empty and CI would fail.
    /// </summary>
    [Test]
    public async Task Codegen_HandEdit_FailsGuard()
    {
        var generatedDir = Path.Combine(RepoLayout.ContractsProjectDir, "Generated");

        // Pick a generated RECORD (one carrying init-only members) — not an enum
        // (e.g. AgwfCode.g.cs, which sorts first but has no `{ get; init; }`), so
        // the simulated init->set hand-edit actually mutates the file.
        string? committed = null;
        foreach (var file in Directory.GetFiles(generatedDir, "*.g.cs").OrderBy(f => f, StringComparer.Ordinal))
        {
            if ((await File.ReadAllTextAsync(file)).Contains("{ get; init; }", StringComparison.Ordinal))
            {
                committed = file;
                break;
            }
        }

        await Assert.That(committed).IsNotNull()
            .Because("at least one generated record must carry init-only members.");

        // Simulate a hand-edit: someone mutates a generated record by hand.
        var handEdited = (await File.ReadAllTextAsync(committed!))
            .Replace("{ get; init; }", "{ get; set; }", StringComparison.Ordinal);
        await Assert.That(handEdited).IsNotEqualTo(await File.ReadAllTextAsync(committed))
            .Because("the simulated hand-edit must change the file (it flips init -> set).");

        // Regenerate from the committed schemas into a clean temp dir.
        var tempOut = Directory.CreateTempSubdirectory("contracts-guard-").FullName;
        try
        {
            var codegenProj = Path.Combine(
                RepoLayout.RepoRoot, "src", "Strategos.Contracts.Codegen",
                "Strategos.Contracts.Codegen.csproj");
            var schemasDir = Path.Combine(RepoLayout.ContractsProjectDir, "schemas", "json-schema");

            var run = await Cli.RunAsync(
                "dotnet",
                $"run --project \"{codegenProj}\" -- \"{schemasDir}\" \"{tempOut}\"");
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);

            var regenerated = await File.ReadAllTextAsync(
                Path.Combine(tempOut, Path.GetFileName(committed!)));

            // The emitter produces init-only; the hand-edit produces set. The
            // guard's diff (regenerated vs hand-edited working tree) is non-empty.
            await Assert.That(regenerated).IsNotEqualTo(handEdited)
                .Because("the codegen-guard must detect the hand-edit as a divergence.");

            // And the emitter reproduces the committed file verbatim (idempotent).
            await Assert.That(regenerated).IsEqualTo(await File.ReadAllTextAsync(committed!))
                .Because("regeneration must be idempotent against the committed output.");
        }
        finally
        {
            Directory.Delete(tempOut, recursive: true);
        }
    }
}
