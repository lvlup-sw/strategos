// =============================================================================
// <copyright file="StepDefinitionSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// T14 — the discriminated <c>StepDefinition</c>. Compiles the canonical
/// <c>.tsp</c> and asserts the wire step is a 5-kind discriminated union
/// (<c>skill | handler | gate | delegate | approval</c>) where every arm pins
/// its <c>kind</c> discriminator and reserves the optional
/// <c>runtime: exarchos | strategos | remote</c> federation slot.
/// </summary>
[Property("Category", "WorkflowIr")]
[NotInParallel("tsp-compile")]
public class StepDefinitionSchemaTests
{
    private static readonly string[] ExpectedKinds =
        ["skill", "handler", "gate", "delegate", "approval"];

    /// <summary>
    /// Asserts <c>StepDefinition</c> emits an <c>anyOf</c> union over the five
    /// kind arms, each pinning <c>kind</c> to its const and reserving
    /// <c>runtime</c>.
    /// </summary>
    [Test]
    public async Task StepDefinition_Discriminates_FiveKinds_ReservesRuntime()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var root = await EventSchemas.LoadAsync("StepDefinition");

        // The union root is an anyOf over the five kind arms.
        await Assert.That(root.TryGetProperty("anyOf", out var anyOf)).IsTrue()
            .Because("StepDefinition must be a discriminated union (anyOf of arms).");

        var armNames = anyOf.EnumerateArray()
            .Where(a => a.TryGetProperty("$ref", out _))
            .Select(a => Path.GetFileNameWithoutExtension(a.GetProperty("$ref").GetString()))
            .ToList();
        await Assert.That(armNames.Count).IsEqualTo(5)
            .Because("there must be exactly five step kinds.");

        // Each arm pins its kind const and carries the reserved runtime slot.
        var seenKinds = new List<string>();
        foreach (var armName in armNames)
        {
            var arm = await EventSchemas.LoadAsync(armName!);
            var props = arm.GetProperty("properties");

            var kindConst = props.GetProperty("kind").GetProperty("const").GetString();
            seenKinds.Add(kindConst!);

            // runtime is reserved (present, optional, enum of three).
            await Assert.That(props.TryGetProperty("runtime", out var runtime)).IsTrue()
                .Because($"arm {armName} must reserve the runtime federation slot.");
            var runtimeValues = EventSchemas.EnumValues(runtime);
            await Assert.That(runtimeValues).Contains("exarchos");
            await Assert.That(runtimeValues).Contains("strategos");
            await Assert.That(runtimeValues).Contains("remote");

            var required = arm.TryGetProperty("required", out var reqEl)
                ? reqEl.EnumerateArray().Select(e => e.GetString()).ToHashSet()
                : new HashSet<string?>();
            await Assert.That(required.Contains("runtime")).IsFalse()
                .Because("runtime must be optional (default exarchos), not required.");
        }

        foreach (var kind in ExpectedKinds)
        {
            await Assert.That(seenKinds.Contains(kind)).IsTrue()
                .Because($"the {kind} step kind must be present.");
        }
    }
}
