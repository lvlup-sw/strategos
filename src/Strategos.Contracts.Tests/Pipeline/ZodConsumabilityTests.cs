// =============================================================================
// <copyright file="ZodConsumabilityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T12 — Zod-consumability. The emitted event JSON Schemas must pass through
/// <c>$ref</c> dereferencing (<c>@apidevtools/json-schema-ref-parser</c>) and
/// <c>json-schema-to-zod</c> with **no manual post-processing** — this is the
/// Exarchos derivation path. The smoke script (<c>scripts/zod-smoke.mjs</c>)
/// dereferences every emitted schema and converts it to Zod, writing a barrel
/// <c>index.ts</c>; the test asserts it exits 0 and produced Zod for the event
/// envelope.
/// </summary>
[Property("Category", "Pipeline")]
[NotInParallel("tsp-compile")]
public class ZodConsumabilityTests
{
    /// <summary>
    /// Compiles the contracts <c>.tsp</c>, runs the Zod smoke script over the
    /// emitted schemas, and asserts it succeeds and emits a Zod module for the
    /// <c>SdlcEventEnvelope</c> plus a barrel index.
    /// </summary>
    [Test]
    public async Task EmittedSchema_GeneratesZod_WithoutManualPostProcessing()
    {
        var compile = await TspToolchain.CompileAsync();
        await Assert.That(compile.ExitCode).IsEqualTo(0).Because(compile.Output);

        var scriptPath = Path.Combine(RepoLayout.ContractsProjectDir, "scripts", "zod-smoke.mjs");
        await Assert.That(File.Exists(scriptPath)).IsTrue()
            .Because($"expected the Zod smoke script at {scriptPath}");

        // Run the smoke script; it dereferences + converts every emitted schema
        // into a temp out-dir and prints the out-dir path on success.
        var outDir = Directory.CreateTempSubdirectory("contracts-zod-").FullName;
        try
        {
            var run = await Cli.RunAsync(
                "node",
                $"\"{scriptPath}\" \"{outDir}\"",
                RepoLayout.ContractsProjectDir);
            await Assert.That(run.ExitCode).IsEqualTo(0)
                .Because($"zod-smoke must convert every emitted schema without manual post-processing:\n{run.Output}");

            // The envelope produced a Zod module.
            var envelopeZod = Path.Combine(outDir, "SdlcEventEnvelope.ts");
            await Assert.That(File.Exists(envelopeZod)).IsTrue()
                .Because($"expected generated Zod for the envelope at {envelopeZod}\n{run.Output}");
            var zod = await File.ReadAllTextAsync(envelopeZod);
            await Assert.That(zod).Contains("z.")
                .Because("the generated module must contain Zod schema code.");

            // A barrel index re-exports the generated modules.
            await Assert.That(File.Exists(Path.Combine(outDir, "index.ts"))).IsTrue()
                .Because("the smoke script must emit a barrel index.ts.");
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }
}
