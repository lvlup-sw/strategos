// =============================================================================
// <copyright file="RankFusionBenchmarkTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;

using Strategos.Benchmarks.Subsystems.RankFusion;

namespace Strategos.Benchmarks.Tests;

/// <summary>
/// PR-B Task 21 (step 2): TUnit smoke gate paired with the BenchmarkDotNet
/// entries in <see cref="ReciprocalBenchmark"/> and <see cref="DistributionBasedBenchmark"/>.
/// Asserts that 10 invocations of the 2-list × 100-candidate fusion complete
/// well within a coarse 50 ms wall budget — non-CI-flake gate. True perf
/// characterization is left to local BenchmarkDotNet runs.
/// </summary>
[Property("Category", "Benchmark")]
public sealed class RankFusionBenchmarkTests
{
    private const int Invocations = 10;
    private const int WallBudgetMs = 50;

    [Test]
    public async Task ReciprocalBenchmark_TenInvocations_CompletesWithin50ms()
    {
        var benchmark = new ReciprocalBenchmark();
        benchmark.GlobalSetup();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Invocations; i++)
        {
            _ = benchmark.Reciprocal_TwoLists_Disjoint_TopK10();
            _ = benchmark.Reciprocal_TwoLists_Overlapping_TopK10();
        }

        sw.Stop();
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(WallBudgetMs);
    }

    [Test]
    public async Task DistributionBasedBenchmark_TenInvocations_CompletesWithin50ms()
    {
        var benchmark = new DistributionBasedBenchmark();
        benchmark.GlobalSetup();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Invocations; i++)
        {
            _ = benchmark.DistributionBased_TwoLists_Disjoint_TopK10();
            _ = benchmark.DistributionBased_TwoLists_Overlapping_TopK10();
        }

        sw.Stop();
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(WallBudgetMs);
    }
}
