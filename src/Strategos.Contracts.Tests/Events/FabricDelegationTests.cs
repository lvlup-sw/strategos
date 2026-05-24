// =============================================================================
// <copyright file="FabricDelegationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Events;

/// <summary>
/// T9 — the ADR §4.2 fabric-query audit (<c>FabricQueryData</c>,
/// <c>FabricQueryType</c>) and §4.3 remote-delegation
/// (<c>TaskDelegatedRemoteData</c>, <c>CrossTierDependencyResolvedData</c>)
/// families. Asserts the JSON Schema shape and the strongly-typed
/// <c>FabricQueryType</c> C# enum.
/// </summary>
[Property("Category", "Events")]
[NotInParallel("tsp-compile")]
public class FabricDelegationTests
{
    /// <summary>
    /// Compiles the contracts <c>.tsp</c> and asserts the fabric-query and
    /// remote-delegation data models + the <c>FabricQueryType</c> enum match
    /// the ADR §4.2 / §4.3 field lists.
    /// </summary>
    [Test]
    public async Task FabricAndDelegation_Schema_MatchesAdrSection4()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        // ── FabricQueryType enum (ADR §4.2) ───────────────────────────────
        var queryType = await EventSchemas.LoadAsync("FabricQueryType");
        var queryVals = EventSchemas.EnumValues(queryType);
        foreach (var v in new[] { "ontologyQuery", "designValidation", "domainStateResolution", "intentRegister" })
        {
            await Assert.That(queryVals).Contains(v).Because($"FabricQueryType must admit '{v}'.");
        }

        // ── FabricQueryData (ADR §4.2) ────────────────────────────────────
        await AssertRequired("FabricQueryData", "queryType", "resultCount", "degraded", "ontologyVersion");
        var fabric = await EventSchemas.LoadAsync("FabricQueryData");
        var objectType = fabric.GetProperty("properties");
        await Assert.That(objectType.TryGetProperty("objectType", out _)).IsTrue()
            .Because("FabricQueryData.objectType is an optional field.");
        var fabricReq = fabric.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        await Assert.That(fabricReq.Contains("objectType")).IsFalse()
            .Because("FabricQueryData.objectType must be optional.");

        // queryType references the FabricQueryType enum.
        var queryTypeProp = fabric.GetProperty("properties").GetProperty("queryType");
        await Assert.That(EventSchemas.EnumValues(queryTypeProp)).Contains("ontologyQuery")
            .Because("FabricQueryData.queryType must resolve to the FabricQueryType enum.");

        // ── Remote delegation (ADR §4.3) ──────────────────────────────────
        await AssertRequired("TaskDelegatedRemoteData", "taskId", "featureId", "target", "agentRole", "reason", "blastRadiusScope");
        await AssertRequired("CrossTierDependencyResolvedData", "dependentTaskId", "resolvedByTaskId", "resolvedByTier", "branch", "commitSha");

        // ── Emitter: FabricQueryType is a generated C# enum ───────────────
        var asm = typeof(ContractsMarker).Assembly;
        var enumType = asm.GetTypes().FirstOrDefault(t => t.Name == "FabricQueryType");
        await Assert.That(enumType?.IsEnum).IsEqualTo(true)
            .Because("FabricQueryType must be a generated C# enum.");
        await Assert.That(Enum.GetNames(enumType!)).Contains("OntologyQuery");
    }

    private static async Task AssertRequired(string model, params string[] fields)
    {
        var root = await EventSchemas.LoadAsync(model);
        var props = root.GetProperty("properties");
        var required = root.TryGetProperty("required", out var req)
            ? req.EnumerateArray().Select(e => e.GetString()).ToHashSet()
            : new HashSet<string?>();
        foreach (var f in fields)
        {
            await Assert.That(props.TryGetProperty(f, out _)).IsTrue().Because($"{model}.{f} must exist.");
            await Assert.That(required.Contains(f)).IsTrue().Because($"{model}.{f} must be required.");
        }
    }
}
