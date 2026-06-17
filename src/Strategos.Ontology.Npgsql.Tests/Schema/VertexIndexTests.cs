using Strategos.Ontology.Builder;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Schema;

/// <summary>
/// DR-13 (R2, #130): unit tests for the vertex KEY-PROPERTY unique expression
/// index. Every relate/unrelate/traversal subquery resolves an endpoint's
/// surrogate <c>id uuid</c> from its BUSINESS id via <c>data->>'key' = @id</c>
/// and assumes that key uniquely identifies one row. Nothing enforced that
/// assumption: two stored rows sharing a business id would make the
/// endpoint-resolving subquery non-deterministic (a relate could attach to
/// either row). R2 emits a <c>CREATE UNIQUE INDEX ... ((data->>'key'))</c>
/// expression index in the vertex schema DDL so the business-id key is unique at
/// the storage layer, and the resolution subqueries are well-defined.
/// </summary>
/// <remarks>
/// Verify-first (per the task brief): the pre-DR-13 <c>BuildSchemaCreationDdl</c>
/// emitted ONLY the pgvector index (asserted by <see cref="PgVectorSchemaTests"/>),
/// never a key-property unique index — so this is net-new, not an
/// assert-and-close. These assert generated-DDL strings only — no live database
/// (INV-2: raw Npgsql + pgvector DDL, no Marten/Wolverine).
/// </remarks>
public class VertexIndexTests
{
    [Test]
    public async Task EnsureSchema_EmitsUniqueExpressionIndex_OnDataKeyPropertyPath()
    {
        // A vertex table whose descriptor declares key property "Symbol" must get
        // a UNIQUE expression index on (data->>'Symbol') so the relate-path
        // endpoint resolution subquery has a single deterministic row per key.
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "stock",
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat,
            keyPropertyName: "Symbol");

        // The unique expression index is on the JSON key-path the resolution
        // subqueries use, double-parenthesized (an expression index).
        await Assert.That(ddl).Contains("CREATE UNIQUE INDEX IF NOT EXISTS");
        await Assert.That(ddl).Contains("ON \"public\".\"stock\" ((data->>'Symbol'))");

        // The existing table + vector index are still emitted.
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"stock\"");
        await Assert.That(ddl).Contains("USING ivfflat");

        // INV-2: raw DDL only.
        await Assert.That(ddl).DoesNotContain("Marten");
        await Assert.That(ddl).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task EnsureSchema_WithoutKeyProperty_OmitsUniqueExpressionIndex()
    {
        // A vertex with NO declared key property (the legacy pgvector-only table)
        // must keep its pre-DR-13 DDL exactly — no key-property unique index. This
        // is the back-compat path that the established PgVectorSchemaTests pin.
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "document_chunk",
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat,
            keyPropertyName: null);

        await Assert.That(ddl).DoesNotContain("CREATE UNIQUE INDEX");
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"document_chunk\"");
    }

    [Test]
    public async Task EnsureSchema_KeyPropertyWithApostrophe_EscapesLiteral()
    {
        // The key-property name is interpolated into a single-quoted JSON-path
        // literal (it is not parameter-bindable), so an embedded apostrophe must
        // be doubled — matching the EscapeStringLiteral posture the relate SQL
        // builders use (review M1).
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "quirky",
            vectorDimensions: 8,
            indexType: PgVectorIndexType.Hnsw,
            keyPropertyName: "o'clock");

        await Assert.That(ddl).Contains("((data->>'o''clock'))");
    }

    [Test]
    public async Task ResolveEnsureSchemaKeyProperty_FromGraphDescriptor_ReturnsDeclaredKey()
    {
        // EnsureSchemaAsync resolves the key property from the same graph
        // descriptor it resolves the table name from. A descriptor with
        // obj.Key(f => f.Id) must surface "Id" as the unique-index key property.
        var graph = new OntologyGraphBuilder()
            .AddDomain<VertexKeyOntology>()
            .Build();

        var keyProperty = PgVectorObjectSetProvider
            .ResolveEnsureSchemaKeyProperty<VertexKeyRow>(descriptorName: "keyed_vertex", graph);

        await Assert.That(keyProperty).IsEqualTo("Id");
    }

    [Test]
    public async Task ResolveEnsureSchemaKeyProperty_GraphAbsent_ReturnsNull()
    {
        // No graph in scope (direct unit-test / DI without a graph): there is no
        // descriptor to read a key property from, so the resolver returns null and
        // the DDL omits the unique index (back-compat with the pgvector-only table).
        var keyProperty = PgVectorObjectSetProvider
            .ResolveEnsureSchemaKeyProperty<VertexKeyRow>(descriptorName: "keyed_vertex", graph: null);

        await Assert.That(keyProperty).IsNull();
    }
}

public sealed class VertexKeyRow
{
    public string Id { get; set; } = string.Empty;
}

public class VertexKeyOntology : DomainOntology
{
    public override string DomainName => "vertex-key";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VertexKeyRow>("keyed_vertex", obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}
