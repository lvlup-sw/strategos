using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// PR-C Tasks 29–40: <see cref="OntologyQueryTool.QueryAsync"/> hybrid path wiring.
/// Tests are organized by behavior-tree leaf (design §6.4).
/// </summary>
public sealed class OntologyQueryToolHybridTests
{
    private OntologyGraph _graph = null!;
    private IObjectSetProvider _objectSetProvider = null!;
    private IEventStreamProvider _eventStreamProvider = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = TestOntologyGraphFactory.CreateTradingGraph();
        _objectSetProvider = Substitute.For<IObjectSetProvider>();
        _eventStreamProvider = Substitute.For<IEventStreamProvider>();
    }

    private OntologyQueryTool BuildTool(IKeywordSearchProvider? keywordProvider = null) =>
        new(_graph, _objectSetProvider, _eventStreamProvider,
            NullLogger<OntologyQueryTool>.Instance, keywordProvider);

    // ---- Task 29: null hybridOptions preserves 2.5.0 behavior ----

    [Test]
    public async Task QueryAsync_HybridOptionsNull_StructuralBranch_ReturnsQueryResult_NoHybridMeta()
    {
        // Structural path with no hybridOptions = unmodified 2.5.0 behavior.
        var items = new List<object> { new { Id = "p1" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties));

        var tool = BuildTool();
        var union = await tool.QueryAsync(objectType: "TestPosition", domain: "trading", hybridOptions: null);

        await Assert.That(union).IsTypeOf<QueryResult>();
        var result = (QueryResult)union;
        await Assert.That(result.Meta.Hybrid).IsNull();
    }

    [Test]
    public async Task QueryAsync_HybridOptionsNull_SemanticBranch_ReturnsSemanticQueryResult_NoHybridMeta()
    {
        var items = new List<object> { new { Id = "p1" } };
        var scores = new List<double> { 0.92 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var tool = BuildTool();
        var union = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "tech stocks",
            hybridOptions: null);

        await Assert.That(union).IsTypeOf<SemanticQueryResult>();
        var result = (SemanticQueryResult)union;
        await Assert.That(result.Meta.Hybrid).IsNull();
    }
}
