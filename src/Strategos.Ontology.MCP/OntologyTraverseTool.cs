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

        var association = ResolveAssociationDescriptor(link, out var ambiguityError);
        if (ambiguityError is not null)
        {
            return Error(meta, ambiguityError);
        }

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

        // F2: real multi-hop chaining — the far node of each level is re-rooted as
        // the source of the next association hop, matching the in-memory chained
        // traversal (.TraverseLink(association).TraverseLink(farRole) per level).
        var farExpression = BuildTraversalExpression(sourceDescriptor, request, association, farRole, farClrType);

        // F1: an edge-view sub-expression that stops one hop short of the far hop —
        // it surfaces the surviving association objects in the SAME GetRelations-
        // ordered, edge-filtered row order as the far endpoints. The two lists pair
        // positionally, so we zip them and read edge-attribute values off each
        // association object (reflection getters; INV-8 governs IDENTITY only).
        var edgeExpression = BuildEdgeViewExpression(sourceDescriptor, request, association, farRole, farClrType);

        var farResult = await _objectSetProvider.ExecuteAsync<object>(farExpression, ct).ConfigureAwait(false);
        var edgeResult = await _objectSetProvider.ExecuteAsync<object>(edgeExpression, ct).ConfigureAwait(false);

        var edgeAttributeReaders = ResolveEdgeAttributeReaders(association);

        var endpoints = ProjectEndpoints(
            farResult.Items,
            edgeResult.Items,
            farRole.DescriptorName,
            farDescriptor,
            edgeAttributeReaders);

        // F3: decode any incoming cursor to an offset, page from it, and emit the
        // next offset as the continuation cursor. The first page has offset 0.
        var offset = DecodeCursorOffset(request);

        // Self-imposed row budget: page the endpoints from the decoded offset and,
        // when more remain, flag truncation so the host emits a resource_link + the
        // opaque continuation cursor for the next page. The next-offset addition is
        // done in `long` so a crafted near-int.MaxValue decoded offset cannot
        // overflow `int` and corrupt the Truncated / NextCursor pagination state.
        var paged = endpoints.Skip(offset).Take(RowBudget).ToList();
        var nextOffset = (long)offset + RowBudget;
        var hasMore = endpoints.Count > nextOffset;
        if (hasMore)
        {
            return new TraversalResult(meta)
            {
                Endpoints = paged,
                Truncated = true,
                NextCursor = EncodeCursor(request, nextOffset),
            };
        }

        return new TraversalResult(meta) { Endpoints = paged };
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
    //
    // INV-8 / #128: when the link points at a plain endpoint and MULTIPLE associations
    // reify an endpoint with that target descriptor, "first match" can bind the WRONG
    // edge type (and so apply the wrong edge-filter vocabulary). Disambiguate by the
    // link NAME — the named role being traversed — preferring an association endpoint
    // whose Role equals the link name; only when no endpoint role matches do we accept
    // a descriptor-only match, and then ONLY if it is unique. A genuinely ambiguous
    // link (same endpoint descriptor, no role disambiguator) fails fast via
    // <paramref name="ambiguityError"/> rather than silently binding the first.
    private ObjectTypeDescriptor? ResolveAssociationDescriptor(LinkDescriptor link, out string? ambiguityError)
    {
        ambiguityError = null;

        var target = _graph.ObjectTypes.FirstOrDefault(t => t.Name == link.TargetTypeName);
        if (target is { Kind: ObjectKind.Association })
        {
            return target;
        }

        // Candidate associations: those whose endpoints include the link's declared
        // target descriptor as a role. (Descriptor-name match only — INV-8.)
        var candidates = _graph.ObjectTypes
            .Where(t => t.Kind == ObjectKind.Association)
            .Where(a => a.AssociationEndpoints.Any(e => e.DescriptorName == link.TargetTypeName))
            .ToList();

        if (candidates.Count <= 1)
        {
            return candidates.FirstOrDefault();
        }

        // More than one association reifies this endpoint descriptor. First try to
        // disambiguate by the link NAME matching an endpoint ROLE (the named role
        // being traversed) on the target descriptor.
        var byRole = candidates
            .Where(a => a.AssociationEndpoints.Any(e =>
                e.DescriptorName == link.TargetTypeName
                && string.Equals(e.Role, link.Name, StringComparison.Ordinal)))
            .ToList();

        if (byRole.Count == 1)
        {
            return byRole[0];
        }

        // Still ambiguous (zero role matches, or several associations share the same
        // link-name role on the same endpoint descriptor) — fail fast naming the
        // ambiguity instead of binding the first candidate.
        var names = string.Join(", ", (byRole.Count > 1 ? byRole : candidates).Select(a => a.Name));
        ambiguityError =
            $"link '{link.Name}' (target '{link.TargetTypeName}') is ambiguous: it matches multiple "
            + $"reified associations [{names}] and cannot be disambiguated by link name. Traverse via a "
            + "link whose name matches the intended association endpoint role.";
        return null;
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

    // F2: builds the FULL far-endpoint chain — Root → anchor filter, then one
    // association/edge-filter/far-role level per requested depth. Each subsequent
    // level re-roots its association hop at the prior far node (whose descriptor is
    // the SAME farRole.DescriptorName, since a level repeats the same link), so a
    // depth-N request walks N hops rather than a fixed 2-hop chain. The in-memory
    // evaluator re-derives each hop's immediate source descriptor from the prior
    // node, so the chain evaluates against the provider unchanged.
    [RequiresUnreferencedCode("Builds an edge-attribute filter that reflects over the association CLR type.")]
    private ObjectSetExpression BuildTraversalExpression(
        ObjectTypeDescriptor sourceDescriptor,
        TraversalRequest request,
        ObjectTypeDescriptor association,
        AssociationEndpoint farRole,
        Type farClrType)
    {
        var expression = BuildAnchoredChain(sourceDescriptor, request, association, farRole, farClrType, request.Depth, stopBeforeFinalFarHop: false);
        return expression;
    }

    // F1: builds the chain ONE hop shorter than the far chain — it stops after the
    // final association hop's edge filter, BEFORE the final far-role hop, so its
    // result is the surviving association objects (the edge view) in the same
    // GetRelations-ordered, edge-filtered order as the far endpoints. The far and
    // edge results therefore pair positionally and can be zipped.
    [RequiresUnreferencedCode("Builds an edge-attribute filter that reflects over the association CLR type.")]
    private ObjectSetExpression BuildEdgeViewExpression(
        ObjectTypeDescriptor sourceDescriptor,
        TraversalRequest request,
        ObjectTypeDescriptor association,
        AssociationEndpoint farRole,
        Type farClrType)
    {
        var expression = BuildAnchoredChain(sourceDescriptor, request, association, farRole, farClrType, request.Depth, stopBeforeFinalFarHop: true);
        return expression;
    }

    // Shared builder for the anchored traversal chain. Emits the Root → anchor
    // filter, then <paramref name="levels"/> association/edge-filter/far-role
    // levels. When <paramref name="stopBeforeFinalFarHop"/> is true the LAST level
    // omits its far-role hop, leaving the chain at the surviving association objects
    // (the F1 edge view); otherwise every level ends at its far endpoint (the F2
    // far chain).
    [RequiresUnreferencedCode("Builds an edge-attribute filter that reflects over the association CLR type.")]
    private ObjectSetExpression BuildAnchoredChain(
        ObjectTypeDescriptor sourceDescriptor,
        TraversalRequest request,
        ObjectTypeDescriptor association,
        AssociationEndpoint farRole,
        Type farClrType,
        int levels,
        bool stopBeforeFinalFarHop)
    {
        var sourceClrType = sourceDescriptor.ClrType ?? typeof(object);
        var edgeClrType = association.ClrType ?? typeof(object);

        // Root → anchor filter (id == objectId), via the reflection-free IdAccessor.
        ObjectSetExpression expression = new RootExpression(sourceClrType, sourceDescriptor.Name);
        expression = new FilterExpression(expression, BuildIdAnchorPredicate(sourceDescriptor.IdAccessor!, request.ObjectId));

        for (var level = 0; level < levels; level++)
        {
            var isFinalLevel = level == levels - 1;

            // Hop to the association objects (the edge view) so edge attributes are
            // filterable BEFORE the far hop — mirrors the in-process
            // .TraverseLink<TEdge>(link) step. Each subsequent level re-roots this
            // association hop at the prior far node's descriptor: the in-memory
            // evaluator resolves the immediate source from the prior hop, and a
            // repeated link keeps the source descriptor stable across levels.
            expression = new TraverseLinkExpression(expression, request.LinkName, edgeClrType, association.Name);

            // Optional edge-attribute equality filter on the association objects.
            if (request.EdgeFilter is { Count: > 0 })
            {
                expression = new FilterExpression(expression, BuildEdgeFilterPredicate(edgeClrType, request.EdgeFilter));
            }

            // The F1 edge view stops at the surviving association objects of the
            // final level (no far-role hop); the far chain always hops to the far
            // endpoint by role — mirrors the in-process .TraverseLink<TFar>(role).
            if (isFinalLevel && stopBeforeFinalFarHop)
            {
                break;
            }

            expression = new TraverseLinkExpression(expression, farRole.Role, farClrType, farRole.DescriptorName);
        }

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

    // F1: resolve a getter per declared edge-attribute property of the association,
    // once (reflection at build time, never per row). Reading edge-attribute VALUES
    // off the association CLR type is permitted — INV-8 governs descriptor IDENTITY
    // (names), not property reads. Properties that do not resolve are skipped.
    [RequiresUnreferencedCode("Reflects over the association CLR type to resolve edge-attribute getters.")]
    private static IReadOnlyList<(string Name, Func<object, object?> Getter)> ResolveEdgeAttributeReaders(
        ObjectTypeDescriptor association)
    {
        var edgeClrType = association.ClrType;
        if (edgeClrType is null)
        {
            return [];
        }

        var readers = new List<(string, Func<object, object?>)>(association.Properties.Count);
        foreach (var property in association.Properties)
        {
            if (ResolveGetter(edgeClrType, property.Name) is { } getter)
            {
                readers.Add((property.Name, getter));
            }
        }

        return readers;
    }

    // F1: project the far endpoints, zipping each with the surviving association
    // object that produced it (lockstep row order — the edge view and the far
    // chain both iterate the same GetRelations-ordered, edge-filtered rows). Edge-
    // attribute VALUES are read off the paired association object via the resolved
    // getters; an endpoint with no paired association carries an empty attribute map.
    private static IReadOnlyList<TraversalEndpoint> ProjectEndpoints(
        IReadOnlyList<object> farItems,
        IReadOnlyList<object> edgeItems,
        string farDescriptorName,
        ObjectTypeDescriptor? farDescriptor,
        IReadOnlyList<(string Name, Func<object, object?> Getter)> edgeAttributeReaders)
    {
        var endpoints = new List<TraversalEndpoint>(farItems.Count);
        for (var i = 0; i < farItems.Count; i++)
        {
            var item = farItems[i];
            var id = farDescriptor?.IdAccessor is { } accessor
                ? ToIdString(accessor(item))
                : ToIdString(item);

            var edgeAttributes = i < edgeItems.Count
                ? ReadEdgeAttributes(edgeItems[i], edgeAttributeReaders)
                : EmptyEdgeAttributes;

            endpoints.Add(new TraversalEndpoint(farDescriptorName, id) { EdgeAttributes = edgeAttributes });
        }

        return endpoints;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyEdgeAttributes =
        new Dictionary<string, object?>();

    private static IReadOnlyDictionary<string, object?> ReadEdgeAttributes(
        object associationObject,
        IReadOnlyList<(string Name, Func<object, object?> Getter)> readers)
    {
        if (readers.Count == 0)
        {
            return EmptyEdgeAttributes;
        }

        var attributes = new Dictionary<string, object?>(readers.Count, StringComparer.Ordinal);
        foreach (var (name, getter) in readers)
        {
            attributes[name] = getter(associationObject);
        }

        return attributes;
    }

    // Opaque cursor: an offset into the far-endpoint set, encoded so the agent
    // treats it as a token rather than a row. The encoding is intentionally not a
    // public schema — only the tool decodes it.
    private static string EncodeCursor(TraversalRequest request, long offset)
    {
        var raw = $"{request.ObjectType}|{request.ObjectId}|{request.LinkName}|{(int)request.Direction}|{offset}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    // F3: decode the offset carried by an incoming cursor (the trailing field of
    // the EncodeCursor payload). A null/blank/malformed cursor yields offset 0 —
    // the first page — so a tampered token degrades to the start rather than
    // throwing. Only the offset is consumed; the other fields are provenance the
    // agent must not have to reconstruct.
    private static int DecodeCursorOffset(TraversalRequest request)
    {
        if (string.IsNullOrEmpty(request.Cursor))
        {
            return 0;
        }

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.Cursor));
            var lastSeparator = raw.LastIndexOf('|');
            if (lastSeparator >= 0
                && int.TryParse(raw.AsSpan(lastSeparator + 1), out var offset)
                && offset >= 0)
            {
                return offset;
            }
        }
        catch (FormatException)
        {
            // Not valid base64 — treat as a missing cursor (first page).
        }

        return 0;
    }
}
