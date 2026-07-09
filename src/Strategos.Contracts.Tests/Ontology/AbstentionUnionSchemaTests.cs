// =============================================================================
// <copyright file="AbstentionUnionSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using NJsonSchema;

using Strategos.Contracts.SchemaDiff;

namespace Strategos.Contracts.Tests.Ontology;

/// <summary>
/// Task 006 (DR-16 twin half + DR-17 event shape, #152) — the Contracts TypeSpec
/// TWIN of the C# abstention union already built in
/// <c>src/Strategos.Ontology.MCP/</c> (task 020). Pins that the discriminated-union
/// emission path lowers <c>AbstentionResponse</c> to a <c>[JsonPolymorphic]</c>
/// abstract record with the SAME discriminator (<c>answerKind</c>), the SAME two
/// arms (<c>Answer</c> / <c>NoAnswerRecorded</c>), and the SAME <c>RecordRef</c>
/// string-pair shape as <c>Strategos.Ontology.MCP.OntologyAnswerUnion</c> — so the
/// C# union serializes to exactly this schema (the mechanical cross-check of that
/// parity is task 022). Also pins the <c>ontology.abstained</c> audit event
/// carries nearest-record COUNTS, never contents (no data exfiltration through
/// audit), and that the whole family is Zod-consumable and non-breaking.
///
/// The generated types are resolved by reflection on their string names, so the
/// test compiles even when the source hunks are reverted (the kill-probe): the
/// reflected type resolves to null and the NotNull assertions go red.
/// </summary>
[Property("Category", "Ontology")]
[NotInParallel("tsp-compile")]
public sealed class AbstentionUnionSchemaTests
{
    private const string GeneratedNs = "Strategos.Contracts.Generated";

    // These constants ARE the C# union's shape (read from the task-020 files):
    // OntologyAnswerUnion pins `answerKind`; the arms are "answer" / "no_answer_recorded"
    // (both wire values snake_case — DR-1 shared-Zod-consumer casing).
    private const string DiscriminatorWireName = "answerKind";
    private const string AnswerDiscriminator = "answer";
    private const string NoAnswerDiscriminator = "no_answer_recorded";

    // ---------------------------------------------------------------------
    // DR-16 — the discriminated-union emission shape (schema side).
    // ---------------------------------------------------------------------

    /// <summary>
    /// The union emits via the discriminated-union path: <c>AbstentionResponse.json</c>
    /// is an <c>anyOf</c> over exactly the two arms (<c>Answer</c>, <c>NoAnswerRecorded</c>),
    /// each of which pins the <c>answerKind</c> discriminator with a <c>const</c>.
    /// </summary>
    [Test]
    public async Task AbstentionResponse_EmitsAnyOfUnion_OverAnswerAndNoAnswerRecorded()
    {
        await Assert.That(EventSchemas.Exists("AbstentionResponse")).IsTrue()
            .Because("`tsp compile` must emit AbstentionResponse.json (run scripts/contracts-codegen.sh).");

        var union = await EventSchemas.LoadAsync("AbstentionResponse");
        await Assert.That(union.TryGetProperty("anyOf", out var anyOf)).IsTrue()
            .Because("AbstentionResponse must be a discriminated union (anyOf of arms).");

        var armNames = anyOf.EnumerateArray()
            .Where(a => a.TryGetProperty("$ref", out _))
            .Select(a => Path.GetFileNameWithoutExtension(a.GetProperty("$ref").GetString())!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        await Assert.That(armNames).IsEquivalentTo(new[] { "Answer", "NoAnswerRecorded" })
            .Because("the union is closed over exactly the Answer and NoAnswerRecorded arms.");

        // Each arm pins the answerKind discriminator const (the shape STJ dispatches on).
        var answer = await EventSchemas.LoadAsync("Answer");
        await Assert.That(answer.GetProperty("properties").GetProperty(DiscriminatorWireName)
                .GetProperty("const").GetString())
            .IsEqualTo(AnswerDiscriminator)
            .Because("the Answer arm pins answerKind: \"answer\" (mirrors the C# union).");

        var noAnswer = await EventSchemas.LoadAsync("NoAnswerRecorded");
        await Assert.That(noAnswer.GetProperty("properties").GetProperty(DiscriminatorWireName)
                .GetProperty("const").GetString())
            .IsEqualTo(NoAnswerDiscriminator)
            .Because("the NoAnswerRecorded arm pins answerKind: \"no_answer_recorded\" (mirrors the C# union).");
    }

    /// <summary>
    /// The generated C# surface is a <c>[JsonPolymorphic]</c> ABSTRACT RECORD whose
    /// discriminator property is <c>answerKind</c> and whose two <c>[JsonDerivedType]</c>
    /// mappings bind <c>answer</c> → <c>Answer</c> and <c>no_answer_recorded</c> →
    /// <c>NoAnswerRecorded</c> — the exact twin of the MCP <c>OntologyAnswerUnion</c>.
    /// The arms are sealed records deriving from the base, and (like the MCP arms) do
    /// NOT re-declare the discriminator as an ordinary property (STJ owns it).
    /// </summary>
    [Test]
    public async Task GeneratedUnion_IsJsonPolymorphicAbstractRecord_MirrorsMcpUnion()
    {
        var asm = typeof(ContractsMarker).Assembly;
        var unionType = asm.GetType($"{GeneratedNs}.AbstentionResponse");
        var answerType = asm.GetType($"{GeneratedNs}.Answer");
        var noAnswerType = asm.GetType($"{GeneratedNs}.NoAnswerRecorded");

        await Assert.That(unionType).IsNotNull()
            .Because("the codegen must emit the AbstentionResponse [JsonPolymorphic] base.");
        await Assert.That(answerType).IsNotNull()
            .Because("the codegen must emit the Answer arm.");
        await Assert.That(noAnswerType).IsNotNull()
            .Because("the codegen must emit the NoAnswerRecorded arm.");

        // The base is an ABSTRACT record (discriminated-union base), not a sealed leaf.
        await Assert.That(unionType!.IsAbstract).IsTrue()
            .Because("the union base must be an abstract record (the [JsonPolymorphic] discriminated-union shape).");
        await Assert.That(IsRecord(unionType)).IsTrue()
            .Because("AbstentionResponse must be a record, not a plain class.");

        // [JsonPolymorphic(TypeDiscriminatorPropertyName = "answerKind")].
        var polymorphic = unionType.GetCustomAttribute<JsonPolymorphicAttribute>();
        await Assert.That(polymorphic).IsNotNull()
            .Because("the union base must carry [JsonPolymorphic] (the discriminated-union emission shape).");
        await Assert.That(polymorphic!.TypeDiscriminatorPropertyName).IsEqualTo(DiscriminatorWireName)
            .Because("the discriminator property must be `answerKind`, matching the C# OntologyAnswerUnion.");

        // Exactly two [JsonDerivedType] arms, mapping discriminator → arm type.
        var derived = unionType.GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToDictionary(a => a.TypeDiscriminator?.ToString()!, a => a.DerivedType, StringComparer.Ordinal);
        await Assert.That(derived.Count).IsEqualTo(2)
            .Because("the union has exactly the Answer and NoAnswerRecorded derived arms.");
        await Assert.That(derived.ContainsKey(AnswerDiscriminator)
                && derived[AnswerDiscriminator] == answerType).IsTrue()
            .Because("[JsonDerivedType(typeof(Answer), \"answer\")] must bind the answer arm.");
        await Assert.That(derived.ContainsKey(NoAnswerDiscriminator)
                && derived[NoAnswerDiscriminator] == noAnswerType).IsTrue()
            .Because("[JsonDerivedType(typeof(NoAnswerRecorded), \"no_answer_recorded\")] must bind the abstention arm.");

        // The arms are sealed records deriving from the base.
        await Assert.That(answerType!.IsSealed).IsTrue()
            .Because("the Answer arm must be a sealed record (INV-6).");
        await Assert.That(answerType.BaseType).IsEqualTo(unionType)
            .Because("Answer must derive from the AbstentionResponse base.");
        await Assert.That(noAnswerType!.IsSealed).IsTrue()
            .Because("the NoAnswerRecorded arm must be a sealed record (INV-6).");
        await Assert.That(noAnswerType.BaseType).IsEqualTo(unionType)
            .Because("NoAnswerRecorded must derive from the AbstentionResponse base.");

        // The arms do NOT re-declare the discriminator (STJ owns `answerKind`) —
        // exactly as the MCP arm records omit it.
        await Assert.That(answerType.GetProperty("AnswerKind")).IsNull()
            .Because("the Answer arm must not re-declare the answerKind discriminator (STJ owns it).");
        await Assert.That(noAnswerType.GetProperty("AnswerKind")).IsNull()
            .Because("the NoAnswerRecorded arm must not re-declare the answerKind discriminator (STJ owns it).");
    }

    /// <summary>
    /// DR-1 (casing) — the abstention discriminator WIRE VALUE is snake_case, the
    /// same frozen-value guard the <c>GateClass</c>/<c>ForkTrigger</c> vocabularies
    /// carry. This is the regression sentinel for FIX-2 H2: the arm once pinned the
    /// lone camelCase multi-word wire value <c>"noAnswerRecorded"</c> in the whole
    /// contract set; a future re-camelCasing (schema const OR generated
    /// <c>[JsonDerivedType]</c>) must go red here. Asserted on BOTH the emitted schema
    /// const (the wire truth) and the generated discriminator binding.
    /// </summary>
    [Test]
    public async Task AbstentionDiscriminator_WireValueIsSnakeCase_NotCamelCase()
    {
        // Strict snake_case: lowercase words joined by single underscores — no
        // uppercase letter can slip back in (the "noAnswerRecorded" regression).
        await Assert.That(NoAnswerDiscriminator).IsEqualTo("no_answer_recorded")
            .Because("the abstention arm's wire value is snake_case (DR-1), never camelCase.");
        await Assert.That(Regex.IsMatch(NoAnswerDiscriminator, "^[a-z0-9]+(_[a-z0-9]+)*$")).IsTrue()
            .Because("the discriminator must be snake_case; a camelCase value (e.g. \"noAnswerRecorded\") is a DR-1 violation.");
        await Assert.That(NoAnswerDiscriminator.Any(char.IsUpper)).IsFalse()
            .Because("no uppercase letter may appear in the snake_case wire value.");

        // The emitted schema const IS the snake_case wire value.
        var noAnswer = await EventSchemas.LoadAsync("NoAnswerRecorded");
        var schemaConst = noAnswer.GetProperty("properties").GetProperty(DiscriminatorWireName)
            .GetProperty("const").GetString();
        await Assert.That(schemaConst).IsEqualTo(NoAnswerDiscriminator)
            .Because("the emitted NoAnswerRecorded.json const must be the snake_case wire value.");

        // The generated [JsonDerivedType] binds the snake_case discriminator to the arm.
        var asm = typeof(ContractsMarker).Assembly;
        var unionType = asm.GetType($"{GeneratedNs}.AbstentionResponse");
        var noAnswerType = asm.GetType($"{GeneratedNs}.NoAnswerRecorded");
        await Assert.That(unionType).IsNotNull()
            .Because("the codegen must emit the AbstentionResponse [JsonPolymorphic] base.");

        var derived = unionType!.GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToDictionary(a => a.TypeDiscriminator?.ToString()!, a => a.DerivedType, StringComparer.Ordinal);
        await Assert.That(derived.ContainsKey(NoAnswerDiscriminator)
                && derived[NoAnswerDiscriminator] == noAnswerType).IsTrue()
            .Because("the generated [JsonDerivedType] must bind the snake_case \"no_answer_recorded\" to the abstention arm.");
    }

    /// <summary>
    /// The <c>Answer</c> arm carries a NON-EMPTY <c>citations</c> array of
    /// <c>RecordRef</c> monikers (<c>@minItems(1)</c>), mirroring the C# guard that a
    /// free-text uncited answer is unrepresentable. <c>RecordRef</c> is the polyglot
    /// string pair (<c>descriptor</c> + <c>recordId</c>). Exercises the real
    /// constraint: an Answer citing nothing FAILS schema validation; one citing a
    /// record VALIDATES (cross-file <c>$ref</c> resolution).
    /// </summary>
    [Test]
    public async Task AnswerArm_CitationsNonEmpty_RecordRefIsDescriptorRecordIdPair()
    {
        await Assert.That(EventSchemas.Exists("Answer")).IsTrue()
            .Because("`tsp compile` must emit Answer.json.");
        await Assert.That(EventSchemas.Exists("RecordRef")).IsTrue()
            .Because("`tsp compile` must emit RecordRef.json.");

        var answer = await EventSchemas.LoadAsync("Answer");
        var props = answer.GetProperty("properties");

        await Assert.That(props.GetProperty("content").GetProperty("type").GetString()).IsEqualTo("string")
            .Because("the answer text is a string.");

        var citations = props.GetProperty("citations");
        await Assert.That(citations.GetProperty("type").GetString()).IsEqualTo("array")
            .Because("citations is a collection of supporting records.");
        await Assert.That(Path.GetFileNameWithoutExtension(
                citations.GetProperty("items").GetProperty("$ref").GetString()))
            .IsEqualTo("RecordRef")
            .Because("each citation is a polyglot RecordRef moniker (INV-8), never a CLR type.");
        await Assert.That(citations.GetProperty("minItems").GetInt32()).IsEqualTo(1)
            .Because("citations is NON-EMPTY (@minItems 1) — a free-text uncited answer is unrepresentable.");

        // RecordRef is the string identity pair.
        var recordRef = await EventSchemas.LoadAsync("RecordRef");
        var refProps = recordRef.GetProperty("properties");
        await Assert.That(refProps.GetProperty("descriptor").GetProperty("type").GetString()).IsEqualTo("string")
            .Because("the descriptor is a name string (INV-8), never a CLR type.");
        await Assert.That(refProps.GetProperty("recordId").GetProperty("type").GetString()).IsEqualTo("string")
            .Because("the recordId is a projected id string.");

        // Behavioral: the @minItems(1) floor actually rejects an empty-citations answer.
        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(EventSchemas.SchemaDir, "Answer.json"));

        const string uncited =
            """
            { "answerKind": "answer", "content": "42", "citations": [] }
            """;
        await Assert.That(schema.Validate(uncited).Count).IsGreaterThan(0)
            .Because("an Answer citing nothing must fail schema validation (@minItems 1).");

        const string cited =
            """
            {
              "answerKind": "answer",
              "content": "42",
              "citations": [ { "descriptor": "Answerable", "recordId": "r-1" } ]
            }
            """;
        var citedErrors = schema.Validate(cited);
        await Assert.That(citedErrors.Count).IsEqualTo(0)
            .Because("a cited Answer with a resolved RecordRef must validate:\n"
                + string.Join("\n", citedErrors.Select(e => e.ToString())));
    }

    // ---------------------------------------------------------------------
    // DR-17 — the ontology.abstained event: COUNTS, not contents.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The <c>ontology.abstained</c> event is envelope-compatible (pins
    /// <c>type: "ontology.abstained"</c>) and its payload carries the nearest-record
    /// COUNT only — never the record identities or contents. Asserted on BOTH the
    /// emitted schema (no array / no <c>$ref</c> / no content-bearing key) and the
    /// generated record (a scalar <c>int</c> count, no RecordRef-bearing property),
    /// so a record moniker cannot be exfiltrated through the audit stream.
    /// </summary>
    [Test]
    public async Task AbstainedEvent_CarriesCountsNotContents_NoExfiltration()
    {
        await Assert.That(EventSchemas.Exists("OntologyAbstained")).IsTrue()
            .Because("`tsp compile` must emit OntologyAbstained.json.");

        var evt = await EventSchemas.LoadAsync("OntologyAbstained");
        var props = evt.GetProperty("properties");

        // Envelope-compatible discriminator.
        await Assert.That(props.GetProperty("type").GetProperty("const").GetString())
            .IsEqualTo("ontology.abstained")
            .Because("the event pins type: \"ontology.abstained\" (SdlcEventEnvelope-compatible).");

        // The payload is a COUNT — an integer with a non-negative floor.
        var count = props.GetProperty("nearestRecordsCount");
        await Assert.That(count.GetProperty("type").GetString()).IsEqualTo("integer")
            .Because("the payload carries a nearestRecords COUNT (integer), not the records.");
        await Assert.That(count.GetProperty("minimum").GetInt32()).IsEqualTo(0)
            .Because("a count is non-negative (@minValue 0).");

        // No CONTENTS leak: no property is an array or a $ref, and no key names a
        // record-content field. The audit stream sees a count, never a moniker.
        foreach (var prop in props.EnumerateObject())
        {
            await Assert.That(prop.Value.TryGetProperty("$ref", out _)).IsFalse()
                .Because($"the abstained payload must not $ref a content model (`{prop.Name}`).");
            var type = prop.Value.TryGetProperty("type", out var t) ? t.GetString() : null;
            await Assert.That(type is "array").IsFalse()
                .Because($"the abstained payload must carry no collection of records (`{prop.Name}`).");
        }

        string[] forbiddenContentKeys =
            ["nearestRecords", "citations", "records", "content", "descriptor", "recordId"];
        var payloadKeys = props.EnumerateObject().Select(p => p.Name).ToList();
        foreach (var forbidden in forbiddenContentKeys)
        {
            await Assert.That(payloadKeys).DoesNotContain(forbidden)
                .Because($"the audit event must NOT carry record contents (`{forbidden}`) — counts only.");
        }

        // The generated record mirrors it: a scalar int count + the type discriminator,
        // and NOTHING that carries a RecordRef (no exfiltration on the CLR side either).
        var asm = typeof(ContractsMarker).Assembly;
        var evtType = asm.GetType($"{GeneratedNs}.OntologyAbstained");
        await Assert.That(evtType).IsNotNull()
            .Because("the codegen must emit the OntologyAbstained record.");
        await Assert.That(evtType!.IsSealed).IsTrue()
            .Because("OntologyAbstained must be a sealed record (INV-6).");

        var countProp = evtType.GetProperty("NearestRecordsCount");
        await Assert.That(countProp).IsNotNull()
            .Because("the event must expose the NearestRecordsCount payload.");
        await Assert.That(countProp!.PropertyType).IsEqualTo(typeof(int))
            .Because("the payload is a scalar int COUNT, never a record collection.");

        var recordRefType = asm.GetType($"{GeneratedNs}.RecordRef");
        foreach (var prop in evtType.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            await Assert.That(MentionsType(prop.PropertyType, recordRefType)).IsFalse()
                .Because($"OntologyAbstained.{prop.Name} must not carry a RecordRef — no content exfiltration.");
        }
    }

    // ---------------------------------------------------------------------
    // Zod-consumability + non-breaking evolution.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The abstention family derives to Zod with no manual post-processing (the
    /// Exarchos derivation path): the union, both arms, the RecordRef moniker, and
    /// the abstained event each convert to a Zod module.
    /// </summary>
    [Test]
    public async Task AbstentionFamily_DerivesToZod_WithoutManualPostProcessing()
    {
        var compile = await TspToolchain.CompileAsync();
        await Assert.That(compile.ExitCode).IsEqualTo(0).Because(compile.Output);

        var scriptPath = Path.Combine(RepoLayout.ContractsProjectDir, "scripts", "zod-smoke.mjs");
        await Assert.That(File.Exists(scriptPath)).IsTrue()
            .Because($"expected the Zod smoke script at {scriptPath}");

        var outDir = Directory.CreateTempSubdirectory("abstention-zod-").FullName;
        try
        {
            var run = await Cli.RunAsync(
                "node", $"\"{scriptPath}\" \"{outDir}\"", RepoLayout.ContractsProjectDir);
            await Assert.That(run.ExitCode).IsEqualTo(0)
                .Because($"zod-smoke must convert the abstention family without manual post-processing:\n{run.Output}");

            string[] modules =
                ["AbstentionResponse", "Answer", "NoAnswerRecorded", "RecordRef", "OntologyAbstained"];
            foreach (var module in modules)
            {
                var modulePath = Path.Combine(outDir, module + ".ts");
                await Assert.That(File.Exists(modulePath)).IsTrue()
                    .Because($"expected generated Zod for {module} at {modulePath}\n{run.Output}");
                await Assert.That(await File.ReadAllTextAsync(modulePath)).Contains("z.")
                    .Because($"the generated {module} module must contain Zod schema code.");
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    /// <summary>
    /// The DR-17 versioning posture: the abstained event evolves additively. Diffs the
    /// ACTUAL emitted <c>OntologyAbstained.json</c> against a copy of itself carrying an
    /// added OPTIONAL field, so the only delta is the additive property — the
    /// <c>JsonSchemaDiff</c> gate classifies it NON-BREAKING and CI stays green. (The
    /// union + event are themselves net-new documents — additive to the contract set —
    /// so no existing consumer is broken by their introduction.)
    /// </summary>
    [Test]
    public async Task AbstainedEvent_EvolvesAdditively_NonBreaking()
    {
        await Assert.That(EventSchemas.Exists("OntologyAbstained")).IsTrue()
            .Because("`tsp compile` must emit OntologyAbstained.json.");

        var before = await File.ReadAllTextAsync(
            Path.Combine(EventSchemas.SchemaDir, "OntologyAbstained.json"));

        // A hypothetical additive minor: add an OPTIONAL field (not in `required`).
        var afterNode = JsonNode.Parse(before)!;
        afterNode["properties"]!.AsObject()["degraded"] = new JsonObject
        {
            ["type"] = "boolean",
            ["description"] = "Whether the answering surface was degraded when it abstained.",
        };
        var after = afterNode.ToJsonString();

        var result = JsonSchemaDiff.Compare(before, after);

        await Assert.That(result.HasBreakingChanges).IsFalse()
            .Because("adding an OPTIONAL field to the abstained event is additive — never breaking (DR-17).");
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking)
            .Because("the additive event evolution is a non-breaking minor, not a major bump.");
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("degraded", StringComparison.Ordinal))
            .Because("the differ must report the added optional field.");
    }

    private static bool IsRecord(Type t) =>
        t.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null
        || t.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) is not null;

    /// <summary>True if <paramref name="candidate"/> is (or wraps, via array/generic) <paramref name="target"/>.</summary>
    private static bool MentionsType(Type candidate, Type? target)
    {
        if (target is null)
        {
            return false;
        }

        if (candidate == target)
        {
            return true;
        }

        if (candidate.IsArray && candidate.GetElementType() == target)
        {
            return true;
        }

        return candidate.IsGenericType && candidate.GetGenericArguments().Any(a => a == target);
    }
}
