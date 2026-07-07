using System.Collections;
using System.Reflection;

using Strategos.Ontology.MCP;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// DR-17 (emission half, #152): the <see cref="OntologyAnswerComposer"/> emits an
/// <see cref="OntologyAbstainedRecord"/> through its <see cref="IOntologyAuditSink"/> on
/// EVERY <see cref="NoAnswerRecorded"/> it produces — direct library use. Because the
/// union's leaf constructors are internal and the composer is the sole producer (DR-11),
/// bypassing the composer cannot construct the abstention at all, so this is the
/// mechanical chokepoint for the audit stream. Pins: emission on every abstain (never on
/// a cited answer), the payload carries COUNTS not record contents, the null-sink guard,
/// the no-op default's source/behavior compatibility, and that the sink abstraction adds
/// no new assembly/package to the core.
/// </summary>
public sealed class AbstainedEmissionTests
{
    private static ResponseMeta Meta => new("sha256:testgraph");

    private static readonly RecordRef RecordA = new("Instrument", "inst-1");
    private static readonly RecordRef RecordB = new("Instrument", "inst-2");

    /// <summary>Records every emitted abstention so multi-emission is observable.</summary>
    private sealed class RecordingAuditSink : IOntologyAuditSink
    {
        public List<OntologyAbstainedRecord> Records { get; } = new();

        public void RecordAbstention(OntologyAbstainedRecord record) => Records.Add(record);
    }

    [Test]
    public async Task Compose_Abstain_EmitsOneAbstainedRecord_WithNearestCount()
    {
        var sink = new RecordingAuditSink();
        var composer = new OntologyAnswerComposer(sink);
        var nearest = new[] { RecordA, RecordB };

        var result = composer.Compose("ignored when abstaining", matchedRecords: [], nearest, Meta);

        await Assert.That(result).IsTypeOf<NoAnswerRecorded>();
        await Assert.That(sink.Records.Count).IsEqualTo(1);
        await Assert.That(sink.Records[0].NearestRecordsCount).IsEqualTo(2);
        await Assert.That(sink.Records[0].Type).IsEqualTo("ontology.abstained");
    }

    [Test]
    public async Task Compose_CitedAnswer_EmitsNothing()
    {
        // Emission is reserved for abstention; a cited answer is never audited as an
        // abstention (no false abstention record in the audit stream).
        var sink = new RecordingAuditSink();
        var composer = new OntologyAnswerComposer(sink);

        var result = composer.Compose("The instrument matured.", new[] { RecordA }, nearestRecords: [], Meta);

        await Assert.That(result).IsTypeOf<Answer>();
        await Assert.That(sink.Records).IsEmpty();
    }

    [Test]
    public async Task Compose_Abstain_EmitsEvenWhenNearestEmpty_CountZero()
    {
        var sink = new RecordingAuditSink();
        var composer = new OntologyAnswerComposer(sink);

        var result = composer.Compose("ignored", matchedRecords: [], nearestRecords: [], Meta);

        await Assert.That(result).IsTypeOf<NoAnswerRecorded>();
        await Assert.That(sink.Records.Count).IsEqualTo(1);
        await Assert.That(sink.Records[0].NearestRecordsCount).IsEqualTo(0);
    }

    [Test]
    public async Task Compose_EveryAbstain_Emits()
    {
        // "Every NoAnswerRecorded production" — two abstentions through one composer emit
        // two audit records; emission is per-production, not once-per-composer.
        var sink = new RecordingAuditSink();
        var composer = new OntologyAnswerComposer(sink);

        composer.Compose("ignored", matchedRecords: [], new[] { RecordA }, Meta);
        composer.Compose("ignored", matchedRecords: [], new[] { RecordA, RecordB }, Meta);

        await Assert.That(sink.Records.Count).IsEqualTo(2);
        await Assert.That(sink.Records[0].NearestRecordsCount).IsEqualTo(1);
        await Assert.That(sink.Records[1].NearestRecordsCount).IsEqualTo(2);
    }

    [Test]
    public async Task AbstainedRecord_CarriesCountsNotRecordContents()
    {
        // No exfiltration through audit: the record's public shape exposes a COUNT and the
        // event type, and NOTHING that carries RecordRef identities/contents.
        var props = typeof(OntologyAbstainedRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            // Record synthesizes EqualityContract; it is not part of the audit payload.
            .Where(p => p.Name != "EqualityContract")
            .ToList();

        await Assert.That(props.Any(p => p.PropertyType == typeof(RecordRef))).IsFalse();
        await Assert.That(props.Any(p => typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
            && p.PropertyType != typeof(string))).IsFalse();

        await Assert.That(props.Select(p => p.Name)).Contains("NearestRecordsCount");
        await Assert.That(props.Select(p => p.Name)).Contains("Type");

        // The count is derived purely from cardinality — distinct record identities collapse
        // to the same count, so identity cannot be reconstructed from the audit payload.
        var sink = new RecordingAuditSink();
        new OntologyAnswerComposer(sink)
            .Compose("ignored", matchedRecords: [], new[] { RecordA, RecordB }, Meta);
        await Assert.That(sink.Records[0].NearestRecordsCount).IsEqualTo(2);
    }

    [Test]
    public async Task ParameterlessComposer_DefaultsToNoOpSink_AndStillAbstains()
    {
        // Source- and behavior-compatible: the pre-DR-17 parameterless constructor still
        // works and still produces the abstention; the default no-op sink audits nothing.
        var composer = new OntologyAnswerComposer();

        var result = composer.Compose("ignored", matchedRecords: [], new[] { RecordA }, Meta);

        await Assert.That(result).IsTypeOf<NoAnswerRecorded>();
        // The no-op sink is a genuine no-op: emitting through it never throws.
        NoOpOntologyAuditSink.Instance.RecordAbstention(new OntologyAbstainedRecord(3));
    }

    [Test]
    public async Task Composer_RejectsNullSink()
    {
        await Assert.That(() => new OntologyAnswerComposer(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AbstainedRecord_RejectsNegativeCount()
    {
        await Assert.That(() => new OntologyAbstainedRecord(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AuditSink_LivesInCoreAssembly_NoNewPackageDependency()
    {
        // The audit abstraction is defined in the core MCP assembly (no new assembly / no
        // new package pulled in for the sink), and the core still takes no Contracts or
        // ModelContextProtocol dependency.
        var coreAssembly = typeof(OntologyAnswerComposer).Assembly;

        await Assert.That(typeof(IOntologyAuditSink).Assembly).IsEqualTo(coreAssembly);
        await Assert.That(typeof(OntologyAbstainedRecord).Assembly).IsEqualTo(coreAssembly);
        await Assert.That(typeof(NoOpOntologyAuditSink).Assembly).IsEqualTo(coreAssembly);

        foreach (var refName in coreAssembly.GetReferencedAssemblies())
        {
            var name = refName.Name ?? string.Empty;
            await Assert.That(name.Contains("Strategos.Contracts", StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(name.Contains("ModelContextProtocol", StringComparison.OrdinalIgnoreCase)).IsFalse();
        }
    }
}
