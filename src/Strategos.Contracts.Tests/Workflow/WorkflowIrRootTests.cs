// =============================================================================
// <copyright file="WorkflowIrRootTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

using Strategos.Contracts.Generated;

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
        var name = props.GetProperty("name");
        await Assert.That(name.GetProperty("minLength").GetInt32()).IsEqualTo(1);
        await Assert.That(name.GetProperty("pattern").GetString()).IsEqualTo(".*\\S.*")
            .Because("the workflow identity must contain at least one non-whitespace character.");

        // The ordered step collection is present.
        await Assert.That(props.TryGetProperty("steps", out var steps)).IsTrue();
        await Assert.That(steps.GetProperty("type").GetString()).IsEqualTo("array");
    }

    /// <summary>
    /// The generated contract rejects empty and whitespace-only workflow identities
    /// during both JSON deserialization and serialization.
    /// </summary>
    /// <param name="workflowName">Invalid workflow identity.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task WorkflowIrRoot_GeneratedContract_RejectsBlankName(string workflowName)
    {
        var json = $$"""
            {
              "schemaVersion": "1.0",
              "name": "{{workflowName}}",
              "steps": [],
              "transitions": [],
              "branchPoints": [],
              "loops": [],
              "forkPoints": [],
              "failureHandlers": [],
              "approvalPoints": []
            }
            """;
        var value = new WorkflowDefinitionV1
        {
            SchemaVersion = "1.0",
            Name = workflowName,
            Steps = [],
            Transitions = [],
            BranchPoints = [],
            Loops = [],
            ForkPoints = [],
            FailureHandlers = [],
            ApprovalPoints = [],
        };

        var read = () => JsonSerializer.Deserialize<WorkflowDefinitionV1>(json, ContractsJson.Options);
        var write = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        await Assert.That(read).Throws<JsonException>();
        await Assert.That(write).Throws<JsonException>();
    }
}
