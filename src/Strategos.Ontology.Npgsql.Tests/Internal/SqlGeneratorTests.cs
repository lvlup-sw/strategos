using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Internal;

public class SqlGeneratorTests
{
    [Test]
    public async Task GetDistanceOperator_Cosine_ReturnsCosineOp()
    {
        var op = SqlGenerator.GetDistanceOperator(DistanceMetric.Cosine);
        await Assert.That(op).IsEqualTo("<=>");
    }

    [Test]
    public async Task GetDistanceOperator_L2_ReturnsL2Op()
    {
        var op = SqlGenerator.GetDistanceOperator(DistanceMetric.L2);
        await Assert.That(op).IsEqualTo("<->");
    }

    [Test]
    public async Task GetDistanceOperator_InnerProduct_ReturnsIpOp()
    {
        var op = SqlGenerator.GetDistanceOperator(DistanceMetric.InnerProduct);
        await Assert.That(op).IsEqualTo("<#>");
    }

    [Test]
    public async Task BuildSimilarityQuery_CosineDistance_GeneratesCorrectSql()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.Cosine);

        await Assert.That(sql).Contains("<=>");
        await Assert.That(sql).Contains("\"public\".\"document_chunk\"");
        await Assert.That(sql).Contains("ORDER BY distance LIMIT @topK");
        await Assert.That(sql).Contains("embedding");
        await Assert.That(sql).Contains("@query");
    }

    [Test]
    public async Task BuildSimilarityQuery_L2Distance_GeneratesCorrectSql()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.L2);

        await Assert.That(sql).Contains("<->");
        await Assert.That(sql).DoesNotContain("<=>");
        await Assert.That(sql).DoesNotContain("<#>");
    }

    [Test]
    public async Task BuildSimilarityQuery_InnerProduct_GeneratesCorrectSql()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.InnerProduct);

        await Assert.That(sql).Contains("<#>");
        await Assert.That(sql).DoesNotContain("<=>");
        await Assert.That(sql).DoesNotContain("<->");
    }

    [Test]
    public async Task BuildSimilarityQuery_WithWhereClause_IncludesWhere()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.Cosine, "data->>'Name' = @p0");

        await Assert.That(sql).Contains("WHERE data->>'Name' = @p0");
    }

    [Test]
    public async Task BuildSimilarityQuery_WithoutWhereClause_NoWhere()
    {
        var sql = SqlGenerator.BuildSimilarityQuery("public", "document_chunk", DistanceMetric.Cosine);

        await Assert.That(sql).DoesNotContain("WHERE");
    }

    [Test]
    public async Task BuildSelectQuery_NoWhere_ReturnsSelectAll()
    {
        var sql = SqlGenerator.BuildSelectQuery("public", "document");

        await Assert.That(sql).IsEqualTo("SELECT id, data FROM \"public\".\"document\"");
    }

    [Test]
    public async Task BuildSelectQuery_WithWhere_IncludesWhereClause()
    {
        var sql = SqlGenerator.BuildSelectQuery("public", "document", "data->>'Name' = @p0");

        await Assert.That(sql).IsEqualTo("SELECT id, data FROM \"public\".\"document\" WHERE data->>'Name' = @p0");
    }

    [Test]
    public async Task BuildInsertSql_WithoutEmbedding_NoEmbeddingColumn()
    {
        var sql = SqlGenerator.BuildInsertSql("public", "document", hasEmbedding: false);

        await Assert.That(sql).IsEqualTo("INSERT INTO \"public\".\"document\" (id, data) VALUES (@id, @data::jsonb)");
    }

    [Test]
    public async Task BuildInsertSql_WithEmbedding_IncludesEmbeddingColumn()
    {
        var sql = SqlGenerator.BuildInsertSql("public", "document", hasEmbedding: true);

        await Assert.That(sql).IsEqualTo("INSERT INTO \"public\".\"document\" (id, data, embedding) VALUES (@id, @data::jsonb, @embedding)");
    }

    [Test]
    public async Task BuildSchemaCreationDdl_IvfFlat_GeneratesCorrectDdl()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl("public", "document_chunk", 1536, PgVectorIndexType.IvfFlat);

        await Assert.That(ddl).Contains("CREATE EXTENSION IF NOT EXISTS vector;");
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"document_chunk\"");
        await Assert.That(ddl).Contains("id uuid PRIMARY KEY DEFAULT gen_random_uuid()");
        await Assert.That(ddl).Contains("data jsonb NOT NULL");
        await Assert.That(ddl).Contains("embedding vector(1536)");
        await Assert.That(ddl).Contains("created_at timestamptz DEFAULT now()");
        await Assert.That(ddl).Contains("USING ivfflat");
        await Assert.That(ddl).Contains("vector_cosine_ops");
        await Assert.That(ddl).Contains("WITH (lists = 100)");
    }

    [Test]
    public async Task BuildSchemaCreationDdl_Hnsw_GeneratesCorrectDdl()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl("public", "document_chunk", 768, PgVectorIndexType.Hnsw);

        await Assert.That(ddl).Contains("USING hnsw");
        await Assert.That(ddl).Contains("vector_cosine_ops");
        await Assert.That(ddl).Contains("embedding vector(768)");
        await Assert.That(ddl).DoesNotContain("WITH (lists = 100)");
    }

    [Test]
    public async Task BuildSchemaCreationDdl_VectorDimension_MatchesProvided()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl("public", "doc", 384, PgVectorIndexType.IvfFlat);

        await Assert.That(ddl).Contains("embedding vector(384)");
    }

    [Test]
    public async Task BuildSchemaCreationDdl_CustomSchema_UsesSchemaName()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl("my_schema", "document", 1536, PgVectorIndexType.IvfFlat);

        await Assert.That(ddl).Contains("\"my_schema\".\"document\"");
    }

    [Test]
    public async Task GetIndexOperatorClass_Cosine_ReturnsCosineOps()
    {
        var ops = SqlGenerator.GetIndexOperatorClass(DistanceMetric.Cosine);
        await Assert.That(ops).IsEqualTo("vector_cosine_ops");
    }

    [Test]
    public async Task GetIndexOperatorClass_L2_ReturnsL2Ops()
    {
        var ops = SqlGenerator.GetIndexOperatorClass(DistanceMetric.L2);
        await Assert.That(ops).IsEqualTo("vector_l2_ops");
    }

    [Test]
    public async Task GetIndexOperatorClass_InnerProduct_ReturnsIpOps()
    {
        var ops = SqlGenerator.GetIndexOperatorClass(DistanceMetric.InnerProduct);
        await Assert.That(ops).IsEqualTo("vector_ip_ops");
    }

    // -----------------------------------------------------------------------
    // Review M1: descriptor-derived identifiers must be HARDENED, not trusted.
    // ToSnakeCase only lowercases/underscores — it does NOT neutralize a quote
    // or a space. These tests prove that a key-property name carrying a single
    // quote is escaped (doubled) inside the data->>'...' literal, and that a
    // role name carrying a space is QUOTED in both the DDL column and the DML
    // INSERT column so the SQL is safe regardless of the caller. They are
    // DB-free: they assert generated-SQL strings only.
    // -----------------------------------------------------------------------

    [Test]
    public async Task BuildRelateInsertSql_KeyPropertyWithQuote_EscapesLiteral()
    {
        // A key-property name carrying a single quote — e.g. an injection probe
        // "Id' OR '1'='1" — must be doubled inside the data->>'...' literal, not
        // interpolated raw (which would terminate the literal early).
        var sql = SqlGenerator.BuildRelateInsertSql(
            schema: "public",
            junctionTableName: "person_authored",
            sourceTableName: "person",
            sourceKeyProperty: "Id' OR '1'='1",
            targetTableName: "document",
            targetKeyProperty: "DocId");

        // The embedded quote is doubled: the literal stays a single, closed token.
        await Assert.That(sql).Contains("data->>'Id'' OR ''1''=''1' = @srcId");
        // The raw, un-doubled form must NOT survive — that would break out of the
        // literal.
        await Assert.That(sql).DoesNotContain("data->>'Id' OR '1'='1'");
    }

    [Test]
    public async Task BuildEndpointExistsSql_KeyPropertyWithQuote_EscapesLiteral()
    {
        var sql = SqlGenerator.BuildEndpointExistsSql(
            schema: "public",
            tableName: "person",
            keyProperty: "Na'me",
            parameterName: "@srcId");

        await Assert.That(sql).Contains("data->>'Na''me' = @srcId");
    }

    [Test]
    public async Task BuildInstanceAnchoredTraversalSql_KeyPropertyWithQuote_EscapesLiteral()
    {
        var sql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            schema: "public",
            sourceTableName: "person",
            sourceKeyProperty: "Id'x",
            junctionTableName: "person_authored",
            targetTableName: "document");

        await Assert.That(sql).Contains("s.data->>'Id''x' = @srcId");
    }

    [Test]
    public async Task BuildAssociationUnrelateDeleteSql_KeyPropertyWithQuote_EscapesLiteral()
    {
        var sql = SqlGenerator.BuildAssociationUnrelateDeleteSql(
            schema: "public",
            associationTableName: "employment",
            associationKeyProperty: "Key'inj");

        await Assert.That(sql).Contains("data->>'Key''inj' = @associationId");
    }

    [Test]
    public async Task BuildAssociationObjectTableDdl_RoleWithSpace_QuotesColumnIdentifier()
    {
        // A role carrying a space snake_cases to a name with an embedded space —
        // an UNQUOTED column would be a syntax error / split token. The column
        // identifier must be quote-delimited.
        var association = new ObjectTypeDescriptor("Employment", typeof(object), "hr")
        {
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("Lead Engineer", "Person"),
                new AssociationEndpoint("Employer", "Company"),
            ],
        };

        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", association);

        // The role snake_cases to "lead engineer" (space preserved) and is
        // quote-delimited as a column identifier.
        await Assert.That(ddl).Contains("\"lead engineer_id\" uuid NOT NULL");
        // The bare, unquoted form must NOT appear — that would be invalid SQL.
        await Assert.That(ddl).DoesNotContain("lead engineer_id uuid");
    }

    [Test]
    public async Task BuildAssociationRelateInsertSql_RoleColumnAndKey_QuotedAndEscaped()
    {
        // The {role}_id INSERT columns must be quoted (identifier-identical with
        // the DDL) and the key-property literals must be quote-escaped — so a
        // role with a space and a key with a quote are both neutralized.
        var sql = SqlGenerator.BuildAssociationRelateInsertSql(
            schema: "public",
            associationTableName: "employment",
            sourceColumn: "lead engineer_id",
            sourceTableName: "person",
            sourceKeyProperty: "Pe'rsonId",
            targetColumn: "employer_id",
            targetTableName: "company",
            targetKeyProperty: "CompanyCode");

        // Role columns are quote-delimited and identifier-identical with the DDL.
        await Assert.That(sql).Contains("(id, data, \"lead engineer_id\", \"employer_id\")");
        // The key literal's embedded quote is doubled.
        await Assert.That(sql).Contains("data->>'Pe''rsonId' = @srcId");
    }

    [Test]
    public async Task AssociationDdlAndInsert_RoleColumns_AreIdentifierIdentical()
    {
        // The keystone M1 invariant: whatever the DDL declares as the {role}_id
        // column, the INSERT must reference the SAME physical identifier. Build
        // both from one descriptor and assert the DDL's quoted column tokens
        // appear verbatim in the INSERT's column list.
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

        var insert = SqlGenerator.BuildAssociationRelateInsertSql(
            schema: "public",
            associationTableName: "reporting",
            sourceColumn: "manager_id",
            sourceTableName: "person",
            sourceKeyProperty: "PersonId",
            targetColumn: "report_id",
            targetTableName: "person",
            targetKeyProperty: "PersonId");

        // Both the DDL and the INSERT use the SAME quoted column identifiers.
        await Assert.That(ddl).Contains("\"manager_id\" uuid NOT NULL");
        await Assert.That(ddl).Contains("\"report_id\" uuid NOT NULL");
        await Assert.That(insert).Contains("(id, data, \"manager_id\", \"report_id\")");
    }
}
