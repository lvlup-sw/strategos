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
}
