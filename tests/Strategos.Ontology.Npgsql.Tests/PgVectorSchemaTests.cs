using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

public class PgVectorSchemaTests
{
    [Test]
    public async Task EnsureSchemaAsync_GeneratesCorrectDdl_IvfFlat()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "document_chunk",
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat);

        await Assert.That(ddl).Contains("CREATE EXTENSION IF NOT EXISTS vector;");
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"document_chunk\"");
        await Assert.That(ddl).Contains("embedding vector(1536)");
        await Assert.That(ddl).Contains("USING ivfflat");
        await Assert.That(ddl).Contains("vector_cosine_ops");
        await Assert.That(ddl).Contains("WITH (lists = 100)");
    }

    [Test]
    public async Task EnsureSchemaAsync_GeneratesCorrectDdl_Hnsw()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "document_chunk",
            vectorDimensions: 768,
            indexType: PgVectorIndexType.Hnsw);

        await Assert.That(ddl).Contains("CREATE EXTENSION IF NOT EXISTS vector;");
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"document_chunk\"");
        await Assert.That(ddl).Contains("embedding vector(768)");
        await Assert.That(ddl).Contains("USING hnsw");
        await Assert.That(ddl).Contains("vector_cosine_ops");
        await Assert.That(ddl).DoesNotContain("WITH (lists = 100)");
    }

    [Test]
    public async Task EnsureSchemaAsync_VectorDimension_MatchesEmbeddingProvider()
    {
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.Dimensions.Returns(384);

        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "my_table",
            vectorDimensions: embeddingProvider.Dimensions,
            indexType: PgVectorIndexType.IvfFlat);

        await Assert.That(ddl).Contains("embedding vector(384)");
    }

    [Test]
    public async Task EnsureSchemaAsync_DdlContainsRequiredColumns()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "test_table",
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat);

        await Assert.That(ddl).Contains("id uuid PRIMARY KEY DEFAULT gen_random_uuid()");
        await Assert.That(ddl).Contains("data jsonb NOT NULL");
        await Assert.That(ddl).Contains("created_at timestamptz DEFAULT now()");
    }

    [Test]
    public async Task EnsureSchemaAsync_DdlContainsIndex()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "test_table",
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat);

        await Assert.That(ddl).Contains("CREATE INDEX IF NOT EXISTS \"idx_test_table_embedding\"");
    }

    [Test]
    public async Task EnsureSchemaAsync_L2Metric_UsesL2Ops()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "test_table",
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat,
            metric: DistanceMetric.L2);

        await Assert.That(ddl).Contains("vector_l2_ops");
    }

    [Test]
    public async Task EnsureSchemaAsync_InnerProductMetric_UsesIpOps()
    {
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            "test_table",
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat,
            metric: DistanceMetric.InnerProduct);

        await Assert.That(ddl).Contains("vector_ip_ops");
    }
}
