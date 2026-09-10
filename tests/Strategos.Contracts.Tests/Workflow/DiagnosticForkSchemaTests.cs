// =============================================================================
// <copyright file="DiagnosticForkSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using NJsonSchema;
using NJsonSchema.Validation;

using Strategos.Contracts.SchemaDiff;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// DR-10 (#151 → #100) — the wire half of the diagnostic fork edge. Asserts the
/// additive <c>DiagnosticForkDefinition</c> structural shape and its
/// <c>PermittedForkTrigger</c> child: anchor step monikers, permitted closed
/// triggers each paired with their DECLARATION-side evidence-ref schema (the
/// evidence FIELD NAMES a future occurrence must carry — not runtime values), a
/// <c>maxForks</c> integer bound, and a compensation-seed moniker. Pins the
/// critical INV-8 invariant mechanically: EVERY step/type reference on the shape
/// is a string moniker — never a CLR-type-shaped field — and the only typed
/// reference (<c>ForkTrigger</c>) is itself a closed string enum, not a runtime
/// <c>Type</c>. Also asserts the new <c>WorkflowDefinitionV1.diagnosticForks?</c>
/// slot is OPTIONAL and additive, so the extension is NON-BREAKING (the differ
/// sees exactly one added optional property). The saga lowering (DR-9), the
/// <c>ToContract()</c> projection (task 014), and the builder surface (task 011)
/// are deliberately out of scope here — this family is the wire contract shape.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class DiagnosticForkSchemaTests
{
    private const string ForkDefSchema = "DiagnosticForkDefinition";
    private const string PermittedSchema = "PermittedForkTrigger";
    private const string TriggerSchema = "ForkTrigger";
    private const string WorkflowRootSchema = "WorkflowDefinitionV1";

    // Property NAMES that would betray a CLR-type-shaped field (INV-8 forbids
    // them). Matched case-insensitively against the emitted schema's structural
    // property keys — never its descriptions.
    private static readonly string[] ForbiddenTypeHandleFragments =
    [
        "assemblyqualified",
        "typeof",
        "clrtype",
        "fullname",
        "systemtype",
        "runtimetype",
    ];

    /// <summary>
    /// INV-8 (critical): every step/type reference on <c>DiagnosticForkDefinition</c>
    /// is a plain string moniker — the anchor step ids and the compensation seed are
    /// <c>type: string</c>, the <c>maxForks</c> bound is an <c>integer</c>, and the
    /// only cross-references (<c>permittedTriggers.items</c> → <c>PermittedForkTrigger</c>,
    /// and its <c>trigger</c> → the closed <c>ForkTrigger</c> STRING enum) resolve to
    /// moniker/vocabulary shapes, never a CLR <c>Type</c>. No property name betrays a
    /// runtime type handle, and no <c>$ref</c> escapes the allowed set.
    /// </summary>
    [Test]
    public async Task DiagnosticForkDefinition_ShapeIsMonikersOnly_Inv8()
    {
        await Assert.That(EventSchemas.Exists(ForkDefSchema)).IsTrue()
            .Because("`tsp compile` must emit DiagnosticForkDefinition.json (run scripts/contracts-codegen.sh).");
        await Assert.That(EventSchemas.Exists(PermittedSchema)).IsTrue()
            .Because("`tsp compile` must emit PermittedForkTrigger.json.");

        var forkDef = await EventSchemas.LoadAsync(ForkDefSchema);
        await Assert.That(forkDef.TryGetProperty("properties", out var props)).IsTrue();

        // Anchor step ref(s) — an array of plain string monikers.
        var anchors = props.GetProperty("anchorStepIds");
        await Assert.That(anchors.GetProperty("type").GetString()).IsEqualTo("array")
            .Because("anchor step refs are a collection of step monikers.");
        await Assert.That(anchors.GetProperty("items").GetProperty("type").GetString()).IsEqualTo("string")
            .Because("INV-8: each anchor step ref is a plain string moniker, never a CLR type.");

        // The maxForks bound — an integer, not a string or a type handle.
        var maxForks = props.GetProperty("maxForks");
        await Assert.That(maxForks.GetProperty("type").GetString()).IsEqualTo("integer")
            .Because("maxForks is an integer bound the generated guard enforces (DR-9).");

        // The compensation seed — a plain string moniker.
        var seed = props.GetProperty("compensationSeed");
        await Assert.That(seed.GetProperty("type").GetString()).IsEqualTo("string")
            .Because("INV-8: the compensation seed is a plain string moniker, never a CLR type.");

        // Permitted triggers — an array whose items $ref the PermittedForkTrigger child.
        var permitted = props.GetProperty("permittedTriggers");
        await Assert.That(permitted.GetProperty("type").GetString()).IsEqualTo("array");
        await Assert.That(Path.GetFileNameWithoutExtension(
                permitted.GetProperty("items").GetProperty("$ref").GetString()))
            .IsEqualTo(PermittedSchema)
            .Because("permitted triggers pair a trigger with its evidence-ref schema (PermittedForkTrigger).");

        // No property NAME on either schema betrays a CLR type handle (structural
        // keys only — never the descriptions, which legitimately say "never a CLR type").
        foreach (var name in PropertyNames(forkDef).Concat(PropertyNames(await EventSchemas.LoadAsync(PermittedSchema))))
        {
            foreach (var fragment in ForbiddenTypeHandleFragments)
            {
                await Assert.That(name.Contains(fragment, StringComparison.OrdinalIgnoreCase)).IsFalse()
                    .Because($"INV-8: property `{name}` must not imply a runtime Type (`{fragment}`).");
            }
        }

        // Every $ref reachable from the shape stays inside the moniker-only closure:
        // the PermittedForkTrigger child and the closed ForkTrigger vocabulary — no
        // ref to a Type-shaped model.
        var refTargets = new HashSet<string>();
        CollectRefTargets(forkDef, refTargets);
        CollectRefTargets(await EventSchemas.LoadAsync(PermittedSchema), refTargets);
        foreach (var target in refTargets)
        {
            await Assert.That(target is PermittedSchema or TriggerSchema).IsTrue()
                .Because($"INV-8: the shape may only reference moniker/vocabulary models, not `{target}`.");
        }

        // The one typed reference is the closed ForkTrigger enum — a STRING wire
        // vocabulary, not a CLR Type handle.
        var trigger = await EventSchemas.LoadAsync(TriggerSchema);
        await Assert.That(trigger.GetProperty("type").GetString()).IsEqualTo("string")
            .Because("INV-8: even the typed reference (ForkTrigger) is a string enum, not a CLR type.");
    }

    /// <summary>
    /// The declaration-side pairing: <c>PermittedForkTrigger</c> binds one closed
    /// <c>ForkTrigger</c> (<c>$ref</c> to the shared identity) to the evidence FIELD
    /// NAMES a future occurrence must carry — a NON-EMPTY array of plain string
    /// monikers (declaration side, NOT runtime values). Both are REQUIRED.
    /// </summary>
    [Test]
    public async Task PermittedForkTrigger_PairsClosedTriggerWithEvidenceFieldNames()
    {
        await Assert.That(EventSchemas.Exists(PermittedSchema)).IsTrue()
            .Because("`tsp compile` must emit PermittedForkTrigger.json.");

        var permitted = await EventSchemas.LoadAsync(PermittedSchema);
        await Assert.That(permitted.TryGetProperty("properties", out var props)).IsTrue();

        // trigger $refs the shared closed ForkTrigger identity (DR-8).
        var trigger = props.GetProperty("trigger");
        await Assert.That(Path.GetFileNameWithoutExtension(trigger.GetProperty("$ref").GetString()))
            .IsEqualTo(TriggerSchema)
            .Because("a permitted trigger names one closed ForkTrigger (the shared identity).");

        // requiredEvidenceFields — the DECLARATION side: field NAMES, a non-empty
        // array of plain strings.
        var evidence = props.GetProperty("requiredEvidenceFields");
        await Assert.That(evidence.GetProperty("type").GetString()).IsEqualTo("array");
        await Assert.That(evidence.GetProperty("items").GetProperty("type").GetString()).IsEqualTo("string")
            .Because("the evidence-ref schema names FIELD NAMES (strings), not runtime values.");
        await Assert.That(evidence.GetProperty("minItems").GetInt32()).IsEqualTo(1)
            .Because("a permitted trigger that names no evidence declares no justification schema.");

        await Assert.That(RequiredNames(permitted)).Contains("trigger")
            .Because("the trigger is the mandatory identity of a permitted-trigger entry.");
        await Assert.That(RequiredNames(permitted)).Contains("requiredEvidenceFields")
            .Because("the evidence-ref schema is REQUIRED — a permitted trigger declares what a fork must carry.");
    }

    /// <summary>
    /// The <c>maxForks</c> bound and the per-trigger evidence-ref schema are PRESENT
    /// and REQUIRED: a definition missing either fails schema validation, a bound of
    /// zero fails (<c>@minValue 1</c>), and a permitted trigger with an empty
    /// evidence-ref set fails (<c>@minItems 1</c>). A complete definition — whose
    /// nested <c>PermittedForkTrigger</c> / <c>ForkTrigger</c> <c>$ref</c>s resolve —
    /// validates. Exercises the real cross-file <c>$ref</c> resolution.
    /// </summary>
    [Test]
    public async Task DiagnosticForkDefinition_RequiresMaxForksAndEvidenceRefSchema()
    {
        await Assert.That(EventSchemas.Exists(ForkDefSchema)).IsTrue()
            .Because("`tsp compile` must emit DiagnosticForkDefinition.json.");

        var root = await EventSchemas.LoadAsync(ForkDefSchema);
        var required = RequiredNames(root);
        await Assert.That(required).Contains("anchorStepIds")
            .Because("an anchor step ref is mandatory — a fork has somewhere to fork from.");
        await Assert.That(required).Contains("permittedTriggers")
            .Because("the permitted-triggers evidence-ref schema is mandatory (DR-7 — no fork without a trigger).");
        await Assert.That(required).Contains("maxForks")
            .Because("the maxForks bound is mandatory (the generated guard enforces it, DR-9).");
        await Assert.That(required).Contains("compensationSeed")
            .Because("the compensation seed moniker is mandatory.");

        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(EventSchemas.SchemaDir, ForkDefSchema + ".json"));

        // A complete, well-formed definition — must validate ($ref resolution).
        const string complete =
            """
            {
              "anchorStepIds": ["ratify-step"],
              "permittedTriggers": [
                {
                  "trigger": "ratification_failure",
                  "requiredEvidenceFields": ["provisionalStampEventId", "taints"]
                }
              ],
              "maxForks": 2,
              "compensationSeed": "seed-rollback"
            }
            """;
        var okErrors = schema.Validate(complete);
        await Assert.That(okErrors.Count).IsEqualTo(0)
            .Because("a complete diagnostic fork definition must validate:\n"
                + string.Join("\n", okErrors.Select(e => e.ToString())));

        // Missing the maxForks bound — REQUIRED.
        const string missingMaxForks =
            """
            {
              "anchorStepIds": ["ratify-step"],
              "permittedTriggers": [
                { "trigger": "ratification_failure", "requiredEvidenceFields": ["provisionalStampEventId"] }
              ],
              "compensationSeed": "seed-rollback"
            }
            """;
        await Assert.That(schema.Validate(missingMaxForks).Any(e =>
                e.Kind == ValidationErrorKind.PropertyRequired && e.Property == "maxForks"))
            .IsTrue()
            .Because("a definition missing its REQUIRED maxForks bound must fail schema validation.");

        // Missing the permitted-triggers evidence-ref schema entirely — REQUIRED.
        const string missingTriggers =
            """
            {
              "anchorStepIds": ["ratify-step"],
              "maxForks": 2,
              "compensationSeed": "seed-rollback"
            }
            """;
        await Assert.That(schema.Validate(missingTriggers).Any(e =>
                e.Kind == ValidationErrorKind.PropertyRequired && e.Property == "permittedTriggers"))
            .IsTrue()
            .Because("a definition missing its REQUIRED permitted-triggers evidence-ref schema must fail.");

        // A maxForks bound of zero — forbids the fork the edge exists to permit.
        const string zeroMaxForks =
            """
            {
              "anchorStepIds": ["ratify-step"],
              "permittedTriggers": [
                { "trigger": "ratification_failure", "requiredEvidenceFields": ["provisionalStampEventId"] }
              ],
              "maxForks": 0,
              "compensationSeed": "seed-rollback"
            }
            """;
        await Assert.That(schema.Validate(zeroMaxForks).Count).IsGreaterThan(0)
            .Because("maxForks must be at least 1 (@minValue 1) — a zero bound is rejected.");

        // A permitted trigger with an EMPTY evidence-ref set — no declared floor.
        const string emptyEvidence =
            """
            {
              "anchorStepIds": ["ratify-step"],
              "permittedTriggers": [
                { "trigger": "ratification_failure", "requiredEvidenceFields": [] }
              ],
              "maxForks": 2,
              "compensationSeed": "seed-rollback"
            }
            """;
        await Assert.That(schema.Validate(emptyEvidence).Count).IsGreaterThan(0)
            .Because("an empty evidence-ref set declares no justification schema (@minItems 1) — rejected.");
    }

    /// <summary>
    /// The additive slot on the root: <c>WorkflowDefinitionV1</c> carries an OPTIONAL
    /// <c>diagnosticForks</c> array whose items <c>$ref</c> the shared
    /// <c>DiagnosticForkDefinition</c>. The slot is present in <c>properties</c> but
    /// absent from <c>required</c>, so a workflow with no fork edge omits it entirely.
    /// </summary>
    [Test]
    public async Task WorkflowRoot_HasOptionalDiagnosticForksSlot_RefsDefinition()
    {
        await Assert.That(EventSchemas.Exists(WorkflowRootSchema)).IsTrue()
            .Because("`tsp compile` must emit WorkflowDefinitionV1.json.");

        var root = await EventSchemas.LoadAsync(WorkflowRootSchema);
        await Assert.That(root.TryGetProperty("properties", out var props)).IsTrue();
        await Assert.That(props.TryGetProperty("diagnosticForks", out var forks)).IsTrue()
            .Because("the workflow root must carry the DR-10 `diagnosticForks` slot.");

        await Assert.That(forks.GetProperty("type").GetString()).IsEqualTo("array")
            .Because("`diagnosticForks` is a collection of fork-edge declarations.");
        await Assert.That(Path.GetFileNameWithoutExtension(
                forks.GetProperty("items").GetProperty("$ref").GetString()))
            .IsEqualTo(ForkDefSchema)
            .Because("`diagnosticForks` items must $ref the shared DiagnosticForkDefinition.");

        await Assert.That(RequiredNames(root)).DoesNotContain("diagnosticForks")
            .Because("`diagnosticForks` is additive and OPTIONAL — a fork-less workflow omits it (DR-10).");
    }

    /// <summary>
    /// The DR-10 versioning posture: adding the OPTIONAL <c>diagnosticForks</c> slot
    /// is a purely additive, NON-BREAKING wire change. Diffs the ACTUAL emitted
    /// <c>WorkflowDefinitionV1.json</c> against a copy of itself with the slot removed,
    /// so the "before" is coupled to the shipped artifact and the only delta is the
    /// added optional property.
    /// </summary>
    [Test]
    public async Task AddingDiagnosticForksSlot_IsNonBreaking()
    {
        await Assert.That(EventSchemas.Exists(WorkflowRootSchema)).IsTrue()
            .Because("`tsp compile` must emit WorkflowDefinitionV1.json.");

        var emitted = await File.ReadAllTextAsync(
            Path.Combine(EventSchemas.SchemaDir, WorkflowRootSchema + ".json"));

        // The pre-DR-10 root: the emitted root with the diagnosticForks slot removed.
        var beforeNode = JsonNode.Parse(emitted)!;
        var removed = beforeNode["properties"]!.AsObject().Remove("diagnosticForks");
        await Assert.That(removed).IsTrue()
            .Because("the emitted root must actually carry the diagnosticForks slot to remove.");
        var before = beforeNode.ToJsonString();

        var result = JsonSchemaDiff.Compare(before, emitted);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("adding an OPTIONAL diagnosticForks slot is additive — never breaking (DR-10).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking)
            .Because("the DR-10 wire addition is an additive minor, not a major bump.");
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("diagnosticForks", StringComparison.Ordinal))
            .Because("the differ must report the added optional `diagnosticForks` property.");
    }

    /// <summary>
    /// INV-8 + INV-6/INV-7 mirror on the emitted C# surface. The
    /// <c>DiagnosticForks</c> slot lands as a nullable
    /// <c>IReadOnlyList&lt;DiagnosticForkDefinition&gt;?</c> (optional collection,
    /// init-only). On the shape itself, every step/type reference is a string
    /// moniker: <c>AnchorStepIds</c> is <c>IReadOnlyList&lt;string&gt;</c>,
    /// <c>CompensationSeed</c> is <c>string</c>, <c>MaxForks</c> is <c>int</c>, and the
    /// per-trigger <c>RequiredEvidenceFields</c> is <c>IReadOnlyList&lt;string&gt;</c>;
    /// the only typed reference is the shared <c>ForkTrigger</c> enum. NO property on
    /// either record is a runtime <c>System.Type</c> (INV-8 on the CLR side).
    /// </summary>
    [Test]
    public async Task EmittedRecords_DiagnosticForkSlot_MonikersAreStrings_NoClrType_InitOnly()
    {
        var asm = typeof(ContractsMarker).Assembly;
        var rootType = asm.GetType("Strategos.Contracts.Generated.WorkflowDefinitionV1");
        var forkDefType = asm.GetType("Strategos.Contracts.Generated.DiagnosticForkDefinition");
        var permittedType = asm.GetType("Strategos.Contracts.Generated.PermittedForkTrigger");
        var triggerType = asm.GetType("Strategos.Contracts.Generated.ForkTrigger");

        await Assert.That(rootType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.WorkflowDefinitionV1.");
        await Assert.That(forkDefType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.DiagnosticForkDefinition.");
        await Assert.That(permittedType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.PermittedForkTrigger.");
        await Assert.That(triggerType).IsNotNull()
            .Because("the DR-8 ForkTrigger enum must exist (the typed reference on the shape).");

        await Assert.That(forkDefType!.IsSealed).IsTrue()
            .Because("DiagnosticForkDefinition must be a sealed record (INV-6).");
        await Assert.That(permittedType!.IsSealed).IsTrue()
            .Because("PermittedForkTrigger must be a sealed record (INV-6).");

        // WorkflowDefinitionV1.DiagnosticForks — optional collection of the shape.
        var forksProp = rootType!.GetProperty("DiagnosticForks");
        await Assert.That(forksProp).IsNotNull()
            .Because("WorkflowDefinitionV1 must expose the DR-10 DiagnosticForks slot.");
        await Assert.That(forksProp!.PropertyType).IsEqualTo(
            typeof(IReadOnlyList<>).MakeGenericType(forkDefType!))
            .Because("DiagnosticForks must be IReadOnlyList<DiagnosticForkDefinition>.");
        await Assert.That(IsInitOnly(forksProp)).IsTrue()
            .Because("DiagnosticForks must be init-only (INV-7).");
        await Assert.That(IsNullable(forksProp)).IsTrue()
            .Because("DiagnosticForks is OPTIONAL — nullable (DR-10).");

        // Monikers-only shape (INV-8): the step/type refs are strings.
        await Assert.That(forkDefType.GetProperty("AnchorStepIds")!.PropertyType)
            .IsEqualTo(typeof(IReadOnlyList<string>))
            .Because("INV-8: anchor step refs are string monikers.");
        await Assert.That(forkDefType.GetProperty("CompensationSeed")!.PropertyType)
            .IsEqualTo(typeof(string))
            .Because("INV-8: the compensation seed is a string moniker.");
        await Assert.That(forkDefType.GetProperty("MaxForks")!.PropertyType)
            .IsEqualTo(typeof(int))
            .Because("maxForks is an integer bound.");
        await Assert.That(forkDefType.GetProperty("PermittedTriggers")!.PropertyType)
            .IsEqualTo(typeof(IReadOnlyList<>).MakeGenericType(permittedType!))
            .Because("permitted triggers are the PermittedForkTrigger declaration entries.");

        // The per-trigger evidence-ref schema is field NAMES (strings); the trigger
        // is the shared typed identity (the ONLY non-string reference).
        await Assert.That(permittedType.GetProperty("RequiredEvidenceFields")!.PropertyType)
            .IsEqualTo(typeof(IReadOnlyList<string>))
            .Because("INV-8: the evidence-ref schema names FIELD NAMES (strings), not runtime values.");
        await Assert.That(permittedType.GetProperty("Trigger")!.PropertyType)
            .IsEqualTo(triggerType)
            .Because("the trigger is the typed ForkTrigger enum — a string wire vocabulary, not a CLR type.");

        // INV-8 on the CLR side: NO property on either record is a runtime System.Type.
        foreach (var prop in forkDefType.GetProperties().Concat(permittedType.GetProperties()))
        {
            await Assert.That(IsTypeHandle(prop.PropertyType)).IsFalse()
                .Because($"INV-8: `{prop.Name}` must not be a CLR Type handle.");
        }
    }

    private static bool IsTypeHandle(Type t)
    {
        if (t == typeof(Type))
        {
            return true;
        }

        if (t.IsArray && t.GetElementType() == typeof(Type))
        {
            return true;
        }

        return t.IsGenericType
            && t.GetGenericArguments().Any(a => a == typeof(Type));
    }

    private static void CollectRefTargets(JsonElement node, ISet<string> acc)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in node.EnumerateObject())
                {
                    if (prop.Name == "$ref" && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var target = Path.GetFileNameWithoutExtension(prop.Value.GetString());
                        if (target is not null)
                        {
                            acc.Add(target);
                        }
                    }
                    else
                    {
                        CollectRefTargets(prop.Value, acc);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    CollectRefTargets(item, acc);
                }

                break;
        }
    }

    private static IReadOnlyList<string> PropertyNames(JsonElement schema) =>
        schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object
            ? props.EnumerateObject().Select(p => p.Name).ToList()
            : Array.Empty<string>();

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
