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

        await Assert.That(result).Count().IsEqualTo(2);
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

        await Assert.That(result).Count().IsEqualTo(1);
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

        await Assert.That(result).Count().IsEqualTo(1);
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

        await Assert.That(result).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Evaluate_EmptyItemResolver_ReturnsEmpty()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        Func<string, IReadOnlyList<object>> emptyResolver = _ => [];

        var result = evaluator.Evaluate<EvalSource>(root, emptyResolver);

        await Assert.That(result).Count().IsEqualTo(0);
    }

    // -----------------------------------------------------------------------
    // Task 8 tests — TraverseLinkExpression
    // -----------------------------------------------------------------------

    [Test]
    public async Task Evaluate_TraverseLink_ReturnsTargetTypeItems()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var traverse = new TraverseLinkExpression(root, "targets", typeof(EvalTarget));
        var resolver = BuildTestResolver();

        var result = evaluator.Evaluate<EvalTarget>(traverse, resolver);

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Label).IsEqualTo("X");
        await Assert.That(result[1].Label).IsEqualTo("Y");
    }

    [Test]
    public async Task Evaluate_TraverseLink_ThenFilter_FiltersTargetItems()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var traverse = new TraverseLinkExpression(root, "targets", typeof(EvalTarget));
        Expression<Func<EvalTarget, bool>> predicate = t => t.Label == "X";
        var filter = new FilterExpression(traverse, predicate);
        var resolver = BuildTestResolver();

        var result = evaluator.Evaluate<EvalTarget>(filter, resolver);

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Label).IsEqualTo("X");
    }

    [Test]
    public async Task Evaluate_TraverseLink_UnknownLink_Throws()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var traverse = new TraverseLinkExpression(root, "nonexistent", typeof(EvalTarget));
        var resolver = BuildTestResolver();

        await Assert.That(() => evaluator.Evaluate<EvalTarget>(traverse, resolver))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    // -----------------------------------------------------------------------
    // Task 9 tests — InterfaceNarrowExpression
    // -----------------------------------------------------------------------

    [Test]
    public async Task Evaluate_InterfaceNarrow_FiltersToImplementors()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        // Seed both EvalSource (implements IEvalInterface) and EvalTarget (does not)
        var sources = new List<EvalSource> { new() { Name = "S1", Value = 1 } };
        var resolver = BuildTestResolver(sources: sources);

        // Start from Root("EvalSource"), then narrow to IEvalInterface
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var narrow = new InterfaceNarrowExpression(root, typeof(IEvalInterface));

        var result = evaluator.Evaluate<IEvalInterface>(narrow, resolver);

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0]).IsTypeOf<EvalSource>();
    }

    [Test]
    public async Task Evaluate_InterfaceNarrow_NoImplementors_ReturnsEmpty()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        // EvalTarget does NOT implement IEvalInterface
        var targets = new List<EvalTarget> { new() { Label = "T1" } };
        Func<string, IReadOnlyList<object>> resolver = name => name switch
        {
            "EvalTarget" => targets.Cast<object>().ToList(),
            _ => []
        };

        var root = new RootExpression(typeof(EvalTarget), "EvalTarget");
        var narrow = new InterfaceNarrowExpression(root, typeof(IEvalInterface));

        var result = evaluator.Evaluate<IEvalInterface>(narrow, resolver);

        await Assert.That(result).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Evaluate_TraverseLink_WithFilterOnSource_IgnoresSourceFilter()
    {
        // Schema-level traversal: source filters don't affect target items.
        // Even with a filter that eliminates all source items, traversal
        // returns ALL target items because it follows the link schema.
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        Expression<Func<EvalSource, bool>> predicate = s => s.Value > 9999; // matches nothing
        var filter = new FilterExpression(root, predicate);
        var traverse = new TraverseLinkExpression(filter, "targets", typeof(EvalTarget));
        var resolver = BuildTestResolver();

        var result = evaluator.Evaluate<EvalTarget>(traverse, resolver);

        // All target items returned despite source filter — schema-level traversal
        await Assert.That(result).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Evaluate_TraverseLink_UnknownSourceDescriptor_Throws()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "NonexistentType");
        var traverse = new TraverseLinkExpression(root, "targets", typeof(EvalTarget));
        var resolver = BuildTestResolver();

        await Assert.That(() => evaluator.Evaluate<EvalTarget>(traverse, resolver))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));

        await Assert.That(() => evaluator.Evaluate<EvalTarget>(traverse, resolver))
            .ThrowsException()
            .WithMessageContaining("not found in ontology graph");
    }

    // -----------------------------------------------------------------------
    // Task 10 tests — RawFilter, Similarity, error handling, thread safety
    // -----------------------------------------------------------------------

    [Test]
    public async Task Evaluate_RawFilter_ThrowsNotSupported()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var rawFilter = new RawFilterExpression(root, "Value > 5");
        var resolver = BuildTestResolver();

        await Assert.That(() => evaluator.Evaluate<EvalSource>(rawFilter, resolver))
            .ThrowsException()
            .WithExceptionType(typeof(NotSupportedException));

        await Assert.That(() => evaluator.Evaluate<EvalSource>(rawFilter, resolver))
            .ThrowsException()
            .WithMessageContaining("RawFilterExpression evaluation is not supported by InMemoryExpressionEvaluator");
    }

    [Test]
    public async Task Evaluate_SimilarityExpression_ThrowsNotSupported()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var similarity = new SimilarityExpression(root, "test query", 5, 0.7);
        var resolver = BuildTestResolver();

        await Assert.That(() => evaluator.Evaluate<EvalSource>(similarity, resolver))
            .ThrowsException()
            .WithExceptionType(typeof(NotSupportedException));

        await Assert.That(() => evaluator.Evaluate<EvalSource>(similarity, resolver))
            .ThrowsException()
            .WithMessageContaining("SimilarityExpression evaluation is not supported");
    }

    [Test]
    public async Task Evaluate_UnknownDescriptor_ReturnsEmpty()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        // Use a descriptor name that the resolver doesn't know about — returns empty
        var root = new RootExpression(typeof(EvalSource), "UnknownType");
        Func<string, IReadOnlyList<object>> resolver = _ => [];

        var result = evaluator.Evaluate<EvalSource>(root, resolver);

        await Assert.That(result).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Evaluate_ConcurrentCalls_ThreadSafe()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), "EvalSource");
        var resolver = BuildTestResolver();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => evaluator.Evaluate<EvalSource>(root, resolver)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            await Assert.That(result).Count().IsEqualTo(2);
        }
    }
}
