// =============================================================================
// <copyright file="ActionCompositionProofBenchmarks.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Numerics;

using BenchmarkDotNet.Attributes;

using Strategos.Ontology.Descriptors;

namespace Strategos.Benchmarks.Subsystems.ActionProofs;

/// <summary>
/// Measures the exact finite-domain action-composition proof kernel.
/// </summary>
/// <remarks>
/// These are observational benchmarks. They intentionally define no latency
/// threshold: correctness tests, rather than wall-clock measurements, gate CI.
/// </remarks>
[MemoryDiagnoser]
public class ActionCompositionProofBenchmarks
{
    private static readonly ActionSubject Subject = new("benchmarks", "Order");
    private static readonly AuthorityLattice EmptyLattice = new([], []);

    private ActionDescriptor upstream = null!;
    private ActionDescriptor provenDownstream = null!;
    private ActionDescriptor refutedDownstream = null!;
    private ActionDescriptor projection = null!;

    /// <summary>Gets or sets the number of constants that induce numeric domain cells.</summary>
    [Params(4, 16, 64)]
    public int ConstantCount { get; set; }

    /// <summary>Builds closed contracts whose integer domain has the requested partition width.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var alternatives = Enumerable.Range(0, ConstantCount)
            .Select(value => Score(PredicateComparisonOperator.Equal, value))
            .ToArray();
        var producedValues = ActionPredicate.Any(alternatives);

        upstream = Action(
            "choose-score",
            ensures: producedValues,
            frame: [ActionResource.Property("Score")]);
        provenDownstream = Action(
            "consume-range",
            requires: ActionPredicate.All(
                Score(PredicateComparisonOperator.GreaterThanOrEqual, 0),
                Score(PredicateComparisonOperator.LessThan, ConstantCount)));
        refutedDownstream = Action(
            "consume-narrow-range",
            requires: Score(PredicateComparisonOperator.LessThan, ConstantCount - 1));
        projection = Action(
            "rewrite-score",
            requires: ActionPredicate.Any(
                producedValues,
                ActionPredicate.Property(
                    new PredicatePropertyReference("Region", PredicateScalarKind.String),
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.String("us"))),
            frame: [ActionResource.Property("Score")]);
    }

    /// <summary>Proves a numeric disjunction implies its enclosing range.</summary>
    [Benchmark(Baseline = true)]
    public ActionCompositionAnalysis ProveNumericSeam() =>
        ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, provenDownstream);

    /// <summary>Finds and minimizes the stable counterexample to an invalid seam.</summary>
    [Benchmark]
    public ActionCompositionAnalysis RefuteNumericSeam() =>
        ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, refutedDownstream);

    /// <summary>Existentially projects a written property from a disjunction.</summary>
    [Benchmark]
    public ActionCompositionAnalysis ForgetWrittenResource() =>
        ActionCalculus.AnalyzeSequential(EmptyLattice, projection);

    private static ActionDescriptor Action(
        string name,
        ActionPredicate? requires = null,
        ActionPredicate? ensures = null,
        IReadOnlyList<ActionResource>? frame = null) => new(Subject, name, name)
        {
            Preconditions = requires is null
                ? []
                : [new ActionPrecondition(requires, requires.Expression)],
            Ensures = ensures is null
                ? []
                : [new ActionGuarantee(ensures)],
            TouchedResources = frame ?? [],
        };

    private static ActionPredicate Score(
        PredicateComparisonOperator comparison,
        int value) => ActionPredicate.Property(
            new PredicatePropertyReference("Score", PredicateScalarKind.Integer),
            comparison,
            PredicateLiteral.Integer(new BigInteger(value)));
}
