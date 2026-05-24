// =============================================================================
// <copyright file="IrCompletenessTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// T17 — completeness of the workflow wire IR. Asserts every one of the 18
/// sub-definitions emits a schema document, and that the bundled
/// <c>workflow-definition-v1.schema.json</c> is produced with a stable
/// <c>$id</c> (the equivalence-gate target, T23).
/// </summary>
[Property("Category", "WorkflowIr")]
[NotInParallel("tsp-compile")]
public class IrCompletenessTests
{
    /// <summary>The 18 sub-definitions of the workflow wire IR (excludes the V1 root).</summary>
    private static readonly string[] SubDefinitions =
    [
        "StepDefinition",
        "SkillStep",
        "HandlerStep",
        "GateStep",
        "DelegateStep",
        "ApprovalStep",
        "TransitionDefinition",
        "BranchPointDefinition",
        "BranchPathDefinition",
        "LoopDefinition",
        "ForkPointDefinition",
        "ForkPathDefinition",
        "ApprovalDefinition",
        "ApprovalEscalationDefinition",
        "ApprovalRejectionDefinition",
        "FailureHandlerDefinition",
        "StepConfigurationDefinition",
        "RetryConfiguration",
    ];

    /// <summary>
    /// Asserts all 18 sub-definition schemas are emitted and the bundled
    /// workflow schema exists with a stable <c>$id</c>.
    /// </summary>
    [Test]
    public async Task WorkflowIr_Emits18Definitions_WithStableIds()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        await Assert.That(SubDefinitions.Length).IsEqualTo(18)
            .Because("the workflow wire IR has 18 sub-definitions.");

        foreach (var name in SubDefinitions)
        {
            await Assert.That(EventSchemas.Exists(name)).IsTrue()
                .Because($"sub-definition {name} must emit a schema document.");
        }

        // The bundled schema is the equivalence-gate target (T23): a single
        // self-contained document rooted at WorkflowDefinitionV1 with a stable $id.
        var bundlePath = Path.Combine(
            RepoLayout.ContractsProjectDir, "schemas", "workflow-definition-v1.schema.json");
        await Assert.That(File.Exists(bundlePath)).IsTrue()
            .Because("the bundled workflow-definition-v1.schema.json must be emitted.");

        using var bundle = JsonDocument.Parse(await File.ReadAllTextAsync(bundlePath));
        var root = bundle.RootElement;
        await Assert.That(root.TryGetProperty("$id", out var id)).IsTrue()
            .Because("the bundled schema must carry a stable $id.");
        await Assert.That(id.GetString()!).Contains("workflow-definition-v1")
            .Because("the $id must identify the workflow IR v1 contract.");

        // The bundle is rooted at WorkflowDefinitionV1 and carries the sub-defs
        // under $defs (a self-contained document for offline validation).
        await Assert.That(root.TryGetProperty("$defs", out var defs)).IsTrue();
        await Assert.That(defs.TryGetProperty("WorkflowDefinitionV1", out _)).IsTrue()
            .Because("the bundle must define the IR root under $defs.");
    }
}
