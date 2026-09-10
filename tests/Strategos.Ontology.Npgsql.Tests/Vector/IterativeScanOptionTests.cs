using Strategos.Ontology.Npgsql;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Vector;

/// <summary>
/// DR-13 (R8, #130): unit tests for the pgvector ITERATIVE-SCAN knobs (a 0.8.0
/// feature). When a similarity query carries a filter, the planner can skip the
/// HNSW index and post-filter — under-returning rows below <c>topK</c>. pgvector
/// 0.8 fixes this with iterative scans, controlled by the GUCs
/// <c>hnsw.iterative_scan</c>, <c>hnsw.max_scan_tuples</c>, and
/// <c>hnsw.ef_search</c>. R8 exposes these as per-query options applied via
/// <c>SET LOCAL</c> (transaction-scoped, so they never leak across pooled
/// connections), and shapes the search as an ANN CTE the outer query then joins
/// against — the canonical pgvector recipe for index-backed filtered search.
/// </summary>
/// <remarks>
/// These assert generated-SQL strings only — no live database (INV-2: raw Npgsql
/// + pgvector). Mirrors <see cref="PgVectorSimilarityTests"/>.
/// </remarks>
public class IterativeScanOptionTests
{
    [Test]
    public async Task ComposedQuery_AppliesIterativeScanKnobs_ViaSetLocal()
    {
        // A similarity query with iterative-scan options must:
        //  1. emit SET LOCAL for each supplied GUC (transaction-scoped),
        //  2. shape the ANN search as a CTE the outer query joins against.
        var options = new IterativeScanOptions
        {
            Mode = IterativeScanMode.RelaxedOrder,
            MaxScanTuples = 40000,
            EfSearch = 100,
        };

        var sql = SqlGenerator.BuildIterativeScanSimilarityQuery(
            schema: "public",
            tableName: "document_chunk",
            metric: DistanceMetric.Cosine,
            options: options,
            whereClause: null);

        // Each knob is applied via SET LOCAL with the pgvector 0.8 GUC names.
        await Assert.That(sql).Contains("SET LOCAL hnsw.iterative_scan = 'relaxed_order';");
        await Assert.That(sql).Contains("SET LOCAL hnsw.max_scan_tuples = 40000;");
        await Assert.That(sql).Contains("SET LOCAL hnsw.ef_search = 100;");

        // ANN-CTE-then-join shape: the vector search is a CTE (so the planner uses
        // the HNSW index for the ordered top-K), and the outer query selects from it.
        await Assert.That(sql).Contains("WITH ann AS (");
        await Assert.That(sql).Contains("embedding <=> @query");
        await Assert.That(sql).Contains("ORDER BY");
        await Assert.That(sql).Contains("LIMIT @topK");

        // INV-2: raw Npgsql/pgvector only.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task ComposedQuery_OmitsUnsetKnobs()
    {
        // Only the supplied knobs are emitted — an option left null contributes no
        // SET LOCAL, so the session default stands.
        var options = new IterativeScanOptions
        {
            Mode = IterativeScanMode.StrictOrder,
            MaxScanTuples = null,
            EfSearch = null,
        };

        var sql = SqlGenerator.BuildIterativeScanSimilarityQuery(
            "public", "document_chunk", DistanceMetric.L2, options, whereClause: null);

        await Assert.That(sql).Contains("SET LOCAL hnsw.iterative_scan = 'strict_order';");
        await Assert.That(sql).DoesNotContain("max_scan_tuples");
        await Assert.That(sql).DoesNotContain("ef_search");
    }

    [Test]
    public async Task ComposedQuery_WithFilter_AppliesWhereInOuterQuery()
    {
        // A filter must be applied in the OUTER query (post-CTE), not inside the
        // ANN CTE — keeping the CTE a pure index-ordered top-K scan while the
        // iterative scan supplies enough rows for the outer filter to satisfy topK.
        var options = new IterativeScanOptions { Mode = IterativeScanMode.RelaxedOrder };

        var sql = SqlGenerator.BuildIterativeScanSimilarityQuery(
            "public", "document_chunk", DistanceMetric.Cosine, options,
            whereClause: "data->>'category' = @category");

        await Assert.That(sql).Contains("WITH ann AS (");
        await Assert.That(sql).Contains("WHERE data->>'category' = @category");
    }

    [Test]
    public async Task IterativeScanMode_Off_EmitsOffLiteral()
    {
        // The explicit "off" mode disables iterative scans — the GUC literal is
        // 'off', the pgvector default.
        var options = new IterativeScanOptions { Mode = IterativeScanMode.Off };

        var sql = SqlGenerator.BuildIterativeScanSimilarityQuery(
            "public", "t", DistanceMetric.Cosine, options, whereClause: null);

        await Assert.That(sql).Contains("SET LOCAL hnsw.iterative_scan = 'off';");
    }
}
