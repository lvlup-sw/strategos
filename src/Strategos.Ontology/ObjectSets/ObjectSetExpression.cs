using System.Linq.Expressions;

namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Abstract base for composable object set query expression nodes.
/// </summary>
public abstract class ObjectSetExpression
{
    protected ObjectSetExpression(Type objectType)
    {
        ObjectType = objectType;
    }

    /// <summary>
    /// The CLR type this expression node produces.
    /// </summary>
    public Type ObjectType { get; }

    /// <summary>
    /// The ontology descriptor name this expression's query targets. Walks the
    /// expression tree back to its <see cref="RootExpression"/> by following
    /// <c>Source</c> references and returns the root's declared
    /// <see cref="RootExpression.ObjectTypeName"/>. <see cref="TraverseLinkExpression"/>
    /// overrides this to return the linked type's descriptor name (see A3).
    /// </summary>
    public virtual string RootObjectTypeName => WalkToRoot(this).ObjectTypeName;

    private static RootExpression WalkToRoot(ObjectSetExpression expr) => expr switch
    {
        RootExpression root => root,
        FilterExpression f => WalkToRoot(f.Source),
        InterfaceNarrowExpression i => WalkToRoot(i.Source),
        RawFilterExpression r => WalkToRoot(r.Source),
        IncludeExpression inc => WalkToRoot(inc.Source),
        SimilarityExpression s => WalkToRoot(s.Source),
        TraverseLinkExpression t => WalkToRoot(t.Source),
        _ => throw new InvalidOperationException($"Unknown expression type: {expr.GetType().Name}")
    };
}

/// <summary>
/// Root expression — the starting point of an object set query.
/// </summary>
public sealed class RootExpression : ObjectSetExpression
{
    public RootExpression(Type objectType, string objectTypeName) : base(objectType)
    {
        ArgumentNullException.ThrowIfNull(objectTypeName);
        ObjectTypeName = objectTypeName;
    }

    /// <summary>
    /// The ontology descriptor name this root was dispatched against.
    /// For a single-registered type this equals <c>ObjectType.Name</c>; for a
    /// multi-registered type this is the explicit descriptor name supplied by
    /// the caller (e.g., via <c>query.GetObjectSet&lt;T&gt;("trading_documents")</c>).
    /// </summary>
    public string ObjectTypeName { get; }
}

/// <summary>
/// Filter expression — represents a Where() predicate applied to a source expression.
/// </summary>
public sealed class FilterExpression : ObjectSetExpression
{
    public FilterExpression(ObjectSetExpression source, LambdaExpression predicate)
        : base(source.ObjectType)
    {
        Source = source;
        Predicate = predicate;
    }

    public ObjectSetExpression Source { get; }
    public LambdaExpression Predicate { get; }
}

/// <summary>
/// Traverse link expression — represents link traversal to a related type.
/// </summary>
public sealed class TraverseLinkExpression : ObjectSetExpression
{
    /// <param name="targetDescriptorName">
    /// DR-10: an OPTIONAL explicit ontology descriptor name for the traversal
    /// target, carried through the expression tree and CONSUMED authoritatively by
    /// the evaluators to dispatch against a specific registration. Mirrors the
    /// <see cref="RootExpression"/>(<c>Type objectType, string objectTypeName</c>)
    /// precedent, where the root carries an explicit descriptor name alongside the
    /// CLR type. When supplied it is the highest-precedence hop-target seam —
    /// <c>InMemoryExpressionEvaluator.ResolveHopTargetDescriptor</c> and
    /// <c>PgVectorObjectSetProvider.ResolveHopTargetDescriptorName</c> both honor it
    /// before consulting the link's declared target, so a CLR type backing several
    /// descriptors routes to the named partition rather than a CLR-first match
    /// (the #128 keystone). When <c>null</c> (the single-arg traversal path), no
    /// override is supplied and the evaluators resolve the hop target from the
    /// source link's declared target instead (never from <c>typeof(TLinked)</c>).
    /// </param>
    public TraverseLinkExpression(
        ObjectSetExpression source,
        string linkName,
        Type linkedType,
        string? targetDescriptorName = null)
        : base(linkedType)
    {
        Source = source;
        LinkName = linkName;
        TargetDescriptorName = targetDescriptorName;
    }

    public ObjectSetExpression Source { get; }
    public string LinkName { get; }

    /// <summary>
    /// DR-10: the EXPLICIT ontology descriptor name the caller selected for the
    /// traversal target, or <c>null</c> when none was supplied. Get-only and
    /// immutable (INV-7). Mirrors <see cref="RootExpression.ObjectTypeName"/>,
    /// which carries the root's explicit descriptor name. The evaluators CONSUME
    /// this field as the highest-precedence hop-target seam (see the constructor
    /// param doc): when set it names the exact target partition authoritatively,
    /// so identity no longer falls back to the CLR type under multi-registration
    /// (the #128 keystone). <c>RootObjectTypeName</c> below is a separate concern —
    /// it reports the post-hop element's CLR-simple name for chained walk-to-root,
    /// not the resolved hop target.
    /// </summary>
    public string? TargetDescriptorName { get; }

    /// <summary>
    /// Traversal breaks the walk-to-root chain: once we've traversed a link, the
    /// query targets the linked type's descriptor, not the source root's. When the
    /// caller supplied an explicit <see cref="TargetDescriptorName"/> this returns
    /// it (so the downstream routing seams — <c>ObjectSet.ApplyAsync</c>,
    /// <c>ObjectSet.EventsAsync</c>, and the Npgsql <c>ResolveTableName</c> — honor
    /// the selected registration), mirroring how <see cref="RootExpression.ObjectTypeName"/>
    /// reports the root's explicit descriptor name. With no override it falls back
    /// to the post-hop element's CLR-simple name.
    /// <para>
    /// DR-10 / #128 note: the <c>TargetDescriptorName ?? ObjectType.Name</c> shape
    /// is exactly what keeps a multi-registered target from misrouting on the
    /// action/event/table path. The earlier assumption — that "multi-registered
    /// types cannot be link targets" makes <c>ObjectType.Name</c> unambiguous here
    /// — was FALSE and is precisely the #128 defect: a CLR type backing several
    /// descriptors does NOT have a unique name, and
    /// <c>OntologyCompositionException</c>'s graph-build guard "AONT041" (a
    /// composition-exception message prefix, NOT a registered Roslyn analyzer id)
    /// only rejects a NARROW case — a link target registered under a non-default
    /// name — not multi-registration generally. The HOP-target resolution proper
    /// still lives in the evaluators, which consume <see cref="TargetDescriptorName"/>
    /// (the explicit override) and otherwise the source link's declared target —
    /// never <c>typeof(TLinked)</c> / <c>ObjectType.Name</c>.
    /// </para>
    /// </summary>
    public override string RootObjectTypeName => TargetDescriptorName ?? ObjectType.Name;
}

/// <summary>
/// Interface narrow expression — narrows to objects implementing a specific interface.
/// </summary>
public sealed class InterfaceNarrowExpression : ObjectSetExpression
{
    public InterfaceNarrowExpression(ObjectSetExpression source, Type interfaceType)
        : base(interfaceType)
    {
        Source = source;
        InterfaceType = interfaceType;
    }

    public ObjectSetExpression Source { get; }
    public Type InterfaceType { get; }
}

/// <summary>
/// Raw filter expression — represents an unprocessed string filter predicate
/// applied to a source expression (e.g., from MCP tool input).
/// </summary>
public sealed class RawFilterExpression : ObjectSetExpression
{
    public RawFilterExpression(ObjectSetExpression source, string filterText)
        : base(source.ObjectType)
    {
        Source = source;
        FilterText = filterText;
    }

    public ObjectSetExpression Source { get; }
    public string FilterText { get; }
}

/// <summary>
/// Include expression — specifies which data facets to include in results.
/// </summary>
public sealed class IncludeExpression : ObjectSetExpression
{
    public IncludeExpression(ObjectSetExpression source, ObjectSetInclusion inclusion)
        : base(source.ObjectType)
    {
        Source = source;
        Inclusion = inclusion;
    }

    public ObjectSetExpression Source { get; }
    public ObjectSetInclusion Inclusion { get; }
}

/// <summary>
/// Similarity expression — represents a vector/semantic similarity search
/// applied to a source expression. Carries raw text; provider is responsible for embedding.
/// </summary>
public sealed class SimilarityExpression : ObjectSetExpression
{
    public SimilarityExpression(
        ObjectSetExpression source,
        string queryText,
        int topK,
        double minRelevance,
        DistanceMetric metric = DistanceMetric.Cosine,
        string? embeddingPropertyName = null,
        float[]? queryVector = null,
        IReadOnlyDictionary<string, object>? filters = null)
        : base(source.ObjectType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryText);
        ArgumentOutOfRangeException.ThrowIfLessThan(topK, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRelevance, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minRelevance, 1.0);

        Source = source;
        QueryText = queryText;
        TopK = topK;
        MinRelevance = minRelevance;
        Metric = metric;
        EmbeddingPropertyName = embeddingPropertyName;
        QueryVector = queryVector;
        Filters = filters;
    }

    /// <summary>
    /// Internal copy constructor for fluent builder mutations.
    /// Chains to the primary constructor so validation always re-runs.
    /// Used by <see cref="SimilarObjectSet{T}"/> fluent setters; not exposed publicly.
    /// </summary>
    internal SimilarityExpression(
        SimilarityExpression source,
        int? topK = null,
        double? minRelevance = null,
        DistanceMetric? metric = null)
        : this(
            source.Source,
            source.QueryText,
            topK ?? source.TopK,
            minRelevance ?? source.MinRelevance,
            metric ?? source.Metric,
            source.EmbeddingPropertyName,
            source.QueryVector,
            source.Filters)
    {
    }

    public ObjectSetExpression Source { get; }
    public string QueryText { get; }
    public int TopK { get; }
    public double MinRelevance { get; }
    public DistanceMetric Metric { get; }
    public string? EmbeddingPropertyName { get; }
    public float[]? QueryVector { get; }
    public IReadOnlyDictionary<string, object>? Filters { get; }
}
