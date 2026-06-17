using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP;

/// <summary>
/// MCP tool for instance-anchored traversal across reified associations (DR-15).
/// Walks from a specific source instance, over a named link, to a far endpoint,
/// optionally filtering on the association's edge attributes before the far hop.
/// </summary>
/// <remarks>
/// <para>
/// Provider-agnostic: every hop is expressed as an <see cref="ObjectSetExpression"/>
/// and dispatched through the public <see cref="IObjectSetProvider.ExecuteAsync{T}"/>.
/// The instance anchor is a typed <see cref="FilterExpression"/> (an
/// <c>id == objectId</c> predicate compiled from the source descriptor's
/// reflection-free <see cref="ObjectTypeDescriptor.IdAccessor"/>, INV-8) — not a
/// <see cref="RawFilterExpression"/>, so the same chain evaluates against the
/// in-memory provider and a SQL provider alike.
/// </para>
/// <para>
/// Inputs are CLOSED VOCABULARY: the link name must resolve to a link on the source
/// descriptor, the depth must be within <see cref="OntologyTraversalLimits.MaxDepth"/>,
/// and the direction is a <see cref="TraversalDirection"/> enum. A violation yields a
/// <see cref="TraversalResult"/> with <see cref="TraversalResult.IsError"/> = true
/// (which the host surfaces as <c>isError: true</c>, SEP-1303) — never a thrown
/// protocol error. Every result carries the INV-3 <c>_meta</c> envelope.
/// </para>
/// </remarks>
public sealed class OntologyTraverseTool
{
    /// <summary>
    /// Self-imposed per-page row budget. A traversal whose far-endpoint set exceeds
    /// this is truncated to the budget and flagged so the host emits a
    /// <c>resource_link</c> to the full subgraph plus a continuation cursor.
    /// </summary>
    public const int RowBudget = 100;

    private readonly OntologyGraph _graph;
    private readonly IObjectSetProvider _objectSetProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OntologyTraverseTool"/> class.
    /// </summary>
    /// <param name="graph">The ontology graph used to resolve descriptors, links, and endpoints.</param>
    /// <param name="objectSetProvider">The provider that executes the traversal expression.</param>
    public OntologyTraverseTool(OntologyGraph graph, IObjectSetProvider objectSetProvider)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(objectSetProvider);
        _graph = graph;
        _objectSetProvider = objectSetProvider;
    }

    /// <summary>
    /// Traverses from the requested source instance across the named association to
    /// the far endpoint, applying any edge-attribute filter before the far hop.
    /// </summary>
    /// <param name="request">The validated, closed-vocabulary traversal request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="TraversalResult"/> carrying the reached endpoints, or an error
    /// result when the request fails closed-vocabulary validation.
    /// </returns>
    [RequiresUnreferencedCode("Edge-attribute filtering reflects over the association CLR type; not safe under trimming.")]
    public async Task<TraversalResult> TraverseAsync(TraversalRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var meta = ResponseMeta.ForGraph(_graph);

        // Closed-vocabulary validation. Each failure returns a structured error
        // result (the host maps it to isError:true) rather than throwing.
        if (request.Depth is < 1 or > OntologyTraversalLimits.MaxDepth)
        {
            return Error(meta, $"depth {request.Depth} is out of range; must be 1..{OntologyTraversalLimits.MaxDepth}.");
        }

        var sourceDescriptor = ResolveDescriptor(request.Domain, request.ObjectType);
        if (sourceDescriptor is null)
        {
            return Error(meta, $"object type '{request.ObjectType}' not found in the ontology graph.");
        }

        var link = sourceDescriptor.Links.FirstOrDefault(l => l.Name == request.LinkName);
        if (link is null)
        {
            var available = string.Join(", ", sourceDescriptor.Links.Select(l => l.Name));
            return Error(meta, $"link '{request.LinkName}' is not a valid link on '{request.ObjectType}'. Available links: [{available}].");
        }

        var association = ResolveAssociationDescriptor(link);
        if (association is null)
        {
            return Error(meta, $"link '{request.LinkName}' does not target a reified association; instance traversal requires an association link.");
        }

        var farRole = ResolveFarRole(association, request.Direction);
        if (farRole is null)
        {
            return Error(meta, $"association '{association.Name}' has no endpoint for direction '{request.Direction}'.");
        }

        if (sourceDescriptor.IdAccessor is null)
        {
            return Error(meta, $"object type '{request.ObjectType}' has no key accessor; cannot anchor a traversal by instance id.");
        }

        // Validate any edge-attribute filter keys against the association's own
        // properties — closed vocabulary on the filter, too.
        if (request.EdgeFilter is { Count: > 0 })
        {
            var unknown = request.EdgeFilter.Keys
                .Where(k => association.Properties.All(p => p.Name != k))
                .ToList();
            if (unknown.Count > 0)
            {
                var available = string.Join(", ", association.Properties.Select(p => p.Name));
                return Error(meta, $"edge attribute(s) [{string.Join(", ", unknown)}] are not defined on association '{association.Name}'. Available: [{available}].");
            }
        }

        var farDescriptor = ResolveDescriptor(association.DomainName, farRole.DescriptorName)
            ?? ResolveDescriptor(request.Domain, farRole.DescriptorName);
        var farClrType = farDescriptor?.ClrType ?? typeof(object);

        var expression = BuildTraversalExpression(sourceDescriptor, request, association, farRole, farClrType);

        var result = await _objectSetProvider.ExecuteAsync<object>(expression, ct).ConfigureAwait(false);

        var endpoints = ProjectEndpoints(result.Items, farRole.DescriptorName, farDescriptor);

        // Self-imposed row budget: truncate and flag so the host emits a
        // resource_link + opaque cursor for the full subgraph.
        if (endpoints.Count > RowBudget)
        {
            var page = endpoints.Take(RowBudget).ToList();
            return new TraversalResult(meta)
            {
                Endpoints = page,
                Truncated = true,
                NextCursor = EncodeCursor(request, RowBudget),
            };
        }

        return new TraversalResult(meta) { Endpoints = endpoints };
    }

    private static TraversalResult Error(ResponseMeta meta, string message) =>
        new(meta) { IsError = true, Error = message };

    private ObjectTypeDescriptor? ResolveDescriptor(string? domain, string name)
    {
        if (domain is not null)
        {
            return _graph.GetObjectType(domain, name);
        }

        // Domain-agnostic resolution: first descriptor whose name matches.
        return _graph.ObjectTypes.FirstOrDefault(t => t.Name == name);
    }

    // A link targets a reified association when its declared target descriptor is
    // itself an ObjectKind.Association. Resolved from the GRAPH by descriptor name
    // (never by CLR type), so a SymbolKey-only association is found identically.
    private ObjectTypeDescriptor? ResolveAssociationDescriptor(LinkDescriptor link)
    {
        var target = _graph.ObjectTypes.FirstOrDefault(t => t.Name == link.TargetTypeName);
        if (target is { Kind: ObjectKind.Association })
        {
            return target;
        }

        // The link may point at a plain endpoint while an association reifies the
        // same link name; find an association whose endpoints include the link's
        // declared target as the destination role.
        return _graph.ObjectTypes
            .Where(t => t.Kind == ObjectKind.Association)
            .FirstOrDefault(a => a.AssociationEndpoints.Any(e => e.DescriptorName == link.TargetTypeName));
    }

    private static AssociationEndpoint? ResolveFarRole(ObjectTypeDescriptor association, TraversalDirection direction)
    {
        // Authoring order: index 0 = source (left), index 1 = destination (right).
        if (association.AssociationEndpoints.Count < 2)
        {
            return null;
        }

        return direction == TraversalDirection.ToDestination
            ? association.AssociationEndpoints[1]
            : association.AssociationEndpoints[0];
    }

    [RequiresUnreferencedCode("Builds an edge-attribute filter that reflects over the association CLR type.")]
    private ObjectSetExpression BuildTraversalExpression(
        ObjectTypeDescriptor sourceDescriptor,
        TraversalRequest request,
        ObjectTypeDescriptor association,
        AssociationEndpoint farRole,
        Type farClrType)
    {
        var sourceClrType = sourceDescriptor.ClrType ?? typeof(object);

        // Root → anchor filter (id == objectId), via the reflection-free IdAccessor.
        ObjectSetExpression expression = new RootExpression(sourceClrType, sourceDescriptor.Name);
        expression = new FilterExpression(expression, BuildIdAnchorPredicate(sourceDescriptor.IdAccessor!, request.ObjectId));

        // Hop to the association objects (the edge view) so edge attributes are
        // filterable BEFORE the far hop — mirrors the in-process
        // .TraverseLink<TEdge>(link) step.
        var edgeClrType = association.ClrType ?? typeof(object);
        expression = new TraverseLinkExpression(expression, request.LinkName, edgeClrType, association.Name);

        // Optional edge-attribute equality filter on the association objects.
        if (request.EdgeFilter is { Count: > 0 })
        {
            expression = new FilterExpression(expression, BuildEdgeFilterPredicate(edgeClrType, request.EdgeFilter));
        }

        // Hop to the far endpoint by role — mirrors the in-process
        // .TraverseLink<TFar>(role) step.
        expression = new TraverseLinkExpression(expression, farRole.Role, farClrType, farRole.DescriptorName);

        return expression;
    }

    // Build a typed FilterExpression predicate wrapping a plain C# closure. The
    // evaluator compiles `Expression.Lambda(...)` and DynamicInvokes it; wrapping a
    // delegate (rather than composing reflective Expression.Call nodes) keeps this
    // AOT-safe (no IL3050) while still producing the LambdaExpression the evaluator
    // expects.
    private static LambdaExpression Predicate(Func<object, bool> body)
    {
        var param = Expression.Parameter(typeof(object), "o");
        var invoke = Expression.Invoke(Expression.Constant(body), param);
        return Expression.Lambda<Func<object, bool>>(invoke, param);
    }

    // `obj => idAccessor(obj) == objectId`. Identity flows ONLY through the supplied
    // IdAccessor (INV-8) — no reflection on the instance to locate identity.
    private static LambdaExpression BuildIdAnchorPredicate(Func<object, object?> idAccessor, string objectId) =>
        Predicate(o => string.Equals(ToIdString(idAccessor(o)), objectId, StringComparison.Ordinal));

    // `obj => all filter pairs equal`, reading each named edge attribute off the
    // association CLR type. Property reads for FILTERING (not identity) are
    // permitted; INV-8 governs identity only.
    [RequiresUnreferencedCode("Edge-attribute filtering reflects over the association CLR type's properties.")]
    private static LambdaExpression BuildEdgeFilterPredicate(Type edgeClrType, IReadOnlyDictionary<string, string> filter)
    {
        var pairs = filter
            .Select(kvp => (Getter: ResolveGetter(edgeClrType, kvp.Key), kvp.Value))
            .ToList();

        return Predicate(o => pairs.All(p => p.Getter is not null
            && string.Equals(ToIdString(p.Getter(o)), p.Value, StringComparison.Ordinal)));
    }

    // Resolve a property getter once (reflection at predicate-build time, never
    // per-row). Returns null when the property is absent so the predicate yields no
    // rows rather than throwing.
    [RequiresUnreferencedCode("Reflects over the association CLR type to resolve an edge-attribute getter.")]
    private static Func<object, object?>? ResolveGetter(Type edgeClrType, string propertyName)
    {
        var property = edgeClrType.GetProperty(propertyName);
        return property is null ? null : property.GetValue;
    }

    // Canonical string form of an id/attribute value for closed-vocabulary equality.
    internal static string ToIdString(object? value) => value?.ToString() ?? string.Empty;

    private static IReadOnlyList<TraversalEndpoint> ProjectEndpoints(
        IReadOnlyList<object> items,
        string farDescriptorName,
        ObjectTypeDescriptor? farDescriptor)
    {
        var endpoints = new List<TraversalEndpoint>(items.Count);
        foreach (var item in items)
        {
            var id = farDescriptor?.IdAccessor is { } accessor
                ? ToIdString(accessor(item))
                : ToIdString(item);
            endpoints.Add(new TraversalEndpoint(farDescriptorName, id));
        }

        return endpoints;
    }

    // Opaque cursor: an offset into the far-endpoint set, encoded so the agent
    // treats it as a token rather than a row. The encoding is intentionally not a
    // public schema — only the tool decodes it.
    private static string EncodeCursor(TraversalRequest request, int offset)
    {
        var raw = $"{request.ObjectType}|{request.ObjectId}|{request.LinkName}|{(int)request.Direction}|{offset}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }
}
