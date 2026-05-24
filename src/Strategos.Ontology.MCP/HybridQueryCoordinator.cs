using Microsoft.Extensions.Logging;

using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Drives the hybrid (dense + sparse) retrieval path for
/// <see cref="OntologyQueryTool"/>: the <c>EnableKeyword</c> ablation gate, the
/// missing-provider and sparse-failure degradations, the parallel two-leg fan-out,
/// and rank fusion (design §6.4 decision tree). <see cref="OntologyQueryTool"/>
/// retains structural and dense-only-semantic orchestration; everything that only
/// matters when <c>hybridOptions</c> is supplied lives here (issue #78, item 5).
/// </summary>
internal sealed class HybridQueryCoordinator
{
    private readonly IObjectSetProvider _objectSetProvider;
    private readonly IKeywordSearchProvider? _keywordProvider;
    private readonly OntologyGraph _graph;
    private readonly ILogger _logger;

    // Warn-once latch for the "hybrid requested but no IKeywordSearchProvider
    // registered" degraded path. Static so the warning fires exactly once per
    // process (design §6.6, DIM-5/DIM-8) regardless of how many OntologyQueryTool /
    // HybridQueryCoordinator instances a host constructs — issue #78 item 4
    // replaced the prior per-instance field, which relied on the unenforced
    // "DI registers a singleton" convention. Reset for test isolation via
    // ResetWarnOnceLatch.
    private static int s_noProviderWarnedOnce;

    public HybridQueryCoordinator(
        IObjectSetProvider objectSetProvider,
        IKeywordSearchProvider? keywordProvider,
        OntologyGraph graph,
        ILogger logger)
    {
        _objectSetProvider = objectSetProvider;
        _keywordProvider = keywordProvider;
        _graph = graph;
        _logger = logger;
    }

    /// <summary>
    /// Resets the process-wide warn-once latch. Test-only seam: the static latch
    /// (item 4) makes the "warns exactly once" assertion order-dependent across
    /// tests in a shared process, so suites that exercise the missing-provider path
    /// call this in their per-test setup.
    /// </summary>
    internal static void ResetWarnOnceLatch() => Interlocked.Exchange(ref s_noProviderWarnedOnce, 0);

    /// <summary>
    /// Executes the hybrid path for a semantic query with non-null
    /// <paramref name="hybridOptions"/>. Returns a dense-only
    /// <see cref="SemanticQueryResult"/> on every degraded leaf and a fused result
    /// when both legs contribute.
    /// </summary>
    public async Task<SemanticQueryResult> ExecuteAsync(
        SimilarityExpression similarityExpression,
        SemanticQueryRequest request,
        HybridQueryOptions hybridOptions,
        CancellationToken ct)
    {
        // EnableKeyword=false: explicit-ablation path takes precedence over
        // provider-missing degradation so callers who explicitly opted out of
        // sparse never see Degraded="no-keyword-provider" even when no
        // IKeywordSearchProvider is registered (CodeRabbit PR #77 finding).
        if (!hybridOptions.EnableKeyword)
        {
            return await DenseOnlyAsync(
                similarityExpression, request, new HybridMeta(Hybrid: false), ct).ConfigureAwait(false);
        }

        // Hybrid requested but no provider registered → degraded dense-only
        // with warn-once. The warning carries enough context for operators
        // to discover the missing DI registration.
        if (_keywordProvider is null)
        {
            if (Interlocked.CompareExchange(ref s_noProviderWarnedOnce, 1, 0) == 0)
            {
                _logger.LogWarning(
                    "HybridQueryOptions supplied but no IKeywordSearchProvider is registered; falling back to dense-only retrieval for this and subsequent calls.");
            }

            var degradedMeta = new HybridMeta(Hybrid: false, Degraded: "no-keyword-provider");
            return await DenseOnlyAsync(similarityExpression, request, degradedMeta, ct).ConfigureAwait(false);
        }

        return await ExecuteHybridAsync(similarityExpression, request, hybridOptions, ct).ConfigureAwait(false);
    }

    private async Task<SemanticQueryResult> DenseOnlyAsync(
        SimilarityExpression similarityExpression,
        SemanticQueryRequest request,
        HybridMeta hybridMeta,
        CancellationToken ct)
    {
        var dense = await _objectSetProvider
            .ExecuteSimilarityAsync<object>(similarityExpression, ct)
            .ConfigureAwait(false);

        return SemanticQueryResult.FromRequest(
            request, dense.Items, dense.Scores, hybridMeta, ResponseMeta.ForGraph(_graph));
    }

    private async Task<SemanticQueryResult> ExecuteHybridAsync(
        SimilarityExpression similarityExpression,
        SemanticQueryRequest request,
        HybridQueryOptions hybridOptions,
        CancellationToken ct)
    {
        // Fan out dense + sparse legs in parallel. Both Tasks start before either
        // is awaited so we get true overlap (design §6.6 parallelism contract).
        // The sparse leg uses HybridQueryOptions.SparseTopK and the descriptor
        // name (objectType) as the collection key.
        //
        // The dense leg uses a candidate pool of max(outer topK, DenseTopK) so
        // fusion sees the full DenseTopK pool the caller requested while still
        // returning at least the outer topK results the caller asked for. The
        // final projection in FuseHybrid trims back to outer topK.
        var denseSimilarityExpression = hybridOptions.DenseTopK > similarityExpression.TopK
            ? new SimilarityExpression(
                similarityExpression.Source,
                similarityExpression.QueryText,
                hybridOptions.DenseTopK,
                similarityExpression.MinRelevance,
                similarityExpression.Metric,
                similarityExpression.EmbeddingPropertyName,
                similarityExpression.QueryVector,
                similarityExpression.Filters)
            : similarityExpression;
        var denseTask = _objectSetProvider.ExecuteSimilarityAsync<object>(denseSimilarityExpression, ct);
        var sparseTask = InvokeSparseLegAsync(request.SemanticQuery, request.ObjectType, hybridOptions.SparseTopK, ct);

        // Await both. Dense throws propagate (baseline — design §6.6, Task 38).
        // Sparse-fault is caught inside InvokeSparseLegAsync and surfaced as null
        // so that Task.WhenAll cannot raise just-the-sparse failure here. We
        // still await Task.WhenAll for cancellation-token propagation symmetry.
        try
        {
            await Task.WhenAll(denseTask, sparseTask).ConfigureAwait(false);
        }
        catch when (denseTask.IsFaulted)
        {
            // Re-throw the dense exception below by awaiting the task directly.
        }

        var dense = await denseTask.ConfigureAwait(false);
        var sparseOrNull = await sparseTask.ConfigureAwait(false);

        if (sparseOrNull is null)
        {
            // Sparse-failed degraded path: return dense-only items with
            // HybridMeta { Hybrid = false, Degraded = "sparse-failed" }.
            return SemanticQueryResult.FromRequest(
                request, dense.Items, dense.Scores,
                new HybridMeta(Hybrid: false, Degraded: "sparse-failed"),
                ResponseMeta.ForGraph(_graph));
        }

        if (sparseOrNull.Count == 0)
        {
            // Sparse-empty path (issue #78 item 1): the sparse leg ran and
            // succeeded but contributed no candidates, so fusion is a no-op over
            // the dense order. HybridMeta.Hybrid is true only when sparse actually
            // contributed (design §6.6, DIM-7), so this surfaces as dense-only with
            // a distinct Degraded="sparse-empty" reason rather than a misleading
            // Hybrid=true envelope carrying a null sparseTopScore.
            return SemanticQueryResult.FromRequest(
                request, dense.Items, dense.Scores,
                new HybridMeta(Hybrid: false, Degraded: "sparse-empty"),
                ResponseMeta.ForGraph(_graph));
        }

        return FuseHybrid(dense, sparseOrNull, hybridOptions, request);
    }

    /// <summary>
    /// Invokes the sparse keyword leg, returning <c>null</c> on caught fault
    /// (logged at Error with the exception + stack) and rethrowing on cancel
    /// so caller-driven cancellation does not silently mutate into a
    /// sparse-failed degraded result.
    /// </summary>
    private async Task<IReadOnlyList<KeywordSearchResult>?> InvokeSparseLegAsync(
        string query, string collection, int sparseTopK, CancellationToken ct)
    {
        try
        {
            return await _keywordProvider!
                .SearchAsync(new KeywordSearchRequest(query, collection, sparseTopK), ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller-driven cancellation: surface naturally so QueryAsync
            // rethrows OperationCanceledException (design §6.6 cancellation).
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hybrid sparse leg threw {ExceptionType}; falling back to dense-only retrieval for this call.",
                ex.GetType().Name);
            return null;
        }
    }

    private SemanticQueryResult FuseHybrid(
        ScoredObjectSetResult<object> dense,
        IReadOnlyList<KeywordSearchResult> sparse,
        HybridQueryOptions hybridOptions,
        SemanticQueryRequest request)
    {
        // Build (id → item, score) lookup for the dense leg, preserving original
        // item order so that DBSF's tie-break (input-order stability inside
        // RankFusion) matches an oracle that iterates dense.Items in order.
        var denseByDocId = new Dictionary<string, (object Item, double Score, int Rank)>(StringComparer.Ordinal);
        var denseRanked = new List<RankedCandidate>(dense.Items.Count);
        var denseScored = new List<ScoredCandidate>(dense.Items.Count);
        for (int i = 0; i < dense.Items.Count; i++)
        {
            var id = ExtractDocumentId(dense.Items[i]);
            if (id is null)
            {
                continue;
            }

            // Tie behavior: first occurrence wins for the dense projection map.
            if (!denseByDocId.ContainsKey(id))
            {
                denseByDocId[id] = (dense.Items[i], dense.Scores[i], i + 1);
                denseRanked.Add(new RankedCandidate(id, i + 1));
                denseScored.Add(new ScoredCandidate(id, dense.Scores[i]));
            }
        }

        var sparseRanked = sparse.Select(r => new RankedCandidate(r.DocumentId, r.Rank)).ToList();

        // Run fusion. Dispatch on FusionMethod.
        IReadOnlyList<FusedResult> fused;
        if (hybridOptions.FusionMethod == FusionMethod.Reciprocal)
        {
            fused = RankFusion.Reciprocal(
                new IReadOnlyList<RankedCandidate>[] { denseRanked, sparseRanked },
                weights: hybridOptions.SourceWeights,
                k: hybridOptions.RrfK,
                topK: request.TopK);
        }
        else
        {
            // DistributionBased uses raw scores rather than ranks. denseScored
            // was built above in dense.Items order so DBSF's stability matches
            // an oracle that iterates dense.Items left-to-right.
            var sparseScored = sparse.Select(r => new ScoredCandidate(r.DocumentId, r.Score)).ToList();

            fused = RankFusion.DistributionBased(
                new IReadOnlyList<ScoredCandidate>[] { denseScored, sparseScored },
                weights: hybridOptions.SourceWeights,
                topK: request.TopK);
        }

        // Project the fused order back to dense items. Documents that exist only
        // on the sparse leg (no matching dense item with extractable Id) are
        // dropped — the 2.5.0 SemanticQueryResult shape is items+scores, and
        // we cannot synthesize an object for a sparse-only DocumentId. See
        // design §6.4 for the documented sparse-only drop policy.
        var projectedItems = new List<object>(fused.Count);
        var projectedScores = new List<double>(fused.Count);
        foreach (var f in fused)
        {
            if (denseByDocId.TryGetValue(f.DocumentId, out var hit))
            {
                projectedItems.Add(hit.Item);
                projectedScores.Add(hit.Score);
            }
        }

        var sparseTopScore = sparse.Count > 0 ? sparse[0].Score : (double?)null;
        var denseTopScore = dense.Scores.Count > 0 ? dense.Scores[0] : (double?)null;
        var fusionTag = hybridOptions.FusionMethod switch
        {
            FusionMethod.Reciprocal => "reciprocal",
            FusionMethod.DistributionBased => "distribution_based",
            _ => null,
        };

        var hybridMeta = new HybridMeta(
            Hybrid: true,
            FusionMethod: fusionTag,
            Degraded: null,
            DenseTopScore: denseTopScore,
            SparseTopScore: sparseTopScore,
            BmSaturationThreshold: hybridOptions.BmSaturationThreshold);

        return SemanticQueryResult.FromRequest(
            request, projectedItems, projectedScores, hybridMeta, ResponseMeta.ForGraph(_graph));
    }

    /// <summary>
    /// Reads the <c>Id</c> property from a dense-leg item via reflection. The
    /// hybrid coordinator needs a stable string identifier to align dense items
    /// with sparse <see cref="KeywordSearchResult.DocumentId"/>; the ontology's
    /// existing convention is a public <c>Id</c> property. Returns <c>null</c>
    /// when no such property exists or its string projection is null.
    /// </summary>
    /// <remarks>
    /// The trim-warning is suppressed because dense items are produced by
    /// <see cref="IObjectSetProvider.ExecuteSimilarityAsync"/> which already
    /// requires the underlying CLR types to be preserved end-to-end; the
    /// ontology graph machinery keeps those types referenced.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method",
        Justification = "Dense items' CLR types are retained by the ontology graph (see IObjectSetProvider). The 'Id' property is part of every ontology object descriptor that participates in similarity search.")]
    private static string? ExtractDocumentId(object item)
    {
        if (item is null)
        {
            return null;
        }

        var prop = item.GetType().GetProperty("Id");
        if (prop is null)
        {
            return null;
        }

        var value = prop.GetValue(item);
        return value?.ToString();
    }
}
