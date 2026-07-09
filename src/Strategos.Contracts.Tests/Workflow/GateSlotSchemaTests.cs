// =============================================================================
// <copyright file="GateSlotSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Strategos.Contracts.SchemaDiff;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// DR-3 (#150 → #100) — the additive gate wire slots. Asserts the workflow wire
/// IR carries gate declarations from birth so they flow through the shared IR to
/// both runtimes: <c>WorkflowDefinitionV1</c> gains an OPTIONAL
/// <c>gates?: GateDeclaration[]</c>, and the <c>GateStep</c> arm gains an OPTIONAL
/// <c>gateId?: string</c> back-reference. Both slots are additive — never in
/// <c>required</c> — so the extension is NON-BREAKING (the differ sees exactly one
/// added optional property per schema). The dangling-<c>gateId</c> rule (a
/// <c>gateId</c> naming an id absent from <c>gates</c>) is a semantic rule JSON
/// Schema cannot express and is deliberately NOT enforced here — it is an
/// import-front-end concern (DR-13/DR-15); this family is schema slots only.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class GateSlotSchemaTests
{
    private const string WorkflowRootSchema = "WorkflowDefinitionV1";
    private const string GateStepSchema = "GateStep";

    /// <summary>
    /// <c>WorkflowDefinitionV1</c> carries an OPTIONAL <c>gates</c> array whose
    /// items <c>$ref</c> the shared <c>GateDeclaration</c> (DR-3). The slot is
    /// additive: it is present in <c>properties</c> but absent from <c>required</c>,
    /// so a workflow that declares no gates omits it entirely.
    /// </summary>
    [Test]
    public async Task WorkflowRoot_HasOptionalGatesSlot_RefsGateDeclaration()
    {
        await Assert.That(EventSchemas.Exists(WorkflowRootSchema)).IsTrue()
            .Because("`tsp compile` must emit WorkflowDefinitionV1.json (run scripts/contracts-codegen.sh).");

        var root = await EventSchemas.LoadAsync(WorkflowRootSchema);

        await Assert.That(root.TryGetProperty("properties", out var props)).IsTrue();
        await Assert.That(props.TryGetProperty("gates", out var gates)).IsTrue()
            .Because("the workflow root must carry the DR-3 `gates` slot.");

        // gates is an array of GateDeclaration $refs (the shared DR-2 identity).
        await Assert.That(gates.GetProperty("type").GetString()).IsEqualTo("array")
            .Because("`gates` is a collection of gate declarations.");
        var itemsRef = gates.GetProperty("items").GetProperty("$ref").GetString();
        await Assert.That(Path.GetFileNameWithoutExtension(itemsRef)).IsEqualTo("GateDeclaration")
            .Because("`gates` items must $ref the shared GateDeclaration, not a loose object.");

        // OPTIONAL — not in required (additive, non-breaking).
        await Assert.That(RequiredNames(root)).DoesNotContain("gates")
            .Because("`gates` is additive and OPTIONAL — a gate-less workflow omits it (DR-3).");
    }

    /// <summary>
    /// The <c>GateStep</c> arm gains an OPTIONAL <c>gateId</c> string back-reference
    /// (DR-3). It is present in <c>properties</c> but NOT in <c>required</c> — a
    /// gate step that names no declaration simply omits it. It is a plain string
    /// moniker (the referenced declaration's <c>id</c>), not a <c>$ref</c>: the
    /// binding to a declaration is a semantic rule, not a structural one.
    /// </summary>
    [Test]
    public async Task GateStep_HasOptionalGateIdBackReference()
    {
        await Assert.That(EventSchemas.Exists(GateStepSchema)).IsTrue()
            .Because("`tsp compile` must emit GateStep.json.");

        var arm = await EventSchemas.LoadAsync(GateStepSchema);

        await Assert.That(arm.TryGetProperty("properties", out var props)).IsTrue();
        await Assert.That(props.TryGetProperty("gateId", out var gateId)).IsTrue()
            .Because("the gate step arm must carry the DR-3 `gateId` back-reference.");

        await Assert.That(gateId.GetProperty("type").GetString()).IsEqualTo("string")
            .Because("`gateId` is a plain string id moniker of a declaration.");

        await Assert.That(RequiredNames(arm)).DoesNotContain("gateId")
            .Because("`gateId` is additive and OPTIONAL — an unbound gate step omits it (DR-3).");
    }

    /// <summary>
    /// The DR-3 versioning posture for the root: adding the OPTIONAL <c>gates</c>
    /// slot is a purely additive, NON-BREAKING wire change. The differ sees exactly
    /// one added optional property (never a new required field, never a removal) and
    /// classifies it non-breaking. Runs against the ACTUAL emitted
    /// <c>WorkflowDefinitionV1.json</c> so it is coupled to the shipped artifact.
    /// </summary>
    [Test]
    public async Task AddingGatesSlot_IsNonBreaking()
    {
        await Assert.That(EventSchemas.Exists(WorkflowRootSchema)).IsTrue()
            .Because("`tsp compile` must emit WorkflowDefinitionV1.json.");

        // The pre-DR-3 root: the workflow IR BEFORE the gates slot existed.
        const string preGates =
            """
            {
              "$id": "WorkflowDefinitionV1.json",
              "type": "object",
              "properties": {
                "schemaVersion": { "type": "string", "const": "1.0" },
                "name": { "type": "string" },
                "steps": { "type": "array" },
                "transitions": { "type": "array" },
                "branchPoints": { "type": "array" },
                "loops": { "type": "array" },
                "forkPoints": { "type": "array" },
                "failureHandlers": { "type": "array" },
                "approvalPoints": { "type": "array" },
                "entryStepId": { "type": "string" },
                "terminalStepId": { "type": "string" }
              },
              "required": [
                "schemaVersion", "name", "steps", "transitions", "branchPoints",
                "loops", "forkPoints", "failureHandlers", "approvalPoints"
              ]
            }
            """;

        var emitted = await File.ReadAllTextAsync(
            Path.Combine(EventSchemas.SchemaDir, WorkflowRootSchema + ".json"));

        var result = JsonSchemaDiff.Compare(preGates, emitted);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("adding an OPTIONAL gates slot is additive — never breaking (DR-3).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking)
            .Because("the DR-3 family is an additive minor, not a major bump.");
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("gates", StringComparison.Ordinal))
            .Because("the differ must report the added optional `gates` property.");
    }

    /// <summary>
    /// The DR-3 versioning posture for the gate step arm: adding the OPTIONAL
    /// <c>gateId</c> back-reference is additive, NON-BREAKING. Runs against the
    /// ACTUAL emitted <c>GateStep.json</c>.
    /// </summary>
    [Test]
    public async Task AddingGateIdSlot_IsNonBreaking()
    {
        await Assert.That(EventSchemas.Exists(GateStepSchema)).IsTrue()
            .Because("`tsp compile` must emit GateStep.json.");

        // The pre-DR-3 gate step arm: BEFORE the gateId back-reference existed.
        const string preGateId =
            """
            {
              "$id": "GateStep.json",
              "type": "object",
              "properties": {
                "kind": { "type": "string", "const": "gate" },
                "stepId": { "type": "string" },
                "stepName": { "type": "string" },
                "instanceName": { "type": "string" },
                "isTerminal": { "type": "boolean" },
                "runtime": { "$ref": "StepRuntime.json" },
                "configuration": { "$ref": "StepConfigurationDefinition.json" },
                "stepType": { "type": "string" }
              },
              "required": ["kind", "stepId", "stepName", "isTerminal", "stepType"]
            }
            """;

        var emitted = await File.ReadAllTextAsync(
            Path.Combine(EventSchemas.SchemaDir, GateStepSchema + ".json"));

        var result = JsonSchemaDiff.Compare(preGateId, emitted);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("adding an OPTIONAL gateId back-reference is additive — never breaking (DR-3).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking)
            .Because("the DR-3 family is an additive minor, not a major bump.");
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("gateId", StringComparison.Ordinal))
            .Because("the differ must report the added optional `gateId` property.");
    }

    /// <summary>
    /// INV-6/INV-7 mirror for the DR-3 slots on the emitted C# surface: the
    /// <c>gates</c> slot lands as a nullable <c>IReadOnlyList&lt;GateDeclaration&gt;?</c>
    /// (the optional collection), and <c>GateStep.GateId</c> as a nullable
    /// <c>string?</c> — both init-only. This is the C#-side proof that the slots are
    /// OPTIONAL (nullable), typed to the shared <c>GateDeclaration</c> identity, and
    /// immutable.
    /// </summary>
    [Test]
    public async Task EmittedRecords_CarryOptionalGateSlots_InitOnlyAndNullable()
    {
        var asm = typeof(ContractsMarker).Assembly;
        var rootType = asm.GetType("Strategos.Contracts.Generated.WorkflowDefinitionV1");
        var gateStepType = asm.GetType("Strategos.Contracts.Generated.GateStep");
        var declType = asm.GetType("Strategos.Contracts.Generated.GateDeclaration");

        await Assert.That(rootType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.WorkflowDefinitionV1.");
        await Assert.That(gateStepType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.GateStep.");
        await Assert.That(declType).IsNotNull()
            .Because("the DR-2 GateDeclaration must exist (the item type of the gates slot).");

        // WorkflowDefinitionV1.Gates — optional collection of the shared identity.
        var gatesProp = rootType!.GetProperty("Gates");
        await Assert.That(gatesProp).IsNotNull()
            .Because("WorkflowDefinitionV1 must expose the DR-3 Gates slot.");
        await Assert.That(gatesProp!.PropertyType).IsEqualTo(
            typeof(IReadOnlyList<>).MakeGenericType(declType!))
            .Because("Gates must be IReadOnlyList<GateDeclaration> (the shared gate identity).");
        await Assert.That(IsInitOnly(gatesProp)).IsTrue()
            .Because("Gates must be init-only (INV-7).");
        await Assert.That(IsNullable(gatesProp)).IsTrue()
            .Because("Gates is OPTIONAL — nullable (DR-3).");

        // GateStep.GateId — optional string back-reference.
        var gateIdProp = gateStepType!.GetProperty("GateId");
        await Assert.That(gateIdProp).IsNotNull()
            .Because("GateStep must expose the DR-3 GateId back-reference.");
        await Assert.That(gateIdProp!.PropertyType).IsEqualTo(typeof(string))
            .Because("GateId is a plain string id moniker.");
        await Assert.That(IsInitOnly(gateIdProp)).IsTrue()
            .Because("GateId must be init-only (INV-7).");
        await Assert.That(IsNullable(gateIdProp)).IsTrue()
            .Because("GateId is OPTIONAL — nullable (DR-3).");
    }

    private static bool IsInitOnly(PropertyInfo prop)
    {
        var setter = prop.SetMethod;
        return setter is not null
            && setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(m => m == typeof(IsExternalInit));
    }

    private static bool IsNullable(PropertyInfo prop) =>
        new NullabilityInfoContext().Create(prop).ReadState == NullabilityState.Nullable;

    private static IReadOnlyList<string> RequiredNames(JsonElement root)
    {
        if (root.TryGetProperty("required", out var required)
            && required.ValueKind == JsonValueKind.Array)
        {
            return required.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        }

        return Array.Empty<string>();
    }
}
