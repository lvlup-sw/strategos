using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// DR-7 (Ontology Edge Foundation): unit tests for the Postgres edge-model
/// table SHAPES. These assert generated-DDL strings only — no live database.
/// They mirror <see cref="PgVectorSchemaTests"/>: raw Npgsql + pgvector DDL,
/// no Marten/Wolverine (INV-2).
///
/// Two lowerings (design line 20, "unified association-row store"):
///   - A pure link  -> a JUNCTION table: endpoint FK columns (source id,
///     target id) + an edge id column.
///   - An Association&lt;T&gt; (<see cref="ObjectKind.Association"/>) -> an
///     OBJECT table whose endpoint columns are FKs to the two endpoint tables.
/// RelateAsync/traversal/pgvector-on-association are later tasks; this is
/// DDL/schema generation only.
/// </summary>
public class PgVectorEdgeSchemaTests
{
    // -----------------------------------------------------------------------
    // Pure link -> junction table
    // -----------------------------------------------------------------------

    [Test]
    public async Task Schema_PureLink_EmitsJunctionTableWithEndpointFksAndEdgeId()
    {
        // A pure link "WrittenBy" from Document -> Author lowers to a junction
        // table whose two endpoint columns are FKs to the source and target
        // object tables, plus an edge-identity column.
        var ddl = SqlGenerator.BuildJunctionTableDdl(
            schema: "public",
            sourceTableName: "document",
            linkName: "WrittenBy",
            targetTableName: "author");

        // Junction table named for (source, link) — snake_cased.
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"document_written_by\"");

        // Edge-identity column (the row's own id — design's optional associationObjectId
        // back-reference hangs off a separate id; the junction row's identity is edge_id).
        await Assert.That(ddl).Contains("edge_id uuid PRIMARY KEY DEFAULT gen_random_uuid()");

        // Endpoint FK columns: source id and target id, each a FK to its endpoint table.
        await Assert.That(ddl).Contains("source_id uuid NOT NULL");
        await Assert.That(ddl).Contains("target_id uuid NOT NULL");
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"document\" (id)");
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"author\" (id)");

        // INV-2: raw Npgsql/pgvector DDL only — no event-store machinery.
        await Assert.That(ddl).DoesNotContain("Marten");
        await Assert.That(ddl).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task Schema_PureLink_JunctionTableNameIsSnakeCased()
    {
        var ddl = SqlGenerator.BuildJunctionTableDdl(
            schema: "public",
            sourceTableName: "document_chunk",
            linkName: "ReferencesSection",
            targetTableName: "section");

        await Assert.That(ddl).Contains("\"document_chunk_references_section\"");
    }

    [Test]
    public async Task Schema_PureLink_DeduplicatesTripleWithUniqueConstraint()
    {
        // The relate-store is idempotent on (src, link, tgt) (plan Task 6); the
        // junction table mirrors that eager posture with a unique constraint on
        // the endpoint pair so a duplicate relate cannot create a second row.
        var ddl = SqlGenerator.BuildJunctionTableDdl(
            schema: "public",
            sourceTableName: "document",
            linkName: "WrittenBy",
            targetTableName: "author");

        await Assert.That(ddl).Contains("UNIQUE (source_id, target_id)");
    }

    // -----------------------------------------------------------------------
    // Association<T> -> object table with FK endpoint columns
    // -----------------------------------------------------------------------

    [Test]
    public async Task Schema_Association_EmitsObjectTableWithFkEndpointColumns()
    {
        // An Employment association reifies the relation between a Person
        // (role "Employee") and a Company (role "Employer"). It is an object
        // (ObjectKind.Association) so it gets an object table; its two endpoint
        // columns are FKs to the endpoint object tables.
        var association = new ObjectTypeDescriptor("Employment", typeof(object), "hr")
        {
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("Employee", "Person"),
                new AssociationEndpoint("Employer", "Company"),
            ],
        };

        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", association);

        // Object table named for the association descriptor — snake_cased.
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"employment\"");

        // It is an object: object identity + jsonb attribute payload.
        await Assert.That(ddl).Contains("id uuid PRIMARY KEY DEFAULT gen_random_uuid()");
        await Assert.That(ddl).Contains("data jsonb NOT NULL");

        // One FK column per endpoint, named for the role (snake_cased) and
        // QUOTE-DELIMITED (review M1: identifier-identical with the DML), FK to
        // the endpoint's object table.
        await Assert.That(ddl).Contains("\"employee_id\" uuid NOT NULL");
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"person\" (id)");
        await Assert.That(ddl).Contains("\"employer_id\" uuid NOT NULL");
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"company\" (id)");

        // INV-2: raw DDL only.
        await Assert.That(ddl).DoesNotContain("Marten");
        await Assert.That(ddl).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task Schema_Association_SelfAssociation_DistinctRoleColumns()
    {
        // A self-association (both endpoints the same object type) must still
        // produce two distinct, role-named FK columns — the role disambiguates.
        var association = new ObjectTypeDescriptor("Reporting", typeof(object), "hr")
        {
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("Manager", "Person"),
                new AssociationEndpoint("Report", "Person"),
            ],
        };

        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", association);

        await Assert.That(ddl).Contains("\"manager_id\" uuid NOT NULL");
        await Assert.That(ddl).Contains("\"report_id\" uuid NOT NULL");
        // Both FK to the same endpoint table.
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"person\" (id)");
    }

    [Test]
    public async Task Schema_Association_NonAssociationDescriptor_Throws()
    {
        // Guard: the association-object lowering is only valid for
        // ObjectKind.Association descriptors carrying two endpoints.
        var entity = new ObjectTypeDescriptor("Person", typeof(object), "hr");

        await Assert.That(() => SqlGenerator.BuildAssociationObjectTableDdl("public", entity))
            .Throws<ArgumentException>();
    }
}
