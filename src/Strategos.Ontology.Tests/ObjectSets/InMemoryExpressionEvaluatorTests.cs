using System.Linq.Expressions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

// ---------------------------------------------------------------------------
// Test domain types
// ---------------------------------------------------------------------------

public interface IEvalInterface;

public class EvalSource : IEvalInterface
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public class EvalTarget
{
    public string Label { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Test domain ontology
// ---------------------------------------------------------------------------

public class EvalTestOntology : DomainOntology
{
    public override string DomainName => "eval";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<EvalSource>(obj =>
        {
            obj.Key(s => s.Name);
            obj.Property(s => s.Value);
            obj.HasMany<EvalTarget>("targets");
        });

        builder.Object<EvalTarget>(obj =>
        {
            obj.Key(t => t.Label);
        });
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class InMemoryExpressionEvaluatorTests
{
    private OntologyGraph _graph = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = BuildTestGraph();
    }

    private static OntologyGraph BuildTestGraph()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<EvalTestOntology>();
        return graphBuilder.Build();
    }

    private static Func<string, IReadOnlyList<object>> BuildTestResolver(
        IReadOnlyList<EvalSource>? sources = null,
        IReadOnlyList<EvalTarget>? targets = null)
    {
        sources ??= [new EvalSource { Name = "A", Value = 10 }, new EvalSource { Name = "B", Value = 3 }];
        targets ??= [new EvalTarget { Label = "X" }, new EvalTarget { Label = "Y" }];

        return descriptorName => descriptorName switch
        {
            "EvalSource" => sources.Cast<object>().ToList(),
            "EvalTarget" => targets.Cast<object>().ToList(),
            _ => []
        };
    }

    // -----------------------------------------------------------------------
    // Task 7 tests
    // -----------------------------------------------------------------------

    [Test]
    public async Task Evaluate_RootExpression_ReturnsAllItems()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var expression = new RootExpression(typeof(EvalSource), "EvalSource");
        var resolver = BuildTestResolver();

        var result = evaluator.Evaluate<EvalSource>(expression, resolver);

        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result[0].Name).IsEqualTo("A");
        await Assert.That(result[1].Name).IsEqualTo("B");
    }

    [Test]
    public async Task Evaluate_FilterExpression_AppliesPredicate()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        Expression<Func<EvalSource, bool>> predicate = s => s.Value > 5;
        var filter = new FilterExpression(root, predicate);
        var resolver = BuildTestResolver();

        var result = evaluator.Evaluate<EvalSource>(filter, resolver);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("A");
    }

    [Test]
    public async Task Evaluate_FilterChain_AppliesAllPredicates()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var sources = new List<EvalSource>
        {
            new() { Name = "A", Value = 10 },
            new() { Name = "B", Value = 3 },
            new() { Name = "C", Value = 8 }
        };
        var resolver = BuildTestResolver(sources: sources);

        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        Expression<Func<EvalSource, bool>> pred1 = s => s.Value > 5;
        Expression<Func<EvalSource, bool>> pred2 = s => s.Name != "C";
        var filter1 = new FilterExpression(root, pred1);
        var filter2 = new FilterExpression(filter1, pred2);

        var result = evaluator.Evaluate<EvalSource>(filter2, resolver);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("A");
    }

    [Test]
    public async Task Evaluate_IncludeExpression_PassesThrough()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var include = new IncludeExpression(root, ObjectSetInclusion.Properties);
        var resolver = BuildTestResolver();

        var result = evaluator.Evaluate<EvalSource>(include, resolver);

        await Assert.That(result).HasCount().EqualTo(2);
    }

    [Test]
    public async Task Evaluate_EmptyItemResolver_ReturnsEmpty()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        Func<string, IReadOnlyList<object>> emptyResolver = _ => [];

        var result = evaluator.Evaluate<EvalSource>(root, emptyResolver);

        await Assert.That(result).HasCount().EqualTo(0);
    }
}
