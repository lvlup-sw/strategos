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

    public OntologyQueryTool(
        OntologyGraph graph,
        IObjectSetProvider objectSetProvider,
        IEventStreamProvider eventStreamProvider)
    {
        _graph = graph;
        _objectSetProvider = objectSetProvider;
        _eventStreamProvider = eventStreamProvider;
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

        return new QueryResult(objectType, result.Items)
        {
            Filter = filter,
            TraverseLink = traverseLink,
            InterfaceName = interfaceName,
            Include = include,
        };
    }

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

        var similarityExpression = new SimilarityExpression(baseExpression, semanticQuery)
        {
            TopK = topK,
            MinRelevance = minRelevance,
            Metric = metric,
        };

        var result = await _objectSetProvider
            .ExecuteSimilarityAsync<object>(similarityExpression, ct)
            .ConfigureAwait(false);

        return new SemanticQueryResult(objectType, result.Items)
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

        ObjectSetExpression expression = new RootExpression(clrType);

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

    private static DistanceMetric ParseDistanceMetric(string? distanceMetric)
    {
        if (distanceMetric is null)
        {
            return DistanceMetric.Cosine;
        }

        return Enum.TryParse<DistanceMetric>(distanceMetric, ignoreCase: true, out var result)
            ? result
            : DistanceMetric.Cosine;
    }

    private static ObjectSetInclusion? ParseInclusion(string? include)
    {
        if (include is null)
        {
            return null;
        }

        return Enum.TryParse<ObjectSetInclusion>(include, ignoreCase: true, out var result)
            ? result
            : null;
    }
}
