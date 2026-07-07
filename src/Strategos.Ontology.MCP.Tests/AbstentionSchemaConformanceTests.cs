using System.Text.Json;

using NJsonSchema;
using NJsonSchema.Validation;

using Strategos.Ontology.MCP;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Task 022 (DR-16 parity half, #152) — the MECHANICAL cross-check that the C#
/// abstention union (<see cref="OntologyAnswerUnion"/>, task 020) and the
/// Contracts-emitted JSON Schema (task 006) are the SAME wire shape. Serializes
/// BOTH union arms (<see cref="Answer"/>, <see cref="NoAnswerRecorded"/>) plus the
/// edge cases (empty <c>nearestRecords</c>, a min-1 <c>citations</c> answer) through
/// the union's real System.Text.Json serialization and validates the bytes against
/// the emitted <c>AbstentionResponse.json</c> / <c>Answer.json</c> /
/// <c>NoAnswerRecorded.json</c> / <c>RecordRef.json</c> with NJsonSchema.
/// </summary>
/// <remarks>
/// Parity is pinned in BOTH directions and goes red on drift from EITHER side:
/// <list type="bullet">
/// <item>VALUE parity — <see cref="JsonSchema.Validate(string)"/> rejects a renamed
/// or dropped required field, a wrong discriminator const, a missing RecordRef leg,
/// or a violated <c>@minItems(1)</c>.</item>
/// <item>FIELD-SET parity — the serialized top-level key set (minus the MCP
/// transport envelope <c>_meta</c>, which the Contracts wire twin deliberately does
/// NOT model — see AbstentionResponse.tsp) must equal the schema's declared
/// <c>properties</c> key set, so a C#-only ADDED field or a schema-only added field
/// is caught even though JSON Schema tolerates extra properties by default.</item>
/// </list>
/// The schema is loaded as a FILE from <c>src/Strategos.Contracts/schemas/json-schema/</c>
/// (so cross-file <c>$ref</c>s resolve); the ontology CORE keeps NO Strategos.Contracts
/// dependency — only this TEST project references NJsonSchema (asserted below).
/// </remarks>
public sealed class AbstentionSchemaConformanceTests
{
    /// <summary>
    /// The MCP <c>_meta</c> transport envelope (INV-3) is present on every union arm
    /// but is intentionally absent from the Contracts wire twin (AbstentionResponse.tsp
    /// models the arms, not the envelope). It is excluded from FIELD-SET parity — and
    /// only it: any OTHER unexpected key is a genuine drift and fails.
    /// </summary>
    private static readonly HashSet<string> EnvelopeKeys = new(StringComparer.Ordinal) { "_meta" };

    private static ResponseMeta Meta => new("sha256:testgraph");

    private static readonly RecordRef RecordA = new("Instrument", "inst-1");
    private static readonly RecordRef RecordB = new("Instrument", "inst-2");

    // ---- Answer arm: cited answer serializes to the Answer / union schema ----

    [Test]
    public async Task AnswerArm_Serialized_ValidatesAgainstAnswerAndUnionSchemas()
    {
        var json = SerializeUnion(Compose("The instrument matured.", matched: [RecordA, RecordB]));

        await AssertConforms("Answer.json", json);
        await AssertConforms("AbstentionResponse.json", json);
    }

    [Test]
    public async Task AnswerArm_FieldSet_MatchesAnswerSchema_ModuloMetaEnvelope()
    {
        var json = SerializeUnion(Compose("cited", matched: [RecordA]));

        await AssertFieldSetParity(json, "Answer.json");
    }

    /// <summary>
    /// Edge case: an <see cref="Answer"/> with exactly ONE citation must conform — the
    /// schema pins <c>@minItems(1)</c> and the C# guard refuses an empty-citations answer,
    /// so the single-citation case is the boundary both sides admit.
    /// </summary>
    [Test]
    public async Task AnswerArm_MinOneCitation_Conforms()
    {
        var json = SerializeUnion(Compose("boundary", matched: [RecordA]));

        using var doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.GetProperty("citations").GetArrayLength()).IsEqualTo(1);
        await AssertConforms("Answer.json", json);
    }

    // ---- NoAnswerRecorded arm: abstention serializes to the arm / union schema ----

    [Test]
    public async Task NoAnswerRecordedArm_Serialized_ValidatesAgainstArmAndUnionSchemas()
    {
        var json = SerializeUnion(Abstain(nearest: [RecordA, RecordB]));

        await AssertConforms("NoAnswerRecorded.json", json);
        await AssertConforms("AbstentionResponse.json", json);
    }

    [Test]
    public async Task NoAnswerRecordedArm_FieldSet_MatchesSchema_ModuloMetaEnvelope()
    {
        var json = SerializeUnion(Abstain(nearest: [RecordA]));

        await AssertFieldSetParity(json, "NoAnswerRecorded.json");
    }

    /// <summary>
    /// Edge case: an abstention with an EMPTY <c>nearestRecords</c> array must conform —
    /// the schema places no floor on <c>nearestRecords</c> and the composer abstains even
    /// when nothing near was found, so the empty array is a valid wire shape.
    /// </summary>
    [Test]
    public async Task NoAnswerRecordedArm_EmptyNearest_Conforms()
    {
        var json = SerializeUnion(Abstain(nearest: []));

        using var doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.GetProperty("nearestRecords").GetArrayLength()).IsEqualTo(0);
        await AssertConforms("NoAnswerRecorded.json", json);
    }

    // ---- RecordRef moniker: the shared string-pair identity ----

    [Test]
    public async Task RecordRef_Serialized_ValidatesAndFieldSetMatchesSchema()
    {
        var json = JsonSerializer.Serialize(RecordA);

        await AssertConforms("RecordRef.json", json);
        await AssertFieldSetParity(json, "RecordRef.json");
    }

    // ---- The @minItems(1) floor is the SAME invariant on both sides ----

    /// <summary>
    /// The C# guard (an <see cref="Answer"/> refuses empty citations) and the schema
    /// (<c>@minItems(1)</c>) enforce the SAME "no free-text uncited answer" invariant.
    /// The C# side cannot even construct the violating value, so this pins the schema
    /// side: a hand-built empty-citations answer FAILS <c>Answer.json</c> validation,
    /// while the single-citation answer the composer actually produces PASSES.
    /// </summary>
    [Test]
    public async Task EmptyCitationsAnswer_FailsSchema_MirroringTheCSharpGuard()
    {
        const string uncited = """{ "answerKind": "answer", "content": "42", "citations": [] }""";
        var uncitedErrors = await Validate("Answer.json", uncited);
        await Assert.That(uncitedErrors.Count).IsGreaterThan(0)
            .Because("@minItems(1) must reject an empty-citations answer, mirroring the C# guard.");

        // The C# guard refuses to construct the same shape.
        await Assert.That(() => new Answer("42", citations: [], Meta)).Throws<ArgumentException>();
    }

    // ---- Dependency posture: NJsonSchema/Contracts stay in the TEST project ----

    /// <summary>
    /// Adding NJsonSchema to this TEST project must NOT leak Strategos.Contracts (or
    /// NJsonSchema) into the ontology CORE assembly — the whole point of validating
    /// against the emitted schema FILE rather than a shared type. The core dependency
    /// set is unchanged.
    /// </summary>
    [Test]
    public async Task OntologyCoreAssembly_TakesNoContractsOrNJsonSchemaDependency()
    {
        var core = typeof(OntologyAnswerUnion).Assembly;

        foreach (var referenced in core.GetReferencedAssemblies())
        {
            var name = referenced.Name ?? string.Empty;
            await Assert.That(name.Contains("Strategos.Contracts", StringComparison.OrdinalIgnoreCase)).IsFalse()
                .Because("the ontology core must not reference Strategos.Contracts (DR-16 independence).");
            await Assert.That(name.Contains("NJsonSchema", StringComparison.OrdinalIgnoreCase)).IsFalse()
                .Because("NJsonSchema belongs to the TEST project, never the ontology core.");
        }
    }

    // ---------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------

    private static OntologyAnswerUnion Compose(string content, RecordRef[] matched) =>
        new OntologyAnswerComposer().Compose(content, matched, nearestRecords: [], Meta);

    private static OntologyAnswerUnion Abstain(RecordRef[] nearest) =>
        new OntologyAnswerComposer().Compose("ignored", matchedRecords: [], nearest, Meta);

    /// <summary>
    /// Serializes through the union BASE so System.Text.Json emits the
    /// <c>answerKind</c> discriminator (the real wire shape a consumer receives).
    /// </summary>
    private static string SerializeUnion(OntologyAnswerUnion result) => JsonSerializer.Serialize(result);

    /// <summary>Asserts the JSON validates against the named schema with zero errors.</summary>
    private static async Task AssertConforms(string schemaFileName, string json)
    {
        var errors = await Validate(schemaFileName, json);
        await Assert.That(errors.Count).IsEqualTo(0)
            .Because($"the C# serialization must conform to {schemaFileName}:\n{json}\n"
                + string.Join("\n", errors.Select(e => e.ToString())));
    }

    /// <summary>
    /// Asserts the serialized top-level key set (minus the <c>_meta</c> envelope) equals
    /// the schema's declared <c>properties</c> keys — bidirectional drift detection that
    /// catches an added/renamed C# field or an added/dropped schema field.
    /// </summary>
    private static async Task AssertFieldSetParity(string json, string schemaFileName)
    {
        var serializedKeys = TopLevelKeys(json)
            .Where(k => !EnvelopeKeys.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        var schemaKeys = SchemaPropertyKeys(schemaFileName)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        await Assert.That(serializedKeys).IsEquivalentTo(schemaKeys)
            .Because($"the serialized fields (minus _meta) must match {schemaFileName} properties exactly — "
                + $"serialized=[{string.Join(",", serializedKeys)}] schema=[{string.Join(",", schemaKeys)}].");
    }

    private static async Task<ICollection<ValidationError>> Validate(string schemaFileName, string json)
    {
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFiles.Dir, schemaFileName));
        return schema.Validate(json);
    }

    private static IReadOnlyList<string> TopLevelKeys(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
    }

    private static IReadOnlyList<string> SchemaPropertyKeys(string schemaFileName)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(SchemaFiles.Dir, schemaFileName)));
        return doc.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
    }
}
