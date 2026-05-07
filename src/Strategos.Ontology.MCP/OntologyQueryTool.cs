using Microsoft.Extensions.Logging;

using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

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

    public OntologyQueryTool(
        OntologyGraph graph,
        IObjectSetProvider objectSetProvider,
        IEventStreamProvider eventStreamProvider,
        ILogger<OntologyQueryTool> logger)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        _graph = graph;
        _objectSetProvider = objectSetProvider;
        _eventStreamProvider = eventStreamProvider;
        _logger = logger;
    }

    /// <summary>
    /// Queries ontology objects by type with optional filtering, link traversal,
    /// interface narrowing, inclusion control, and semantic search.
    /// </summary>
    public async Task<QueryResult> QueryAsync(
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
        CancellationToken ct = default)
    {
        var inclusion = ParseInclusion(include);
        var expression = BuildExpression(domain, objectType, filter, traverseLink, interfaceName, inclusion);

        if (semanticQuery is not null)
        {
            return await ExecuteSemanticQueryAsync(
                expression, objectType, semanticQuery, topK, minRelevance, distanceMetric,
                filter, traverseLink, interfaceName, include, ct).ConfigureAwait(false);
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
        CancellationToken ct)
    {
        var metric = ParseDistanceMetric(distanceMetric);

        var similarityExpression = new SimilarityExpression(
            baseExpression, semanticQuery, topK, minRelevance, metric);

        var result = await _objectSetProvider
            .ExecuteSimilarityAsync<object>(similarityExpression, ct)
            .ConfigureAwait(false);

        return new SemanticQueryResult(objectType, result.Items, CurrentMeta())
        {
            Scores = result.Scores,
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
