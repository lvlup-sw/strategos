// =============================================================================
// <copyright file="DistributionBasedBenchmark.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using BenchmarkDotNet.Attributes;

using Strategos.Ontology.Retrieval;

namespace Strategos.Benchmarks.Subsystems.RankFusion;

/// <summary>
/// PR-B Task 21: BenchmarkDotNet entries for <see cref="Ontology.Retrieval.RankFusion.DistributionBased"/>.
/// Configurations: 2 lists × 100 candidates with both disjoint and overlapping inputs.
/// Acceptance gate: median &lt; 1 ms on x64.
/// </summary>
[MemoryDiagnoser]
public class DistributionBasedBenchmark
{
    private const int CandidatesPerList = 100;

    private IReadOnlyList<IReadOnlyList<ScoredCandidate>> disjointLists = null!;
    private IReadOnlyList<IReadOnlyList<ScoredCandidate>> overlappingLists = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Deterministic scores via a seeded RNG.
        this.disjointLists = new IReadOnlyList<ScoredCandidate>[]
        {
            BuildList(offset: 0, seed: 1),
            BuildList(offset: CandidatesPerList, seed: 2),
        };

        this.overlappingLists = new IReadOnlyList<ScoredCandidate>[]
        {
            BuildList(offset: 0, seed: 1),
            BuildList(offset: CandidatesPerList / 2, seed: 2),
        };
    }

    private static IReadOnlyList<ScoredCandidate> BuildList(int offset, int seed)
    {
        var rng = new Random(seed);
        var list = new ScoredCandidate[CandidatesPerList];
        for (int i = 0; i < CandidatesPerList; i++)
        {
            list[i] = new ScoredCandidate($"doc-{offset + i:D6}", rng.NextDouble() * 10.0);
        }

        return list;
    }

    [Benchmark]
    public IReadOnlyList<FusedResult> DistributionBased_TwoLists_Disjoint_TopK10()
        => Ontology.Retrieval.RankFusion.DistributionBased(this.disjointLists, weights: null, topK: 10);

    [Benchmark]
    public IReadOnlyList<FusedResult> DistributionBased_TwoLists_Overlapping_TopK10()
        => Ontology.Retrieval.RankFusion.DistributionBased(this.overlappingLists, weights: null, topK: 10);
}
