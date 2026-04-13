using System.Linq.Expressions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

// ── Test domain types ──────────────────────────────────────────────────────
public interface IEvalInterface
{
    string Name { get; }
}

public sealed record EvalSource(string Name, int Value) : IEvalInterface;

public sealed record EvalTarget(string Label);

// ── Test ontology ──────────────────────────────────────────────────────────
public class EvalDomainOntology : DomainOntology
{
    public override string DomainName => "eval-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IEvalInterface>("IEvalInterface", iface =>
        {
            iface.Property(e => e.Name);
        });

        builder.Object<EvalSource>(obj =>
        {
            obj.Property(s => s.Name).Required();
            obj.Property(s => s.Value);
            obj.HasMany<EvalTarget>("targets");
            obj.Implements<IEvalInterface>(map => { });
        });

        builder.Object<EvalTarget>(obj =>
        {
            obj.Property(t => t.Label).Required();
        });
    }
}

public class InMemoryExpressionEvaluatorTests
{
    private OntologyGraph _graph = null!;
    private Func<string, IReadOnlyList<object>> _resolver = null!;

    private readonly List<EvalSource> _sources =
    [
        new("Alpha", 10),
        new("Beta", 3),
        new("Gamma", 20),
    ];

    private readonly List<EvalTarget> _targets =
    [
        new("A"),
        new("B"),
    ];

    [Before(Test)]
    public void Setup()
    {
        _graph = new OntologyGraphBuilder()
            .AddDomain<EvalDomainOntology>()
            .Build();

        _resolver = descriptorName => descriptorName switch
        {
            nameof(EvalSource) => _sources.Cast<object>().ToList(),
            nameof(EvalTarget) => _targets.Cast<object>().ToList(),
            _ => [],
        };
    }

    // ── Root expression ────────────────────────────────────────────────────

    [Test]
    public async Task Evaluate_RootExpression_ReturnsAllItems()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var expression = new RootExpression(typeof(EvalSource), nameof(EvalSource));

        var result = evaluator.Evaluate<EvalSource>(expression, _resolver);

        await Assert.That(result).HasCount().EqualTo(3);
        await Assert.That(result[0].Name).IsEqualTo("Alpha");
    }

    // ── Filter expression ──────────────────────────────────────────────────

    [Test]
    public async Task Evaluate_FilterExpression_AppliesPredicate()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        Expression<Func<EvalSource, bool>> predicate = s => s.Value > 5;
        var filter = new FilterExpression(root, predicate);

        var result = evaluator.Evaluate<EvalSource>(filter, _resolver);

        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result.Select(s => s.Name)).IsEquivalentTo(new[] { "Alpha", "Gamma" });
    }

    [Test]
    public async Task Evaluate_FilterChain_AppliesAllPredicates()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        Expression<Func<EvalSource, bool>> pred1 = s => s.Value > 5;
        Expression<Func<EvalSource, bool>> pred2 = s => s.Name.StartsWith("A");
        var filter1 = new FilterExpression(root, pred1);
        var filter2 = new FilterExpression(filter1, pred2);

        var result = evaluator.Evaluate<EvalSource>(filter2, _resolver);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("Alpha");
    }

    // ── Include expression ─────────────────────────────────────────────────

    [Test]
    public async Task Evaluate_IncludeExpression_PassesThrough()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        var include = new IncludeExpression(root, ObjectSetInclusion.Properties);

        var result = evaluator.Evaluate<EvalSource>(include, _resolver);

        await Assert.That(result).HasCount().EqualTo(3);
    }

    // ── TraverseLink expression ────────────────────────────────────────────

    [Test]
    public async Task Evaluate_TraverseLink_ReturnsTargetTypeItems()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        var traverse = new TraverseLinkExpression(root, "targets", typeof(EvalTarget));

        var result = evaluator.Evaluate<EvalTarget>(traverse, _resolver);

        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result[0].Label).IsEqualTo("A");
    }

    [Test]
    public async Task Evaluate_TraverseLink_ThenFilter_FiltersTargetItems()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        var traverse = new TraverseLinkExpression(root, "targets", typeof(EvalTarget));
        Expression<Func<EvalTarget, bool>> predicate = t => t.Label == "A";
        var filter = new FilterExpression(traverse, predicate);

        var result = evaluator.Evaluate<EvalTarget>(filter, _resolver);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Label).IsEqualTo("A");
    }

    [Test]
    public void Evaluate_TraverseLink_UnknownLink_Throws()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        var traverse = new TraverseLinkExpression(root, "nonexistent", typeof(EvalTarget));

        Assert.Throws<InvalidOperationException>(() =>
            evaluator.Evaluate<EvalTarget>(traverse, _resolver));
    }

    // ── InterfaceNarrow expression ─────────────────────────────────────────

    [Test]
    public async Task Evaluate_InterfaceNarrow_FiltersToImplementors()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        var narrow = new InterfaceNarrowExpression(root, typeof(IEvalInterface));

        var result = evaluator.Evaluate<IEvalInterface>(narrow, _resolver);

        await Assert.That(result).HasCount().EqualTo(3);
    }

    [Test]
    public async Task Evaluate_InterfaceNarrow_NoImplementors_ReturnsEmpty()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        // EvalTarget does not implement IEvalInterface
        var root = new RootExpression(typeof(EvalTarget), nameof(EvalTarget));
        var narrow = new InterfaceNarrowExpression(root, typeof(IEvalInterface));

        var result = evaluator.Evaluate<IEvalInterface>(narrow, _resolver);

        await Assert.That(result).HasCount().EqualTo(0);
    }

    // ── RawFilter expression ───────────────────────────────────────────────

    [Test]
    public void Evaluate_RawFilter_ThrowsNotSupported()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var root = new RootExpression(typeof(EvalSource), nameof(EvalSource));
        var raw = new RawFilterExpression(root, "Value > 5");

        Assert.Throws<NotSupportedException>(() =>
            evaluator.Evaluate<EvalSource>(raw, _resolver));
    }

    // ── Empty resolver ─────────────────────────────────────────────────────

    [Test]
    public async Task Evaluate_EmptyItemResolver_ReturnsEmpty()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var expression = new RootExpression(typeof(EvalSource), nameof(EvalSource));

        var result = evaluator.Evaluate<EvalSource>(expression, _ => []);

        await Assert.That(result).HasCount().EqualTo(0);
    }

    // ── Thread safety ──────────────────────────────────────────────────────

    [Test]
    public async Task Evaluate_ConcurrentCalls_ThreadSafe()
    {
        var evaluator = new InMemoryExpressionEvaluator(_graph);
        var expression = new RootExpression(typeof(EvalSource), nameof(EvalSource));

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            evaluator.Evaluate<EvalSource>(expression, _resolver)));

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            await Assert.That(result).HasCount().EqualTo(3);
        }
    }
}
