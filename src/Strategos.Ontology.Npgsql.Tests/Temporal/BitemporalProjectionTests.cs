using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Temporal;

namespace Strategos.Ontology.Npgsql.Tests.Temporal;

/// <summary>
/// DR-16 (T20, #126): the XTDB-style bitemporal quartet
/// (<c>valid_from</c>/<c>valid_to</c>/<c>system_from</c>/<c>system_to</c>) layered
/// over the reified-association object table as an ADDITIVE projection, and the
/// as-of-transaction-time reconstruction query that reads a historical
/// relationship set back out of it.
/// </summary>
/// <remarks>
/// Asserts generated DDL / SQL SHAPE only — no live database (INV-2: raw Npgsql +
/// pgvector, no Marten/Wolverine). The live-execution variant is gated by the
/// repo's existing <c>STRATEGOS_PG_TEST_CONN</c> DB-gate and runs only in a
/// provisioned lane. Transaction-time is DERIVED from the append-only event
/// stream (INV-7): the projection NEVER adds a mutation surface — a retraction is
/// an appended CLOSE event that sets <c>system_to</c>, never a physical delete
/// (see <c>RetractionReplayTests</c> for the replay-determinism guard).
/// </remarks>
public class BitemporalProjectionTests
{
    private static ObjectTypeDescriptor EmploymentAssociation() =>
        new("Employment", typeof(object), "hr")
        {
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("Employee", "Person"),
                new AssociationEndpoint("Employer", "Company"),
            ],
        };

    [Test]
    public async Task AssociationDdl_CarriesBitemporalQuartetColumns()
    {
        // The reified-association object table is extended with the XTDB quartet:
        // valid-time (user-asserted tstzrange endpoints) + system-time (infra-
        // derived transaction-time endpoints). system_to is NULLABLE — an open
        // (currently-asserted) row has system_to IS NULL; a retracted row has it
        // closed. valid_from/system_from are NOT NULL.
        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", EmploymentAssociation());

        await Assert.That(ddl).Contains("valid_from timestamptz NOT NULL");
        await Assert.That(ddl).Contains("valid_to timestamptz");
        await Assert.That(ddl).Contains("system_from timestamptz NOT NULL");
        await Assert.That(ddl).Contains("system_to timestamptz");

        // INV-2: raw DDL only.
        await Assert.That(ddl).DoesNotContain("Marten");
        await Assert.That(ddl).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task AsOfTransactionTime_ReconstructsHistoricalRelationshipSet()
    {
        // The as-of-transaction-time projection reconstructs the relationship set
        // as it was KNOWN at @asOfTx: a row is visible iff its system interval
        // [system_from, system_to) contains @asOfTx — i.e. it was asserted at or
        // before @asOfTx and not yet retracted as of @asOfTx (system_to IS NULL,
        // or system_to strictly after @asOfTx). Transaction-time is the infra-
        // derived axis, so the historical view is a pure read over the projection.
        var sql = SqlGenerator.BuildAsOfTransactionTimeSql("public", "employment");

        // Reads the association object table.
        await Assert.That(sql).Contains("FROM \"public\".\"employment\"");

        // The system interval is half-open [system_from, system_to): asserted at
        // or before @asOfTx ...
        await Assert.That(sql).Contains("system_from <= @asOfTx");
        // ... and still open (NULL) or closed strictly after @asOfTx.
        await Assert.That(sql).Contains("system_to IS NULL OR system_to > @asOfTx");

        // The transaction-time anchor binds via @asOfTx, never interpolated.
        await Assert.That(sql).DoesNotContain("now()");
    }

    [Test]
    public async Task TemporalAssociationRow_CarriesQuartetAndEndpoints()
    {
        // The sealed temporal-row record is the in-memory projection of one
        // association assertion: the two endpoint ids, the user-asserted valid
        // interval, and the infra-derived system interval (system_to null while
        // the assertion is open). It is the typed shape the projection replays to.
        var assertedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var row = new TemporalAssociationRow
        {
            AssociationId = "emp-1",
            SourceId = "person-1",
            TargetId = "company-1",
            ValidFrom = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ValidTo = null,
            SystemFrom = assertedAt,
            SystemTo = null,
        };

        await Assert.That(row.SystemTo).IsNull();
        await Assert.That(row.SystemFrom).IsEqualTo(assertedAt);
        await Assert.That(row.ValidTo).IsNull();
    }
}
