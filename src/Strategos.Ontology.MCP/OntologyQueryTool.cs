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

        // TODO Tasks 32–40: EnableKeyword=false, happy paths, weighted snapshots,
        // sparse/dense failure handling, cancellation, parallelism.
        throw new NotImplementedException("Hybrid path with registered provider is wired in later PR-C tasks.");
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
