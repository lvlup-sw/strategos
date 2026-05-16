// =============================================================================
// <copyright file="RankFusionDistributionBasedTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-B Tasks 18 and 19: <see cref="RankFusion.DistributionBased"/> Qdrant parity
/// + edge cases.
/// </summary>
public sealed class RankFusionDistributionBasedTests
{
    private const double ParityTolerance = 1e-9;
    private const double EdgeTolerance = 1e-9;

    private static readonly string OraclePath = Path.Combine(
        AppContext.BaseDirectory, "Retrieval", "Fixtures", "qdrant-dbsf-oracle.json");

    private sealed record OracleScored(string DocumentId, double Score);
    private sealed record OracleExpected(string DocumentId, double FusedScore, int Rank);
    private sealed record OracleQuery(
        string QueryId,
        string Description,
        IReadOnlyList<IReadOnlyList<OracleScored>> Lists,
        IReadOnlyList<double>? Weights,
        int TopK,
        IReadOnlyList<OracleExpected> ExpectedFused);

    private static IReadOnlyList<OracleQuery> LoadOracle()
    {
        using var stream = File.OpenRead(OraclePath);
        using var doc = JsonDocument.Parse(stream);
        var queries = doc.RootElement.GetProperty("queries");
        var results = new List<OracleQuery>();
        foreach (var q in queries.EnumerateArray())
        {
            var lists = new List<IReadOnlyList<OracleScored>>();
            foreach (var lst in q.GetProperty("lists").EnumerateArray())
            {
                var items = new List<OracleScored>();
                foreach (var item in lst.EnumerateArray())
                {
                    items.Add(new OracleScored(
                        item.GetProperty("document_id").GetString()!,
                        item.GetProperty("score").GetDouble()));
                }

                lists.Add(items);
            }

            IReadOnlyList<double>? weights = null;
            var weightsEl = q.GetProperty("weights");
            if (weightsEl.ValueKind == JsonValueKind.Array)
            {
                weights = weightsEl.EnumerateArray().Select(e => e.GetDouble()).ToList();
            }

            var expected = new List<OracleExpected>();
            foreach (var ex in q.GetProperty("expected_fused").EnumerateArray())
            {
                expected.Add(new OracleExpected(
                    ex.GetProperty("document_id").GetString()!,
                    ex.GetProperty("fused_score").GetDouble(),
                    ex.GetProperty("rank").GetInt32()));
            }

            results.Add(new OracleQuery(
                q.GetProperty("query_id").GetString()!,
                q.GetProperty("description").GetString()!,
                lists,
                weights,
                q.GetProperty("top_k").GetInt32(),
                expected));
        }

        return results;
    }

    [Test]
    public async Task DistributionBased_AgainstQdrantOracle_AllQueriesMatch_Within1eMinus9()
    {
        var oracle = LoadOracle();
        await Assert.That(oracle.Count).IsEqualTo(6);

        foreach (var query in oracle)
        {
            var lists = query.Lists
                .Select(l => (IReadOnlyList<ScoredCandidate>)l
                    .Select(o => new ScoredCandidate(o.DocumentId, o.Score))
                    .ToList())
                .ToList();

            var actual = RankFusion.DistributionBased(
                lists,
                weights: query.Weights,
                topK: query.TopK);

            // Same length.
            await Assert.That(actual.Count)
                .IsEqualTo(query.ExpectedFused.Count);

            // Per-doc score within tolerance + identical ordering + rank.
            for (int i = 0; i < actual.Count; i++)
            {
                var e = query.ExpectedFused[i];
                var a = actual[i];
                await Assert.That(a.DocumentId).IsEqualTo(e.DocumentId);
                await Assert.That(a.Rank).IsEqualTo(e.Rank);
                await Assert.That(Math.Abs(a.FusedScore - e.FusedScore))
                    .IsLessThanOrEqualTo(ParityTolerance);
            }
        }
    }

    // ---------------------------------------------------------------------
    // PR-B Task 19: edge cases
    // ---------------------------------------------------------------------

    [Test]
    public async Task DistributionBased_EmptyInput_ReturnsEmptyList()
    {
        var actual = RankFusion.DistributionBased(
            Array.Empty<IReadOnlyList<ScoredCandidate>>(),
            weights: null,
            topK: 10);
        await Assert.That(actual.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DistributionBased_SingleElementList_NormalizesToHalf()
    {
        var lists = new IReadOnlyList<ScoredCandidate>[]
        {
            new[] { new ScoredCandidate("X", 99.0) },
        };
        var actual = RankFusion.DistributionBased(lists, weights: null, topK: 10);
        await Assert.That(actual.Count).IsEqualTo(1);
        await Assert.That(actual[0].DocumentId).IsEqualTo("X");
        await Assert.That(Math.Abs(actual[0].FusedScore - 0.5)).IsLessThanOrEqualTo(EdgeTolerance);
    }

    [Test]
    public async Task DistributionBased_ZeroVarianceList_AllElementsNormalizeToHalf()
    {
        var lists = new IReadOnlyList<ScoredCandidate>[]
        {
            new[]
            {
                new ScoredCandidate("A", 5.0),
                new ScoredCandidate("B", 5.0),
                new ScoredCandidate("C", 5.0),
            },
        };
        var actual = RankFusion.DistributionBased(lists, weights: null, topK: 10);
        await Assert.That(actual.Count).IsEqualTo(3);
        foreach (var fused in actual)
        {
            await Assert.That(Math.Abs(fused.FusedScore - 0.5)).IsLessThanOrEqualTo(EdgeTolerance);
        }
    }

    [Test]
    public async Task DistributionBased_MixedPositiveAndNegativeScores_NormalizesCorrectly()
    {
        var lists = new IReadOnlyList<ScoredCandidate>[]
        {
            new[]
            {
                new ScoredCandidate("A", 1.0),
                new ScoredCandidate("B", 0.0),
                new ScoredCandidate("C", -1.0),
            },
        };
        var actual = RankFusion.DistributionBased(lists, weights: null, topK: 10);
        // Mean = 0, sigma = sqrt(2/3) ≈ 0.8165, low = -2.449, high = +2.449,
        // so all scores lie in [low, high] and normalize linearly.
        // Highest score "A" should be greater than B, then C.
        await Assert.That(actual.Count).IsEqualTo(3);
        await Assert.That(actual[0].DocumentId).IsEqualTo("A");
        await Assert.That(actual[1].DocumentId).IsEqualTo("B");
        await Assert.That(actual[2].DocumentId).IsEqualTo("C");
        await Assert.That(actual[0].FusedScore).IsGreaterThan(actual[1].FusedScore);
        await Assert.That(actual[1].FusedScore).IsGreaterThan(actual[2].FusedScore);
    }

    [Test]
    public async Task DistributionBased_OutlierPath_TracksExpectedOrdering()
    {
        // Same data as the oracle's q4-outlier-heavy: an outlier in list 2 that
        // DBSF should clamp. Compare to a hand-rolled min-max baseline to assert
        // DBSF orders differently.
        var lists = new IReadOnlyList<ScoredCandidate>[]
        {
            new[]
            {
                new ScoredCandidate("d-A", 1.0),
                new ScoredCandidate("d-B", 0.8),
                new ScoredCandidate("d-C", 0.6),
                new ScoredCandidate("d-D", 0.4),
            },
            new[]
            {
                new ScoredCandidate("d-A", 100.0), // outlier
                new ScoredCandidate("d-B", 2.0),
                new ScoredCandidate("d-C", 1.5),
                new ScoredCandidate("d-D", 1.0),
            },
        };

        var dbsf = RankFusion.DistributionBased(lists, weights: null, topK: 10);

        // Min-max baseline: per-list min-max normalize to [0,1] then sum.
        // d-A in list 2 → 1.0 (max), so min-max would pin its contribution at 1.0.
        // Compute and check ordering MAY differ from DBSF (DBSF clamps the outlier).
        // The KEY assertion: DBSF's gap between d-A and d-B is smaller than min-max's,
        // because DBSF clamps d-A's outlier list-2 score to μ+3σ.
        var minMax = MinMaxBaseline(lists);
        var dbsfGapAB = dbsf[0].FusedScore - dbsf[1].FusedScore;
        var mmGapAB = minMax["d-A"] - minMax["d-B"];
        await Assert.That(dbsfGapAB).IsLessThan(mmGapAB);
    }

    private static Dictionary<string, double> MinMaxBaseline(IReadOnlyList<IReadOnlyList<ScoredCandidate>> lists)
    {
        var sums = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var lst in lists)
        {
            if (lst.Count == 0) continue;
            double min = lst.Min(c => c.Score);
            double max = lst.Max(c => c.Score);
            double span = max - min;
            foreach (var c in lst)
            {
                double n = span < 1e-9 ? 0.5 : (c.Score - min) / span;
                sums[c.DocumentId] = sums.GetValueOrDefault(c.DocumentId, 0.0) + n;
            }
        }

        return sums;
    }

    [Test]
    public async Task DistributionBased_WeightsLengthMismatch_ThrowsArgumentException()
    {
        var lists = new IReadOnlyList<ScoredCandidate>[]
        {
            new[] { new ScoredCandidate("A", 1.0), new ScoredCandidate("B", 0.5) },
            new[] { new ScoredCandidate("A", 0.9) },
        };
        await Assert.That(() => RankFusion.DistributionBased(lists, weights: new[] { 1.0 }, topK: 10))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task DistributionBased_NegativeWeight_ThrowsArgumentException()
    {
        var lists = new IReadOnlyList<ScoredCandidate>[]
        {
            new[] { new ScoredCandidate("A", 1.0), new ScoredCandidate("B", 0.5) },
            new[] { new ScoredCandidate("A", 0.9), new ScoredCandidate("B", 0.4) },
        };
        await Assert.That(() => RankFusion.DistributionBased(lists, weights: new[] { 1.0, -0.5 }, topK: 10))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task DistributionBased_TopKZero_ReturnsEmptyList()
    {
        var lists = new IReadOnlyList<ScoredCandidate>[]
        {
            new[] { new ScoredCandidate("A", 1.0), new ScoredCandidate("B", 0.5) },
        };
        var actual = RankFusion.DistributionBased(lists, weights: null, topK: 0);
        await Assert.That(actual.Count).IsEqualTo(0);
    }
}
