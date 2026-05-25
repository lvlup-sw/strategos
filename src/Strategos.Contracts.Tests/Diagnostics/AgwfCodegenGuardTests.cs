// =============================================================================
// <copyright file="AgwfCodegenGuardTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T7 — the AGWF artifacts are emitter-owned (DIM-6): the catalog JSON, the
/// generated enum/constants, and the Markdown reference must be mechanically
/// guarded against hand-edits. Asserts (a) the codegen-guard workflow's diff set
/// covers <c>docs/diagnostics</c> (the one AGWF artifact emitted outside the
/// Contracts tree) in addition to the already-covered <c>Generated/</c> +
/// <c>schemas/</c>, and (b) a hand-edit to <c>agwf-catalog.json</c> diverges
/// from freshly-regenerated output — i.e. the guard's diff would be non-empty.
/// </summary>
[Property("Category", "Pipeline")]
[NotInParallel("tsp-compile")]
public class AgwfCodegenGuardTests
{
    /// <summary>
    /// Verifies the guard workflow regenerates AND diffs the AGWF artifact paths,
    /// including the externally-emitted <c>docs/diagnostics</c> reference.
    /// </summary>
    [Test]
    public async Task CodegenGuard_DiffSet_CoversAgwfArtifacts()
    {
        var workflow = Path.Combine(
            RepoLayout.RepoRoot, ".github", "workflows", "contracts-codegen-guard.yml");
        await Assert.That(File.Exists(workflow)).IsTrue()
            .Because($"expected guard workflow at {workflow}");

        var yaml = await File.ReadAllTextAsync(workflow);

        // The catalog JSON + generated enum/constants live under Generated/
        // (already covered). The reference page is emitted to docs/diagnostics,
        // OUTSIDE the Contracts tree — the guard's diff set must include it so a
        // hand-edit to agwf.md fails CI.
        await Assert.That(yaml).Contains("docs/diagnostics")
            .Because("the guard must diff the externally-emitted AGWF reference page.");
        await Assert.That(yaml).Contains("Generated");
    }

    /// <summary>
    /// Regenerates the AGWF artifacts into a temp <c>Generated/</c> dir from the
    /// committed schemas, then asserts a hand-edited <c>agwf-catalog.json</c> does
    /// NOT match the freshly-emitted output — the guard's diff would be non-empty.
    /// </summary>
    [Test]
    public async Task AgwfCatalog_HandEdit_FailsGuard()
    {
        var committedCatalog = Path.Combine(
            RepoLayout.ContractsProjectDir, "Generated", "agwf-catalog.json");
        var committed = await File.ReadAllTextAsync(committedCatalog);

        // Simulate a hand-edit: someone bumps a severity by hand.
        var handEdited = committed.Replace(
            "\"severity\": \"error\"", "\"severity\": \"warning\"", StringComparison.Ordinal);
        await Assert.That(handEdited).IsNotEqualTo(committed)
            .Because("the simulated hand-edit must change the catalog (flips a severity).");

        // Regenerate the full pipeline into a clean temp output dir.
        var tempOut = Directory.CreateTempSubdirectory("agwf-guard-").FullName;
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
                Path.Combine(tempOut, "agwf-catalog.json"));

            // The guard's diff (regenerated vs hand-edited working tree) is non-empty.
            await Assert.That(regenerated).IsNotEqualTo(handEdited)
                .Because("the codegen-guard must detect the hand-edited catalog as a divergence.");

            // And regeneration is idempotent against the committed catalog.
            await Assert.That(regenerated).IsEqualTo(committed)
                .Because("regeneration must reproduce the committed agwf-catalog.json verbatim.");
        }
        finally
        {
            Directory.Delete(tempOut, recursive: true);
        }
    }
}
