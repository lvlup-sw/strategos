// =============================================================================
// <copyright file="CodegenGuardTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T5 — the codegen-guard. The emitted <c>Generated/*.g.cs</c> are emitter-owned;
/// a hand-edit must be mechanically detected (DIM-6), never trusted by
/// convention. These tests assert (a) the guard workflow checks both tracked
/// diffs and newly emitted untracked files after regeneration, and (b) a hand-edit
/// to a generated file diverges from freshly-emitted output, so CI fails.
/// </summary>
[Property("Category", "Pipeline")]
[NotInParallel("tsp-compile")]
public sealed class CodegenGuardTests
{
    /// <summary>
    /// Verifies the codegen-guard workflow exists and encodes the
    /// regenerate-then-diff contract over <c>Generated/</c> and <c>schemas/</c>, including
    /// newly emitted files that are not yet tracked by Git.
    /// </summary>
    [Test]
    public async Task CodegenGuard_Workflow_RunsRegenerateThenDiff()
    {
        var workflow = Path.Combine(
            RepoLayout.RepoRoot, ".github", "workflows", "contracts-codegen-guard.yml");
        await Assert.That(File.Exists(workflow)).IsTrue()
            .Because($"expected guard workflow at {workflow}");

        var yaml = await File.ReadAllTextAsync(workflow);
        var regenerate = yaml.IndexOf("contracts-codegen.sh", StringComparison.Ordinal);
        var trackedDiff = yaml.IndexOf("git diff --exit-code", StringComparison.Ordinal);
        var untrackedDiff = yaml.IndexOf(
            "git status --porcelain --untracked-files=all",
            StringComparison.Ordinal);

        await Assert.That(regenerate >= 0).IsTrue();
        await Assert.That(trackedDiff > regenerate).IsTrue();
        await Assert.That(untrackedDiff > trackedDiff).IsTrue();
        await Assert.That(yaml).Contains("Generated");
        await Assert.That(yaml).Contains("schemas");
    }

    /// <summary>The two Contracts PR jobs must use the reviewed npm lockfile without fallback.</summary>
    [Test]
    public async Task ContractsPrWorkflows_UseLockedNodeRestore()
    {
        string[] workflows =
        [
            Path.Combine(RepoLayout.RepoRoot, ".github", "workflows", "ci.yml"),
            Path.Combine(
                RepoLayout.RepoRoot,
                ".github",
                "workflows",
                "contracts-codegen-guard.yml"),
        ];

        foreach (var workflow in workflows)
        {
            var yaml = await File.ReadAllTextAsync(workflow);
            await Assert.That(yaml).Contains("run: npm ci");
            await Assert.That(yaml.Contains("npm ci || npm install", StringComparison.Ordinal))
                .IsFalse()
                .Because($"{Path.GetFileName(workflow)} must fail closed when its lockfile is invalid.");
        }
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

    /// <summary>
    /// Proves that the record emitter projects only the exact direct-property
    /// non-whitespace pattern into executable JSON callbacks. Required and
    /// optional properties have different null semantics, while unrelated
    /// regular expressions and constraints inherited through scalar aliases
    /// remain schema-only.
    /// </summary>
    [Test]
    public async Task RecordEmitter_NonWhitespaceBoundary_IsExact()
    {
        var tempRoot = Directory.CreateTempSubdirectory("record-emitter-boundary-").FullName;
        var schemas = Path.Combine(tempRoot, "schemas");
        var generated = Path.Combine(tempRoot, "generated");
        var consumer = Path.Combine(tempRoot, "consumer");
        Directory.CreateDirectory(schemas);
        Directory.CreateDirectory(consumer);

        try
        {
            var committedSchemas = Path.Combine(
                RepoLayout.ContractsProjectDir,
                "schemas",
                "json-schema");
            foreach (var schema in Directory.GetFiles(committedSchemas, "*.json"))
            {
                File.Copy(schema, Path.Combine(schemas, Path.GetFileName(schema)));
            }

            await File.WriteAllTextAsync(
                Path.Combine(schemas, "NonWhitespaceBoundaryFixture.json"),
                """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": "object",
                  "properties": {
                    "requiredExact": {
                      "type": "string",
                      "pattern": ".*\\S.*"
                    },
                    "optionalExact": {
                      "type": "string",
                      "pattern": ".*\\S.*"
                    },
                    "unrelatedPattern": {
                      "type": "string",
                      "pattern": "^x+$"
                    },
                    "scalarAlias": {
                      "$ref": "ScalarAliasBoundary.json"
                    }
                  },
                  "required": ["requiredExact"]
                }
                """);
            await File.WriteAllTextAsync(
                Path.Combine(schemas, "ScalarAliasBoundary.json"),
                """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": "string",
                  "pattern": ".*\\S.*"
                }
                """);

            var codegenProject = Path.Combine(
                RepoLayout.RepoRoot,
                "src",
                "Strategos.Contracts.Codegen",
                "Strategos.Contracts.Codegen.csproj");
            var emit = await Cli.RunAsync(
                "dotnet",
                $"run --project \"{codegenProject}\" -- \"{schemas}\" \"{generated}\"");
            await Assert.That(emit.ExitCode).IsEqualTo(0).Because(emit.Output);

            var emittedPath = Path.Combine(generated, "NonWhitespaceBoundaryFixture.g.cs");
            var emitted = await File.ReadAllTextAsync(emittedPath);
            await Assert.That(emitted).Contains(
                "RequireNonWhitespace(RequiredExact, \"NonWhitespaceBoundaryFixture.requiredExact\", required: true)");
            await Assert.That(emitted).Contains(
                "RequireNonWhitespace(OptionalExact, \"NonWhitespaceBoundaryFixture.optionalExact\", required: false)");
            await Assert.That(emitted).DoesNotContain("RequireNonWhitespace(UnrelatedPattern");
            await Assert.That(emitted).DoesNotContain("RequireNonWhitespace(ScalarAlias");

            File.Copy(emittedPath, Path.Combine(consumer, Path.GetFileName(emittedPath)));
            File.Copy(
                Path.Combine(
                    RepoLayout.RepoRoot,
                    "src",
                    "Strategos.Contracts",
                    "ContractJsonValidation.cs"),
                Path.Combine(consumer, "ContractJsonValidation.cs"));

            await File.WriteAllTextAsync(
                Path.Combine(consumer, "BoundaryConsumer.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(
                Path.Combine(consumer, "Program.cs"),
                """
                using System.Text.Json;
                using Strategos.Contracts.Generated;

                ExpectReadRejected("{}");
                ExpectReadRejected("{\"requiredExact\":\"\"}");
                ExpectReadRejected("{\"requiredExact\":\"   \"}");
                ExpectWriteRejected(new() { RequiredExact = "" });
                ExpectWriteRejected(new() { RequiredExact = "   " });

                ExpectReadAccepted("{\"requiredExact\":\"ok\"}");
                ExpectReadAccepted("{\"requiredExact\":\"ok\",\"optionalExact\":null}");
                ExpectWriteAccepted(new() { RequiredExact = "ok", OptionalExact = null });
                ExpectReadRejected("{\"requiredExact\":\"ok\",\"optionalExact\":\"\"}");
                ExpectReadRejected("{\"requiredExact\":\"ok\",\"optionalExact\":\"   \"}");
                ExpectWriteRejected(new() { RequiredExact = "ok", OptionalExact = "" });
                ExpectWriteRejected(new() { RequiredExact = "ok", OptionalExact = "   " });

                ExpectReadAccepted("{\"requiredExact\":\"ok\",\"unrelatedPattern\":\"\",\"scalarAlias\":\"   \"}");
                ExpectWriteAccepted(new()
                {
                    RequiredExact = "ok",
                    UnrelatedPattern = "",
                    ScalarAlias = "   ",
                });

                static void ExpectReadAccepted(string json) =>
                    _ = JsonSerializer.Deserialize<NonWhitespaceBoundaryFixture>(json)
                        ?? throw new InvalidOperationException("Expected a value.");

                static void ExpectReadRejected(string json)
                {
                    try
                    {
                        _ = JsonSerializer.Deserialize<NonWhitespaceBoundaryFixture>(json);
                    }
                    catch (JsonException)
                    {
                        return;
                    }

                    throw new InvalidOperationException($"Expected JSON read rejection: {json}");
                }

                static void ExpectWriteAccepted(NonWhitespaceBoundaryFixture value) =>
                    _ = JsonSerializer.Serialize(value);

                static void ExpectWriteRejected(NonWhitespaceBoundaryFixture value)
                {
                    try
                    {
                        _ = JsonSerializer.Serialize(value);
                    }
                    catch (JsonException)
                    {
                        return;
                    }

                    throw new InvalidOperationException("Expected JSON write rejection.");
                }
                """);

            var execute = await Cli.RunAsync(
                "dotnet",
                "run --project BoundaryConsumer.csproj --configuration Release",
                consumer);
            await Assert.That(execute.ExitCode).IsEqualTo(0).Because(execute.Output);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    /// <summary>
    /// The Contracts tag workflow re-establishes codegen and test evidence at the
    /// checked-out tag before it packs immutable NuGet bytes.
    /// </summary>
    [Test]
    public async Task ContractsRelease_RegeneratesAndTestsBeforePack()
    {
        var workflow = Path.Combine(
            RepoLayout.RepoRoot, ".github", "workflows", "publish-contracts.yml");
        var yaml = await File.ReadAllTextAsync(workflow);

        var tagBinding = yaml.IndexOf("git rev-list -n 1", StringComparison.Ordinal);
        var lockedRestore = yaml.IndexOf("npm ci", StringComparison.Ordinal);
        var regenerate = yaml.IndexOf("contracts-codegen.sh", StringComparison.Ordinal);
        var tests = yaml.IndexOf("$CONTRACTS_TESTS_PROJECT", StringComparison.Ordinal);
        var cleanDiff = yaml.IndexOf("git diff --exit-code", StringComparison.Ordinal);
        var untrackedDiff = yaml.IndexOf(
            "git status --porcelain --untracked-files=all",
            StringComparison.Ordinal);
        var publishedBaseline = yaml.IndexOf(
            "api.nuget.org/v3-flatcontainer",
            StringComparison.Ordinal);
        var compatibility = yaml.IndexOf("contracts-schema-diff.mjs", StringComparison.Ordinal);
        var pack = yaml.IndexOf("dotnet pack", StringComparison.Ordinal);
        var candidateDigestStep = yaml.IndexOf(
            "- name: Record candidate package digest",
            StringComparison.Ordinal);
        var candidateDigest = yaml.IndexOf(
            "candidate_sha256=\"$(sha256sum",
            StringComparison.Ordinal);
        var push = yaml.IndexOf("dotnet nuget push", StringComparison.Ordinal);

        await Assert.That(tagBinding >= 0).IsTrue();
        await Assert.That(lockedRestore > tagBinding).IsTrue();
        await Assert.That(regenerate > lockedRestore).IsTrue();
        await Assert.That(tests > regenerate).IsTrue();
        await Assert.That(cleanDiff > tests).IsTrue();
        await Assert.That(untrackedDiff > cleanDiff).IsTrue();
        await Assert.That(publishedBaseline > untrackedDiff).IsTrue();
        await Assert.That(compatibility > publishedBaseline).IsTrue();
        await Assert.That(pack > compatibility).IsTrue();
        await Assert.That(candidateDigestStep > pack).IsTrue();
        await Assert.That(candidateDigest > candidateDigestStep).IsTrue();
        await Assert.That(push > candidateDigest).IsTrue();
        await Assert.That(yaml).Contains("nupkg_sha256=$candidate_sha256");
        await Assert.That(yaml.Contains("--skip-duplicate", StringComparison.Ordinal)).IsFalse()
            .Because("a duplicate push must not turn different local package bytes into a green release.");
        await Assert.That(yaml).Contains(
            "actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020");
        await Assert.That(yaml.Contains("packages: write", StringComparison.Ordinal)).IsFalse();
        await Assert.That(yaml).Contains("persist-credentials: false");
    }
}
