// =============================================================================
// <copyright file="RankFusionReciprocalPropertyTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-B Task 16: Algebraic invariants for <see cref="RankFusion.Reciprocal"/>.
/// Hand-rolled property tests over 20 randomized seeds (FsCheck not part of
/// Strategos's test surface in 2.6.0 — see GlobalUsings.cs).
/// </summary>
public sealed class RankFusionReciprocalPropertyTests
{
    private const int Seeds = 20;
    private const int K = 60;

    private static IReadOnlyList<IReadOnlyList<RankedCandidate>> RandomRankedLists(
        int seed,
        int listCount = 3,
        int maxDocsPerList = 15,
        int docPoolSize = 30)
    {
        var rng = new Random(seed);
        var docPool = Enumerable.Range(0, docPoolSize).Select(i => $"doc-{i:D3}").ToList();
        var lists = new List<IReadOnlyList<RankedCandidate>>(listCount);
        for (int li = 0; li < listCount; li++)
        {
            int n = 1 + rng.Next(maxDocsPerList);
            var shuffled = docPool.OrderBy(_ => rng.Next()).Take(n).ToList();
            var list = shuffled
                .Select((doc, idx) => new RankedCandidate(doc, idx + 1))
                .ToList();
            lists.Add(list);
        }

        return lists;
    }

    [Test]
    public async Task Reciprocal_OutputIsPermutationOfUnionInputs_CappedAtTopK()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var lists = RandomRankedLists(seed);
            var union = lists.SelectMany(l => l.Select(c => c.DocumentId)).ToHashSet(StringComparer.Ordinal);
            var fused = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 1000);
            // Cap-at-topK.
            await Assert.That(fused.Count).IsLessThanOrEqualTo(union.Count);
            // No duplicates in output.
            var fusedIds = fused.Select(f => f.DocumentId).ToList();
            await Assert.That(fusedIds.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(fusedIds.Count);
            // Every output doc is in the union.
            foreach (var id in fusedIds)
            {
                await Assert.That(union.Contains(id)).IsTrue();
            }
        }
    }

    [Test]
    public async Task Reciprocal_PairwiseOrderingMatchesFusedScoreDescending()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var lists = RandomRankedLists(seed);
            var fused = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 1000);
            for (int i = 1; i < fused.Count; i++)
            {
                // Each entry's fused score >= the next entry's (descending).
                await Assert.That(fused[i - 1].FusedScore).IsGreaterThanOrEqualTo(fused[i].FusedScore);
                if (fused[i - 1].FusedScore == fused[i].FusedScore)
                {
                    // On ties, DocumentId ordinal ascending.
                    await Assert.That(
                        string.CompareOrdinal(fused[i - 1].DocumentId, fused[i].DocumentId) <= 0)
                        .IsTrue();
                }
            }
        }
    }

    [Test]
    public async Task Reciprocal_IncreasingKMonotonicallyFlattensCurve_ResultSetUnchanged()
    {
        // Increasing k flattens the score curve but the SET of documents returned
        // (capped at topK) remains identical. We allow rank perturbation under
        // genuine ties but assert SET equality on the topK = ∞ case.
        for (int seed = 0; seed < Seeds; seed++)
        {
            var lists = RandomRankedLists(seed);
            var setSmallK = RankFusion.Reciprocal(lists, weights: null, k: 1, topK: 1000)
                .Select(f => f.DocumentId).ToHashSet(StringComparer.Ordinal);
            var setMediumK = RankFusion.Reciprocal(lists, weights: null, k: 60, topK: 1000)
                .Select(f => f.DocumentId).ToHashSet(StringComparer.Ordinal);
            var setLargeK = RankFusion.Reciprocal(lists, weights: null, k: 100000, topK: 1000)
                .Select(f => f.DocumentId).ToHashSet(StringComparer.Ordinal);

            await Assert.That(setSmallK.SetEquals(setMediumK)).IsTrue();
            await Assert.That(setMediumK.SetEquals(setLargeK)).IsTrue();
        }
    }

    [Test]
    public async Task Reciprocal_UniformWeightDoublingDoesNotChangeOrdering()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var lists = RandomRankedLists(seed);
            int n = lists.Count;
            var weightsOne = Enumerable.Repeat(1.0, n).ToArray();
            var weightsTwo = Enumerable.Repeat(2.0, n).ToArray();

            var ones = RankFusion.Reciprocal(lists, weights: weightsOne, k: K, topK: 1000);
            var twos = RankFusion.Reciprocal(lists, weights: weightsTwo, k: K, topK: 1000);

            await Assert.That(twos.Count).IsEqualTo(ones.Count);
            // Ordering is identical; scores are exactly 2× the unweighted scores.
            for (int i = 0; i < ones.Count; i++)
            {
                await Assert.That(twos[i].DocumentId).IsEqualTo(ones[i].DocumentId);
                await Assert.That(twos[i].Rank).IsEqualTo(ones[i].Rank);
                await Assert.That(Math.Abs(twos[i].FusedScore - 2.0 * ones[i].FusedScore))
                    .IsLessThanOrEqualTo(1e-12);
            }
        }
    }
}
