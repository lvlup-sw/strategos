using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.ObjectSets;

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
/// Link traversal is schema-level: <c>TraverseLink("countered_by")</c> resolves the link
/// descriptor from the graph and returns all items of the target type. It does not follow
/// materialized link instances between specific objects.
/// </para>
/// </remarks>
public sealed class InMemoryExpressionEvaluator
{
    private readonly OntologyGraph _graph;
    private readonly Dictionary<string, ObjectTypeDescriptor> _descriptorIndex;

    /// <summary>
    /// Initializes a new instance with the specified ontology graph.
    /// </summary>
    /// <param name="graph">
    /// The frozen ontology graph used to resolve link descriptors and interface implementors.
    /// </param>
    public InMemoryExpressionEvaluator(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
        _descriptorIndex = graph.ObjectTypes.ToDictionary(t => t.Name);
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
    /// Thrown for <see cref="RawFilterExpression"/> (string filter parsing not implemented).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a link name or interface cannot be resolved from the ontology graph.
    /// </exception>
    public List<T> Evaluate<T>(
        ObjectSetExpression expression,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(itemResolver);

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
            _ => throw new NotSupportedException(
                $"Expression type '{expression.GetType().Name}' is not supported by InMemoryExpressionEvaluator."),
        };
    }

    private static List<T> EvaluateRoot<T>(
        RootExpression root,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        var items = itemResolver(root.ObjectTypeName);
        return items.OfType<T>().ToList();
    }

    private List<T> EvaluateFilter<T>(
        FilterExpression filter,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        var items = Evaluate<T>(filter.Source, itemResolver);

        var compiled = filter.Predicate.Compile();
        if (compiled is not Func<T, bool> func)
        {
            throw new InvalidOperationException(
                $"Filter predicate type '{compiled.GetType()}' is not compatible with Func<{typeof(T).Name}, bool>.");
        }

        return items.Where(func).ToList();
    }

    private List<T> EvaluateTraverseLink<T>(
        TraverseLinkExpression traverse,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        var sourceDescriptorName = ResolveSourceDescriptorName(traverse.Source);

        if (!_descriptorIndex.TryGetValue(sourceDescriptorName, out var sourceDescriptor))
        {
            var availableTypes = string.Join(", ", _descriptorIndex.Keys.Select(k => $"'{k}'"));
            throw new InvalidOperationException(
                $"Object type '{sourceDescriptorName}' not found in ontology graph. Available types: {availableTypes}");
        }

        var link = sourceDescriptor.Links.FirstOrDefault(l => l.Name == traverse.LinkName);
        if (link is null)
        {
            var availableLinks = string.Join(", ", sourceDescriptor.Links.Select(l => $"'{l.Name}'"));
            throw new InvalidOperationException(
                $"Link '{traverse.LinkName}' not found on object type '{sourceDescriptorName}'. Available links: {availableLinks}");
        }

        var targetItems = itemResolver(link.TargetTypeName);
        return targetItems.OfType<T>().ToList();
    }

    private List<T> EvaluateInterfaceNarrow<T>(
        InterfaceNarrowExpression narrow,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class
    {
        // Recurse with object to avoid premature type casting
        var items = Evaluate<object>(narrow.Source, itemResolver);

        return items
            .Where(item => typeof(T).IsAssignableFrom(item.GetType()))
            .Cast<T>()
            .ToList();
    }

    /// <summary>
    /// Walks the source expression to find the originating descriptor name.
    /// Unlike <see cref="ObjectSetExpression.RootObjectTypeName"/> (which on
    /// <see cref="TraverseLinkExpression"/> returns the target type name),
    /// this walks the source subtree.
    /// </summary>
    private static string ResolveSourceDescriptorName(ObjectSetExpression expression) =>
        expression switch
        {
            RootExpression root => root.ObjectTypeName,
            FilterExpression filter => ResolveSourceDescriptorName(filter.Source),
            IncludeExpression include => ResolveSourceDescriptorName(include.Source),
            TraverseLinkExpression traverse => ResolveSourceDescriptorName(traverse.Source),
            InterfaceNarrowExpression narrow => ResolveSourceDescriptorName(narrow.Source),
            RawFilterExpression raw => ResolveSourceDescriptorName(raw.Source),
            _ => throw new NotSupportedException(
                $"Cannot resolve source descriptor from {expression.GetType().Name}"),
        };
}
