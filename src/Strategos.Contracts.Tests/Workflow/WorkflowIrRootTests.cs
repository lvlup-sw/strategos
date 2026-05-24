// =============================================================================
// <copyright file="WorkflowIrRootTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// T13 — the <c>WorkflowDefinitionV1</c> wire-IR root. Compiles the canonical
/// <c>.tsp</c> and asserts the emitted JSON Schema carries the
/// <c>schemaVersion: "1.0"</c> literal at the root (the design's versioning
/// anchor: additive minors, breaking changes require V2) and the workflow's
/// structural collections.
/// </summary>
[Property("Category", "WorkflowIr")]
[NotInParallel("tsp-compile")]
public class WorkflowIrRootTests
{
    /// <summary>
    /// Asserts the IR root pins <c>schemaVersion</c> to the literal <c>"1.0"</c>
    /// and carries the workflow name + ordered step collection.
    /// </summary>
    [Test]
    public async Task WorkflowIrRoot_HasSchemaVersionLiteral_1_0()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var root = await EventSchemas.LoadAsync("WorkflowDefinitionV1");

        await Assert.That(root.TryGetProperty("properties", out var props)).IsTrue();

        // schemaVersion is the literal "1.0" (emitted as { type: string, const: "1.0" }).
        var schemaVersion = props.GetProperty("schemaVersion");
        await Assert.That(schemaVersion.GetProperty("const").GetString()).IsEqualTo("1.0")
            .Because("the IR root must pin schemaVersion to the literal \"1.0\".");

        var required = root.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .ToHashSet();
        await Assert.That(required.Contains("schemaVersion")).IsTrue();
        await Assert.That(required.Contains("name")).IsTrue()
            .Because("the workflow name is the IR identity.");

        // The ordered step collection is present.
        await Assert.That(props.TryGetProperty("steps", out var steps)).IsTrue();
        await Assert.That(steps.GetProperty("type").GetString()).IsEqualTo("array");
    }
}
