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
    /// interface narrowing, and inclusion control.
    /// </summary>
    public async Task<QueryResult> QueryAsync(
        string objectType,
        string? domain = null,
        string? filter = null,
        string? traverseLink = null,
        string? interfaceName = null,
        string? include = null,
        CancellationToken ct = default)
    {
        var inclusion = ParseInclusion(include);
        var expression = BuildExpression(objectType, filter, traverseLink, interfaceName, inclusion);

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

    private static ObjectSetExpression BuildExpression(
        string objectType,
        string? filter,
        string? traverseLink,
        string? interfaceName,
        ObjectSetInclusion? inclusion)
    {
        ObjectSetExpression expression = new RootExpression(typeof(object));

        if (traverseLink is not null)
        {
            expression = new TraverseLinkExpression(expression, traverseLink, typeof(object));
        }

        if (interfaceName is not null)
        {
            expression = new InterfaceNarrowExpression(expression, typeof(object));
        }

        if (inclusion.HasValue)
        {
            expression = new IncludeExpression(expression, inclusion.Value);
        }

        return expression;
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
