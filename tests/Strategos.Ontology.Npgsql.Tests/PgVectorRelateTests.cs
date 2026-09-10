using Strategos.Ontology.Builder;
using Strategos.Ontology.Npgsql.Internal;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// DR-7/DR-8 (Ontology Edge Foundation, t9): unit tests for the Postgres
/// relate/unrelate SQL SHAPES on <see cref="PgVectorObjectSetProvider"/>.
///
/// These assert generated parameterized SQL strings only — NO live database.
/// They mirror <see cref="PgVectorEdgeSchemaTests"/> and
/// <see cref="PgVectorObjectSetProviderTests"/>: raw Npgsql + pgvector
/// (INV-2 — no Marten/Wolverine), and the dispatch/SQL-building seams are
/// exposed as internal statics so the full code path is pinned without a
/// live <see cref="global::Npgsql.NpgsqlDataSource"/>.
///
/// The lowering mirrors the in-memory relate-store
/// (<c>InMemoryObjectSetProvider</c>): the contract addresses endpoints by
/// their projected BUSINESS id (a string), so the Postgres relate resolves
/// the endpoint row's surrogate <c>id uuid</c> via a subquery against the
/// endpoint object table's <c>data jsonb</c> key field
/// (<c>data->>'KeyProperty'</c>). Idempotency rides the T8 junction
/// <c>UNIQUE(source_id, target_id)</c> via <c>ON CONFLICT DO NOTHING</c>;
/// eager endpoint validation (DR-8) is a per-endpoint <c>SELECT EXISTS</c>
/// emitted BEFORE the insert so a missing endpoint surfaces a typed error and
/// no row is written.
/// </summary>
public class PgVectorRelateTests
{
    // -----------------------------------------------------------------------
    // Junction-table naming agrees with the T8 BuildJunctionTableDdl lowering
    // -----------------------------------------------------------------------

    [Test]
    public async Task JunctionTableName_MatchesT8DdlLowering()
    {
        // The relate/unrelate SQL must target the SAME physical junction table
        // the T8 BuildJunctionTableDdl creates: {source}_{snake(link)}.
        var name = SqlGenerator.JunctionTableName("document", "WrittenBy");
        await Assert.That(name).IsEqualTo("document_written_by");

        // And the DDL the table is created by contains that exact identifier,
        // so writes can never drift from the schema.
        var ddl = SqlGenerator.BuildJunctionTableDdl(
            schema: "public",
            sourceTableName: "document",
            linkName: "WrittenBy",
            targetTableName: "author");
        await Assert.That(ddl).Contains($"\"{name}\"");
    }

    // -----------------------------------------------------------------------
    // Relate -> idempotent INSERT into the junction table
    // -----------------------------------------------------------------------

    [Test]
    public async Task BuildRelateInsertSql_EmitsParameterizedIdempotentInsert()
    {
        var sql = SqlGenerator.BuildRelateInsertSql(
            schema: "public",
            junctionTableName: "document_written_by",
            sourceTableName: "document",
            sourceKeyProperty: "Id",
            targetTableName: "author",
            targetKeyProperty: "Id");

        // Targets the T8 junction table's endpoint FK columns.
        await Assert.That(sql).Contains("INSERT INTO \"public\".\"document_written_by\" (source_id, target_id)");

        // Resolves each endpoint's surrogate id uuid from its object table by the
        // BUSINESS id stored in data jsonb — parameterized, never interpolated.
        // Both endpoint tables are joined (aliased) in the SELECT source.
        await Assert.That(sql).Contains("\"public\".\"document\" s");
        await Assert.That(sql).Contains("\"public\".\"author\" t");
        await Assert.That(sql).Contains("data->>'Id' = @srcId");
        await Assert.That(sql).Contains("data->>'Id' = @tgtId");

        // Idempotent on the T8 UNIQUE(source_id, target_id): a duplicate relate
        // is a no-op, mirroring the in-memory store's idempotency.
        await Assert.That(sql).Contains("ON CONFLICT (source_id, target_id) DO NOTHING");

        // Parameterized — the business ids never appear as literals.
        await Assert.That(sql).Contains("@srcId");
        await Assert.That(sql).Contains("@tgtId");

        // INV-2: raw Npgsql/pgvector — no event-store machinery.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task BuildRelateInsertSql_HonoursDistinctEndpointKeyProperties()
    {
        // Endpoints may key on differently-named properties; each subquery must
        // use its OWN endpoint's key property name (INV-8: identity by
        // descriptor, never a shared assumption).
        var sql = SqlGenerator.BuildRelateInsertSql(
            schema: "public",
            junctionTableName: "person_employed_by",
            sourceTableName: "person",
            sourceKeyProperty: "PersonId",
            targetTableName: "company",
            targetKeyProperty: "CompanyCode");

        await Assert.That(sql).Contains("data->>'PersonId' = @srcId");
        await Assert.That(sql).Contains("data->>'CompanyCode' = @tgtId");
    }

    // -----------------------------------------------------------------------
    // Unrelate -> DELETE of the junction row for the endpoint pair
    // -----------------------------------------------------------------------

    [Test]
    public async Task BuildUnrelateDeleteSql_EmitsParameterizedDelete()
    {
        var sql = SqlGenerator.BuildUnrelateDeleteSql(
            schema: "public",
            junctionTableName: "document_written_by",
            sourceTableName: "document",
            sourceKeyProperty: "Id",
            targetTableName: "author",
            targetKeyProperty: "Id");

        // Deletes from the junction table, keyed on the endpoint pair resolved
        // from business ids via the same data->>'key' subqueries as the insert.
        await Assert.That(sql).Contains("DELETE FROM \"public\".\"document_written_by\"");
        await Assert.That(sql).Contains("source_id = (SELECT id FROM \"public\".\"document\"");
        await Assert.That(sql).Contains("target_id = (SELECT id FROM \"public\".\"author\"");
        await Assert.That(sql).Contains("data->>'Id' = @srcId");
        await Assert.That(sql).Contains("data->>'Id' = @tgtId");

        // Parameterized.
        await Assert.That(sql).Contains("@srcId");
        await Assert.That(sql).Contains("@tgtId");

        // INV-2.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    // -----------------------------------------------------------------------
    // Eager endpoint validation (DR-8) -> per-endpoint SELECT EXISTS
    // -----------------------------------------------------------------------

    [Test]
    public async Task BuildEndpointExistsSql_EmitsParameterizedExistenceProbe()
    {
        // DR-8: before any junction row is written, each endpoint's existence is
        // probed by its business id so a missing endpoint surfaces a typed
        // RelationEndpointNotFoundException and no row is written.
        var sql = SqlGenerator.BuildEndpointExistsSql(
            schema: "public",
            tableName: "author",
            keyProperty: "Id",
            parameterName: "@id");

        await Assert.That(sql).Contains("SELECT EXISTS");
        await Assert.That(sql).Contains("FROM \"public\".\"author\"");
        await Assert.That(sql).Contains("data->>'Id' = @id");

        // Parameterized — never interpolates the id.
        await Assert.That(sql).Contains("@id");

        // INV-2.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task BuildEndpointExistsSql_HonoursParameterAndKeyName()
    {
        // The probe must use the caller-supplied parameter name (so source and
        // target probes can be distinguished) and the endpoint's own key
        // property name.
        var sql = SqlGenerator.BuildEndpointExistsSql(
            schema: "public",
            tableName: "company",
            keyProperty: "CompanyCode",
            parameterName: "@tgtId");

        await Assert.That(sql).Contains("data->>'CompanyCode' = @tgtId");
        await Assert.That(sql).DoesNotContain("@id");
    }

    // -----------------------------------------------------------------------
    // Provider-level endpoint resolution (graph -> table name + key property)
    //
    // RelateAsync/UnrelateAsync resolve each descriptor name to its physical
    // table name AND its key property name (for the data->>'key' subqueries).
    // The dispatch step is exposed as PgVectorObjectSetProvider's internal
    // static ResolveRelateEndpoint helper so the full code path is pinned
    // without a live NpgsqlDataSource.
    // -----------------------------------------------------------------------

    [Test]
    public async Task ResolveRelateEndpoint_ResolvesTableAndKeyProperty_FromGraph()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<RelateDomainOntology>()
            .Build();

        var (table, keyProperty) =
            PgVectorObjectSetProvider.ResolveRelateEndpoint(graph, "Document");

        await Assert.That(table).IsEqualTo("document");
        await Assert.That(keyProperty).IsEqualTo("Id");
    }

    [Test]
    public async Task ResolveRelateEndpoint_UnknownDescriptor_Throws()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<RelateDomainOntology>()
            .Build();

        await Assert.That(() => PgVectorObjectSetProvider.ResolveRelateEndpoint(graph, "NoSuchType"))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    [Test]
    public async Task ResolveRelateEndpoint_NullGraph_Throws()
    {
        // Relate is a graph-aware operation: it needs the descriptors' key
        // property names to resolve endpoints. A graph-less provider cannot
        // serve it, so the resolver throws rather than emitting wrong SQL.
        await Assert.That(() => PgVectorObjectSetProvider.ResolveRelateEndpoint(graph: null, "Document"))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    [Test]
    public async Task ResolveRelateEndpoint_FeedsRelateInsertSql_EndToEnd()
    {
        // The resolver output feeds the SQL builders unchanged: pin the full
        // provider dispatch -> SQL path the production RelateAsync walks.
        var graph = new OntologyGraphBuilder()
            .AddDomain<RelateDomainOntology>()
            .Build();

        var (srcTable, srcKey) = PgVectorObjectSetProvider.ResolveRelateEndpoint(graph, "Document");
        var (tgtTable, tgtKey) = PgVectorObjectSetProvider.ResolveRelateEndpoint(graph, "Author");
        var junction = SqlGenerator.JunctionTableName(srcTable, "WrittenBy");

        var insert = SqlGenerator.BuildRelateInsertSql(
            "public", junction, srcTable, srcKey, tgtTable, tgtKey);

        await Assert.That(junction).IsEqualTo("document_written_by");
        await Assert.That(insert).Contains("INSERT INTO \"public\".\"document_written_by\"");
        await Assert.That(insert).Contains("\"public\".\"document\" s");
        await Assert.That(insert).Contains("\"public\".\"author\" t");
        await Assert.That(insert).Contains("ON CONFLICT (source_id, target_id) DO NOTHING");
    }
}

// ---------------------------------------------------------------------------
// Relate test fixtures — top-level so OntologyGraphBuilder.AddDomain<T>()
// can instantiate them (requires a public parameterless constructor).
// ---------------------------------------------------------------------------

public sealed class RelateDocument
{
    public string Id { get; set; } = string.Empty;
}

public sealed class RelateAuthor
{
    public string Id { get; set; } = string.Empty;
}

public class RelateDomainOntology : DomainOntology
{
    public override string DomainName => "relate-domain";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<RelateDocument>("Document", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.Object<RelateAuthor>("Author", obj =>
        {
            obj.Key(a => a.Id);
        });
    }
}
