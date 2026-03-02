using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

public class PgVectorWriteTests
{
    [Test]
    public async Task StoreAsync_GeneratesCorrectInsertSql()
    {
        var sql = SqlGenerator.BuildInsertSql("public", "test_document", hasEmbedding: false);

        await Assert.That(sql).Contains("INSERT INTO");
        await Assert.That(sql).Contains("\"public\".\"test_document\"");
        await Assert.That(sql).Contains("@id");
        await Assert.That(sql).Contains("@data::jsonb");
        await Assert.That(sql).DoesNotContain("@embedding");
    }

    [Test]
    public async Task StoreAsync_ISearchableItem_UsesProvidedEmbedding()
    {
        var sql = SqlGenerator.BuildInsertSql("public", "searchable_document", hasEmbedding: true);

        await Assert.That(sql).Contains("INSERT INTO");
        await Assert.That(sql).Contains("@embedding");
        await Assert.That(sql).Contains("(id, data, embedding)");
    }

    [Test]
    public async Task StoreBatchAsync_GeneratesCorrectBatchSql()
    {
        // The batch SQL is the same per-item INSERT, executed as batch commands
        var sql = SqlGenerator.BuildInsertSql("public", "test_document", hasEmbedding: false);

        await Assert.That(sql).Contains("INSERT INTO \"public\".\"test_document\"");
        await Assert.That(sql).Contains("VALUES (@id, @data::jsonb)");
    }

    [Test]
    public async Task StoreBatchAsync_WithEmbedding_IncludesEmbeddingParam()
    {
        var sql = SqlGenerator.BuildInsertSql("public", "test_document", hasEmbedding: true);

        await Assert.That(sql).Contains("VALUES (@id, @data::jsonb, @embedding)");
    }

    [Test]
    public async Task BuildInsertSql_CustomSchema_UsesSchema()
    {
        var sql = SqlGenerator.BuildInsertSql("my_schema", "document", hasEmbedding: false);

        await Assert.That(sql).Contains("\"my_schema\".\"document\"");
    }

    [Test]
    public async Task TypeMapper_ForISearchableType_ProducesTableName()
    {
        var tableName = TypeMapper.GetTableName<SearchableDocument>();

        await Assert.That(tableName).IsEqualTo("searchable_document");
    }

    [Test]
    public async Task ISearchable_Interface_CanBeCheckedAtRuntime()
    {
        var doc = new SearchableDocument { Embedding = [0.1f, 0.2f, 0.3f] };
        var isSearchable = doc is ISearchable;

        await Assert.That(isSearchable).IsTrue();
    }

    private sealed class SearchableDocument : ISearchable
    {
        public Guid Id { get; set; }

        public string Content { get; set; } = string.Empty;

        public float[] Embedding { get; set; } = [];
    }
}
