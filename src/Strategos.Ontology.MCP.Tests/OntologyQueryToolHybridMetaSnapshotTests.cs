using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// PR-C Task 41: snapshot tests for the JSON wire shape of <see cref="ResponseMeta"/>
/// across all five hybrid behavior-tree leaves (design §6.4 / §6.5). These pin the
/// exact serialized payload that consumers (Exarchos clients, dashboards, log
/// pipelines) observe.
/// </summary>
public sealed class OntologyQueryToolHybridMetaSnapshotTests
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

    private sealed record TestItem(string Id, string Symbol);

    private static string SerializeMeta(ResponseMeta meta) =>
        JsonSerializer.Serialize(meta);

    // ---- Snapshot 1: hybridOptions null → no "hybrid" key ----

    [Test]
    public async Task Snapshot_HybridOptionsNull_NoHybridKey()
    {
        var items = new List<object> { new TestItem("doc-a", "AAPL") };
        var scores = new List<double> { 0.91 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var tool = BuildTool();
        var result = (SemanticQueryResult)await tool.QueryAsync(
            objectType: "TestPosition", domain: "trading",
            semanticQuery: "q", hybridOptions: null);

        var json = SerializeMeta(result.Meta);
        await Assert.That(json).DoesNotContain("\"hybrid\"");
        await Assert.That(json).Contains("\"ontologyVersion\"");
    }

    // ---- Snapshot 2: healthy Reciprocal ----

    [Test]
    public async Task Snapshot_HybridHealthyReciprocal()
    {
        var items = new List<object> { new TestItem("doc-a", "AAPL") };
        var scores = new List<double> { 0.91 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var keywordProvider = Substitute.For<IKeywordSearchProvider>();
        keywordProvider
            .SearchAsync(Arg.Any<KeywordSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KeywordSearchResult>>(
                new List<KeywordSearchResult> { new("doc-a", 17.4, 1) }));

        var tool = BuildTool(keywordProvider);
        var result = (SemanticQueryResult)await tool.QueryAsync(
            objectType: "TestPosition", domain: "trading",
            semanticQuery: "q", hybridOptions: new HybridQueryOptions());

        var json = SerializeMeta(result.Meta);
        await Assert.That(json).Contains("\"hybrid\":{");
        await Assert.That(json).Contains("\"hybrid\":true");
        await Assert.That(json).Contains("\"fusionMethod\":\"reciprocal\"");
        await Assert.That(json).Contains("\"denseTopScore\":0.91");
        await Assert.That(json).Contains("\"sparseTopScore\":17.4");
        await Assert.That(json).Contains("\"bmSaturationThreshold\":18");
        await Assert.That(json).DoesNotContain("\"degraded\"");
    }

    // ---- Snapshot 3: healthy DistributionBased ----

    [Test]
    public async Task Snapshot_HybridHealthyDistributionBased()
    {
        var items = new List<object>
        {
            new TestItem("doc-a", "AAPL"),
            new TestItem("doc-b", "MSFT"),
        };
        var scores = new List<double> { 0.91, 0.88 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var keywordProvider = Substitute.For<IKeywordSearchProvider>();
        keywordProvider
            .SearchAsync(Arg.Any<KeywordSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KeywordSearchResult>>(
                new List<KeywordSearchResult>
                {
                    new("doc-a", 17.4, 1),
                    new("doc-b", 12.0, 2),
                }));

        var tool = BuildTool(keywordProvider);
        var result = (SemanticQueryResult)await tool.QueryAsync(
            objectType: "TestPosition", domain: "trading",
            semanticQuery: "q",
            hybridOptions: new HybridQueryOptions { FusionMethod = FusionMethod.DistributionBased });

        var json = SerializeMeta(result.Meta);
        await Assert.That(json).Contains("\"hybrid\":true");
        await Assert.That(json).Contains("\"fusionMethod\":\"distribution_based\"");
        await Assert.That(json).Contains("\"denseTopScore\":0.91");
        await Assert.That(json).Contains("\"sparseTopScore\":17.4");
        await Assert.That(json).Contains("\"bmSaturationThreshold\":18");
        await Assert.That(json).DoesNotContain("\"degraded\"");
    }

    // ---- Snapshot 4: degraded no-keyword-provider ----

    [Test]
    public async Task Snapshot_HybridDegradedNoKeywordProvider()
    {
        var items = new List<object> { new TestItem("doc-a", "AAPL") };
        var scores = new List<double> { 0.91 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var tool = BuildTool(keywordProvider: null);
        var result = (SemanticQueryResult)await tool.QueryAsync(
            objectType: "TestPosition", domain: "trading",
            semanticQuery: "q", hybridOptions: new HybridQueryOptions());

        var json = SerializeMeta(result.Meta);
        await Assert.That(json).Contains("\"hybrid\":{");
        await Assert.That(json).Contains("\"hybrid\":false");
        await Assert.That(json).Contains("\"degraded\":\"no-keyword-provider\"");
        await Assert.That(json).DoesNotContain("\"fusionMethod\"");
        await Assert.That(json).DoesNotContain("\"denseTopScore\"");
        await Assert.That(json).DoesNotContain("\"sparseTopScore\"");
        await Assert.That(json).DoesNotContain("\"bmSaturationThreshold\"");
    }

    // ---- Snapshot 5: degraded sparse-failed ----

    [Test]
    public async Task Snapshot_HybridDegradedSparseFailed()
    {
        var items = new List<object> { new TestItem("doc-a", "AAPL") };
        var scores = new List<double> { 0.91 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var keywordProvider = Substitute.For<IKeywordSearchProvider>();
        keywordProvider
            .SearchAsync(Arg.Any<KeywordSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<KeywordSearchResult>>>(_ => throw new System.IO.IOException("synthetic"));

        var tool = BuildTool(keywordProvider);
        var result = (SemanticQueryResult)await tool.QueryAsync(
            objectType: "TestPosition", domain: "trading",
            semanticQuery: "q", hybridOptions: new HybridQueryOptions());

        var json = SerializeMeta(result.Meta);
        await Assert.That(json).Contains("\"hybrid\":false");
        await Assert.That(json).Contains("\"degraded\":\"sparse-failed\"");
        await Assert.That(json).DoesNotContain("\"fusionMethod\"");
        await Assert.That(json).DoesNotContain("\"denseTopScore\"");
        await Assert.That(json).DoesNotContain("\"sparseTopScore\"");
        await Assert.That(json).DoesNotContain("\"bmSaturationThreshold\"");
    }
}
