using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;

namespace Strategos.Ontology.Npgsql.Tests.Temporal;

/// <summary>
/// DR-16 (T21, #126): the temporal-integrity DDL on the reified-association
/// object table — a GiST exclusion constraint (via <c>btree_gist</c>) that
/// rejects two CURRENTLY-asserted assertions of the same endpoint pair with
/// overlapping valid-time, plus the as-of-now partial+covering index
/// (<c>WHERE system_to IS NULL</c>).
/// </summary>
/// <remarks>
/// Asserts generated DDL SHAPE only — no live database (INV-2: raw Npgsql +
/// pgvector). The live-execution variant (a real overlapping INSERT hitting the
/// constraint) is gated by the repo's <c>STRATEGOS_PG_TEST_CONN</c> DB-gate and
/// runs only in a provisioned lane.
///
/// <para>
/// The exclusion is HONEST here precisely because DR-11/DR-11b made the
/// association endpoint columns homogeneous, <c>NOT NULL</c>, FK-backed
/// <c>uuid</c>s (the per-(link, target) physical tables) — so the constraint
/// equates the ACTUAL typed referent, never a <c>(target_type, target_id)</c>
/// discriminator pair (the polymorphic-FK smell). The exclusion is PARTIAL on
/// <c>system_to IS NULL</c>: a retracted (system-closed) row never blocks a fresh
/// assertion over the same valid-time, so transaction-time stays the soft-delete
/// axis (INV-7).
/// </para>
/// </remarks>
public class TemporalConstraintTests
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
    public async Task Exclude_RejectsOverlappingValidity_SameEndpoints()
    {
        // The association DDL emits a GiST EXCLUDE constraint that forbids two
        // currently-asserted rows with the SAME endpoint pair and OVERLAPPING
        // valid-time. The endpoint keys compare WITH = and the valid range
        // compares WITH && (overlap). btree_gist supplies the = operator class
        // for uuid/gist.
        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", EmploymentAssociation());

        // btree_gist is required so the uuid endpoint keys can take part in a
        // gist exclusion alongside the range overlap.
        await Assert.That(ddl).Contains("CREATE EXTENSION IF NOT EXISTS btree_gist");

        // The exclusion itself: endpoint keys WITH =, the valid-time tstzrange
        // WITH && (overlap). The endpoint columns are the role-disambiguated
        // {role}_id FKs (homogeneous typed referents — DR-11 honesty).
        await Assert.That(ddl).Contains("EXCLUDE USING gist");
        await Assert.That(ddl).Contains("\"employee_id\" WITH =");
        await Assert.That(ddl).Contains("\"employer_id\" WITH =");
        await Assert.That(ddl).Contains("tstzrange(valid_from, valid_to) WITH &&");

        // PARTIAL on system_to IS NULL: a retracted row does not block a fresh
        // assertion (transaction-time is the soft-delete axis, INV-7).
        await Assert.That(ddl).Contains("WHERE (system_to IS NULL)");

        // INV-2: raw DDL only.
        await Assert.That(ddl).DoesNotContain("Marten");
        await Assert.That(ddl).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task AsOfNow_UsesPartialIndex_SystemToIsNull()
    {
        // As-of-now is the dominant query class, so the projection carries a
        // PARTIAL index over the open (system_to IS NULL) rows, COVERING the
        // valid-time endpoints (INCLUDE) so an as-of-now valid-time filter is an
        // index-only scan. Do NOT optimize the sequenced (both-axes) class.
        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", EmploymentAssociation());

        // A partial index restricted to the open rows ...
        await Assert.That(ddl).Contains("CREATE INDEX IF NOT EXISTS");
        await Assert.That(ddl).Contains("WHERE system_to IS NULL");
        // ... covering the valid-time endpoints for an index-only as-of-now read.
        await Assert.That(ddl).Contains("INCLUDE (valid_from, valid_to)");

        // The partial index is on the association object table.
        await Assert.That(ddl).Contains("ON \"public\".\"employment\"");
    }

    [Test]
    public async Task Exclude_SelfAssociation_DistinctRoleKeysInConstraint()
    {
        // A self-association (both endpoints the same object type) must still key
        // the exclusion on the two DISTINCT role columns, so the constraint is
        // well-formed (two key columns, not one repeated).
        var reporting = new ObjectTypeDescriptor("Reporting", typeof(object), "hr")
        {
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("Manager", "Person"),
                new AssociationEndpoint("Report", "Person"),
            ],
        };

        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", reporting);

        await Assert.That(ddl).Contains("\"manager_id\" WITH =");
        await Assert.That(ddl).Contains("\"report_id\" WITH =");
        await Assert.That(ddl).Contains("tstzrange(valid_from, valid_to) WITH &&");
    }
}
