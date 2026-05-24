// =============================================================================
// <copyright file="OntologicalLifecycleTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Events;

/// <summary>
/// T8 — the ADR §4.1 ontological-record lifecycle family: the intent events
/// (<c>IntentProposedData</c>, <c>IntentEnrichedData</c>,
/// <c>IntentCompletedData</c>), the composite record
/// (<c>OntologicalRecordData</c> with its <c>ProcessLayerData</c> /
/// <c>DomainLayerData</c> sub-layers), and the <c>RecordStatus</c> /
/// <c>DelegationPolicy</c> enums. Asserts the JSON Schema shape and — for the
/// enums — that the in-repo emitter produces a strongly-typed C# <c>enum</c>
/// (not a bare <c>string</c>).
/// </summary>
[Property("Category", "Events")]
[NotInParallel("tsp-compile")]
public class OntologicalLifecycleTests
{
    /// <summary>
    /// Compiles the contracts <c>.tsp</c> and asserts the ADR §4.1 ontological
    /// lifecycle models + enums emit the expected JSON Schema, and the emitter
    /// projects <c>RecordStatus</c> / <c>DelegationPolicy</c> to C# enums.
    /// </summary>
    [Test]
    public async Task OntologicalLifecycle_Schema_MatchesAdrSection4()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        // ── RecordStatus enum (ADR §2.5 lifecycle states) ─────────────────
        var status = await EventSchemas.LoadAsync("RecordStatus");
        await Assert.That(status.GetProperty("type").GetString()).IsEqualTo("string");
        var statusVals = EventSchemas.EnumValues(status);
        foreach (var v in new[] { "proposed", "validated", "enriched", "executing", "completed", "failed" })
        {
            await Assert.That(statusVals).Contains(v).Because($"RecordStatus must admit '{v}'.");
        }

        // ── DelegationPolicy enum ─────────────────────────────────────────
        var policy = await EventSchemas.LoadAsync("DelegationPolicy");
        var policyVals = EventSchemas.EnumValues(policy);
        await Assert.That(policyVals.Count).IsGreaterThan(0)
            .Because("DelegationPolicy must be a non-empty enum.");

        // ── Intent events (ADR §4.1) ──────────────────────────────────────
        await AssertRequired("IntentProposedData", "recordId", "featureId", "processLayer", "delegationPolicy", "ontologyVersion");
        await AssertRequired("IntentEnrichedData", "recordId", "featureId", "domainLayer");
        await AssertRequired("IntentCompletedData", "recordId", "featureId", "tasksCompleted", "tasksFailed");

        // ── Composite record + layers ─────────────────────────────────────
        await AssertRequired("OntologicalRecordData", "id", "featureId", "status", "processLayer");
        await AssertRequired("ProcessLayerData", "designRef", "delegationPolicy");
        await AssertRequired("DomainLayerData", "affectedNodes");

        // OntologicalRecordData.status references the RecordStatus enum.
        var record = await EventSchemas.LoadAsync("OntologicalRecordData");
        var statusProp = record.GetProperty("properties").GetProperty("status");
        var statusRefVals = EventSchemas.EnumValues(statusProp);
        await Assert.That(statusRefVals).Contains("proposed")
            .Because("OntologicalRecordData.status must resolve to the RecordStatus enum.");

        // ── Emitter: enums become C# enums (the T8 emitter extension) ─────
        var asm = typeof(ContractsMarker).Assembly;
        var recordStatusType = asm.GetTypes().FirstOrDefault(t => t.Name == "RecordStatus");
        await Assert.That(recordStatusType).IsNotNull()
            .Because("the emitter must generate a RecordStatus type.");
        await Assert.That(recordStatusType!.IsEnum).IsTrue()
            .Because("RecordStatus must be a C# enum, not a string alias (INV-6 strong typing).");
        var names = Enum.GetNames(recordStatusType);
        await Assert.That(names).Contains("Proposed");
        await Assert.That(names).Contains("Completed");

        var policyType = asm.GetTypes().FirstOrDefault(t => t.Name == "DelegationPolicy");
        await Assert.That(policyType?.IsEnum).IsEqualTo(true)
            .Because("DelegationPolicy must be a generated C# enum.");

        // And a record that references an enum uses the enum type, not string.
        var ontoRecordType = asm.GetTypes().FirstOrDefault(t => t.Name == "OntologicalRecordData");
        await Assert.That(ontoRecordType).IsNotNull();
        var statusClrProp = ontoRecordType!.GetProperty("Status");
        await Assert.That(statusClrProp).IsNotNull();
        await Assert.That(statusClrProp!.PropertyType).IsEqualTo(recordStatusType)
            .Because("an enum-typed field must project to the generated enum type.");
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
