// =============================================================================
// <copyright file="ReciprocalBenchmark.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using BenchmarkDotNet.Attributes;

using Strategos.Ontology.Retrieval;

namespace Strategos.Benchmarks.Subsystems.RankFusion;

/// <summary>
/// PR-B Task 21: BenchmarkDotNet entries for <see cref="Ontology.Retrieval.RankFusion.Reciprocal"/>.
/// Configurations: 2 lists × 100 candidates with both disjoint and overlapping inputs.
/// Acceptance gate: median &lt; 1 ms on x64.
/// </summary>
[MemoryDiagnoser]
public class ReciprocalBenchmark
{
    private const int CandidatesPerList = 100;

    private IReadOnlyList<IReadOnlyList<RankedCandidate>> disjointLists = null!;
    private IReadOnlyList<IReadOnlyList<RankedCandidate>> overlappingLists = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Disjoint: list 1 contains docs 0..99, list 2 contains docs 100..199.
        this.disjointLists = new IReadOnlyList<RankedCandidate>[]
        {
            BuildList(offset: 0),
            BuildList(offset: CandidatesPerList),
        };

        // Overlapping: list 1 docs 0..99, list 2 docs 50..149.
        this.overlappingLists = new IReadOnlyList<RankedCandidate>[]
        {
            BuildList(offset: 0),
            BuildList(offset: CandidatesPerList / 2),
        };
    }

    private static IReadOnlyList<RankedCandidate> BuildList(int offset)
    {
        var list = new RankedCandidate[CandidatesPerList];
        for (int i = 0; i < CandidatesPerList; i++)
        {
            list[i] = new RankedCandidate($"doc-{offset + i:D6}", i + 1);
        }

        return list;
    }

    [Benchmark]
    public IReadOnlyList<FusedResult> Reciprocal_TwoLists_Disjoint_TopK10()
        => Ontology.Retrieval.RankFusion.Reciprocal(this.disjointLists, weights: null, k: 60, topK: 10);

    [Benchmark]
    public IReadOnlyList<FusedResult> Reciprocal_TwoLists_Overlapping_TopK10()
        => Ontology.Retrieval.RankFusion.Reciprocal(this.overlappingLists, weights: null, k: 60, topK: 10);
}
