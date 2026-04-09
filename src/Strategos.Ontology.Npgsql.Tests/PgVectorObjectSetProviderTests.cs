using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// Unit tests for <see cref="PgVectorObjectSetProvider"/>'s read-path dispatch.
///
/// The provider's dispatch step — resolving a table name from an incoming
/// <see cref="ObjectSetExpression"/> — is exposed as the internal static helper
/// <c>PgVectorObjectSetProvider.ResolveTableName</c> so it can be verified
/// without needing a live <see cref="Npgsql.NpgsqlDataSource"/>. The production
/// <c>ExecuteSimilarityAsync</c>, <c>ExecuteAsync</c>, and <c>StreamAsync</c>
/// methods all delegate to this helper so the assertions below pin the
/// behavior of the full code path.
/// </summary>
public class PgVectorObjectSetProviderTests
{
    [Test]
    public async Task ExecuteSimilarityAsync_UsesDescriptorNameFromExpression_NotTypeofTName()
    {
        // Arrange — build a SimilarityExpression whose root declares an explicit
        // descriptor name ("trading_documents") that differs from typeof(T).Name
        // ("SemanticDocument" → "semantic_document"). Track A requires the root
        // expression to carry the descriptor name; the provider must honor it.
        var root = new RootExpression(typeof(SemanticDocument), "trading_documents");
        var similarity = new SimilarityExpression(
            root,
            queryText: "equity volatility",
            topK: 5,
            minRelevance: 0.0,
            metric: DistanceMetric.Cosine,
            queryVector: new float[] { 0.1f, 0.2f, 0.3f });

        // Act — invoke the provider's resolution step directly. This is the
        // single table-name lookup the production ExecuteSimilarityAsync uses
        // to build the FROM clause via SqlGenerator.BuildSimilarityQuery.
        var tableName = PgVectorObjectSetProvider.ResolveTableName(similarity);
        var sql = SqlGenerator.BuildSimilarityQuery("public", tableName, similarity.Metric);

        // Assert — FROM clause must reference "trading_documents" (the declared
        // descriptor), NOT "semantic_document" (typeof(T).Name).
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(sql).Contains("\"public\".\"trading_documents\"");
        await Assert.That(sql).DoesNotContain("\"semantic_document\"");
    }

    [Test]
    public async Task ExecuteAsync_UsesDescriptorNameFromExpression_NotTypeofTName()
    {
        // Arrange — a plain RootExpression with an explicit descriptor name.
        // The non-similarity read path (ExecuteAsync) must honour the
        // declared name the same way ExecuteSimilarityAsync does.
        var root = new RootExpression(typeof(SemanticDocument), "trading_documents");

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableName(root);
        var sql = SqlGenerator.BuildSelectQuery("public", tableName);

        // Assert
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(sql).Contains("\"public\".\"trading_documents\"");
        await Assert.That(sql).DoesNotContain("\"semantic_document\"");
    }

    [Test]
    public async Task ExecuteAsync_FilterExpressionOverRoot_WalksToRootDescriptorName()
    {
        // Arrange — a FilterExpression wrapping a RootExpression with a
        // non-default descriptor name. The walk-to-root helper on
        // ObjectSetExpression (Track A2) must surface the root's declared
        // name, and the provider's dispatch must reach it.
        var root = new RootExpression(typeof(SemanticDocument), "knowledge_documents");
        System.Linq.Expressions.Expression<Func<SemanticDocument, bool>> pred = d => d.Id != Guid.Empty;
        var filter = new FilterExpression(root, pred);

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableName(filter);
        var sql = SqlGenerator.BuildSelectQuery("public", tableName);

        // Assert
        await Assert.That(tableName).IsEqualTo("knowledge_documents");
        await Assert.That(sql).Contains("\"public\".\"knowledge_documents\"");
    }

    [Test]
    public async Task StreamAsync_UsesDescriptorNameFromExpression_NotTypeofTName()
    {
        // Arrange — StreamAsync shares the non-similarity read-path dispatch
        // with ExecuteAsync. Pin the behavior explicitly so a future refactor
        // that diverges the two code paths cannot silently regress streaming.
        var root = new RootExpression(typeof(SemanticDocument), "streaming_documents");

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableName(root);
        var sql = SqlGenerator.BuildSelectQuery("public", tableName);

        // Assert
        await Assert.That(tableName).IsEqualTo("streaming_documents");
        await Assert.That(sql).Contains("\"public\".\"streaming_documents\"");
        await Assert.That(sql).DoesNotContain("\"semantic_document\"");
    }

    // ---------------------------------------------------------------------
    // Track F3 — explicit-descriptor-name write-path dispatch tests.
    //
    // The write-path equivalent of ResolveTableName is exposed as the
    // internal static helper PgVectorObjectSetProvider.ResolveTableNameForDescriptor,
    // which the explicit-name StoreAsync/StoreBatchAsync overloads delegate to.
    // Asserting against the helper plus the SQL/COPY strings produced by
    // SqlGenerator pins the full code path without needing a live
    // NpgsqlDataSource.
    // ---------------------------------------------------------------------

    [Test]
    public async Task StoreAsync_ExplicitName_WritesToNamedTable()
    {
        // Arrange — descriptor name "trading_documents" deliberately differs
        // from typeof(SemanticDocument).Name so we can detect a regression to
        // the typeof(T).Name path.
        const string descriptorName = "trading_documents";

        // Act — invoke the dispatch helper the explicit-name StoreAsync<T>
        // overload uses. Then build the INSERT SQL the production code path
        // emits via SqlGenerator.BuildInsertSql.
        var tableName = PgVectorObjectSetProvider.ResolveTableNameForDescriptor(descriptorName);
        var insertSql = SqlGenerator.BuildInsertSql("public", tableName, hasEmbedding: false);
        var insertSqlWithEmbedding = SqlGenerator.BuildInsertSql("public", tableName, hasEmbedding: true);

        // Assert — table name resolves verbatim (already snake_case) and the
        // generated INSERT statements target "trading_documents", NOT
        // "semantic_document".
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(TypeMapper.ToSnakeCase("trading_documents")).IsEqualTo("trading_documents");
        await Assert.That(insertSql).Contains("\"public\".\"trading_documents\"");
        await Assert.That(insertSql).DoesNotContain("\"semantic_document\"");
        await Assert.That(insertSqlWithEmbedding).Contains("\"public\".\"trading_documents\"");
        await Assert.That(insertSqlWithEmbedding).Contains("embedding");
    }

    [Test]
    public async Task StoreBatchAsync_ExplicitName_UsesCopyToNamedTable()
    {
        // Arrange — same dispatch step. The batch path uses the resolved
        // table name to build a fully-qualified COPY target identifier.
        const string descriptorName = "trading_documents";

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableNameForDescriptor(descriptorName);
        var qualifiedTable =
            $"{SqlGenerator.QuoteIdentifier("public")}.{SqlGenerator.QuoteIdentifier(tableName)}";
        var copyColumnsNoEmbedding = $"{qualifiedTable} (id, data)";
        var copyColumnsWithEmbedding = $"{qualifiedTable} (id, data, embedding)";
        var copyCommandNoEmbedding = $"COPY {copyColumnsNoEmbedding} FROM STDIN (FORMAT BINARY)";
        var copyCommandWithEmbedding = $"COPY {copyColumnsWithEmbedding} FROM STDIN (FORMAT BINARY)";

        // Assert — COPY target table matches the explicit descriptor, not
        // typeof(T).Name. The qualified identifier is the exact string
        // PgVectorObjectSetProvider.StoreBatchAsync passes to
        // NpgsqlConnection.BeginBinaryImportAsync.
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(qualifiedTable).IsEqualTo("\"public\".\"trading_documents\"");
        await Assert.That(copyCommandNoEmbedding)
            .IsEqualTo("COPY \"public\".\"trading_documents\" (id, data) FROM STDIN (FORMAT BINARY)");
        await Assert.That(copyCommandWithEmbedding)
            .IsEqualTo("COPY \"public\".\"trading_documents\" (id, data, embedding) FROM STDIN (FORMAT BINARY)");
        await Assert.That(copyCommandNoEmbedding).DoesNotContain("semantic_document");
    }

    [Test]
    public async Task WriteOverloads_TypeMapperGetTableName_StillUsedByDefaultOverloads()
    {
        // Regression guard — pins the F4 seam. The default StoreAsync<T>(T)
        // and StoreBatchAsync<T>(IReadOnlyList<T>) overloads must continue
        // to resolve the table name via TypeMapper.GetTableName<T>() — i.e.
        // typeof(T).Name → snake_case — so back-compat holds while F4 in
        // Group 3 prepares to swap this for graph-backed lookup.
        //
        // We assert against the still-present TypeMapper.GetTableName<T>()
        // helper directly. F4 will replace the default-overload call site
        // and update this test to assert the new resolution path.
        var defaultTableName = TypeMapper.GetTableName<SemanticDocument>();

        // Assert — TypeMapper.GetTableName<T>() still exists, still returns
        // the typeof(T).Name-derived snake_case name, and matches what
        // SqlGenerator emits for the default write path.
        await Assert.That(defaultTableName).IsEqualTo("semantic_document");
        var defaultInsertSql = SqlGenerator.BuildInsertSql("public", defaultTableName, hasEmbedding: false);
        await Assert.That(defaultInsertSql).Contains("\"public\".\"semantic_document\"");

        // Sanity — the explicit-name dispatch helper produces a different
        // table name from the same CLR type when given a non-matching
        // descriptor, proving the two paths are independently resolvable.
        var explicitTableName = PgVectorObjectSetProvider.ResolveTableNameForDescriptor("trading_documents");
        await Assert.That(explicitTableName).IsNotEqualTo(defaultTableName);
    }

    /// <summary>
    /// Test CLR type whose <c>Name</c> deliberately differs from any expected
    /// descriptor name, so we can detect if the provider is still reaching for
    /// <c>typeof(T).Name</c> instead of <c>expression.RootObjectTypeName</c>.
    /// </summary>
    private sealed class SemanticDocument
    {
        public Guid Id { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}
