// =============================================================================
// <copyright file="StructuralDefsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// T15 — the structural sub-definitions of the workflow wire IR: transitions,
/// branch points / branch paths, loops, and fork points / fork paths. Each must
/// emit a JSON Schema whose shape mirrors the builder IR's graph edges so the
/// projected document round-trips structurally.
/// </summary>
[Property("Category", "WorkflowIr")]
[NotInParallel("tsp-compile")]
public class StructuralDefsTests
{
    /// <summary>
    /// Asserts the transition / branch / loop / fork sub-definitions emit with
    /// their identifying graph fields.
    /// </summary>
    [Test]
    public async Task StructuralDefs_Schema_MatchTransitionBranchLoopFork()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        await AssertRequiredProps("TransitionDefinition", "transitionId", "fromStepId", "toStepId");
        await AssertRequiredProps("BranchPointDefinition", "branchPointId", "fromStepId");
        await AssertRequiredProps("BranchPathDefinition", "pathId", "conditionDescription");
        await AssertRequiredProps("LoopDefinition", "loopId", "loopName", "fromStepId", "maxIterations");
        await AssertRequiredProps("ForkPointDefinition", "forkPointId", "fromStepId", "joinStepId");
        await AssertRequiredProps("ForkPathDefinition", "pathId", "pathIndex");

        // A branch point carries its paths as a collection of branch paths.
        var branchPoint = await EventSchemas.LoadAsync("BranchPointDefinition");
        var paths = branchPoint.GetProperty("properties").GetProperty("paths");
        await Assert.That(paths.GetProperty("type").GetString()).IsEqualTo("array");
        var itemRef = Path.GetFileNameWithoutExtension(
            paths.GetProperty("items").GetProperty("$ref").GetString());
        await Assert.That(itemRef).IsEqualTo("BranchPathDefinition");

        // A loop's body steps reference the StepDefinition union.
        var loop = await EventSchemas.LoadAsync("LoopDefinition");
        var bodySteps = loop.GetProperty("properties").GetProperty("bodySteps");
        await Assert.That(bodySteps.GetProperty("type").GetString()).IsEqualTo("array");
        var bodyRef = Path.GetFileNameWithoutExtension(
            bodySteps.GetProperty("items").GetProperty("$ref").GetString());
        await Assert.That(bodyRef).IsEqualTo("StepDefinition");
    }

    private static async Task AssertRequiredProps(string model, params string[] requiredNames)
    {
        var root = await EventSchemas.LoadAsync(model);
        var required = root.TryGetProperty("required", out var reqEl)
            ? reqEl.EnumerateArray().Select(e => e.GetString()).ToHashSet()
            : new HashSet<string?>();
        foreach (var name in requiredNames)
        {
            await Assert.That(required.Contains(name)).IsTrue()
                .Because($"{model} must require {name}.");
        }
    }
}
