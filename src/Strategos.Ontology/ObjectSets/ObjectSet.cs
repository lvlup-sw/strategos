using System.Linq.Expressions;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Events;
using Strategos.Ontology.Identity;

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
    private readonly ActionSubject _actionSubject;
    private readonly OntologyGraph? _graph;
    private readonly ObjectTypeDescriptor? _objectTypeDescriptor;
    private readonly IObjectIdentityProjector _identityProjector;

    /// <summary>
    /// Creates a root object set with the domain-qualified ontology identity used
    /// for action dispatch and event queries.
    /// </summary>
    /// <param name="subject">The ontology domain and object descriptor names.</param>
    /// <param name="provider">Provider that materializes object-set expressions.</param>
    /// <param name="actionDispatcher">Dispatcher used by <see cref="ApplyAsync"/>.</param>
    /// <param name="eventStreamProvider">Provider used by <see cref="EventsAsync"/>.</param>
    public ObjectSet(
        ActionSubject subject,
        IObjectSetProvider provider,
        IActionDispatcher actionDispatcher,
        IEventStreamProvider eventStreamProvider)
        : this(
            new RootExpression(typeof(T), subject?.ObjectTypeName ?? throw new ArgumentNullException(nameof(subject))),
            provider,
            actionDispatcher,
            eventStreamProvider,
            subject,
            graph: null,
            objectTypeDescriptor: null,
            identityProjector: null)
    {
    }

    /// <summary>
    /// Creates the query-owned root object set with the authoritative descriptor
    /// used to project materialized instances to their ontology identifiers.
    /// </summary>
    internal ObjectSet(
        OntologyGraph graph,
        ObjectTypeDescriptor objectTypeDescriptor,
        IObjectSetProvider provider,
        IActionDispatcher actionDispatcher,
        IEventStreamProvider eventStreamProvider)
        : this(
            new RootExpression(
                typeof(T),
                objectTypeDescriptor?.Name ?? throw new ArgumentNullException(nameof(objectTypeDescriptor))),
            provider,
            actionDispatcher,
            eventStreamProvider,
            new ActionSubject(objectTypeDescriptor.DomainName, objectTypeDescriptor.Name),
            graph,
            objectTypeDescriptor,
            new ObjectIdentityProjector())
    {
        ArgumentNullException.ThrowIfNull(graph);
    }

    private ObjectSet(
        ObjectSetExpression expression,
        IObjectSetProvider provider,
        IActionDispatcher actionDispatcher,
        IEventStreamProvider eventStreamProvider,
        ActionSubject actionSubject,
        OntologyGraph? graph,
        ObjectTypeDescriptor? objectTypeDescriptor,
        IObjectIdentityProjector? identityProjector)
    {
        Expression = expression;
        _provider = provider;
        _actionDispatcher = actionDispatcher;
        _eventStreamProvider = eventStreamProvider;
        _actionSubject = actionSubject;
        _graph = graph;
        _objectTypeDescriptor = objectTypeDescriptor;
        _identityProjector = identityProjector ?? new ObjectIdentityProjector();

        if (_graph is not null && _objectTypeDescriptor is null)
        {
            throw new InvalidOperationException(
                "A graph-backed ObjectSet must carry an authoritative object type descriptor.");
        }
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
        return new ObjectSet<T>(
            filterExpr,
            _provider,
            _actionDispatcher,
            _eventStreamProvider,
            _actionSubject,
            _graph,
            _objectTypeDescriptor,
            _identityProjector);
    }

    /// <summary>
    /// Traverses a named link to produce an ObjectSet of the linked type. No
    /// explicit target descriptor name is supplied. Query-created sets resolve the
    /// target from the ontology graph and carry that authoritative descriptor into
    /// the traversal expression and subsequent action dispatch. Manually constructed,
    /// graphless sets preserve the legacy <c>null</c> target override. Use the
    /// <see cref="TraverseLink{TLinked}(string, string)"/> overload to name a
    /// specific registration when the link target is multi-registered (#128).
    /// </summary>
    public ObjectSet<TLinked> TraverseLink<TLinked>(string linkName) where TLinked : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkName);
        var target = ResolveTraversalTarget<TLinked>(linkName, explicitDescriptorName: null);
        var traverseExpr = new TraverseLinkExpression(
            Expression,
            linkName,
            typeof(TLinked),
            target?.Name);
        return new ObjectSet<TLinked>(
            traverseExpr,
            _provider,
            _actionDispatcher,
            _eventStreamProvider,
            target is null
                ? new ActionSubject(_actionSubject.DomainName, traverseExpr.RootObjectTypeName)
                : new ActionSubject(target.DomainName, target.Name),
            _graph,
            target,
            _identityProjector);
    }

    /// <summary>
    /// DR-10: traverses a named link to produce an ObjectSet of the linked type,
    /// carrying an EXPLICIT ontology descriptor name for the traversal target. This
    /// mirrors the <see cref="RootExpression"/>(<c>Type objectType, string objectTypeName</c>)
    /// precedent — where the root carries an explicit descriptor name alongside the
    /// CLR type — so an instance-anchored traversal dispatches against a specific
    /// registration. The name is set on
    /// <see cref="TraverseLinkExpression.TargetDescriptorName"/>, which the
    /// evaluators CONSUME as the highest-precedence hop-target seam: it names the
    /// exact target partition authoritatively, so a CLR type backing several
    /// descriptors routes to the named registration rather than a CLR-first match
    /// (the #128 keystone). This is the disambiguator for multi-registered targets.
    /// </summary>
    public ObjectSet<TLinked> TraverseLink<TLinked>(string linkName, string descriptorName) where TLinked : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkName);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptorName);
        var target = ResolveTraversalTarget<TLinked>(linkName, descriptorName);
        var traverseExpr = new TraverseLinkExpression(
            Expression,
            linkName,
            typeof(TLinked),
            target?.Name ?? descriptorName);
        return new ObjectSet<TLinked>(
            traverseExpr,
            _provider,
            _actionDispatcher,
            _eventStreamProvider,
            target is null
                ? new ActionSubject(_actionSubject.DomainName, traverseExpr.RootObjectTypeName)
                : new ActionSubject(target.DomainName, target.Name),
            _graph,
            target,
            _identityProjector);
    }

    /// <summary>
    /// Narrows the object set to objects implementing the given interface type.
    /// </summary>
    public ObjectSet<TInterface> OfInterface<TInterface>() where TInterface : class
    {
        var narrowExpr = new InterfaceNarrowExpression(Expression, typeof(TInterface));
        return new ObjectSet<TInterface>(
            narrowExpr,
            _provider,
            _actionDispatcher,
            _eventStreamProvider,
            _actionSubject,
            _graph,
            _objectTypeDescriptor,
            _identityProjector);
    }

    /// <summary>
    /// Specifies which data facets to include in the result. Returns a new immutable ObjectSet.
    /// </summary>
    public ObjectSet<T> Include(ObjectSetInclusion inclusion)
    {
        var includeExpr = new IncludeExpression(Expression, inclusion);
        return new ObjectSet<T>(
            includeExpr,
            _provider,
            _actionDispatcher,
            _eventStreamProvider,
            _actionSubject,
            _graph,
            _objectTypeDescriptor,
            _identityProjector);
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
    /// <param name="principal">Authenticated principal requesting the action.</param>
    /// <param name="actionName">Name of the action to apply.</param>
    /// <param name="request">Action request payload.</param>
    /// <param name="ct">Token used to cancel materialization or dispatch.</param>
    /// <returns>One result for each materialized object.</returns>
    public Task<IReadOnlyList<ActionResult>> ApplyAsync(
        ActionPrincipal principal,
        string actionName,
        object request,
        CancellationToken ct = default) =>
        ApplyCoreAsync(principal, actionName, request, options: null, ct);

    /// <summary>
    /// Applies an action with explicit dispatch options to every materialized
    /// object. Use <see cref="ActionDispatchOptions.EnforcePreconditions"/> to
    /// require every hard action predicate at the authoritative dispatch boundary.
    /// </summary>
    /// <param name="principal">Authenticated principal requesting the action.</param>
    /// <param name="actionName">Name of the action to apply.</param>
    /// <param name="request">Action request payload.</param>
    /// <param name="options">Dispatch-time authorization options.</param>
    /// <param name="ct">Token used to cancel materialization or dispatch.</param>
    /// <returns>One result for each materialized object.</returns>
    public Task<IReadOnlyList<ActionResult>> ApplyAsync(
        ActionPrincipal principal,
        string actionName,
        object request,
        ActionDispatchOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ApplyCoreAsync(principal, actionName, request, options, ct);
    }

    private async Task<IReadOnlyList<ActionResult>> ApplyCoreAsync(
        ActionPrincipal principal,
        string actionName,
        object request,
        ActionDispatchOptions? options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var result = await _provider.ExecuteAsync<T>(Expression, ct).ConfigureAwait(false);
        var results = new List<ActionResult>(result.Items.Count);

        foreach (var item in result.Items)
        {
            var objectId = ProjectObjectId(item);
            var context = new ActionContext(
                principal,
                _actionSubject.DomainName,
                _actionSubject.ObjectTypeName,
                objectId,
                actionName,
                options);
            var actionResult = await _actionDispatcher.DispatchAsync(context, request, ct).ConfigureAwait(false);
            results.Add(actionResult);
        }

        return results;
    }

    private string ProjectObjectId(T item)
    {
        if (_objectTypeDescriptor is not null)
        {
            return _identityProjector.ProjectId(_objectTypeDescriptor, item);
        }

        // Callers that instantiate ObjectSet directly have no graph descriptor.
        // Every
        // IOntologyQuery-created set is graph-backed and therefore takes the
        // descriptor/id-accessor path above, including after link traversal.
        if (_graph is not null)
        {
            throw new InvalidOperationException(
                "Cannot dispatch an action from a graph-backed ObjectSet without an authoritative descriptor.");
        }

        return item?.ToString() ?? string.Empty;
    }

    private ObjectTypeDescriptor? ResolveTraversalTarget<TLinked>(
        string linkName,
        string? explicitDescriptorName)
        where TLinked : class
    {
        if (_graph is null)
        {
            // Public, manually constructed sets have no ontology graph from which
            // an authoritative traversal target could be derived.
            return null;
        }

        var source = _objectTypeDescriptor
            ?? throw new InvalidOperationException(
                "Cannot resolve a graph-backed traversal without its source descriptor.");

        ObjectTypeDescriptor target;
        if (source.Kind == ObjectKind.Association)
        {
            var endpoint = source.AssociationEndpoints.FirstOrDefault(candidate =>
                string.Equals(candidate.Role, linkName, StringComparison.Ordinal));
            if (endpoint is null)
            {
                var availableRoles = string.Join(", ", source.AssociationEndpoints.Select(candidate => candidate.Role));
                throw new InvalidOperationException(
                    $"Endpoint role '{linkName}' was not found on association '{source.DomainName}.{source.Name}'. " +
                    $"Available roles: {availableRoles}.");
            }

            target = ResolveNamedTarget(source.DomainName, endpoint.DescriptorName);
            if (explicitDescriptorName is not null
                && !string.Equals(explicitDescriptorName, target.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Association endpoint role '{linkName}' resolves to '{target.Name}', not " +
                    $"the requested descriptor '{explicitDescriptorName}'.");
            }
        }
        else
        {
            var link = source.Links.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, linkName, StringComparison.Ordinal));
            if (link is null)
            {
                var availableLinks = string.Join(", ", source.Links.Select(candidate => candidate.Name));
                throw new InvalidOperationException(
                    $"Link '{linkName}' was not found on object type '{source.DomainName}.{source.Name}'. " +
                    $"Available links: {availableLinks}.");
            }

            target = explicitDescriptorName is not null
                ? ResolveNamedTarget(source.DomainName, explicitDescriptorName)
                : ResolveDeclaredOrConcreteTarget<TLinked>(source, link);
        }

        return target;
    }

    private ObjectTypeDescriptor ResolveDeclaredOrConcreteTarget<TLinked>(
        ObjectTypeDescriptor source,
        LinkDescriptor link)
        where TLinked : class
    {
        var graph = _graph
            ?? throw new InvalidOperationException("Cannot resolve a traversal target without an ontology graph.");
        ObjectTypeDescriptor? declaredTarget = null;
        if (!string.IsNullOrWhiteSpace(link.TargetTypeName))
        {
            declaredTarget = graph.GetObjectType(source.DomainName, link.TargetTypeName);
            if (declaredTarget is null)
            {
                var namedMatches = graph.ObjectTypes
                    .Where(candidate =>
                        string.Equals(candidate.Name, link.TargetTypeName, StringComparison.Ordinal))
                    .ToArray();
                if (namedMatches.Length == 1)
                {
                    declaredTarget = namedMatches[0];
                }
                else if (namedMatches.Length > 1)
                {
                    throw AmbiguousTraversalTarget<TLinked>(source, link, namedMatches);
                }
            }
        }

        if (declaredTarget is null && !string.IsNullOrWhiteSpace(link.TargetSymbolKey))
        {
            var symbolMatches = graph.ObjectTypes
                .Where(candidate =>
                    string.Equals(candidate.SymbolKey, link.TargetSymbolKey, StringComparison.Ordinal))
                .ToArray();
            if (symbolMatches.Length == 1)
            {
                declaredTarget = symbolMatches[0];
            }
            else if (symbolMatches.Length > 1)
            {
                throw AmbiguousTraversalTarget<TLinked>(source, link, symbolMatches);
            }
        }

        // A link that itself names an association is authoritative even when the
        // association's CLR type has several registrations. The descriptor name,
        // not typeof(TLinked), selects the partition.
        if (declaredTarget?.Kind == ObjectKind.Association)
        {
            return declaredTarget;
        }

        // Preserve the established reified-edge view: requesting the CLR type of
        // exactly one association selects that association when the source link
        // declares the far endpoint. More than one matching graph descriptor is
        // an identity ambiguity and must be named explicitly.
        var associationMatches = graph.ObjectTypes
            .Where(candidate =>
                candidate.DomainName == source.DomainName
                && candidate.Kind == ObjectKind.Association
                && candidate.ClrType == typeof(TLinked))
            .ToArray();
        if (associationMatches.Length > 1)
        {
            throw AmbiguousTraversalTarget<TLinked>(source, link, associationMatches);
        }

        if (associationMatches.Length == 1)
        {
            return associationMatches[0];
        }

        if (declaredTarget is not null)
        {
            return declaredTarget;
        }

        if (typeof(TLinked).IsInterface || typeof(TLinked).IsAbstract)
        {
            throw new InvalidOperationException(
                $"Link '{source.DomainName}.{source.Name}.{link.Name}' does not identify one concrete " +
                $"descriptor for polymorphic traversal type '{typeof(TLinked).FullName}'. " +
                "Use the descriptor-name traversal overload.");
        }

        var concreteMatches = graph.ObjectTypes
            .Where(candidate =>
                candidate.DomainName == source.DomainName
                && candidate.ClrType == typeof(TLinked))
            .ToArray();
        return concreteMatches.Length switch
        {
            1 => concreteMatches[0],
            > 1 => throw AmbiguousTraversalTarget<TLinked>(source, link, concreteMatches),
            _ => throw new InvalidOperationException(
                $"Link '{source.DomainName}.{source.Name}.{link.Name}' has no graph-resolvable target " +
                $"descriptor for '{typeof(TLinked).FullName}'. Use the descriptor-name traversal overload."),
        };
    }

    private ObjectTypeDescriptor ResolveNamedTarget(string preferredDomain, string descriptorName)
    {
        var local = _graph!.GetObjectType(preferredDomain, descriptorName);
        if (local is not null)
        {
            return local;
        }

        var matches = _graph.ObjectTypes
            .Where(candidate => string.Equals(candidate.Name, descriptorName, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            > 1 => throw new InvalidOperationException(
                $"Descriptor name '{descriptorName}' is ambiguous outside domain '{preferredDomain}'."),
            _ => throw new InvalidOperationException(
                $"Descriptor '{preferredDomain}.{descriptorName}' is not registered in the ontology graph."),
        };
    }

    private static InvalidOperationException AmbiguousTraversalTarget<TLinked>(
        ObjectTypeDescriptor source,
        LinkDescriptor link,
        IReadOnlyCollection<ObjectTypeDescriptor> matches)
        where TLinked : class
    {
        var names = string.Join(", ", matches.Select(candidate => $"{candidate.DomainName}.{candidate.Name}"));
        return new InvalidOperationException(
            $"Link '{source.DomainName}.{source.Name}.{link.Name}' has an ambiguous target for " +
            $"'{typeof(TLinked).FullName}': {names}. Use the descriptor-name traversal overload.");
    }

    /// <summary>
    /// Queries events for the object type represented by this set. The query carries
    /// the descriptor name from the expression root, so multi-registered CLR types
    /// resolve against the caller-selected registration rather than the raw CLR name.
    /// </summary>
    public IAsyncEnumerable<OntologyEvent> EventsAsync(TimeSpan? since = null, IReadOnlyList<string>? eventTypes = null)
    {
        var sinceTimestamp = since.HasValue ? DateTimeOffset.UtcNow - since.Value : (DateTimeOffset?)null;
        var query = new EventQuery(
            _actionSubject.DomainName,
            _actionSubject.ObjectTypeName,
            Since: sinceTimestamp,
            EventTypes: eventTypes);
        return _eventStreamProvider.QueryEventsAsync(query);
    }
}
