using Microsoft.Extensions.Logging;
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

    private OntologyQueryTool BuildTool(CapturingLogger<OntologyQueryTool> logger, IKeywordSearchProvider? keywordProvider = null) =>
        new(_graph, _objectSetProvider, _eventStreamProvider, logger, keywordProvider);

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

    // ---- Task 30: structural + hybridOptions → silent ignore ----

    [Test]
    public async Task QueryAsync_StructuralQueryWithHybridOptions_IgnoresOptions_NoHybridMeta_NoWarnLog()
    {
        // Non-semantic branch must be byte-identical to 2.5.0 even when caller
        // supplies hybridOptions (structural branch ignores them entirely,
        // does not emit a warning, and does not surface HybridMeta).
        var items = new List<object> { new { Id = "p1", Symbol = "AAPL" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties));

        var logger = new CapturingLogger<OntologyQueryTool>();
        var tool = BuildTool(logger);

        var union = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            hybridOptions: new HybridQueryOptions());

        await Assert.That(union).IsTypeOf<QueryResult>();
        var result = (QueryResult)union;
        await Assert.That(result.Meta.Hybrid).IsNull();
        await Assert.That(logger.Entries.Count).IsEqualTo(0);
    }

    // ---- Task 31: semantic + hybridOptions + no provider → degraded ----

    [Test]
    public async Task QueryAsync_SemanticWithHybridOptions_NoProviderRegistered_DenseOnly_DegradedNoKeywordProvider_WarnsOnce()
    {
        var items = new List<object> { new { Id = "p1" } };
        var scores = new List<double> { 0.91 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var logger = new CapturingLogger<OntologyQueryTool>();
        var tool = BuildTool(logger, keywordProvider: null);

        var first = (SemanticQueryResult)await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "tech stocks",
            hybridOptions: new HybridQueryOptions());

        var second = (SemanticQueryResult)await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "tech stocks",
            hybridOptions: new HybridQueryOptions());

        // Items match dense-only.
        await Assert.That(first.Items).HasCount().EqualTo(1);
        await Assert.That(second.Items).HasCount().EqualTo(1);

        // HybridMeta degraded shape on both calls.
        await Assert.That(first.Meta.Hybrid).IsNotNull();
        await Assert.That(first.Meta.Hybrid!.Hybrid).IsFalse();
        await Assert.That(first.Meta.Hybrid.Degraded).IsEqualTo("no-keyword-provider");
        await Assert.That(second.Meta.Hybrid).IsNotNull();
        await Assert.That(second.Meta.Hybrid!.Degraded).IsEqualTo("no-keyword-provider");

        // Warn-once: exactly one warning across two calls.
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        await Assert.That(warnings.Count).IsEqualTo(1);
    }

    // ---- Task 32: EnableKeyword=false → dense-only, no Degraded ----

    [Test]
    public async Task QueryAsync_SemanticWithHybridOptionsEnableKeywordFalse_DenseOnly_HybridMetaHybridFalse_NoDegraded()
    {
        // Provider IS registered but EnableKeyword=false forces dense-only.
        // This is the explicit-ablation path: HybridMeta surfaces (so callers
        // can observe their request was honored) but Degraded is null because
        // nothing degraded — the caller asked for this.
        var items = new List<object> { new { Id = "p1" } };
        var scores = new List<double> { 0.88 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(items, items.Count, ObjectSetInclusion.Properties, scores));

        var keywordProvider = Substitute.For<IKeywordSearchProvider>();
        var tool = BuildTool(keywordProvider: keywordProvider);

        var union = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "tech stocks",
            hybridOptions: new HybridQueryOptions { EnableKeyword = false });

        await Assert.That(union).IsTypeOf<SemanticQueryResult>();
        var result = (SemanticQueryResult)union;
        await Assert.That(result.Meta.Hybrid).IsNotNull();
        await Assert.That(result.Meta.Hybrid!.Hybrid).IsFalse();
        await Assert.That(result.Meta.Hybrid.Degraded).IsNull();

        // Sparse leg must NOT have been called.
        await keywordProvider.DidNotReceive().SearchAsync(Arg.Any<KeywordSearchRequest>(), Arg.Any<CancellationToken>());
    }

    // ---- Task 33: Hybrid happy path — Reciprocal ----

    /// <summary>Test item with a stable Id reflection-readable by the hybrid path.</summary>
    private sealed record TestItem(string Id, string Symbol);

    [Test]
    public async Task QueryAsync_HybridReciprocal_BothLegsReturn_FusedOutputMatchesRankFusionReciprocal_HybridMetaHealthyReciprocal()
    {
        // Dense fixture: 3 items with descending scores.
        var denseItems = new List<object>
        {
            new TestItem("doc-a", "AAPL"),
            new TestItem("doc-b", "MSFT"),
            new TestItem("doc-c", "GOOG"),
        };
        var denseScores = new List<double> { 0.90, 0.85, 0.70 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(denseItems, denseItems.Count, ObjectSetInclusion.Properties, denseScores));

        // Sparse fixture: different ordering, overlap on doc-b and doc-c.
        var sparseResults = new List<KeywordSearchResult>
        {
            new("doc-c", Score: 18.0, Rank: 1),
            new("doc-b", Score: 12.0, Rank: 2),
            new("doc-d", Score: 9.0, Rank: 3),
        };
        var keywordProvider = Substitute.For<IKeywordSearchProvider>();
        keywordProvider
            .SearchAsync(Arg.Any<KeywordSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KeywordSearchResult>>(sparseResults));

        var tool = BuildTool(keywordProvider: keywordProvider);

        var union = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "tech stocks",
            topK: 3,
            hybridOptions: new HybridQueryOptions());

        // Compute the oracle directly via RankFusion.Reciprocal.
        var denseRanked = denseItems
            .Cast<TestItem>()
            .Select((it, i) => new RankedCandidate(it.Id, i + 1))
            .ToList();
        var sparseRanked = sparseResults
            .Select(r => new RankedCandidate(r.DocumentId, r.Rank))
            .ToList();
        var expectedFused = RankFusion.Reciprocal(
            new IReadOnlyList<RankedCandidate>[] { denseRanked, sparseRanked },
            weights: null,
            k: 60,
            topK: 3);

        await Assert.That(union).IsTypeOf<SemanticQueryResult>();
        var result = (SemanticQueryResult)union;

        // Ordering matches oracle (only documents that exist on the dense leg
        // can be projected back to items — sparse-only docs are dropped here).
        var resultIds = result.Items.Cast<TestItem>().Select(i => i.Id).ToList();
        var expectedIds = expectedFused.Select(f => f.DocumentId).Where(id => denseItems.Any(d => ((TestItem)d).Id == id)).ToList();
        await Assert.That(resultIds).IsEquivalentTo(expectedIds);

        // HybridMeta healthy shape.
        await Assert.That(result.Meta.Hybrid).IsNotNull();
        await Assert.That(result.Meta.Hybrid!.Hybrid).IsTrue();
        await Assert.That(result.Meta.Hybrid.FusionMethod).IsEqualTo("reciprocal");
        await Assert.That(result.Meta.Hybrid.Degraded).IsNull();
        await Assert.That(result.Meta.Hybrid.DenseTopScore).IsEqualTo(0.90);
        await Assert.That(result.Meta.Hybrid.SparseTopScore).IsEqualTo(18.0);
        await Assert.That(result.Meta.Hybrid.BmSaturationThreshold).IsEqualTo(18.0);
    }

    // ---- Task 34: Hybrid happy path — DistributionBased ----

    [Test]
    public async Task QueryAsync_HybridDistributionBased_BothLegsReturn_FusedOutputMatchesRankFusionDistributionBased_HybridMetaHealthyDistributionBased()
    {
        var denseItems = new List<object>
        {
            new TestItem("doc-a", "AAPL"),
            new TestItem("doc-b", "MSFT"),
            new TestItem("doc-c", "GOOG"),
        };
        var denseScores = new List<double> { 0.90, 0.85, 0.70 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(denseItems, denseItems.Count, ObjectSetInclusion.Properties, denseScores));

        var sparseResults = new List<KeywordSearchResult>
        {
            new("doc-c", Score: 18.0, Rank: 1),
            new("doc-b", Score: 12.0, Rank: 2),
            new("doc-d", Score: 9.0, Rank: 3),
        };
        var keywordProvider = Substitute.For<IKeywordSearchProvider>();
        keywordProvider
            .SearchAsync(Arg.Any<KeywordSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KeywordSearchResult>>(sparseResults));

        var tool = BuildTool(keywordProvider: keywordProvider);

        var union = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "tech stocks",
            topK: 3,
            hybridOptions: new HybridQueryOptions { FusionMethod = FusionMethod.DistributionBased });

        // Oracle.
        var denseScored = denseItems
            .Cast<TestItem>()
            .Select((it, i) => new ScoredCandidate(it.Id, denseScores[i]))
            .ToList();
        var sparseScored = sparseResults
            .Select(r => new ScoredCandidate(r.DocumentId, r.Score))
            .ToList();
        var expectedFused = RankFusion.DistributionBased(
            new IReadOnlyList<ScoredCandidate>[] { denseScored, sparseScored },
            weights: null,
            topK: 3);

        await Assert.That(union).IsTypeOf<SemanticQueryResult>();
        var result = (SemanticQueryResult)union;

        var resultIds = result.Items.Cast<TestItem>().Select(i => i.Id).ToList();
        var expectedIds = expectedFused.Select(f => f.DocumentId).Where(id => denseItems.Any(d => ((TestItem)d).Id == id)).ToList();
        await Assert.That(resultIds).IsEquivalentTo(expectedIds);

        await Assert.That(result.Meta.Hybrid).IsNotNull();
        await Assert.That(result.Meta.Hybrid!.Hybrid).IsTrue();
        await Assert.That(result.Meta.Hybrid.FusionMethod).IsEqualTo("distribution_based");
        await Assert.That(result.Meta.Hybrid.Degraded).IsNull();
    }

    // ---- Task 35: Weighted Reciprocal snapshot ----

    [Test]
    public async Task QueryAsync_HybridReciprocalWeighted_DenseDominantWeights_SnapshotOrdering()
    {
        // SourceWeights = [1.0, 0.5] (dense dominant). Assert ordering matches
        // a direct call to RankFusion.Reciprocal with the same weights.
        var denseItems = new List<object>
        {
            new TestItem("doc-a", "AAPL"),
            new TestItem("doc-b", "MSFT"),
            new TestItem("doc-c", "GOOG"),
        };
        var denseScores = new List<double> { 0.90, 0.85, 0.70 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(denseItems, denseItems.Count, ObjectSetInclusion.Properties, denseScores));

        var sparseResults = new List<KeywordSearchResult>
        {
            new("doc-c", Score: 18.0, Rank: 1),
            new("doc-b", Score: 12.0, Rank: 2),
            new("doc-d", Score: 9.0, Rank: 3),
        };
        var keywordProvider = Substitute.For<IKeywordSearchProvider>();
        keywordProvider
            .SearchAsync(Arg.Any<KeywordSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KeywordSearchResult>>(sparseResults));

        var tool = BuildTool(keywordProvider: keywordProvider);
        var weights = new[] { 1.0, 0.5 };

        var union = await tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "tech stocks",
            topK: 3,
            hybridOptions: new HybridQueryOptions
            {
                FusionMethod = FusionMethod.Reciprocal,
                SourceWeights = weights,
            });

        var denseRanked = denseItems.Cast<TestItem>().Select((it, i) => new RankedCandidate(it.Id, i + 1)).ToList();
        var sparseRanked = sparseResults.Select(r => new RankedCandidate(r.DocumentId, r.Rank)).ToList();
        var expectedFused = RankFusion.Reciprocal(
            new IReadOnlyList<RankedCandidate>[] { denseRanked, sparseRanked },
            weights: weights,
            k: 60,
            topK: 3);

        var result = (SemanticQueryResult)union;
        var resultIds = result.Items.Cast<TestItem>().Select(i => i.Id).ToList();
        var expectedIds = expectedFused.Select(f => f.DocumentId).Where(id => denseItems.Any(d => ((TestItem)d).Id == id)).ToList();
        await Assert.That(resultIds).IsEquivalentTo(expectedIds);
    }
}
