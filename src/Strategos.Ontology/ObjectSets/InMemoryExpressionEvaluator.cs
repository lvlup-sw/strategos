using System.Linq.Expressions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Identity;

namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Resolves the materialized relation rows for a relation triple
/// (source descriptor, source id, link name), in deterministic read order.
/// Backed by <see cref="InMemoryObjectSetProvider.GetRelations"/> (DR-2).
/// </summary>
/// <param name="srcDescriptor">Descriptor name of the source endpoint.</param>
/// <param name="srcId">Projected id of the source instance.</param>
/// <param name="linkName">Name of the link being traversed.</param>
/// <returns>The relation rows for the triple, or an empty list when none exist.</returns>
internal delegate IReadOnlyList<RelationRow> RelationResolver(
    string srcDescriptor,
    string srcId,
    string linkName);

/// <summary>
/// Evaluates <see cref="ObjectSetExpression"/> trees against in-memory item collections
/// resolved from an external source. Graph-aware: resolves link traversals and interface
/// narrowing against the frozen <see cref="OntologyGraph"/>.
/// </summary>
/// <remarks>
/// This evaluator handles structural expressions (Filter, TraverseLink, InterfaceNarrow,
/// Include, Root). It does NOT handle <see cref="SimilarityExpression"/> (provider-specific
/// scoring) or <see cref="RawFilterExpression"/> (throws <see cref="NotSupportedException"/>).
/// <para>
/// DR-3: link traversal is INSTANCE-ANCHORED, not schema-level. <c>TraverseLink("link")</c>
/// resolves the upstream source set to concrete instances, projects each source id via the
/// reflection-free <see cref="IObjectIdentityProjector"/> (DR-1), and follows only the
/// materialized relation rows stored for that source under the named link (DR-2). It returns
/// exactly the related target instances — never all objects of the link's target type. An
/// instance with no rows yields an empty set (#114). When a row carries a DR-4
/// <see cref="RelationRow.AssociationObjectId"/>, traversing to the association descriptor's
/// CLR type yields the filterable association object; traversing to the target endpoint type
/// yields the far endpoint.
/// </para>
/// </remarks>
public sealed class InMemoryExpressionEvaluator
{
    private readonly Dictionary<string, ObjectTypeDescriptor> _descriptorIndex;
    private readonly Dictionary<string, string> _descriptorNameBySymbolKey;
    private readonly RelationResolver? _relationResolver;
    private readonly IObjectIdentityProjector _idProjector;

    /// <summary>
    /// Initializes a new instance with the specified ontology graph.
    /// </summary>
    /// <param name="graph">
    /// The frozen ontology graph used to resolve link descriptors and interface implementors.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if the graph contains multiple object types with the same descriptor name
    /// across different domains. Use unique descriptor names or qualify with domain.
    /// </exception>
    public InMemoryExpressionEvaluator(OntologyGraph graph)
        : this(graph, relationResolver: null, idProjector: null)
    {
    }

    /// <summary>
    /// Initializes a new instance wired to the relate-store for instance-anchored
    /// traversal (DR-3). The owning <see cref="InMemoryObjectSetProvider"/> supplies its
    /// own <see cref="InMemoryObjectSetProvider.GetRelations"/> accessor and id projector,
    /// both internal to this assembly.
    /// </summary>
    /// <param name="graph">The frozen ontology graph.</param>
    /// <param name="relationResolver">
    /// Resolves the materialized relation rows for a source instance under a link. When
    /// null (graph-only construction with no relate-store), instance-anchored traversal
    /// yields an empty set — it never falls back to a type-level fetch.
    /// </param>
    /// <param name="idProjector">
    /// The DR-1 projector used to map each upstream source/target instance to its stable
    /// id with zero reflection on the instance type (INV-8). Defaults to the standard
    /// <see cref="ObjectIdentityProjector"/> when null.
    /// </param>
    internal InMemoryExpressionEvaluator(
        OntologyGraph graph,
        RelationResolver? relationResolver,
        IObjectIdentityProjector? idProjector)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _descriptorIndex = new Dictionary<string, ObjectTypeDescriptor>();
        // DR-10: reverse index from a descriptor's polyglot SymbolKey to its
        // canonical name, used to resolve a link's TargetSymbolKey fallback to a
        // partition name without any CLR Type participation (INV-8).
        _descriptorNameBySymbolKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ot in graph.ObjectTypes)
        {
            if (!_descriptorIndex.TryAdd(ot.Name, ot))
            {
                var existing = _descriptorIndex[ot.Name];
                throw new ArgumentException(
                    $"Descriptor name '{ot.Name}' is registered in both domain '{existing.DomainName}' " +
                    $"and domain '{ot.DomainName}'. InMemoryExpressionEvaluator requires globally unique " +
                    $"descriptor names. Rename one of the registrations to disambiguate.",
                    nameof(graph));
            }

            if (ot.SymbolKey is not null)
            {
                // Last writer wins on a shared SymbolKey; descriptor names are
                // already globally unique so this only collides under a genuine
                // SymbolKey duplication, which is a graph-authoring error.
                _descriptorNameBySymbolKey[ot.SymbolKey] = ot.Name;
            }
        }

        _relationResolver = relationResolver;
        _idProjector = idProjector ?? new ObjectIdentityProjector();
    }

    /// <summary>
    /// Evaluates an expression tree against items resolved from the given source.
    /// </summary>
    /// <typeparam name="T">The expected result element type.</typeparam>
    /// <param name="expression">The expression tree to evaluate.</param>
    /// <param name="itemResolver">
    /// Resolves items by ontology descriptor name. Called with a descriptor name (e.g.,
    /// "WedgeFormation"), returns all items of that type as an untyped list. The evaluator
    /// handles casting and filtering.
    /// </param>
    /// <returns>Filtered, traversed, or narrowed items matching the expression.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown for <see cref="RawFilterExpression"/> or <see cref="SimilarityExpression"/>
    /// (string filter parsing and similarity scoring not implemented).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a link name or interface cannot be resolved from the ontology graph.
    /// </exception>
    public List<T> Evaluate<T>(
        ObjectSetExpression expression,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        return expression switch
        {
            RootExpression root => EvaluateRoot<T>(root, itemResolver),
            FilterExpression filter => EvaluateFilter<T>(filter, itemResolver),
            IncludeExpression include => Evaluate<T>(include.Source, itemResolver),
            TraverseLinkExpression traverse => EvaluateTraverseLink<T>(traverse, itemResolver),
            InterfaceNarrowExpression narrow => EvaluateInterfaceNarrow<T>(narrow, itemResolver),
            RawFilterExpression => throw new NotSupportedException(
                "RawFilterExpression evaluation is not supported by InMemoryExpressionEvaluator. " +
                "Use ObjectSet<T>.Where() with typed predicates instead of raw filter strings."),
            SimilarityExpression => throw new NotSupportedException(
                "SimilarityExpression evaluation is not supported by InMemoryExpressionEvaluator. " +
                "Similarity scoring is provider-specific."),
            _ => throw new NotSupportedException(
                $"Expression type '{expression.GetType().Name}' is not supported by InMemoryExpressionEvaluator.")
        };
    }

    private static List<T> EvaluateRoot<T>(
        RootExpression root,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        var items = itemResolver(root.ObjectTypeName);
        return items.Cast<T>().ToList();
    }

    private List<T> EvaluateFilter<T>(
        FilterExpression filter,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        var sourceItems = Evaluate<T>(filter.Source, itemResolver);
        var compiled = filter.Predicate.Compile();

        if (compiled is Func<T, bool> typedPredicate)
        {
            return sourceItems.Where(typedPredicate).ToList();
        }

        throw new InvalidOperationException(
            $"Filter predicate type '{compiled.GetType().Name}' is not compatible with Func<{typeof(T).Name}, bool>.");
    }

    // Untyped (object-space) evaluation of a source expression, used by DR-3
    // traversal to resolve the upstream instances regardless of their static
    // element type. Filter predicates run by their own declared element type via
    // DynamicInvoke, so a Func&lt;TSource, bool&gt; applies correctly even though
    // the result list is typed as object. The id projection downstream is still
    // reflection-free (DR-1 IdAccessor); this walk performs no reflection on the
    // instance to LOCATE identity.
    private List<object> EvaluateUntyped(
        ObjectSetExpression expression,
        Func<string, IReadOnlyList<object>> itemResolver)
    {
        switch (expression)
        {
            case RootExpression root:
                return itemResolver(root.ObjectTypeName).ToList();

            case IncludeExpression include:
                return EvaluateUntyped(include.Source, itemResolver);

            case FilterExpression filter:
                var sourceItems = EvaluateUntyped(filter.Source, itemResolver);
                var compiled = filter.Predicate.Compile();
                return sourceItems
                    .Where(item => (bool)compiled.DynamicInvoke(item)!)
                    .ToList();

            case InterfaceNarrowExpression narrow:
                return EvaluateUntyped(narrow.Source, itemResolver)
                    .Where(item => narrow.InterfaceType.IsInstanceOfType(item))
                    .ToList();

            case TraverseLinkExpression traverse:
                // A chained traversal: resolve the prior hop's results in object
                // space via the same core, so the association-vs-target decision
                // is driven by the expression's own ObjectType.
                return EvaluateTraverseLinkCore(traverse, itemResolver);

            default:
                throw new NotSupportedException(
                    $"Expression type '{expression.GetType().Name}' is not supported as a traversal source.");
        }
    }

    private List<T> EvaluateTraverseLink<T>(
        TraverseLinkExpression traverse,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
        => EvaluateTraverseLinkCore(traverse, itemResolver).Cast<T>().ToList();

    // DR-3: instance-anchored traversal core. Resolve the upstream source set to
    // concrete instances, project each source id (DR-1, reflection-free), follow
    // ONLY the materialized relation rows for that source under the named link
    // (DR-2), and resolve the requested instances from the rows — never a
    // type-level fetch of all target-type items (the #114 defect).
    //
    // The requested type is carried on the expression (traverse.ObjectType), so
    // the typed and untyped entry points make the same association-vs-target
    // decision regardless of the caller's generic argument.
    private List<object> EvaluateTraverseLinkCore(
        TraverseLinkExpression traverse,
        Func<string, IReadOnlyList<object>> itemResolver)
    {
        // The source descriptor of a traversal is the descriptor of the IMMEDIATE
        // upstream element type — not the chain root. A chained hop
        // (.TraverseLink<TRel>("link").TraverseLink<TNode>("To")) must resolve its
        // source as the association descriptor, so it routes through the
        // far-endpoint hop rather than treating TNode as the source.
        var sourceDescriptorName = ResolveImmediateSourceDescriptorName(traverse.Source);

        if (!_descriptorIndex.TryGetValue(sourceDescriptorName, out var sourceDescriptor))
        {
            var available = string.Join(", ", _descriptorIndex.Keys);
            throw new InvalidOperationException(
                $"Object type '{sourceDescriptorName}' not found in ontology graph. Available types: {available}");
        }

        // DR-4 far-endpoint hop: when the upstream set is association objects
        // (e.g. the result of a prior TraverseLink<TRel>), this traversal hops
        // FROM each surviving association TO its endpoint, named by the
        // association descriptor's endpoint role. The far endpoint is resolved
        // from the SAME relation row that produced the association object — never
        // by reflecting over the association instance's endpoint property.
        if (sourceDescriptor.Kind == ObjectKind.Association)
        {
            return EvaluateAssociationEndpointHop(traverse, sourceDescriptor, itemResolver);
        }

        var link = sourceDescriptor.Links.FirstOrDefault(l => l.Name == traverse.LinkName);
        if (link is null)
        {
            var available = string.Join(", ", sourceDescriptor.Links.Select(l => l.Name));
            throw new InvalidOperationException(
                $"Link '{traverse.LinkName}' not found on object type '{sourceDescriptorName}'. Available links: {available}");
        }

        // Resolve the upstream set to concrete source instances. Filters, narrows
        // and prior traversals on the source all run first, so traversal is
        // anchored to exactly the instances the upstream query selected. The
        // untyped walk applies filter predicates by their declared element type
        // (the source's typed Func&lt;TSource, bool&gt;), so it works whatever the
        // upstream element type is.
        var sourceInstances = EvaluateUntyped(traverse.Source, itemResolver);

        var rows = GatherRows(sourceDescriptor, sourceDescriptorName, traverse.LinkName, sourceInstances);

        // DR-4 association hop: when the rows carry an association object id and
        // the traversal targets an ASSOCIATION descriptor, yield the association
        // objects so edge attributes are filterable BEFORE the far hop. Otherwise
        // yield the far-endpoint target instances.
        //
        // DR-10 (#128): the association descriptor's PARTITION is resolved from
        // the ONTOLOGY GRAPH, never re-derived from typeof(traverse.ObjectType).
        if (TryResolveAssociationHopDescriptor(traverse, link, out var associationDescriptorName))
        {
            return ResolveAssociationObjects(rows, associationDescriptorName, itemResolver);
        }

        return ResolveTargetEndpoints(rows, itemResolver);
    }

    // DR-10 (#128) keystone: resolve a hop's TARGET descriptor name from the
    // ontology GRAPH, never from typeof(traverse.ObjectType). Precedence:
    //   1. an explicit TraverseLinkExpression.TargetDescriptorName (the caller's
    //      .TraverseLink(link, descriptorName) selection — mirrors how a root
    //      carries an explicit descriptor name);
    //   2. otherwise the SOURCE descriptor's LinkDescriptor for this link:
    //      its TargetTypeName (the canonical hand-authored target name), falling
    //      back to the descriptor named by TargetSymbolKey (the polyglot,
    //      SymbolKey-only target).
    //
    // This is the single seam multi-registration and SymbolKey-only targets must
    // flow through: a CLR type backing two descriptors no longer routes to the
    // first CLR match, and an ingested (ClrType == null) target is resolvable.
    // No CLR Type participates in identity/partition resolution here (INV-8).
    //
    // Factored as a standalone helper so a later Npgsql traversal task can reuse
    // the exact same graph-driven hop-target resolution.
    private string ResolveHopTargetDescriptor(TraverseLinkExpression traverse, LinkDescriptor link)
    {
        if (traverse.TargetDescriptorName is { } explicitName)
        {
            return explicitName;
        }

        if (!string.IsNullOrEmpty(link.TargetTypeName))
        {
            return link.TargetTypeName;
        }

        if (link.TargetSymbolKey is { } symbolKey
            && _descriptorNameBySymbolKey.TryGetValue(symbolKey, out var nameFromSymbol))
        {
            return nameFromSymbol;
        }

        // No graph-resolvable target. Surface the link's own polyglot key (or its
        // empty TargetTypeName) so the downstream partition lookup misses
        // deterministically rather than silently consulting typeof(TLinked).
        return link.TargetSymbolKey ?? link.TargetTypeName;
    }

    // DR-10 (#128): decide whether this hop yields the DR-4 edge view (the
    // association objects) and, if so, resolve the association descriptor's
    // PARTITION from the ONTOLOGY GRAPH. A hop yields the edge view when its
    // graph-resolved target descriptor is itself an ObjectKind.Association.
    //
    // Two graph-driven seams produce that descriptor name, in precedence order:
    //   1. an explicit TraverseLinkExpression.TargetDescriptorName — the caller's
    //      .TraverseLink(link, descriptorName) selection. This is the authoritative
    //      MULTI-REGISTRATION disambiguator (#128): when one CLR type backs several
    //      association descriptors, the override names the exact partition, so no
    //      CLR Type participates in identity/partition resolution.
    //   2. otherwise the link's declared target (ResolveHopTargetDescriptor) when
    //      that target is itself an association — i.e. the link points straight at
    //      the reified edge.
    //
    // LEGACY CLR-VIEW fallback: when NO override is supplied AND the link's
    // declared target is a plain endpoint (a node), a caller can still request the
    // edge view by passing the association's CLR type as TLinked. With a single
    // registration this is unambiguous, so we resolve the lone association
    // descriptor whose ClrType matches. This is the only place a CLR Type is
    // consulted, and ONLY when the graph supplies no disambiguating descriptor
    // name; under multi-registration the override (seam 1) is required and the
    // CLR path is never reached. This preserves the DR-4 edge-view ergonomics
    // (.TraverseLink&lt;TRel&gt;("link")) for the common single-registration case.
    private bool TryResolveAssociationHopDescriptor(
        TraverseLinkExpression traverse,
        LinkDescriptor link,
        out string associationDescriptorName)
    {
        // Seam 1 + 2: the graph-resolved hop target descriptor name. An explicit
        // override wins; otherwise the link's declared target.
        var hopTargetName = ResolveHopTargetDescriptor(traverse, link);
        if (_descriptorIndex.TryGetValue(hopTargetName, out var hopTarget)
            && hopTarget.Kind == ObjectKind.Association)
        {
            associationDescriptorName = hopTargetName;
            return true;
        }

        // The graph-resolved target is a plain endpoint. Only when the caller gave
        // NO descriptor override do we fall back to the legacy single-registration
        // CLR-type edge-view match (see method remarks).
        if (traverse.TargetDescriptorName is null)
        {
            return TryResolveSingleAssociationByClrType(traverse.ObjectType, out associationDescriptorName);
        }

        associationDescriptorName = string.Empty;
        return false;
    }

    // Legacy single-registration edge-view resolution: the lone association
    // descriptor whose ClrType is the requested type. Returns false when the type
    // backs no association OR backs MORE THAN ONE (ambiguous — the caller must
    // disambiguate with an explicit descriptor name, see #128). Never consulted
    // when an explicit TargetDescriptorName is supplied.
    private bool TryResolveSingleAssociationByClrType(Type requestedType, out string associationDescriptorName)
    {
        associationDescriptorName = string.Empty;
        var matches = 0;
        foreach (var descriptor in _descriptorIndex.Values)
        {
            if (descriptor.Kind == ObjectKind.Association && descriptor.ClrType == requestedType)
            {
                associationDescriptorName = descriptor.Name;
                matches++;
            }
        }

        // Ambiguous (multi-registration) — refuse to guess a partition from the
        // CLR type. The caller must pass an explicit descriptor name (#128).
        if (matches != 1)
        {
            associationDescriptorName = string.Empty;
            return false;
        }

        return true;
    }

    // Gathers the materialized relation rows for every selected source instance,
    // in the store's deterministic read order (INV-7), de-duplicated across
    // sources. Returns empty when no relate-store is wired in — instance-anchored
    // traversal NEVER falls back to a type-level fetch (#114).
    private List<RelationRow> GatherRows(
        ObjectTypeDescriptor sourceDescriptor,
        string sourceDescriptorName,
        string linkName,
        IReadOnlyList<object> sourceInstances)
    {
        var rows = new List<RelationRow>();
        if (_relationResolver is null)
        {
            return rows;
        }

        var seenRowKeys = new HashSet<(string, string, string?)>();
        foreach (var source in sourceInstances)
        {
            var srcId = _idProjector.ProjectId(sourceDescriptor, source);
            foreach (var row in _relationResolver(sourceDescriptorName, srcId, linkName))
            {
                if (seenRowKeys.Add((row.TargetDescriptor, row.TargetId, row.AssociationObjectId)))
                {
                    rows.Add(row);
                }
            }
        }

        return rows;
    }

    // DR-4 far-endpoint hop. The traversal source is a set of association objects
    // produced by a prior TraverseLink&lt;TRel&gt;; this hop resolves each
    // surviving association's far endpoint. The link name must match one of the
    // association descriptor's endpoint roles (index 0 = source, 1 = destination).
    //
    // The far endpoint is taken from the original relation row whose
    // AssociationObjectId equals the surviving association's projected id, so an
    // intervening .Where(a => a.Status == ...) filter on the association objects
    // changes which far endpoints come back — without ever reflecting over the
    // association instance to read its endpoint property.
    private List<object> EvaluateAssociationEndpointHop(
        TraverseLinkExpression traverse,
        ObjectTypeDescriptor associationDescriptor,
        Func<string, IReadOnlyList<object>> itemResolver)
    {
        var role = traverse.LinkName;
        var endpointIndex = IndexOfEndpointRole(associationDescriptor, role);
        if (endpointIndex < 0)
        {
            var roles = string.Join(", ", associationDescriptor.AssociationEndpoints.Select(e => e.Role));
            throw new InvalidOperationException(
                $"Endpoint role '{role}' not found on association '{associationDescriptor.Name}'. Available roles: {roles}");
        }

        // Re-derive the ORIGINAL rows from the association traversal in the source
        // chain — these carry both the AssociationObjectId and the far endpoint.
        var originatingTraversal = FindAssociationTraversal(traverse.Source)
            ?? throw new InvalidOperationException(
                $"Endpoint hop '{role}' has no originating association traversal in its source chain.");

        var originSourceDescriptorName = ResolveImmediateSourceDescriptorName(originatingTraversal.Source);
        if (!_descriptorIndex.TryGetValue(originSourceDescriptorName, out var originSource))
        {
            var available = string.Join(", ", _descriptorIndex.Keys);
            throw new InvalidOperationException(
                $"Object type '{originSourceDescriptorName}' not found in ontology graph. Available types: {available}");
        }

        var originInstances = EvaluateUntyped(originatingTraversal.Source, itemResolver);
        var originRows = GatherRows(
            originSource,
            originSource.Name,
            originatingTraversal.LinkName,
            originInstances);

        // The surviving (post-filter) association objects, by their projected id.
        var survivingAssociationIds = EvaluateUntyped(traverse.Source, itemResolver)
            .Select(a => _idProjector.ProjectId(associationDescriptor, a))
            .ToHashSet(StringComparer.Ordinal);

        // The destination-role endpoint (index 1) resolves via the row's
        // (TargetDescriptor, TargetId); the source-role endpoint (index 0) resolves
        // via the originating traversal's source side.
        var results = new List<object>();
        foreach (var row in originRows)
        {
            if (row.AssociationObjectId is null
                || !survivingAssociationIds.Contains(row.AssociationObjectId))
            {
                continue;
            }

            var resolved = endpointIndex == 0
                ? ResolveById(originSource.Name, ProjectedSourceIdForRow(originInstances, originSource, originatingTraversal, row), itemResolver)
                : ResolveById(row.TargetDescriptor, row.TargetId, itemResolver);

            if (resolved is not null)
            {
                results.Add(resolved);
            }
        }

        return results;
    }

    // For the source-role endpoint hop, find the originating source instance id
    // that produced this row. Walks the origin instances and returns the first
    // whose rows include this exact row (same association object id + target).
    //
    // F-MED-1: a row produced by the association traversal MUST have an
    // originating source — so failing to find one is an inconsistency, not a
    // routine miss. Returning string.Empty here would feed ResolveById(..., "")
    // → null → a SILENT drop, diverging from the destination-role hop's
    // never-silent-drop contract (DR-8). Throw a typed error instead.
    private string ProjectedSourceIdForRow(
        IReadOnlyList<object> originInstances,
        ObjectTypeDescriptor originSource,
        TraverseLinkExpression originatingTraversal,
        RelationRow row)
    {
        if (_relationResolver is not null)
        {
            foreach (var instance in originInstances)
            {
                var srcId = _idProjector.ProjectId(originSource, instance);
                foreach (var candidate in _relationResolver(originSource.Name, srcId, originatingTraversal.LinkName))
                {
                    if (candidate.AssociationObjectId == row.AssociationObjectId
                        && candidate.TargetDescriptor == row.TargetDescriptor
                        && candidate.TargetId == row.TargetId)
                    {
                        return srcId;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"Source-role endpoint hop on association '{originSource.Name}' could not resolve the " +
            $"originating source for row (target '{row.TargetDescriptor}':'{row.TargetId}', " +
            $"association '{row.AssociationObjectId}'). The relation row has no originating source " +
            $"instance under link '{originatingTraversal.LinkName}' — the relate-store is inconsistent.");
    }

    private static int IndexOfEndpointRole(ObjectTypeDescriptor associationDescriptor, string role)
    {
        for (var i = 0; i < associationDescriptor.AssociationEndpoints.Count; i++)
        {
            if (string.Equals(associationDescriptor.AssociationEndpoints[i].Role, role, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // Walks a traversal source chain to the nearest TraverseLinkExpression that
    // produced association objects. DR-10 (#128): "produced an association" is
    // decided from the GRAPH — the hop's graph-resolved target descriptor is an
    // ObjectKind.Association — never from typeof(traverse.ObjectType). Returns
    // null when none is present.
    private TraverseLinkExpression? FindAssociationTraversal(ObjectSetExpression expression) =>
        expression switch
        {
            TraverseLinkExpression traverse when IsAssociationProducingHop(traverse) => traverse,
            TraverseLinkExpression traverse => FindAssociationTraversal(traverse.Source),
            FilterExpression filter => FindAssociationTraversal(filter.Source),
            IncludeExpression include => FindAssociationTraversal(include.Source),
            InterfaceNarrowExpression narrow => FindAssociationTraversal(narrow.Source),
            _ => null,
        };

    // DR-10 (#128): a hop "produces association objects" when it resolves to the
    // DR-4 edge view via TryResolveAssociationHopDescriptor — the SAME graph-first
    // decision the live hop makes (explicit override → link-declared association →
    // legacy single-registration CLR match). Routing this detection through the
    // shared helper keeps the chained far-endpoint hop and the live hop in lock-
    // step, so neither re-derives a partition from typeof(TLinked) for identity.
    // A hop whose immediate source is not a known non-association descriptor with
    // the named link is not association-producing.
    private bool IsAssociationProducingHop(TraverseLinkExpression traverse)
    {
        var sourceDescriptorName = ResolveImmediateSourceDescriptorName(traverse.Source);
        if (!_descriptorIndex.TryGetValue(sourceDescriptorName, out var sourceDescriptor)
            || sourceDescriptor.Kind == ObjectKind.Association)
        {
            return false;
        }

        var link = sourceDescriptor.Links.FirstOrDefault(l => l.Name == traverse.LinkName);
        if (link is null)
        {
            return false;
        }

        return TryResolveAssociationHopDescriptor(traverse, link, out _);
    }

    // Resolves the far-endpoint target instances referenced by the rows. Each row
    // names a (TargetDescriptor, TargetId); the instance is the one in that
    // descriptor's partition whose projected id matches TargetId.
    private List<object> ResolveTargetEndpoints(
        IReadOnlyList<RelationRow> rows,
        Func<string, IReadOnlyList<object>> itemResolver)
    {
        var results = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            var resolved = ResolveById(row.TargetDescriptor, row.TargetId, itemResolver);
            if (resolved is not null)
            {
                results.Add(resolved);
            }
        }

        return results;
    }

    // Resolves the association objects referenced by the rows' AssociationObjectId
    // (DR-4) from the association descriptor's partition. Rows with no association
    // (plain DR-2 relate) contribute nothing to an association hop.
    private List<object> ResolveAssociationObjects(
        IReadOnlyList<RelationRow> rows,
        string associationDescriptorName,
        Func<string, IReadOnlyList<object>> itemResolver)
    {
        var results = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            if (row.AssociationObjectId is null)
            {
                continue;
            }

            var resolved = ResolveById(associationDescriptorName, row.AssociationObjectId, itemResolver);
            if (resolved is not null)
            {
                results.Add(resolved);
            }
        }

        return results;
    }

    // Resolves a single instance from a descriptor partition by matching its
    // projected id (DR-1, reflection-free) against the requested id. Returns null
    // when the partition has no matching instance.
    private object? ResolveById(
        string descriptorName,
        string id,
        Func<string, IReadOnlyList<object>> itemResolver)
    {
        if (!_descriptorIndex.TryGetValue(descriptorName, out var descriptor))
        {
            return null;
        }

        foreach (var candidate in itemResolver(descriptorName))
        {
            if (string.Equals(_idProjector.ProjectId(descriptor, candidate), id, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private List<T> EvaluateInterfaceNarrow<T>(
        InterfaceNarrowExpression narrow,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        var sourceItems = Evaluate<object>(narrow.Source, itemResolver);
        return sourceItems
            .Where(item => typeof(T).IsAssignableFrom(item.GetType()))
            .Cast<T>()
            .ToList();
    }

    // Resolves the descriptor name of the IMMEDIATE upstream element type (not the
    // chain root). Filters/includes preserve their source's element type, so we
    // skip them to the nearest PRODUCING node: a traversal produces its linked
    // type (ObjectType.Name); a root produces its declared descriptor name (which
    // can differ from ObjectType.Name for a multi-registered type). This is what
    // lets a chained TraverseLink resolve its source as the IMMEDIATE prior hop —
    // e.g. an association hop routes through the far-endpoint path rather than
    // mistaking the chain root for the source.
    //
    // ALIAS-LOSS limitation (strategos #128, deferred): a TraverseLinkExpression
    // hop resolves to traverse.ObjectType.Name, which is the CLR/object-type name
    // rather than the descriptor ALIAS the prior hop was registered under. After
    // a hop the descriptor alias is therefore lost, so a subsequent hop whose
    // source was an aliased (multi-registered) descriptor resolves against the
    // bare type name and may route to the wrong partition. Threading the prior
    // hop's descriptor identity (alias-preserving) through the chain is tracked
    // under strategos #128 (descriptor-identity-through-the-chain) and
    // intentionally NOT fixed here.
    private static string ResolveImmediateSourceDescriptorName(ObjectSetExpression expression) =>
        expression switch
        {
            RootExpression root => root.ObjectTypeName,
            TraverseLinkExpression traverse => traverse.ObjectType.Name,
            FilterExpression filter => ResolveImmediateSourceDescriptorName(filter.Source),
            IncludeExpression include => ResolveImmediateSourceDescriptorName(include.Source),
            InterfaceNarrowExpression narrow => narrow.InterfaceType.Name,
            RawFilterExpression raw => ResolveImmediateSourceDescriptorName(raw.Source),
            _ => throw new NotSupportedException(
                $"Cannot resolve immediate source descriptor from {expression.GetType().Name}"),
        };
}
