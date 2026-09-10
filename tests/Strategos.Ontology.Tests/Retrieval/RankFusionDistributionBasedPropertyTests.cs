// =============================================================================
// <copyright file="RankFusionDistributionBasedPropertyTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-B Task 20: Algebraic invariants of <see cref="RankFusion.DistributionBased"/>.
/// </summary>
public sealed class RankFusionDistributionBasedPropertyTests
{
    private const double Tolerance = 1e-9;
    private const int Seeds = 20;

    private static IReadOnlyList<IReadOnlyList<ScoredCandidate>> RandomLists(int seed)
    {
        var rng = new Random(seed);
        var listCount = 2 + rng.Next(2); // 2 or 3 lists
        var lists = new List<IReadOnlyList<ScoredCandidate>>(listCount);
        for (int li = 0; li < listCount; li++)
        {
            int n = 3 + rng.Next(8); // 3..10 docs per list (≥3 so σ varies meaningfully)
            var docs = Enumerable.Range(0, n)
                .Select(i => new ScoredCandidate($"doc-{li}-{i:D3}", rng.NextDouble() * 10.0 - 5.0))
                .ToList();
            lists.Add(docs);
        }

        return lists;
    }

    [Test]
    public async Task DistributionBased_TranslationInvariance_AddingConstantOffsetPreservesOutput()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var lists = RandomLists(seed);
            var baseline = RankFusion.DistributionBased(lists, weights: null, topK: 1000);

            // Add a different constant offset to each list.
            const double offset0 = 12.5;
            const double offset1 = -7.25;
            const double offset2 = 100.0;
            var translated = new List<IReadOnlyList<ScoredCandidate>>(lists.Count);
            for (int li = 0; li < lists.Count; li++)
            {
                double off = li == 0 ? offset0 : (li == 1 ? offset1 : offset2);
                translated.Add(lists[li].Select(c => new ScoredCandidate(c.DocumentId, c.Score + off)).ToList());
            }

            var actual = RankFusion.DistributionBased(translated, weights: null, topK: 1000);

            await Assert.That(actual.Count).IsEqualTo(baseline.Count);
            for (int i = 0; i < baseline.Count; i++)
            {
                await Assert.That(actual[i].DocumentId).IsEqualTo(baseline[i].DocumentId);
                await Assert.That(actual[i].Rank).IsEqualTo(baseline[i].Rank);
                await Assert.That(Math.Abs(actual[i].FusedScore - baseline[i].FusedScore))
                    .IsLessThanOrEqualTo(Tolerance);
            }
        }
    }

    [Test]
    public async Task DistributionBased_ScaleInvariance_MultiplyingByPositiveConstantPreservesOutput()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var lists = RandomLists(seed);
            var baseline = RankFusion.DistributionBased(lists, weights: null, topK: 1000);

            const double scale0 = 17.0;
            const double scale1 = 0.25;
            const double scale2 = 3.5;
            var scaled = new List<IReadOnlyList<ScoredCandidate>>(lists.Count);
            for (int li = 0; li < lists.Count; li++)
            {
                double s = li == 0 ? scale0 : (li == 1 ? scale1 : scale2);
                scaled.Add(lists[li].Select(c => new ScoredCandidate(c.DocumentId, c.Score * s)).ToList());
            }

            var actual = RankFusion.DistributionBased(scaled, weights: null, topK: 1000);

            await Assert.That(actual.Count).IsEqualTo(baseline.Count);
            for (int i = 0; i < baseline.Count; i++)
            {
                await Assert.That(actual[i].DocumentId).IsEqualTo(baseline[i].DocumentId);
                await Assert.That(Math.Abs(actual[i].FusedScore - baseline[i].FusedScore))
                    .IsLessThanOrEqualTo(Tolerance);
            }
        }
    }

    [Test]
    public async Task DistributionBased_WeightMonotonicity_IncreasingWeightIncreasesContribution()
    {
        // For a fixed two-list query, varying weights[0] across {0.5, 1.0, 2.0}
        // should monotonically increase the contribution of list 0 to docs that
        // are ONLY in list 0.
        var listA = new[]
        {
            new ScoredCandidate("only-A1", 5.0),
            new ScoredCandidate("only-A2", 3.0),
            new ScoredCandidate("only-A3", 1.0),
        };
        var listB = new[]
        {
            new ScoredCandidate("only-B1", 5.0),
            new ScoredCandidate("only-B2", 3.0),
            new ScoredCandidate("only-B3", 1.0),
        };
        var lists = new IReadOnlyList<ScoredCandidate>[] { listA, listB };

        var lo = RankFusion.DistributionBased(lists, weights: new[] { 0.5, 1.0 }, topK: 10)
            .First(f => f.DocumentId == "only-A1").FusedScore;
        var mid = RankFusion.DistributionBased(lists, weights: new[] { 1.0, 1.0 }, topK: 10)
            .First(f => f.DocumentId == "only-A1").FusedScore;
        var hi = RankFusion.DistributionBased(lists, weights: new[] { 2.0, 1.0 }, topK: 10)
            .First(f => f.DocumentId == "only-A1").FusedScore;

        await Assert.That(lo).IsLessThan(mid);
        await Assert.That(mid).IsLessThan(hi);
    }
}
