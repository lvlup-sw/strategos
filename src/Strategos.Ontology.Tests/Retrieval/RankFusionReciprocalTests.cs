// =============================================================================
// <copyright file="RankFusionReciprocalTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-B Task 13: Cormack 2009 §3.3 reference-vector parity for
/// <see cref="RankFusion.Reciprocal"/>. With <c>weights = null</c> and <c>k = 60</c>
/// the fused output must be bit-identical (within 1e-12) to the analytically
/// computed RRF formula <c>Σ_L 1 / (k + rank_L(d))</c>.
/// </summary>
/// <remarks>
/// Reference: Cormack, Clarke, Buettcher (SIGIR 2009) — "Reciprocal Rank Fusion
/// outperforms Condorcet and individual Rank Learning Methods" §3.3. Fixture
/// uses 3 rankers × 6 documents with overlapping and disjoint coverage so the
/// fused ordering exercises both the additive-presence and single-list cases.
/// </remarks>
public sealed class RankFusionReciprocalTests
{
    private const double Tolerance = 1e-12;
    private const int K = 60;

    // Cormack 2009 §3.3 worked-example fixture (3 rankers × 6 docs).
    // Doc D-A is top-ranked in S1 and S3, second-ranked in S2 → highest fused score.
    // Doc D-F appears only in S2 → lowest fused score among present docs.
    private static IReadOnlyList<IReadOnlyList<RankedCandidate>> Cormack2009Fixture() => new[]
    {
        // S1: A, B, C, D, E (5 docs)
        new[]
        {
            new RankedCandidate("D-A", 1),
            new RankedCandidate("D-B", 2),
            new RankedCandidate("D-C", 3),
            new RankedCandidate("D-D", 4),
            new RankedCandidate("D-E", 5),
        },
        // S2: B, A, E, F (4 docs)
        new[]
        {
            new RankedCandidate("D-B", 1),
            new RankedCandidate("D-A", 2),
            new RankedCandidate("D-E", 3),
            new RankedCandidate("D-F", 4),
        },
        // S3: A, C, E, D (4 docs)
        new[]
        {
            new RankedCandidate("D-A", 1),
            new RankedCandidate("D-C", 2),
            new RankedCandidate("D-E", 3),
            new RankedCandidate("D-D", 4),
        },
    };

    // Analytically computed expected fused scores for the fixture at k=60.
    // expected[docId] = Σ_L 1 / (60 + rank_L(docId))   (term ≡ 0 when docId ∉ L)
    private static IReadOnlyDictionary<string, double> ExpectedCormack2009Scores() =>
        new Dictionary<string, double>
        {
            // D-A: S1 rank 1 + S2 rank 2 + S3 rank 1 = 1/61 + 1/62 + 1/61
            ["D-A"] = (1.0 / 61.0) + (1.0 / 62.0) + (1.0 / 61.0),
            // D-B: S1 rank 2 + S2 rank 1 = 1/62 + 1/61
            ["D-B"] = (1.0 / 62.0) + (1.0 / 61.0),
            // D-E: S1 rank 5 + S2 rank 3 + S3 rank 3 = 1/65 + 1/63 + 1/63
            ["D-E"] = (1.0 / 65.0) + (1.0 / 63.0) + (1.0 / 63.0),
            // D-C: S1 rank 3 + S3 rank 2 = 1/63 + 1/62
            ["D-C"] = (1.0 / 63.0) + (1.0 / 62.0),
            // D-D: S1 rank 4 + S3 rank 4 = 1/64 + 1/64
            ["D-D"] = (1.0 / 64.0) + (1.0 / 64.0),
            // D-F: S2 rank 4 only = 1/64
            ["D-F"] = 1.0 / 64.0,
        };

    [Test]
    public async Task Reciprocal_UnweightedAgainstCormack2009Reference_BitIdentical()
    {
        var lists = Cormack2009Fixture();
        var expected = ExpectedCormack2009Scores();

        // topK = 10 to receive all 6 unique docs.
        var actual = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 10);

        await Assert.That(actual.Count).IsEqualTo(6);

        // Validate per-doc score parity within 1e-12.
        foreach (var fused in actual)
        {
            await Assert.That(expected.ContainsKey(fused.DocumentId)).IsTrue();
            var expectedScore = expected[fused.DocumentId];
            await Assert.That(Math.Abs(fused.FusedScore - expectedScore))
                .IsLessThanOrEqualTo(Tolerance);
        }

        // Validate ordering by fused score descending (stable tie-break).
        var byScoreDesc = expected
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key)
            .ToList();
        for (int i = 0; i < byScoreDesc.Count; i++)
        {
            await Assert.That(actual[i].DocumentId).IsEqualTo(byScoreDesc[i]);
            await Assert.That(actual[i].Rank).IsEqualTo(i + 1);
        }
    }

    // ---------------------------------------------------------------------
    // PR-B Task 14: weighted-vs-unweighted parity tests
    // ---------------------------------------------------------------------

    [Test]
    public async Task Reciprocal_AllOnesWeights_BitIdenticalToNullWeights()
    {
        var lists = Cormack2009Fixture();
        var unweighted = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 10);
        var ones = RankFusion.Reciprocal(lists, weights: new[] { 1.0, 1.0, 1.0 }, k: K, topK: 10);

        await Assert.That(ones.Count).IsEqualTo(unweighted.Count);
        for (int i = 0; i < ones.Count; i++)
        {
            await Assert.That(ones[i].DocumentId).IsEqualTo(unweighted[i].DocumentId);
            await Assert.That(ones[i].FusedScore).IsEqualTo(unweighted[i].FusedScore);
            await Assert.That(ones[i].Rank).IsEqualTo(unweighted[i].Rank);
        }
    }

    [Test]
    public async Task Reciprocal_WeightsLengthMismatch_ThrowsArgumentException()
    {
        var lists = Cormack2009Fixture();
        await Assert.That(() => RankFusion.Reciprocal(lists, weights: new[] { 1.0, 1.0 }, k: K, topK: 10))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Reciprocal_NegativeWeight_ThrowsArgumentException()
    {
        var lists = Cormack2009Fixture();
        await Assert.That(() => RankFusion.Reciprocal(lists, weights: new[] { 1.0, -0.5, 1.0 }, k: K, topK: 10))
            .Throws<ArgumentException>();
    }

    // ---------------------------------------------------------------------
    // PR-B Task 15: edge cases
    // ---------------------------------------------------------------------

    [Test]
    public async Task Reciprocal_EmptyInput_ReturnsEmptyList()
    {
        var actual = RankFusion.Reciprocal(
            Array.Empty<IReadOnlyList<RankedCandidate>>(),
            weights: null, k: K, topK: 10);
        await Assert.That(actual.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Reciprocal_AllListsEmpty_ReturnsEmptyList()
    {
        var lists = new IReadOnlyList<RankedCandidate>[]
        {
            Array.Empty<RankedCandidate>(),
            Array.Empty<RankedCandidate>(),
        };
        var actual = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 10);
        await Assert.That(actual.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Reciprocal_SingleListInput_EquivalentToTopKOverThatList()
    {
        var single = new[]
        {
            new RankedCandidate("X", 1),
            new RankedCandidate("Y", 2),
            new RankedCandidate("Z", 3),
        };
        var lists = new IReadOnlyList<RankedCandidate>[] { single };
        var actual = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 2);
        await Assert.That(actual.Count).IsEqualTo(2);
        await Assert.That(actual[0].DocumentId).IsEqualTo("X");
        await Assert.That(actual[0].FusedScore).IsEqualTo(1.0 / 61.0);
        await Assert.That(actual[0].Rank).IsEqualTo(1);
        await Assert.That(actual[1].DocumentId).IsEqualTo("Y");
        await Assert.That(actual[1].FusedScore).IsEqualTo(1.0 / 62.0);
        await Assert.That(actual[1].Rank).IsEqualTo(2);
    }

    [Test]
    public async Task Reciprocal_DisjointLists_AllUniqueDocsAppear()
    {
        var lists = new IReadOnlyList<RankedCandidate>[]
        {
            new[] { new RankedCandidate("A", 1), new RankedCandidate("B", 2) },
            new[] { new RankedCandidate("C", 1), new RankedCandidate("D", 2) },
        };
        var actual = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 10);
        await Assert.That(actual.Count).IsEqualTo(4);
        var docs = actual.Select(f => f.DocumentId).ToHashSet();
        await Assert.That(docs.Contains("A")).IsTrue();
        await Assert.That(docs.Contains("B")).IsTrue();
        await Assert.That(docs.Contains("C")).IsTrue();
        await Assert.That(docs.Contains("D")).IsTrue();
    }

    [Test]
    public async Task Reciprocal_FullOverlap_DocOrderingMatchesUnweightedRrf()
    {
        // Both lists contain {A,B,C} but in different orders. Doc with smaller
        // sum-of-ranks wins.
        var lists = new IReadOnlyList<RankedCandidate>[]
        {
            new[]
            {
                new RankedCandidate("A", 1),
                new RankedCandidate("B", 2),
                new RankedCandidate("C", 3),
            },
            new[]
            {
                new RankedCandidate("C", 1),
                new RankedCandidate("A", 2),
                new RankedCandidate("B", 3),
            },
        };
        var actual = RankFusion.Reciprocal(lists, weights: null, k: K, topK: 10);
        // A: 1/61 + 1/62 = 0.032598...
        // C: 1/63 + 1/61 = 0.032273... -> wait C is rank 3 in list 1 and rank 1 in list 2
        // C: 1/63 + 1/61
        // B: 1/62 + 1/63
        // A has the highest sum.
        await Assert.That(actual.Count).IsEqualTo(3);
        await Assert.That(actual[0].DocumentId).IsEqualTo("A");
        var expectedA = (1.0 / 61.0) + (1.0 / 62.0);
        await Assert.That(Math.Abs(actual[0].FusedScore - expectedA)).IsLessThanOrEqualTo(Tolerance);
    }

    [Test]
    public async Task Reciprocal_TopKZero_ReturnsEmptyList()
    {
        var actual = RankFusion.Reciprocal(Cormack2009Fixture(), weights: null, k: K, topK: 0);
        await Assert.That(actual.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Reciprocal_TopKGreaterThanUniqueDocs_ReturnsAllRanked()
    {
        var actual = RankFusion.Reciprocal(Cormack2009Fixture(), weights: null, k: K, topK: 1000);
        await Assert.That(actual.Count).IsEqualTo(6);
        // Verify ranks are 1..6 contiguously.
        for (int i = 0; i < actual.Count; i++)
        {
            await Assert.That(actual[i].Rank).IsEqualTo(i + 1);
        }
    }

    [Test]
    public async Task Reciprocal_KZero_ThrowsArgumentOutOfRangeException()
    {
        var lists = Cormack2009Fixture();
        await Assert.That(() => RankFusion.Reciprocal(lists, weights: null, k: 0, topK: 10))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Reciprocal_KNegative_ThrowsArgumentOutOfRangeException()
    {
        var lists = Cormack2009Fixture();
        await Assert.That(() => RankFusion.Reciprocal(lists, weights: null, k: -1, topK: 10))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Reciprocal_ZeroWeightList_ContributesNothing()
    {
        var lists = Cormack2009Fixture();

        // Fuse with weight 0 on S2: result must equal the fusion of S1+S3 only.
        var zeroed = RankFusion.Reciprocal(lists, weights: new[] { 1.0, 0.0, 1.0 }, k: K, topK: 10);

        var withoutS2 = new[] { lists[0], lists[2] };
        var dropped = RankFusion.Reciprocal(withoutS2, weights: null, k: K, topK: 10);

        await Assert.That(zeroed.Count).IsEqualTo(dropped.Count);
        for (int i = 0; i < zeroed.Count; i++)
        {
            await Assert.That(zeroed[i].DocumentId).IsEqualTo(dropped[i].DocumentId);
            await Assert.That(Math.Abs(zeroed[i].FusedScore - dropped[i].FusedScore))
                .IsLessThanOrEqualTo(Tolerance);
            await Assert.That(zeroed[i].Rank).IsEqualTo(dropped[i].Rank);
        }
    }
}
