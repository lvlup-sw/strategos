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
