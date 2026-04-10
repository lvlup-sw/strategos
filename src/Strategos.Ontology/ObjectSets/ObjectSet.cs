using System.Linq.Expressions;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;

namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// A lazy, composable query expression over ontology-typed domain objects.
/// Analogous to IQueryable&lt;T&gt; but operating over the ontology graph.
/// Each operation returns a new immutable instance.
/// </summary>
/// <typeparam name="T">The domain object type.</typeparam>
public sealed class ObjectSet<T> where T : class
{
    private readonly IObjectSetProvider _provider;
    private readonly IActionDispatcher _actionDispatcher;
    private readonly IEventStreamProvider _eventStreamProvider;

    /// <summary>
    /// Creates a new root ObjectSet for the given type, carrying an explicit ontology
    /// descriptor name on the root expression so the provider can dispatch against the
    /// correct registration when the CLR type is registered under multiple names.
    /// Callers constructing an <see cref="ObjectSet{T}"/> directly (rather than through
    /// <see cref="Strategos.Ontology.Query.IOntologyQuery.GetObjectSet{T}"/>) must supply
    /// the descriptor name they want dispatched.
    /// </summary>
    /// <param name="descriptorName">
    /// The ontology descriptor name registered for this type in the graph. For
    /// single-registered types this is typically <c>typeof(T).Name</c>; for multi-registered
    /// types this is the explicit name passed to <c>Object&lt;T&gt;(name, ...)</c>.
    /// </param>
    public ObjectSet(
        string descriptorName,
        IObjectSetProvider provider,
        IActionDispatcher actionDispatcher,
        IEventStreamProvider eventStreamProvider)
        : this(new RootExpression(typeof(T), descriptorName), provider, actionDispatcher, eventStreamProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptorName);
    }

    internal ObjectSet(ObjectSetExpression expression, IObjectSetProvider provider, IActionDispatcher actionDispatcher, IEventStreamProvider eventStreamProvider)
    {
        Expression = expression;
        _provider = provider;
        _actionDispatcher = actionDispatcher;
        _eventStreamProvider = eventStreamProvider;
    }

    /// <summary>
    /// The expression tree representing this query.
    /// </summary>
    public ObjectSetExpression Expression { get; }

    /// <summary>
    /// Filters the object set by the given predicate. Returns a new immutable ObjectSet.
    /// </summary>
    public ObjectSet<T> Where(Expression<Func<T, bool>> predicate)
    {
        var filterExpr = new FilterExpression(Expression, predicate);
        return new ObjectSet<T>(filterExpr, _provider, _actionDispatcher, _eventStreamProvider);
    }

    /// <summary>
    /// Traverses a named link to produce an ObjectSet of the linked type.
    /// </summary>
    public ObjectSet<TLinked> TraverseLink<TLinked>(string linkName) where TLinked : class
    {
        var traverseExpr = new TraverseLinkExpression(Expression, linkName, typeof(TLinked));
        return new ObjectSet<TLinked>(traverseExpr, _provider, _actionDispatcher, _eventStreamProvider);
    }

    /// <summary>
    /// Narrows the object set to objects implementing the given interface type.
    /// </summary>
    public ObjectSet<TInterface> OfInterface<TInterface>() where TInterface : class
    {
        var narrowExpr = new InterfaceNarrowExpression(Expression, typeof(TInterface));
        return new ObjectSet<TInterface>(narrowExpr, _provider, _actionDispatcher, _eventStreamProvider);
    }

    /// <summary>
    /// Specifies which data facets to include in the result. Returns a new immutable ObjectSet.
    /// </summary>
    public ObjectSet<T> Include(ObjectSetInclusion inclusion)
    {
        var includeExpr = new IncludeExpression(Expression, inclusion);
        return new ObjectSet<T>(includeExpr, _provider, _actionDispatcher, _eventStreamProvider);
    }

    /// <summary>
    /// Returns a similarity search over this object set. Configure additional knobs
    /// (TopK, MinRelevance, DistanceMetric) via the fluent setters on
    /// <see cref="SimilarObjectSet{T}"/>.
    /// </summary>
    public SimilarObjectSet<T> SimilarTo(string queryText)
    {
        ArgumentNullException.ThrowIfNull(queryText);
        var expression = new SimilarityExpression(
            Expression, queryText, topK: 5, minRelevance: 0.7);
        return new SimilarObjectSet<T>(expression, _provider);
    }

    /// <summary>
    /// Materializes the object set query and returns the result.
    /// </summary>
    public Task<ObjectSetResult<T>> ExecuteAsync(CancellationToken ct = default)
    {
        return _provider.ExecuteAsync<T>(Expression, ct);
    }

    /// <summary>
    /// Streams the results of the object set query as an async enumerable.
    /// </summary>
    public IAsyncEnumerable<T> StreamAsync(CancellationToken ct = default)
    {
        return _provider.StreamAsync<T>(Expression, ct);
    }

    /// <summary>
    /// Applies an action to all objects in the set by first materializing the query,
    /// then dispatching the action to each object. The dispatch routes against the
    /// descriptor name carried on the expression's root, so a multi-registered CLR
    /// type reaches the registration the caller selected.
    /// </summary>
    public async Task<IReadOnlyList<ActionResult>> ApplyAsync(string actionName, object request, CancellationToken ct = default)
    {
        var result = await _provider.ExecuteAsync<T>(Expression, ct).ConfigureAwait(false);
        var results = new List<ActionResult>(result.Items.Count);
        var descriptorName = Expression.RootObjectTypeName;

        foreach (var item in result.Items)
        {
            var objectId = item?.ToString() ?? string.Empty;
            var context = new ActionContext(descriptorName, descriptorName, objectId, actionName);
            var actionResult = await _actionDispatcher.DispatchAsync(context, request, ct).ConfigureAwait(false);
            results.Add(actionResult);
        }

        return results;
    }

    /// <summary>
    /// Queries events for the object type represented by this set. The query carries
    /// the descriptor name from the expression root, so multi-registered CLR types
    /// resolve against the caller-selected registration rather than the raw CLR name.
    /// </summary>
    public IAsyncEnumerable<OntologyEvent> EventsAsync(TimeSpan? since = null, IReadOnlyList<string>? eventTypes = null)
    {
        var sinceTimestamp = since.HasValue ? DateTimeOffset.UtcNow - since.Value : (DateTimeOffset?)null;
        var descriptorName = Expression.RootObjectTypeName;
        var query = new EventQuery(descriptorName, descriptorName, Since: sinceTimestamp, EventTypes: eventTypes);
        return _eventStreamProvider.QueryEventsAsync(query);
    }
}
