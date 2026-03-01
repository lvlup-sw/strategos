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
}

/// <summary>
/// Root expression — the starting point of an object set query.
/// </summary>
public sealed class RootExpression : ObjectSetExpression
{
    public RootExpression(Type objectType) : base(objectType) { }
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
    public TraverseLinkExpression(ObjectSetExpression source, string linkName, Type linkedType)
        : base(linkedType)
    {
        Source = source;
        LinkName = linkName;
    }

    public ObjectSetExpression Source { get; }
    public string LinkName { get; }
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
