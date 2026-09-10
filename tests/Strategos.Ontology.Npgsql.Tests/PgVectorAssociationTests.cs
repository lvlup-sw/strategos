using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// DR-7/DR-8 (Ontology Edge Foundation, t11): unit tests for the Postgres
/// ATTRIBUTED relate/unrelate SQL SHAPES over the T8 association-object table,
/// and for composing a SINGLE pgvector similarity + edge-attribute query on an
/// association table.
///
/// These assert generated parameterized SQL strings only — NO live database.
/// They mirror <see cref="PgVectorRelateTests"/> and
/// <see cref="PgVectorEdgeSchemaTests"/>: raw Npgsql + pgvector (INV-2 — no
/// Marten/Wolverine), and the dispatch/SQL-building seams are exposed as
/// internal statics so the full code path is pinned without a live
/// <see cref="global::Npgsql.NpgsqlDataSource"/>.
///
/// (A) Closes the attributed-relate gap left by t9: the attributed
/// <c>RelateAsync&lt;TRel&gt;</c> INSERTs a row into the association-object
/// table (<c>id</c> PK, <c>data</c> jsonb, one <c>{role}_id</c> FK per endpoint
/// resolved from each endpoint's business id via <c>data->>'KeyProperty'</c>,
/// like t9's junction relate), with the SAME eager endpoint validation + typed
/// <see cref="RelationEndpointNotFoundException"/> (DR-8). The attributed
/// unrelate DELETEs the association row.
///
/// (B) pgvector coexists on an association table: a single query filters by BOTH
/// similarity (vector distance order/filter) AND an edge-attribute predicate (a
/// <c>data->></c> filter).
/// </summary>
public class PgVectorAssociationTests
{
    // -----------------------------------------------------------------------
    // (A) Attributed relate -> INSERT a row into the association-object table
    // with one {role}_id endpoint FK resolved per endpoint business id, plus
    // the serialized association attributes in data jsonb.
    // -----------------------------------------------------------------------

    [Test]
    public async Task BuildAssociationRelateInsertSql_InsertsAssociationRowWithEndpointFks()
    {
        // An Employment association reifies the relation between a Person
        // (role "Employee", source) and a Company (role "Employer", target).
        // The INSERT writes one row into the association-object table: a fresh
        // id, the association attributes as data jsonb, and one {role}_id FK per
        // endpoint resolved from each endpoint's BUSINESS id via the same
        // data->>'key' subquery t9's junction relate uses.
        var sql = SqlGenerator.BuildAssociationRelateInsertSql(
            schema: "public",
            associationTableName: "employment",
            sourceColumn: "employee_id",
            sourceTableName: "person",
            sourceKeyProperty: "PersonId",
            targetColumn: "employer_id",
            targetTableName: "company",
            targetKeyProperty: "CompanyCode");

        // Inserts into the association-object table: id, data jsonb, both
        // endpoint FK columns named for the roles, QUOTE-DELIMITED so they stay
        // identifier-identical with the T8 association-object DDL (review M1).
        await Assert.That(sql).Contains("INSERT INTO \"public\".\"employment\" (id, data, \"employee_id\", \"employer_id\")");

        // The association id + attributes bind via parameters, never interpolated.
        await Assert.That(sql).Contains("@id");
        await Assert.That(sql).Contains("@data::jsonb");

        // Each endpoint FK resolves the endpoint row's surrogate id uuid from its
        // object table by the BUSINESS id in data jsonb — using THIS endpoint's
        // own key property (INV-8: identity by descriptor, never a shared
        // assumption), parameterized, never interpolated.
        await Assert.That(sql).Contains("\"public\".\"person\"");
        await Assert.That(sql).Contains("\"public\".\"company\"");
        await Assert.That(sql).Contains("data->>'PersonId' = @srcId");
        await Assert.That(sql).Contains("data->>'CompanyCode' = @tgtId");

        // Parameterized — the business ids never appear as literals.
        await Assert.That(sql).Contains("@srcId");
        await Assert.That(sql).Contains("@tgtId");

        // INV-2: raw Npgsql/pgvector — no event-store machinery.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task BuildAssociationRelateInsertSql_SelfAssociation_DistinctRoleColumns()
    {
        // A self-association (both endpoints the same object type) must still
        // route each endpoint to its own role-named FK column — the role
        // disambiguates which surrogate id lands where.
        var sql = SqlGenerator.BuildAssociationRelateInsertSql(
            schema: "public",
            associationTableName: "reporting",
            sourceColumn: "manager_id",
            sourceTableName: "person",
            sourceKeyProperty: "PersonId",
            targetColumn: "report_id",
            targetTableName: "person",
            targetKeyProperty: "PersonId");

        await Assert.That(sql).Contains("INSERT INTO \"public\".\"reporting\" (id, data, \"manager_id\", \"report_id\")");
        await Assert.That(sql).Contains("data->>'PersonId' = @srcId");
        await Assert.That(sql).Contains("data->>'PersonId' = @tgtId");
    }

    // -----------------------------------------------------------------------
    // (A) Attributed unrelate -> DELETE the association row by its id
    // -----------------------------------------------------------------------

    [Test]
    public async Task BuildAssociationUnrelateDeleteSql_DeletesAssociationRowByBusinessId()
    {
        // The attributed unrelate removes the single association row whose
        // BUSINESS id (the data jsonb key field) equals the parameter-bound
        // associationId. Removing a row that does not exist deletes zero rows —
        // a no-op (no throw), mirroring the in-memory store's posture.
        var sql = SqlGenerator.BuildAssociationUnrelateDeleteSql(
            schema: "public",
            associationTableName: "employment",
            associationKeyProperty: "EmploymentId");

        await Assert.That(sql).Contains("DELETE FROM \"public\".\"employment\"");
        await Assert.That(sql).Contains("data->>'EmploymentId' = @associationId");

        // Parameterized.
        await Assert.That(sql).Contains("@associationId");

        // INV-2.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    // -----------------------------------------------------------------------
    // (A) Provider-level association-endpoint resolution: the association
    // descriptor's two endpoints (role -> descriptor name) map the supplied
    // srcDescriptor/tgtDescriptor onto the {role}_id FK columns and the
    // endpoints' physical tables + key properties. Exposed as an internal
    // static so the full dispatch -> SQL path is pinned without a live
    // NpgsqlDataSource.
    // -----------------------------------------------------------------------

    [Test]
    public async Task ResolveAssociationRelate_MapsEndpointsToRoleColumns_FromGraph()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<AssocDomainOntology>()
            .Build();

        var plan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            graph,
            associationDescriptor: "Employment",
            srcDescriptor: "AssocPerson",
            tgtDescriptor: "AssocCompany");

        await Assert.That(plan.AssociationTable).IsEqualTo("employment");
        // Source endpoint is the association endpoint whose descriptor matches
        // srcDescriptor; its FK column is the role-named {role}_id.
        await Assert.That(plan.SourceColumn).IsEqualTo("employee_id");
        await Assert.That(plan.SourceTable).IsEqualTo("assoc_person");
        await Assert.That(plan.SourceKeyProperty).IsEqualTo("Id");
        await Assert.That(plan.TargetColumn).IsEqualTo("employer_id");
        await Assert.That(plan.TargetTable).IsEqualTo("assoc_company");
        await Assert.That(plan.TargetKeyProperty).IsEqualTo("Id");
    }

    [Test]
    public async Task ResolveAssociationRelate_UnknownAssociationDescriptor_Throws()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<AssocDomainOntology>()
            .Build();

        await Assert.That(() => PgVectorObjectSetProvider.ResolveAssociationRelate(
                graph, "NoSuchAssoc", "AssocPerson", "AssocCompany"))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    [Test]
    public async Task ResolveAssociationRelate_EndpointNotAnAssociationEndpoint_Throws()
    {
        // An attributed relate whose src/tgt descriptor is not one of the
        // association's declared endpoints is a caller error — refuse rather
        // than mis-route the FK.
        var graph = new OntologyGraphBuilder()
            .AddDomain<AssocDomainOntology>()
            .Build();

        await Assert.That(() => PgVectorObjectSetProvider.ResolveAssociationRelate(
                graph, "Employment", "AssocPerson", "AssocPerson"))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    [Test]
    public async Task ResolveAssociationRelate_FeedsInsertSql_EndToEnd()
    {
        // The resolver output feeds the SQL builder unchanged: pin the full
        // provider dispatch -> SQL path the production attributed RelateAsync walks.
        var graph = new OntologyGraphBuilder()
            .AddDomain<AssocDomainOntology>()
            .Build();

        var plan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            graph, "Employment", "AssocPerson", "AssocCompany");

        var insert = SqlGenerator.BuildAssociationRelateInsertSql(
            "public",
            plan.AssociationTable,
            plan.SourceColumn,
            plan.SourceTable,
            plan.SourceKeyProperty,
            plan.TargetColumn,
            plan.TargetTable,
            plan.TargetKeyProperty);

        await Assert.That(insert).Contains("INSERT INTO \"public\".\"employment\" (id, data, \"employee_id\", \"employer_id\")");
        await Assert.That(insert).Contains("\"public\".\"assoc_person\"");
        await Assert.That(insert).Contains("\"public\".\"assoc_company\"");
    }

    // -----------------------------------------------------------------------
    // (A) The attributed surfaces no longer throw NotSupportedException —
    // they now reach the resolver, which fails with the typed graph error when
    // the provider is graph-less rather than refusing the operation outright.
    // -----------------------------------------------------------------------

    [Test]
    public async Task AttributedRelate_NoLongerThrowsNotSupported()
    {
        var provider = CreateGraphlessProvider();

        // The attributed relate path is implemented: a graph-less provider can't
        // resolve the association endpoints, so it throws InvalidOperationException
        // (the same graph-required posture as the plain relate path) — NOT the old
        // NotSupportedException stub.
        await Assert.That(async () => await provider.RelateAsync(
                "AssocPerson", "p1", "Employs", "AssocCompany", "c1",
                "Employment", new AssocEmployment { Id = "e1" }))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    [Test]
    public async Task AttributedUnrelate_NoLongerThrowsNotSupported()
    {
        var provider = CreateGraphlessProvider();

        await Assert.That(async () => await provider.UnrelateAsync(
                "AssocPerson", "p1", "Employs", "AssocCompany", "c1",
                "Employment", "e1"))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    // -----------------------------------------------------------------------
    // (B) pgvector + edge-attribute filter compose into ONE query on the
    // association table: vector-distance order/filter AND a data->> edge-attr
    // predicate in the same statement.
    // -----------------------------------------------------------------------

    [Test]
    public async Task Query_AssociationWithPgVectorColumn_ComposesSimilarityAndEdgeAttributeFilter()
    {
        // A single query over the association-object table that ranks by vector
        // distance AND filters by an edge attribute stored in the association's
        // data jsonb. The edge-attribute predicate is a data->> filter (e.g.
        // "Strength" above a threshold) threaded as the similarity WHERE clause.
        var edgeAttrFilter = "data->>'Strength' = @strength";
        var sql = SqlGenerator.BuildSimilarityQuery(
            schema: "public",
            tableName: "employment",
            metric: DistanceMetric.Cosine,
            whereClause: edgeAttrFilter);

        // Similarity half: vector distance projection + order + top-K.
        await Assert.That(sql).Contains("(embedding <=> @query) AS distance");
        await Assert.That(sql).Contains("FROM \"public\".\"employment\"");
        await Assert.That(sql).Contains("ORDER BY distance LIMIT @topK");

        // Edge-attribute half: the data->> predicate coexists in the SAME query's
        // WHERE clause — one composed statement, not two round trips.
        await Assert.That(sql).Contains("WHERE data->>'Strength' = @strength");

        // The vector distance and the edge-attribute filter are both present.
        await Assert.That(sql).Contains("<=>");
        await Assert.That(sql).Contains("data->>'Strength'");

        // Parameterized — the edge-attribute value never appears as a literal.
        await Assert.That(sql).Contains("@strength");

        // INV-2.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static PgVectorObjectSetProvider CreateGraphlessProvider()
    {
        // A direct, graph-less provider instance: enough to exercise the
        // attributed relate/unrelate ENTRY point (which now reaches the graph
        // resolver and fails with a typed InvalidOperationException), without a
        // live NpgsqlDataSource — no SQL is executed on these paths because the
        // graph-required guard trips first.
        var dataSource = global::Npgsql.NpgsqlDataSource.Create(
            "Host=localhost;Database=unused;Username=unused;Password=unused");
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.Dimensions.Returns(3);
        var options = Microsoft.Extensions.Options.Options.Create(new PgVectorOptions());
        var logger = Substitute.For<global::Microsoft.Extensions.Logging.ILogger<PgVectorObjectSetProvider>>();

        return new PgVectorObjectSetProvider(dataSource, embeddingProvider, options, logger, graph: null);
    }
}

// ---------------------------------------------------------------------------
// Association test fixtures — top-level so OntologyGraphBuilder.AddDomain<T>()
// can instantiate them (requires a public parameterless constructor).
// ---------------------------------------------------------------------------

public sealed class AssocPerson
{
    public string Id { get; set; } = string.Empty;
}

public sealed class AssocCompany
{
    public string Id { get; set; } = string.Empty;
}

public sealed class AssocEmployment
{
    public string Id { get; set; } = string.Empty;

    public string Strength { get; set; } = string.Empty;

    // Endpoint-carrying properties: their member names become the endpoint
    // ROLES (Employee/Employer), and the property types name the endpoint
    // descriptors (AssocPerson/AssocCompany) — INV-8: identity by descriptor
    // name, never typeof on the association instance.
    public AssocPerson Employee { get; set; } = new();

    public AssocCompany Employer { get; set; } = new();
}

public class AssocDomainOntology : DomainOntology
{
    public override string DomainName => "assoc-domain";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AssocPerson>("AssocPerson", obj =>
        {
            obj.Key(p => p.Id);
        });

        builder.Object<AssocCompany>("AssocCompany", obj =>
        {
            obj.Key(c => c.Id);
        });

        builder.Association<AssocEmployment>("Employment", assoc =>
        {
            assoc.Key(e => e.Id);
            assoc.Between(e => e.Employee).And(e => e.Employer);
        });
    }
}
