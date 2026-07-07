using System.Text.Json;

using NJsonSchema;
using NJsonSchema.Validation;

using Strategos.Ontology.MCP;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// Task 022 (DR-16 parity half, EVENT side, #152) — the MECHANICAL cross-check that
/// the hosting-mapped abstained payload (task 021's <see cref="OntologyAbstainedRecord"/>,
/// the occurrence-side sibling emitted through the audit sink) is the SAME wire shape
/// as the Contracts-emitted <c>OntologyAbstained.json</c> event schema (task 006).
/// The record carries NO Contracts type — the shape is mirrored by counts — so this
/// suite is what keeps the hand-authored twin from drifting from the schema.
/// </summary>
/// <remarks>
/// Parity is pinned in BOTH directions and goes red on drift from EITHER side:
/// <list type="bullet">
/// <item>VALUE parity — the serialized payload must validate against the schema
/// (<c>type</c> const, the <c>nearestRecordsCount</c> integer floor).</item>
/// <item>FIELD-SET parity — the serialized top-level key set must equal the schema's
/// declared <c>properties</c> keys exactly (there is no <c>_meta</c> envelope on the
/// event), so an added/renamed field on either side is caught.</item>
/// <item>The <c>type</c> discriminator VALUE and the non-negative floor are asserted
/// equal to the schema's <c>const</c> / <c>minimum</c>, so a drift in either the C#
/// constant or the schema is red.</item>
/// </list>
/// The schema is loaded as a FILE; neither the ontology core nor hosting takes a
/// Strategos.Contracts dependency (asserted below).
/// </remarks>
public sealed class AbstainedPayloadConformanceTests
{
    private const string SchemaFile = "OntologyAbstained.json";

    /// <summary>
    /// The wire projection: camelCase, matching the TypeSpec-emitted schema's field
    /// names (<c>type</c>, <c>nearestRecordsCount</c>) and the MCP SDK's Web defaults.
    /// </summary>
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    // ---- The abstained payload serializes to the OntologyAbstained event schema ----

    [Test]
    public async Task AbstainedRecord_Serialized_ValidatesAgainstEventSchema()
    {
        var json = SerializePayload(new OntologyAbstainedRecord(3));

        await AssertConforms(json);
    }

    [Test]
    public async Task AbstainedRecord_FieldSet_MatchesEventSchemaExactly()
    {
        var json = SerializePayload(new OntologyAbstainedRecord(2));

        var serializedKeys = TopLevelKeys(json).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var schemaKeys = SchemaPropertyKeys().OrderBy(k => k, StringComparer.Ordinal).ToList();

        await Assert.That(serializedKeys).IsEquivalentTo(schemaKeys)
            .Because($"the serialized fields must match {SchemaFile} properties exactly — "
                + $"serialized=[{string.Join(",", serializedKeys)}] schema=[{string.Join(",", schemaKeys)}].");
    }

    /// <summary>
    /// Edge case: the boundary count 0 (an abstention that surfaced no nearest records)
    /// must conform — the schema floors <c>nearestRecordsCount</c> at <c>minimum: 0</c>.
    /// </summary>
    [Test]
    public async Task AbstainedRecord_ZeroCount_Conforms()
    {
        var json = SerializePayload(new OntologyAbstainedRecord(0));

        await AssertConforms(json);
    }

    // ---- Discriminator + floor VALUE parity, asserted against the schema itself ----

    /// <summary>
    /// The C# <see cref="OntologyAbstainedRecord.EventType"/> and the schema's
    /// <c>type.const</c> must be the SAME string, so a drift in either the C# constant
    /// or the schema discriminator goes red.
    /// </summary>
    [Test]
    public async Task AbstainedRecord_TypeDiscriminator_EqualsSchemaConst()
    {
        var schemaConst = SchemaPropertyConst("type");
        await Assert.That(new OntologyAbstainedRecord(0).Type).IsEqualTo(schemaConst)
            .Because("the event type discriminator must equal the schema's type const.");
    }

    /// <summary>
    /// The non-negative floor is the SAME invariant on both sides: the C# constructor
    /// refuses a negative count, and the schema pins <c>minimum: 0</c> so a hand-built
    /// negative-count payload FAILS validation. The C# side cannot construct the
    /// violating value, so this pins the schema side too.
    /// </summary>
    [Test]
    public async Task NegativeCount_Unrepresentable_MirroredBySchemaMinimum()
    {
        await Assert.That(() => new OntologyAbstainedRecord(-1)).Throws<ArgumentOutOfRangeException>();

        const string negative = """{ "type": "ontology.abstained", "nearestRecordsCount": -1 }""";
        var errors = await Validate(negative);
        await Assert.That(errors.Count).IsGreaterThan(0)
            .Because("minimum:0 must reject a negative count, mirroring the C# constructor guard.");
    }

    // ---- Dependency posture: NJsonSchema/Contracts stay in the TEST project ----

    /// <summary>
    /// Validating against the emitted schema FILE must NOT leak Strategos.Contracts (or
    /// NJsonSchema) into the ontology CORE assembly that owns
    /// <see cref="OntologyAbstainedRecord"/> — the record mirrors the schema by counts,
    /// not by a shared type. The core dependency set is unchanged.
    /// </summary>
    [Test]
    public async Task OntologyCoreAssembly_TakesNoContractsOrNJsonSchemaDependency()
    {
        var core = typeof(OntologyAbstainedRecord).Assembly;

        foreach (var referenced in core.GetReferencedAssemblies())
        {
            var name = referenced.Name ?? string.Empty;
            await Assert.That(name.Contains("Strategos.Contracts", StringComparison.OrdinalIgnoreCase)).IsFalse()
                .Because("the ontology core must not reference Strategos.Contracts (DR-16/DR-17 independence).");
            await Assert.That(name.Contains("NJsonSchema", StringComparison.OrdinalIgnoreCase)).IsFalse()
                .Because("NJsonSchema belongs to the TEST project, never the ontology core.");
        }
    }

    // ---------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------

    private static string SerializePayload(OntologyAbstainedRecord record) =>
        JsonSerializer.Serialize(record, WireOptions);

    private static async Task AssertConforms(string json)
    {
        var errors = await Validate(json);
        await Assert.That(errors.Count).IsEqualTo(0)
            .Because($"the hosting-mapped payload must conform to {SchemaFile}:\n{json}\n"
                + string.Join("\n", errors.Select(e => e.ToString())));
    }

    private static async Task<ICollection<ValidationError>> Validate(string json)
    {
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFiles.Dir, SchemaFile));
        return schema.Validate(json);
    }

    private static IReadOnlyList<string> TopLevelKeys(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
    }

    private static IReadOnlyList<string> SchemaPropertyKeys()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(SchemaFiles.Dir, SchemaFile)));
        return doc.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
    }

    private static string SchemaPropertyConst(string propertyName)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(SchemaFiles.Dir, SchemaFile)));
        return doc.RootElement.GetProperty("properties").GetProperty(propertyName).GetProperty("const").GetString()!;
    }
}
