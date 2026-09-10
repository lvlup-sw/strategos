using System.Reflection;
using System.Text.Json;

using Strategos.Ontology.MCP;
using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// DR-11 (#152): the closed abstention response union (<see cref="OntologyAnswerUnion"/>)
/// with internal leaf constructors, <see cref="RecordRef"/> string identity, and the
/// <see cref="OntologyAnswerComposer"/> as the SOLE producer with retrieval-decided
/// nulls. Pins: sealed-record posture + discriminator emission, INV-3 <c>_meta</c>,
/// INV-8 string identity, the composer decision proof (empty ⇒ abstain with nearest;
/// non-empty ⇒ cited answer; never hides results), the "no free-text uncited answer"
/// guard clause, the advertised-schema <c>minItems: 1</c>, and the unchanged
/// dependency set (no Contracts reference).
/// </summary>
public sealed class AbstentionUnionTests
{
    private static ResponseMeta Meta => new("sha256:testgraph");

    private static readonly RecordRef RecordA = new("Instrument", "inst-1");
    private static readonly RecordRef RecordB = new("Instrument", "inst-2");

    // ---- RecordRef: polyglot STRING identity pair, never a CLR type (INV-8) ----

    [Test]
    public async Task RecordRef_IsAStringIdentityPair_NeverAClrType()
    {
        // INV-8: identity is a descriptor NAME + projected id, both strings — never a
        // System.Type surfaces anywhere in the record's public shape.
        var props = typeof(RecordRef).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(props.Select(p => p.Name)).Contains("Descriptor");
        await Assert.That(props.Select(p => p.Name)).Contains("RecordId");
        await Assert.That(props.All(p => p.PropertyType == typeof(string))).IsTrue();
        await Assert.That(props.Any(p => p.PropertyType == typeof(Type))).IsFalse();
    }

    [Test]
    public async Task RecordRef_SerializesWithStringMonikerFields()
    {
        var json = JsonSerializer.Serialize(RecordA);

        await Assert.That(json).Contains("\"descriptor\":\"Instrument\"");
        await Assert.That(json).Contains("\"recordId\":\"inst-1\"");
    }

    // ---- Sealed-record posture + discriminated-union base (mirrors QueryResultUnion) ----

    [Test]
    public async Task OntologyAnswerUnion_BranchPosture_Holds()
    {
        // The union base is the inheritance seam: abstract, never sealed.
        await Assert.That(typeof(OntologyAnswerUnion).IsAbstract).IsTrue();
        await Assert.That(typeof(OntologyAnswerUnion).IsSealed).IsFalse();

        // Its leaves are sealed concrete records (INV-6 sealed-by-default).
        await Assert.That(typeof(Answer).IsSealed).IsTrue();
        await Assert.That(typeof(NoAnswerRecorded).IsSealed).IsTrue();
        await Assert.That(typeof(Answer).BaseType).IsEqualTo(typeof(OntologyAnswerUnion));
        await Assert.That(typeof(NoAnswerRecorded).BaseType).IsEqualTo(typeof(OntologyAnswerUnion));
        await Assert.That(typeof(RecordRef).IsSealed).IsTrue();
    }

    // ---- Union leaf constructors are INTERNAL (composer is the sole producer) ----

    [Test]
    public async Task UnionLeaves_HaveNoPublicConstructor()
    {
        // A free-text uncited answer is unrepresentable in part because external code
        // cannot construct either branch: no public constructor exists on the leaves.
        var answerPublicCtors = typeof(Answer).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var abstainPublicCtors = typeof(NoAnswerRecorded).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(answerPublicCtors).IsEmpty();
        await Assert.That(abstainPublicCtors).IsEmpty();

        // The only producing constructor is internal.
        var answerInternalCtor = typeof(Answer)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .SingleOrDefault(c => c.GetParameters().Length == 3);
        await Assert.That(answerInternalCtor).IsNotNull();
        await Assert.That(answerInternalCtor!.IsAssembly).IsTrue();
    }

    // ---- Composer decision proof (retrieval-decided null) ----

    [Test]
    public async Task Compose_WithMatchedRecords_YieldsCitedAnswer()
    {
        var composer = new OntologyAnswerComposer();
        var matched = new[] { RecordA, RecordB };

        var result = composer.Compose("The instrument matured on 2026-01-01.", matched, nearestRecords: [], Meta);

        await Assert.That(result).IsTypeOf<Answer>();
        var answer = (Answer)result;
        await Assert.That(answer.Content).IsEqualTo("The instrument matured on 2026-01-01.");
        await Assert.That(answer.Citations).IsEquivalentTo(matched);
        await Assert.That(answer.Meta.OntologyVersion).IsEqualTo("sha256:testgraph");
    }

    [Test]
    public async Task Compose_WithEmptyRetrieval_YieldsNoAnswerRecordedWithNearest()
    {
        var composer = new OntologyAnswerComposer();
        var nearest = new[] { RecordA, RecordB };

        var result = composer.Compose("ignored when abstaining", matchedRecords: [], nearest, Meta);

        await Assert.That(result).IsTypeOf<NoAnswerRecorded>();
        var abstention = (NoAnswerRecorded)result;
        await Assert.That(abstention.NearestRecords).IsEquivalentTo(nearest);
        await Assert.That(abstention.Meta.OntologyVersion).IsEqualTo("sha256:testgraph");
    }

    [Test]
    public async Task Compose_NeverAbstainsWhileHidingResults()
    {
        // "No code path yields NoAnswerRecorded while hiding results": whenever matched
        // records exist, the composer MUST cite them — even if nearest records are also
        // supplied. Abstention is reserved for the empty-match case only.
        var composer = new OntologyAnswerComposer();
        var matched = new[] { RecordA };
        var nearest = new[] { RecordB };

        var result = composer.Compose("cited", matched, nearest, Meta);

        await Assert.That(result).IsTypeOf<Answer>();
        await Assert.That(((Answer)result).Citations).IsEquivalentTo(matched);
    }

    [Test]
    public async Task Compose_EmptyRetrievalWithEmptyNearest_StillAbstains()
    {
        var composer = new OntologyAnswerComposer();

        var result = composer.Compose("ignored", matchedRecords: [], nearestRecords: [], Meta);

        await Assert.That(result).IsTypeOf<NoAnswerRecorded>();
        await Assert.That(((NoAnswerRecorded)result).NearestRecords).IsEmpty();
    }

    // ---- Guard clause: composer refuses an Answer with empty citations ----

    [Test]
    public async Task Answer_RefusesEmptyCitations()
    {
        // The composer is the sole caller of this internal constructor; the guard
        // clause is what makes a free-text uncited answer unrepresentable at runtime.
        await Assert.That(() => new Answer("uncited free text", citations: [], Meta))
            .Throws<ArgumentException>();
    }

    // ---- Discriminator emission + INV-3 _meta envelope on both branches ----

    [Test]
    public async Task Answer_SerializesWithDiscriminatorAndMeta()
    {
        OntologyAnswerUnion result = new OntologyAnswerComposer()
            .Compose("answer text", new[] { RecordA }, nearestRecords: [], Meta);

        var json = JsonSerializer.Serialize(result);

        await Assert.That(json).Contains("\"answerKind\":\"answer\"");
        await Assert.That(json).Contains("\"_meta\"");
        await Assert.That(json).Contains("\"citations\"");
        await Assert.That(json).Contains("\"ontologyVersion\":\"sha256:testgraph\"");
    }

    [Test]
    public async Task NoAnswerRecorded_SerializesWithDiscriminatorAndMeta()
    {
        OntologyAnswerUnion result = new OntologyAnswerComposer()
            .Compose("ignored", matchedRecords: [], new[] { RecordA }, Meta);

        var json = JsonSerializer.Serialize(result);

        await Assert.That(json).Contains("\"answerKind\":\"no_answer_recorded\"");
        await Assert.That(json).Contains("\"_meta\"");
        await Assert.That(json).Contains("\"nearestRecords\"");
    }

    // ---- Advertised output schema carries minItems: 1 on citations only ----

    [Test]
    public async Task AdvertisedOutputSchema_IsAOneOfUnionWithMinItemsOnCitations()
    {
        var schema = OntologyAnswerComposer.AdvertisedOutputSchema();
        var raw = schema.GetRawText();

        // Discriminated-union shape mirroring QueryResultUnion: a top-level oneOf that
        // dispatches on answerKind.
        await Assert.That(raw).Contains("oneOf");
        await Assert.That(raw).Contains("answerKind");

        using var doc = JsonDocument.Parse(raw);
        var branches = doc.RootElement.GetProperty("oneOf");

        var citationsMinItems = FindArrayPropertyMinItems(branches, "citations");
        var nearestMinItems = FindArrayPropertyMinItems(branches, "nearestRecords");

        // Answer.citations is pinned non-empty; NoAnswerRecorded.nearestRecords is not.
        await Assert.That(citationsMinItems).IsEqualTo(1);
        await Assert.That(nearestMinItems).IsNull();
    }

    /// <summary>
    /// Walks the union's oneOf branches, finds the branch declaring an array property
    /// named <paramref name="propertyName"/>, and returns its <c>minItems</c> (or null
    /// when the branch/keyword is absent).
    /// </summary>
    private static int? FindArrayPropertyMinItems(JsonElement branches, string propertyName)
    {
        foreach (var branch in branches.EnumerateArray())
        {
            if (!branch.TryGetProperty("properties", out var properties)
                || !properties.TryGetProperty(propertyName, out var prop))
            {
                continue;
            }

            return prop.TryGetProperty("minItems", out var minItems)
                ? minItems.GetInt32()
                : null;
        }

        return null;
    }

    // ---- Dependency set unchanged: no Contracts reference leaked in (DR-11 independence) ----

    [Test]
    public async Task CoreMcpAssembly_DoesNotReferenceContracts()
    {
        // DR-11 lands independently of the Contracts twin (DR-16): the ontology layer
        // must NOT take a dependency on Strategos.Contracts.
        var referenced = typeof(OntologyAnswerComposer).Assembly.GetReferencedAssemblies();

        foreach (var refName in referenced)
        {
            var leaks = refName.Name?.Contains("Strategos.Contracts", StringComparison.OrdinalIgnoreCase) ?? false;
            await Assert.That(leaks).IsFalse();
        }
    }
}
