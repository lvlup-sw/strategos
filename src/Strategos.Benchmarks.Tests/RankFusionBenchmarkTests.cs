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
/// Asserts functional completion always, and a coarse wall-budget ceiling only
/// when not running in CI to avoid shared-runner flake. True perf characterization
/// is left to local BenchmarkDotNet runs.
/// </summary>
[Property("Category", "Benchmark")]
public sealed class RankFusionBenchmarkTests
{
    private const int Invocations = 10;
    private const int WallBudgetMs = 200;

    private static bool RunningOnCi =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    [Test]
    public async Task ReciprocalBenchmark_TenInvocations_CompletesWithinWallBudget()
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
        if (!RunningOnCi)
        {
            await Assert.That(sw.ElapsedMilliseconds).IsLessThan(WallBudgetMs);
        }
    }

    [Test]
    public async Task DistributionBasedBenchmark_TenInvocations_CompletesWithinWallBudget()
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
        if (!RunningOnCi)
        {
            await Assert.That(sw.ElapsedMilliseconds).IsLessThan(WallBudgetMs);
        }
    }
}
