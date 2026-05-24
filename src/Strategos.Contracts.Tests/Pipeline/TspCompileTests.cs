// =============================================================================
// <copyright file="TspCompileTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T2 — verifies the TypeSpec toolchain compiles the canonical <c>.tsp</c> source
/// and emits JSON Schema with a stable <c>$id</c> into <c>schemas/</c>.
/// </summary>
[Property("Category", "Pipeline")]
[NotInParallel("tsp-compile")]
public class TspCompileTests
{
    /// <summary>
    /// Runs <c>tsp compile</c> in the contracts project and asserts the trivial
    /// foundation model emits a JSON Schema document carrying a stable <c>$id</c>
    /// and the 2020-12 <c>$schema</c> dialect.
    /// </summary>
    [Test]
    public async Task TspCompile_TrivialModel_EmitsJsonSchemaWithStableId()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var schemaPath = Path.Combine(
            RepoLayout.ContractsProjectDir, "schemas", "json-schema", "PipelineProbe.json");
        await Assert.That(File.Exists(schemaPath)).IsTrue()
            .Because($"expected emitted schema at {schemaPath}\n{result.Output}");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(schemaPath));
        var root = doc.RootElement;

        await Assert.That(root.TryGetProperty("$id", out var id)).IsTrue();
        await Assert.That(id.GetString()).IsNotNull();
        await Assert.That(id.GetString()!.Length).IsGreaterThan(0);

        await Assert.That(root.TryGetProperty("$schema", out var schema)).IsTrue();
        await Assert.That(schema.GetString()).Contains("json-schema.org");
    }
}
