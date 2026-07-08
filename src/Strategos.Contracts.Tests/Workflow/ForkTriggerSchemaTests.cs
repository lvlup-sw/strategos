// =============================================================================
// <copyright file="ForkTriggerSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using NJsonSchema;
using NJsonSchema.Validation;

using Strategos.Contracts;
using Strategos.Contracts.SchemaDiff;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// DR-8 (issue #151) — the closed <c>ForkTrigger</c> enum (declaration side) and
/// the <c>ForkOccurrence</c> runtime-payload model (occurrence side) with its
/// versioned schema marker. Asserts (a) the emitted JSON Schema is a CLOSED string
/// enum carrying the frozen snake_case wire vocabulary in order; (b) the generated
/// C# enum round-trips each member to its snake_case wire value via the
/// <c>[JsonStringEnumMemberName]</c> + <c>JsonStringEnumConverter&lt;T&gt;</c> path
/// (the GateClass/DR-1 precedent) by VALUE, never ordinal (INV-8); (c) an
/// occurrence missing its REQUIRED evidence — or carrying an EMPTY evidence map —
/// fails schema validation (no unjustified fork); (d) the occurrence carries the
/// explicit <c>schemaVersion</c> version marker; and (e) appending a future
/// <c>exploratory</c> trigger member is a NON-BREAKING additive change under the
/// DR-18 enum-evolution policy — the reason the marker exists.
/// </summary>
/// <remarks>
/// The evidence is a field-name → value MAP (<c>ForkEvidence</c> = <c>Record&lt;string&gt;</c>),
/// so ANY permitted trigger's declared evidence fields are representable on the wire
/// (a <c>gate_contradiction</c> fork carries <c>leftGateId</c>/<c>rightGateId</c>, a
/// <c>ratification_failure</c> fork carries <c>provisionalStampEventId</c>/<c>taints</c>).
/// The schema floor is only that the map is present and non-empty; PER-TRIGGER
/// completeness (WHICH fields a given trigger requires — author-declared per edge, not
/// fixed in the closed enum) is enforced by the generated guard (DR-9), never at the
/// contract level.
/// </remarks>
[Property("Category", "WorkflowIr")]
public sealed class ForkTriggerSchemaTests
{
    private const string TriggerSchema = "ForkTrigger";
    private const string OccurrenceSchema = "ForkOccurrence";

    // The frozen identity map: C# member name -> snake_case wire value, in the
    // exact declaration order of the closed enum (DR-8, #151). Order is part of
    // the contract: the emitted JSON Schema enum array must match it verbatim.
    private static readonly (string Name, string Wire)[] Frozen =
    [
        ("RatificationFailure", "ratification_failure"),
        ("GateContradiction", "gate_contradiction"),
        ("OperatorExplicit", "operator_explicit"),
    ];

    /// <summary>
    /// The emitted <c>ForkTrigger.json</c> is a closed string enum whose values are
    /// EXACTLY the frozen snake_case tokens, in order — no more, no fewer. The
    /// bounded <c>enum</c> array (no open extension) is the closed-vocabulary
    /// contract both runtimes derive from.
    /// </summary>
    [Test]
    public async Task ForkTriggerSchema_IsClosedStringEnum_WithFrozenSnakeCaseValues()
    {
        await Assert.That(EventSchemas.Exists(TriggerSchema)).IsTrue()
            .Because("`tsp compile` must emit a ForkTrigger.json schema document (run scripts/contracts-codegen.sh).");

        var root = await EventSchemas.LoadAsync(TriggerSchema);

        await Assert.That(root.TryGetProperty("type", out var type)).IsTrue()
            .Because("ForkTrigger must be a scalar string enum.");
        await Assert.That(type.GetString()).IsEqualTo("string");

        var values = EventSchemas.EnumValues(root);
        await Assert.That(values.Count).IsEqualTo(Frozen.Length)
            .Because("ForkTrigger is a CLOSED enum — exactly the frozen members, no more.");

        for (var i = 0; i < Frozen.Length; i++)
        {
            await Assert.That(values[i]).IsEqualTo(Frozen[i].Wire)
                .Because($"ForkTrigger wire value #{i} must be \"{Frozen[i].Wire}\".");
        }
    }

    /// <summary>
    /// Reflects over the generated <c>Strategos.Contracts.Generated.ForkTrigger</c>
    /// enum and asserts every member carries its snake_case
    /// <c>[JsonStringEnumMemberName]</c> and round-trips by VALUE (serialize →
    /// snake_case token, back → member) — the same emission path as GateClass.
    /// </summary>
    [Test]
    public async Task ForkTriggerEnum_CarriesSnakeCaseWireNames_AndRoundTripsByValue()
    {
        var enumType = typeof(ContractsMarker).Assembly
            .GetType("Strategos.Contracts.Generated.ForkTrigger");

        await Assert.That(enumType).IsNotNull()
            .Because("the codegen must emit a Strategos.Contracts.Generated.ForkTrigger enum.");
        await Assert.That(enumType!.IsEnum).IsTrue();

        var members = Enum.GetNames(enumType);
        await Assert.That(members.Length).IsEqualTo(Frozen.Length)
            .Because("ForkTrigger must have exactly the frozen member set (closed enum).");

        var options = ContractsJson.Options;
        foreach (var (name, wire) in Frozen)
        {
            await Assert.That(members).Contains(name)
                .Because($"ForkTrigger must carry the member '{name}'.");

            var field = enumType.GetField(name, BindingFlags.Public | BindingFlags.Static);
            await Assert.That(field).IsNotNull();
            var attr = field!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
            await Assert.That(attr).IsNotNull()
                .Because($"ForkTrigger.{name} must carry a [JsonStringEnumMemberName] (snake_case wire identity).");
            await Assert.That(attr!.Name).IsEqualTo(wire)
                .Because($"ForkTrigger.{name} must serialize as \"{wire}\".");

            var value = Enum.Parse(enumType, name);

            var json = JsonSerializer.Serialize(value, enumType, options);
            await Assert.That(json).IsEqualTo($"\"{wire}\"")
                .Because($"ForkTrigger.{name} must serialize to \"{wire}\" (by value, not ordinal).");

            var back = JsonSerializer.Deserialize($"\"{wire}\"", enumType, options);
            await Assert.That(back!.ToString()).IsEqualTo(name)
                .Because($"\"{wire}\" must deserialize back to ForkTrigger.{name}.");
        }
    }

    /// <summary>
    /// The version marker: <c>ForkOccurrence</c> carries an explicit, REQUIRED
    /// <c>schemaVersion</c> pinned to the literal <c>fork.v1</c>. This is the DR-18
    /// marker that makes a future <c>exploratory</c> trigger an additive minor
    /// rather than a redesign — a breaking payload change would bump it to
    /// <c>fork.v2</c>, never mutate <c>fork.v1</c> in place.
    /// </summary>
    [Test]
    public async Task ForkOccurrence_CarriesPinnedSchemaVersionMarker()
    {
        await Assert.That(EventSchemas.Exists(OccurrenceSchema)).IsTrue()
            .Because("`tsp compile` must emit ForkOccurrence.json.");

        var root = await EventSchemas.LoadAsync(OccurrenceSchema);

        await Assert.That(root.TryGetProperty("properties", out var props)).IsTrue();
        await Assert.That(props.TryGetProperty("schemaVersion", out var version)).IsTrue()
            .Because("ForkOccurrence must carry an explicit schemaVersion marker (DR-18).");
        await Assert.That(version.TryGetProperty("const", out var constVal)).IsTrue()
            .Because("the version marker must be a pinned literal (const), not a free string.");
        await Assert.That(constVal.GetString()).IsEqualTo("fork.v1")
            .Because("the pinned wire version of the occurrence payload is `fork.v1`.");

        await Assert.That(RequiredNames(root)).Contains("schemaVersion")
            .Because("the version marker is mandatory — an occurrence with no version is unversioned.");
    }

    /// <summary>
    /// The evidence shape: <c>ForkEvidence</c> is a field-name → value MAP
    /// (<c>Record&lt;string&gt;</c>), so any permitted trigger's declared evidence fields
    /// are representable — a <c>gate_contradiction</c> occurrence carries its own
    /// <c>leftGateId</c>/<c>rightGateId</c>, not a fixed ratification pair. The emitted
    /// <c>evidence</c> slot on <c>ForkOccurrence</c> refs the string-valued
    /// <c>RecordString</c> map and carries the <c>minProperties: 1</c> non-empty floor.
    /// </summary>
    [Test]
    public async Task ForkEvidence_IsNonEmptyFieldNameValueMap()
    {
        await Assert.That(EventSchemas.Exists(OccurrenceSchema)).IsTrue()
            .Because("`tsp compile` must emit ForkOccurrence.json (run scripts/contracts-codegen.sh).");

        var root = await EventSchemas.LoadAsync(OccurrenceSchema);
        await Assert.That(root.TryGetProperty("properties", out var props)).IsTrue();
        await Assert.That(props.TryGetProperty("evidence", out var evidence)).IsTrue()
            .Because("ForkOccurrence carries an evidence slot.");

        // The evidence slot is a map ($ref to the string-valued RecordString map), not a
        // fixed pair of ratification fields — so ANY trigger's evidence is representable.
        await Assert.That(evidence.TryGetProperty("$ref", out var evidenceRef)).IsTrue()
            .Because("evidence is a field-name → value map (Record<string>), carried as a $ref.");
        await Assert.That(evidenceRef.GetString()).IsEqualTo("RecordString.json")
            .Because("the evidence map is the string-valued RecordString map.");

        // The non-empty floor: an occurrence with an empty evidence map carries no
        // justification (@minProperties(1)); per-trigger completeness is the guard's job.
        await Assert.That(evidence.TryGetProperty("minProperties", out var minProps)).IsTrue()
            .Because("the evidence map declares a non-empty floor (@minProperties(1)).");
        await Assert.That(minProps.GetInt32()).IsEqualTo(1);

        // The RecordString map itself is a string-valued open map (unevaluated/additional
        // properties typed as string).
        await Assert.That(EventSchemas.Exists("RecordString")).IsTrue()
            .Because("the Record<string> map schema must be emitted.");
        var mapRoot = await EventSchemas.LoadAsync("RecordString");
        await Assert.That(mapRoot.GetProperty("type").GetString()).IsEqualTo("object");
    }

    /// <summary>
    /// The occurrence shape: <c>schemaVersion</c>, <c>trigger</c>, and
    /// <c>evidence</c> are all REQUIRED, so an occurrence that omits its evidence
    /// fails schema validation — an unjustified fork is unrepresentable on the wire.
    /// A fully-formed occurrence (whose <c>trigger</c>/<c>evidence</c> <c>$ref</c>s
    /// resolve) validates.
    /// </summary>
    [Test]
    public async Task ForkOccurrence_MissingEvidence_FailsSchemaValidation()
    {
        await Assert.That(EventSchemas.Exists(OccurrenceSchema)).IsTrue()
            .Because("`tsp compile` must emit ForkOccurrence.json.");

        var root = await EventSchemas.LoadAsync(OccurrenceSchema);
        var required = RequiredNames(root);
        await Assert.That(required).Contains("trigger")
            .Because("the trigger is the mandatory identity of an occurrence (DR-8).");
        await Assert.That(required).Contains("evidence")
            .Because("evidence is REQUIRED — no unjustified fork.");

        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(EventSchemas.SchemaDir, OccurrenceSchema + ".json"));

        // An occurrence with a trigger but NO evidence — must be rejected.
        const string missingEvidence =
            """{ "schemaVersion": "fork.v1", "trigger": "ratification_failure" }""";
        var missingErrors = schema.Validate(missingEvidence);
        await Assert.That(missingErrors.Any(e =>
                e.Kind == ValidationErrorKind.PropertyRequired
                && e.Property == "evidence"))
            .IsTrue()
            .Because("an occurrence missing its REQUIRED `evidence` must fail schema validation.");

        // A complete, justified occurrence — the evidence is a non-empty field-name → value
        // map keyed by the trigger's declared fields (exercises $ref resolution to the map).
        const string complete =
            """
            {
              "schemaVersion": "fork.v1",
              "trigger": "ratification_failure",
              "evidence": {
                "provisionalStampEventId": "evt-42",
                "taints": "state:dirty"
              }
            }
            """;
        var okErrors = schema.Validate(complete);
        await Assert.That(okErrors.Count).IsEqualTo(0)
            .Because("a complete, justified fork occurrence must validate:\n"
                + string.Join("\n", okErrors.Select(e => e.ToString())));

        // A non-ratification trigger carrying its OWN declared evidence fields is equally
        // representable — the map is not fixed to the ratification fields.
        const string gateContradiction =
            """
            {
              "schemaVersion": "fork.v1",
              "trigger": "gate_contradiction",
              "evidence": {
                "leftGateId": "gate-L",
                "rightGateId": "gate-R"
              }
            }
            """;
        var gateErrors = schema.Validate(gateContradiction);
        await Assert.That(gateErrors.Count).IsEqualTo(0)
            .Because("a gate_contradiction occurrence carrying its own evidence fields must validate:\n"
                + string.Join("\n", gateErrors.Select(e => e.ToString())));
    }

    /// <summary>
    /// The DR-18 evolution property the version marker exists for: appending a future
    /// <c>exploratory</c> trigger member to the closed <c>ForkTrigger</c> enum is a
    /// NON-BREAKING additive change (flagged NOTICE — strict converters reject unknown
    /// members, so consumers upgrade before producers emit it), never breaking. Runs
    /// against the ACTUAL emitted <c>ForkTrigger.json</c> so it is coupled to the
    /// shipped artifact.
    /// </summary>
    [Test]
    public async Task AddingExploratoryTrigger_IsNonBreaking_UnderDr18()
    {
        await Assert.That(EventSchemas.Exists(TriggerSchema)).IsTrue()
            .Because("`tsp compile` must emit ForkTrigger.json.");

        var emitted = await File.ReadAllTextAsync(
            Path.Combine(EventSchemas.SchemaDir, TriggerSchema + ".json"));

        // The FUTURE ForkTrigger: the emitted closed set with the budget-bounded
        // `exploratory` member appended (basileus PR #401 rec L-4).
        var futureNode = JsonNode.Parse(emitted)!;
        futureNode["enum"]!.AsArray().Add("exploratory");
        var future = futureNode.ToJsonString();

        var result = JsonSchemaDiff.Compare(emitted, future);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("appending a new closed-enum member is additive — never breaking (DR-18).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Notice)
            .Because("an added enum member is a flagged NOTICE (consumer-upgrade-ordering), not silent.");
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Notice
                && c.Description.Contains("exploratory", StringComparison.Ordinal))
            .Because("the differ must report the added `exploratory` member.");
    }

    /// <summary>
    /// INV-6/INV-7 mirror for the DR-8 occurrence surface: <c>ForkOccurrence</c> is a
    /// sealed record whose <c>Trigger</c> is the typed generated <c>ForkTrigger</c>
    /// enum (the shared identity, not a string) and whose <c>Evidence</c> is the
    /// non-nullable field-name → value map (<c>IReadOnlyDictionary&lt;string, string&gt;</c>,
    /// required — no unjustified fork). A focused mirror of the surface-wide
    /// <c>EmitterShapeTests</c>, pinned to DR-8.
    /// </summary>
    [Test]
    public async Task ForkOccurrence_EmittedRecord_TriggerIsEnum_EvidenceIsRequiredMap_InitOnly()
    {
        var asm = typeof(ContractsMarker).Assembly;
        var occType = asm.GetType("Strategos.Contracts.Generated.ForkOccurrence");
        var triggerType = asm.GetType("Strategos.Contracts.Generated.ForkTrigger");

        await Assert.That(occType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.ForkOccurrence.");
        await Assert.That(triggerType).IsNotNull()
            .Because("the codegen must emit Strategos.Contracts.Generated.ForkTrigger.");

        // The monomorphic ForkEvidence record is gone — evidence is now a map.
        await Assert.That(asm.GetType("Strategos.Contracts.Generated.ForkEvidence")).IsNull()
            .Because("the monomorphic ForkEvidence record is replaced by a field-name → value map.");

        await Assert.That(occType!.IsSealed).IsTrue()
            .Because("ForkOccurrence must be a sealed record (INV-6).");

        // Trigger is the typed shared enum, not a loose string.
        var triggerProp = occType.GetProperty("Trigger");
        await Assert.That(triggerProp).IsNotNull();
        await Assert.That(triggerProp!.PropertyType).IsEqualTo(triggerType)
            .Because("ForkOccurrence.Trigger must be the typed ForkTrigger enum (INV-8).");

        // Evidence is the required (non-nullable) field-name → value map.
        var evidenceProp = occType.GetProperty("Evidence");
        await Assert.That(evidenceProp).IsNotNull();
        await Assert.That(evidenceProp!.PropertyType).IsEqualTo(typeof(IReadOnlyDictionary<string, string>))
            .Because("ForkOccurrence.Evidence must be a field-name → value map (Record<string>).");
        await Assert.That(IsInitOnly(evidenceProp)).IsTrue()
            .Because("Evidence must be init-only (INV-7).");
        await Assert.That(new NullabilityInfoContext().Create(evidenceProp).ReadState)
            .IsEqualTo(NullabilityState.NotNull)
            .Because("Evidence is REQUIRED — non-nullable (DR-8, no unjustified fork).");
    }

    private static bool IsInitOnly(PropertyInfo prop)
    {
        var setter = prop.SetMethod;
        return setter is not null
            && setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(m => m == typeof(IsExternalInit));
    }

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
