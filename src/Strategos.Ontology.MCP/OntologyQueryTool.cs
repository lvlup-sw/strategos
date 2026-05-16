using Microsoft.Extensions.Logging;

using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.MCP;

/// <summary>
/// MCP tool for ontology read operations.
/// Translates JSON-like parameters to ObjectSet queries.
/// </summary>
public sealed class OntologyQueryTool
{
    private readonly OntologyGraph _graph;
    private readonly IObjectSetProvider _objectSetProvider;
    private readonly IEventStreamProvider _eventStreamProvider;
    private readonly ILogger<OntologyQueryTool> _logger;
    private readonly IKeywordSearchProvider? _keywordProvider;

    // Warn-once latch for the "hybrid requested but no IKeywordSearchProvider
    // registered" degraded path. Scoped per OntologyQueryTool instance per design
    // §6.6 ("Warning-once: ... per process" — DI typically registers the tool as a
    // singleton, making per-instance equivalent to per-process for production hosts).
    private int _noProviderWarnedOnce;

    public OntologyQueryTool(
        OntologyGraph graph,
        IObjectSetProvider objectSetProvider,
        IEventStreamProvider eventStreamProvider,
        ILogger<OntologyQueryTool> logger,
        IKeywordSearchProvider? keywordProvider = null)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        _graph = graph;
        _objectSetProvider = objectSetProvider;
        _eventStreamProvider = eventStreamProvider;
        _logger = logger;
        _keywordProvider = keywordProvider;
    }

    /// <summary>
    /// Queries ontology objects by type with optional filtering, link traversal,
    /// interface narrowing, inclusion control, and semantic search.
    /// </summary>
    /// <remarks>
    /// The return type is the polymorphic union <see cref="QueryResultUnion"/> so that
    /// <c>System.Text.Json</c>'s <c>[JsonPolymorphic]</c> machinery emits the
    /// <c>resultKind</c> discriminator on the wire — matching the <c>oneOf</c>
    /// schema advertised by <see cref="OntologyToolDiscovery.Discover"/>. If the
    /// static return type were a concrete branch (e.g. <see cref="QueryResult"/>),
    /// callers serializing through that static type would silently drop the
    /// discriminator and the schema↔runtime contract would diverge.
    /// </remarks>
    public async Task<QueryResultUnion> QueryAsync(
        string objectType,
        string? domain = null,
        string? filter = null,
        string? traverseLink = null,
        string? interfaceName = null,
        string? include = null,
        string? semanticQuery = null,
        int topK = 5,
        double minRelevance = 0.7,
        string? distanceMetric = null,
        HybridQueryOptions? hybridOptions = null,
        CancellationToken ct = default)
    {
        // Validate at call entry so the caller observes argument faults synchronously
        // before any retrieval work is initiated (design §6.6).
        hybridOptions?.Validate();

        var inclusion = ParseInclusion(include);
        var expression = BuildExpression(domain, objectType, filter, traverseLink, interfaceName, inclusion);

        if (semanticQuery is not null)
        {
            return await ExecuteSemanticQueryAsync(
                expression, objectType, semanticQuery, topK, minRelevance, distanceMetric,
                filter, traverseLink, interfaceName, include, hybridOptions, ct).ConfigureAwait(false);
        }

        var result = await _objectSetProvider.ExecuteAsync<object>(expression, ct).ConfigureAwait(false);

        return new QueryResult(objectType, result.Items, CurrentMeta())
        {
            Filter = filter,
            TraverseLink = traverseLink,
            InterfaceName = interfaceName,
            Include = include,
        };
    }

    private ResponseMeta CurrentMeta() => ResponseMeta.ForGraph(_graph);

    /// <summary>
    /// Queries temporal events for an object type.
    /// </summary>
    public async Task<IReadOnlyList<OntologyEvent>> QueryEventsAsync(
        string objectType,
        string domain,
        string? objectId = null,
        DateTimeOffset? since = null,
        IReadOnlyList<string>? eventTypes = null,
        CancellationToken ct = default)
    {
        var query = new EventQuery(domain, objectType, objectId, since, eventTypes);
        var events = new List<OntologyEvent>();

        await foreach (var evt in _eventStreamProvider.QueryEventsAsync(query, ct).ConfigureAwait(false))
        {
            events.Add(evt);
        }

        return events;
    }

    private async Task<SemanticQueryResult> ExecuteSemanticQueryAsync(
        ObjectSetExpression baseExpression,
        string objectType,
        string semanticQuery,
        int topK,
        double minRelevance,
        string? distanceMetric,
        string? filter,
        string? traverseLink,
        string? interfaceName,
        string? include,
        HybridQueryOptions? hybridOptions,
        CancellationToken ct)
    {
        var metric = ParseDistanceMetric(distanceMetric);

        var similarityExpression = new SimilarityExpression(
            baseExpression, semanticQuery, topK, minRelevance, metric);

        // Hybrid dispatch lives entirely on the semantic branch; structural
        // queries return 2.5.0 QueryResult untouched. See design §6.4 decision tree.
        if (hybridOptions is null)
        {
            var dense = await _objectSetProvider
                .ExecuteSimilarityAsync<object>(similarityExpression, ct)
                .ConfigureAwait(false);

            return BuildSemanticResult(objectType, dense.Items, dense.Scores,
                hybridMeta: null, semanticQuery, topK, minRelevance,
                filter, traverseLink, interfaceName, include);
        }

        // EnableKeyword=false: explicit-ablation path takes precedence over
        // provider-missing degradation so callers who explicitly opted out of
        // sparse never see Degraded="no-keyword-provider" even when no
        // IKeywordSearchProvider is registered (CodeRabbit PR #77 finding).
        if (!hybridOptions.EnableKeyword)
        {
            var dense = await _objectSetProvider
                .ExecuteSimilarityAsync<object>(similarityExpression, ct)
                .ConfigureAwait(false);

            var enabledFalseMeta = new HybridMeta(Hybrid: false);
            return BuildSemanticResult(objectType, dense.Items, dense.Scores,
                hybridMeta: enabledFalseMeta, semanticQuery, topK, minRelevance,
                filter, traverseLink, interfaceName, include);
        }

        // Hybrid requested but no provider registered → degraded dense-only
        // with warn-once. The warning carries enough context for operators
        // to discover the missing DI registration.
        if (_keywordProvider is null)
        {
            if (Interlocked.CompareExchange(ref _noProviderWarnedOnce, 1, 0) == 0)
            {
                _logger.LogWarning(
                    "HybridQueryOptions supplied but no IKeywordSearchProvider is registered; falling back to dense-only retrieval for this and subsequent calls.");
            }

            var dense = await _objectSetProvider
                .ExecuteSimilarityAsync<object>(similarityExpression, ct)
                .ConfigureAwait(false);

            var degradedMeta = new HybridMeta(Hybrid: false, Degraded: "no-keyword-provider");
            return BuildSemanticResult(objectType, dense.Items, dense.Scores,
                hybridMeta: degradedMeta, semanticQuery, topK, minRelevance,
                filter, traverseLink, interfaceName, include);
        }

        return await ExecuteHybridSemanticAsync(
            similarityExpression, objectType, semanticQuery, topK, minRelevance,
            filter, traverseLink, interfaceName, include, hybridOptions, ct).ConfigureAwait(false);
    }

    private async Task<SemanticQueryResult> ExecuteHybridSemanticAsync(
        SimilarityExpression similarityExpression,
        string objectType,
        string semanticQuery,
        int topK,
        double minRelevance,
        string? filter,
        string? traverseLink,
        string? interfaceName,
        string? include,
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
        var sparseTask = InvokeSparseLegAsync(semanticQuery, objectType, hybridOptions.SparseTopK, ct);

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
            var degradedMeta = new HybridMeta(Hybrid: false, Degraded: "sparse-failed");
            return BuildSemanticResult(objectType, dense.Items, dense.Scores,
                degradedMeta, semanticQuery, topK, minRelevance,
                filter, traverseLink, interfaceName, include);
        }

        return FuseHybrid(
            dense, sparseOrNull, hybridOptions, objectType, semanticQuery, topK, minRelevance,
            filter, traverseLink, interfaceName, include);
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
        string objectType,
        string semanticQuery,
        int topK,
        double minRelevance,
        string? filter,
        string? traverseLink,
        string? interfaceName,
        string? include)
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
                topK: topK);
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
                topK: topK);
        }

        // Project the fused order back to dense items. Documents that exist only
        // on the sparse leg (no matching dense item with extractable Id) are
        // dropped — the 2.5.0 SemanticQueryResult shape is items+scores, and
        // we cannot synthesize an object for a sparse-only DocumentId.
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

        return BuildSemanticResult(objectType, projectedItems, projectedScores,
            hybridMeta, semanticQuery, topK, minRelevance,
            filter, traverseLink, interfaceName, include);
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

    private SemanticQueryResult BuildSemanticResult(
        string objectType,
        IReadOnlyList<object> items,
        IReadOnlyList<double> scores,
        HybridMeta? hybridMeta,
        string semanticQuery,
        int topK,
        double minRelevance,
        string? filter,
        string? traverseLink,
        string? interfaceName,
        string? include)
    {
        var meta = CurrentMeta();
        if (hybridMeta is not null)
        {
            meta = meta with { Hybrid = hybridMeta };
        }

        return new SemanticQueryResult(objectType, items, meta)
        {
            Scores = scores,
            SemanticQuery = semanticQuery,
            TopK = topK,
            MinRelevance = minRelevance,
            Filter = filter,
            TraverseLink = traverseLink,
            InterfaceName = interfaceName,
            Include = include,
        };
    }

    private ObjectSetExpression BuildExpression(
        string? domain,
        string objectType,
        string? filter,
        string? traverseLink,
        string? interfaceName,
        ObjectSetInclusion? inclusion)
    {
        var clrType = domain is not null
            ? _graph.GetObjectType(domain, objectType)?.ClrType ?? typeof(object)
            : typeof(object);

        // The MCP protocol passes the ontology descriptor name as the objectType parameter,
        // which is exactly what RootExpression needs to dispatch against the correct descriptor
        // partition when the same CLR type is registered under multiple names. See D2 tests in
        // OntologyQueryToolTests.QueryAsync_WithExplicitDescriptorName_*.
        ObjectSetExpression expression = new RootExpression(clrType, objectType);

        if (filter is not null)
        {
            expression = new RawFilterExpression(expression, filter);
        }

        if (traverseLink is not null)
        {
            expression = new TraverseLinkExpression(expression, traverseLink, clrType);
        }

        if (interfaceName is not null)
        {
            expression = new InterfaceNarrowExpression(expression, clrType);
        }

        if (inclusion.HasValue)
        {
            expression = new IncludeExpression(expression, inclusion.Value);
        }

        return expression;
    }

    private DistanceMetric ParseDistanceMetric(string? distanceMetric)
    {
        if (distanceMetric is null)
        {
            return DistanceMetric.Cosine;
        }

        if (Enum.TryParse<DistanceMetric>(distanceMetric, ignoreCase: true, out var result))
        {
            return result;
        }

        _logger.LogWarning("Failed to parse distance metric {DistanceMetric}, defaulting to Cosine", distanceMetric);
        return DistanceMetric.Cosine;
    }

    private ObjectSetInclusion? ParseInclusion(string? include)
    {
        if (include is null)
        {
            return null;
        }

        if (Enum.TryParse<ObjectSetInclusion>(include, ignoreCase: true, out var result))
        {
            return result;
        }

        _logger.LogWarning("Failed to parse inclusion value {Include}, defaulting to null", include);
        return null;
    }
}
