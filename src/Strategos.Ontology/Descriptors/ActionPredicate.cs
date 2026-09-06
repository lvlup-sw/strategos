using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Strategos.Ontology.Descriptors;

/// <summary>Scalar domains supported by property predicates.</summary>
public enum PredicateScalarKind
{
    Boolean,
    Integer,
    Decimal,
    String,
    Enum,
    Symbol,
}

/// <summary>Comparison operators supported by the predicate fragment.</summary>
public enum PredicateComparisonOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
}

/// <summary>Language-neutral reference to an action subject's property.</summary>
public sealed record PredicatePropertyReference
{
    /// <summary>Initializes a property reference.</summary>
    public PredicatePropertyReference(
        string name,
        PredicateScalarKind scalarKind,
        bool isNullable = false,
        string? enumTypeName = null)
    {
        RequireName(name, nameof(name));
        if (!Enum.IsDefined(typeof(PredicateScalarKind), scalarKind))
        {
            throw new ArgumentOutOfRangeException(nameof(scalarKind), scalarKind, "Unknown predicate scalar kind.");
        }

        if (scalarKind == PredicateScalarKind.Enum)
        {
            RequireName(enumTypeName ?? string.Empty, nameof(enumTypeName));
        }
        else if (enumTypeName is not null)
        {
            throw new ArgumentException("Enum type identity is valid only for enum properties.", nameof(enumTypeName));
        }

        Name = name;
        ScalarKind = scalarKind;
        IsNullable = isNullable;
        EnumTypeName = enumTypeName;
    }

    /// <summary>Gets the property name.</summary>
    public string Name { get; }

    /// <summary>Gets the property's supported scalar domain.</summary>
    public PredicateScalarKind ScalarKind { get; }

    /// <summary>Gets whether null is in the property's domain.</summary>
    public bool IsNullable { get; }

    /// <summary>Gets the language-neutral enum type name for enum properties.</summary>
    public string? EnumTypeName { get; }

    internal string CanonicalToken =>
        $"property:{PredicateLiteral.Segment(Name)}:{(int)ScalarKind}:{(IsNullable ? 1 : 0)}:"
        + PredicateLiteral.Segment(EnumTypeName ?? string.Empty);

    private static void RequireName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Predicate property names cannot be empty.", parameterName);
        }
    }
}

/// <summary>
/// Base of the closed, immutable predicate language used by action contracts.
/// Instances are created through the canonicalizing factory members on this type.
/// </summary>
public abstract class ActionPredicate : IEquatable<ActionPredicate>
{
    private protected ActionPredicate()
    {
    }

    /// <summary>Gets the explicit predicate that always holds.</summary>
    public static ActionPredicate True { get; } = new ConstantActionPredicate(true);

    /// <summary>Gets the explicit predicate that never holds.</summary>
    public static ActionPredicate False { get; } = new ConstantActionPredicate(false);

    /// <summary>Gets a deterministic, human-readable projection.</summary>
    public abstract string Expression { get; }

    /// <summary>Gets an unambiguous normalized token for hashes and caches.</summary>
    public abstract string CanonicalToken { get; }

    /// <summary>Gets whether any subtree requires a custom runtime evaluator.</summary>
    public abstract bool ContainsCustom { get; }

    /// <summary>Gets whether any subtree reads a principal relation.</summary>
    public abstract bool ContainsRelation { get; }

    /// <summary>Gets the resources read by this predicate.</summary>
    public abstract ImmutableArray<ActionResource> ReferencedResources { get; }

    /// <summary>Creates a property-to-literal comparison.</summary>
    public static ActionPredicate Property(
        PredicatePropertyReference property,
        PredicateComparisonOperator comparison,
        PredicateLiteral value)
    {
        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!Enum.IsDefined(typeof(PredicateComparisonOperator), comparison))
        {
            throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown predicate comparison operator.");
        }

        ValidateComparison(property, comparison, value);
        return new PropertyComparisonPredicate(property, comparison, value);
    }

    /// <summary>Creates a predicate requiring at least one target for a named link.</summary>
    public static ActionPredicate LinkExists(string linkName)
    {
        RequireName(linkName, nameof(linkName));
        return new LinkExistsPredicate(linkName);
    }

    /// <summary>Creates a principal-relation predicate at the end of a link path.</summary>
    public static ActionPredicate RelationHolds(string relationName, params string[] linkPath)
    {
        RequireName(relationName, nameof(relationName));
        if (linkPath is null)
        {
            throw new ArgumentNullException(nameof(linkPath));
        }

        for (var i = 0; i < linkPath.Length; i++)
        {
            RequireName(linkPath[i], nameof(linkPath));
        }

        return new RelationHoldsPredicate(relationName, linkPath.ToImmutableArray());
    }

    /// <summary>Creates a canonical conjunction.</summary>
    public static ActionPredicate All(params ActionPredicate[] operands) => All((IEnumerable<ActionPredicate>)operands);

    /// <summary>Creates a canonical conjunction.</summary>
    public static ActionPredicate All(IEnumerable<ActionPredicate> operands) => NormalizeAggregate(operands, all: true);

    /// <summary>Creates a canonical disjunction.</summary>
    public static ActionPredicate Any(params ActionPredicate[] operands) => Any((IEnumerable<ActionPredicate>)operands);

    /// <summary>Creates a canonical disjunction.</summary>
    public static ActionPredicate Any(IEnumerable<ActionPredicate> operands) => NormalizeAggregate(operands, all: false);

    /// <summary>Creates a canonical negation.</summary>
    public static ActionPredicate Not(ActionPredicate operand)
    {
        if (operand is null)
        {
            throw new ArgumentNullException(nameof(operand));
        }

        return operand switch
        {
            ConstantActionPredicate constant => constant.Value ? False : True,
            NotPredicate not => not.Operand,
            _ => new NotPredicate(operand),
        };
    }

    /// <summary>Creates an explicitly opaque predicate evaluated by a registered runtime evaluator.</summary>
    public static ActionPredicate Custom(
        string evaluatorKey,
        IEnumerable<PredicateLiteral>? arguments = null,
        IEnumerable<ActionResource>? readSet = null)
    {
        RequireName(evaluatorKey, nameof(evaluatorKey));
        var argumentArray = arguments is null
            ? ImmutableArray<PredicateLiteral>.Empty
            : Snapshot(arguments, nameof(arguments));
        var resources = readSet is null
            ? ImmutableArray<ActionResource>.Empty
            : CanonicalResources(readSet, nameof(readSet));
        return new CustomPredicate(evaluatorKey, argumentArray, resources);
    }

    /// <inheritdoc />
    public bool Equals(ActionPredicate? other) =>
        other is not null && string.Equals(CanonicalToken, other.CanonicalToken, StringComparison.Ordinal);

    /// <inheritdoc />
    public sealed override bool Equals(object? obj) => obj is ActionPredicate predicate && Equals(predicate);

    /// <inheritdoc />
    public sealed override int GetHashCode() => StringComparer.Ordinal.GetHashCode(CanonicalToken);

    /// <summary>Compares predicates by normalized structural identity.</summary>
    public static bool operator ==(ActionPredicate? left, ActionPredicate? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>Compares predicates by normalized structural identity.</summary>
    public static bool operator !=(ActionPredicate? left, ActionPredicate? right) => !(left == right);

    /// <inheritdoc />
    public sealed override string ToString() => Expression;

    private static ActionPredicate NormalizeAggregate(IEnumerable<ActionPredicate> operands, bool all)
    {
        if (operands is null)
        {
            throw new ArgumentNullException(nameof(operands));
        }

        var unique = new SortedDictionary<string, ActionPredicate>(StringComparer.Ordinal);
        foreach (var operand in operands)
        {
            if (operand is null)
            {
                throw new ArgumentException("Predicate aggregates cannot contain null operands.", nameof(operands));
            }

            if (all && operand is ConstantActionPredicate { Value: false })
            {
                return False;
            }

            if (!all && operand is ConstantActionPredicate { Value: true })
            {
                return True;
            }

            if ((all && operand is ConstantActionPredicate { Value: true })
                || (!all && operand is ConstantActionPredicate { Value: false }))
            {
                continue;
            }

            if (all && operand is AllPredicate nestedAll)
            {
                AddRange(unique, nestedAll.Operands);
            }
            else if (!all && operand is AnyPredicate nestedAny)
            {
                AddRange(unique, nestedAny.Operands);
            }
            else
            {
                unique[operand.CanonicalToken] = operand;
            }
        }

        if (unique.Count == 0)
        {
            return all ? True : False;
        }

        if (unique.Count == 1)
        {
            return unique.Values.First();
        }

        var normalized = unique.Values.ToImmutableArray();
        return all ? new AllPredicate(normalized) : new AnyPredicate(normalized);
    }

    private static void AddRange(IDictionary<string, ActionPredicate> destination, IEnumerable<ActionPredicate> operands)
    {
        foreach (var operand in operands)
        {
            destination[operand.CanonicalToken] = operand;
        }
    }

    private static void ValidateComparison(
        PredicatePropertyReference property,
        PredicateComparisonOperator comparison,
        PredicateLiteral value)
    {
        if (value.Kind == PredicateLiteralKind.Null)
        {
            if (!property.IsNullable)
            {
                throw new ArgumentException($"Non-nullable property '{property.Name}' cannot be compared with null.", nameof(value));
            }

            if (comparison is not PredicateComparisonOperator.Equal and not PredicateComparisonOperator.NotEqual)
            {
                throw new ArgumentException("Null supports equality and inequality only.", nameof(comparison));
            }

            return;
        }

        var expectedLiteral = property.ScalarKind switch
        {
            PredicateScalarKind.Boolean => PredicateLiteralKind.Boolean,
            PredicateScalarKind.Integer => PredicateLiteralKind.Integer,
            PredicateScalarKind.Decimal => PredicateLiteralKind.Decimal,
            PredicateScalarKind.String => PredicateLiteralKind.String,
            PredicateScalarKind.Enum => PredicateLiteralKind.Enum,
            PredicateScalarKind.Symbol => PredicateLiteralKind.Symbol,
            _ => throw new ArgumentOutOfRangeException(nameof(property), property.ScalarKind, "Unknown scalar kind."),
        };

        if (value.Kind != expectedLiteral)
        {
            throw new ArgumentException(
                $"Property '{property.Name}' has scalar kind '{property.ScalarKind}' but literal kind is '{value.Kind}'.",
                nameof(value));
        }

        if (property.ScalarKind == PredicateScalarKind.Enum
            && !string.Equals(property.EnumTypeName, value.TypeName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Enum property '{property.Name}' has type '{property.EnumTypeName}' but literal type is '{value.TypeName}'.",
                nameof(value));
        }

        var ordered = property.ScalarKind is PredicateScalarKind.Integer or PredicateScalarKind.Decimal;
        if (!ordered && comparison is not PredicateComparisonOperator.Equal and not PredicateComparisonOperator.NotEqual)
        {
            throw new ArgumentException(
                $"Scalar kind '{property.ScalarKind}' supports equality and inequality only.",
                nameof(comparison));
        }
    }

    private static ImmutableArray<T> Snapshot<T>(IEnumerable<T> source, string parameterName)
        where T : class
    {
        var builder = ImmutableArray.CreateBuilder<T>();
        foreach (var item in source)
        {
            if (item is null)
            {
                throw new ArgumentException("Predicate collections cannot contain null values.", parameterName);
            }

            builder.Add(item);
        }

        return builder.ToImmutable();
    }

    internal static ImmutableArray<ActionResource> CanonicalResources(IEnumerable<ActionResource> resources, string parameterName)
    {
        var unique = new SortedDictionary<string, ActionResource>(StringComparer.Ordinal);
        foreach (var resource in resources)
        {
            if (resource is null)
            {
                throw new ArgumentException("Predicate read sets cannot contain null resources.", parameterName);
            }

            if (!Enum.IsDefined(typeof(ActionResourceKind), resource.Kind))
            {
                throw new ArgumentException(
                    $"Predicate read sets cannot contain unknown resource kind '{(int)resource.Kind}'.",
                    parameterName);
            }

            if (string.IsNullOrWhiteSpace(resource.Name))
            {
                throw new ArgumentException("Predicate read-set resource names cannot be empty.", parameterName);
            }

            var key = ((int)resource.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + PredicateLiteral.Segment(resource.Name);
            unique[key] = resource;
        }

        return unique.Values.ToImmutableArray();
    }

    private static void RequireName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Predicate names cannot be empty.", parameterName);
        }
    }

    internal static ImmutableArray<ActionResource> UnionResources(IEnumerable<ActionPredicate> predicates)
    {
        return CanonicalResources(predicates.SelectMany(predicate => predicate.ReferencedResources), "predicates");
    }

    internal static string AggregateToken(string prefix, ImmutableArray<ActionPredicate> operands) =>
        prefix + ":" + string.Concat(operands.Select(operand => PredicateLiteral.Segment(operand.CanonicalToken)));
}

/// <summary>An explicit Boolean constant predicate.</summary>
public sealed class ConstantActionPredicate : ActionPredicate
{
    internal ConstantActionPredicate(bool value) => Value = value;

    public bool Value { get; }

    public override string Expression => Value ? "true" : "false";

    public override string CanonicalToken => Value ? "constant:1" : "constant:0";

    public override bool ContainsCustom => false;

    public override bool ContainsRelation => false;

    public override ImmutableArray<ActionResource> ReferencedResources => ImmutableArray<ActionResource>.Empty;
}

/// <summary>A property-to-literal comparison.</summary>
public sealed class PropertyComparisonPredicate : ActionPredicate
{
    internal PropertyComparisonPredicate(
        PredicatePropertyReference property,
        PredicateComparisonOperator comparison,
        PredicateLiteral value)
    {
        PropertyReference = property;
        Operator = comparison;
        Value = value;
        ReferencedResources = ImmutableArray.Create(ActionResource.Property(property.Name));
        CanonicalToken = $"comparison:{PredicateLiteral.Segment(property.CanonicalToken)}:{(int)comparison}:{PredicateLiteral.Segment(value.CanonicalToken)}";
        Expression = $"{property.Name} {OperatorText(comparison)} {value}";
    }

    public PredicatePropertyReference PropertyReference { get; }

    public PredicateComparisonOperator Operator { get; }

    public PredicateLiteral Value { get; }

    public override string Expression { get; }

    public override string CanonicalToken { get; }

    public override bool ContainsCustom => false;

    public override bool ContainsRelation => false;

    public override ImmutableArray<ActionResource> ReferencedResources { get; }

    private static string OperatorText(PredicateComparisonOperator comparison) => comparison switch
    {
        PredicateComparisonOperator.Equal => "==",
        PredicateComparisonOperator.NotEqual => "!=",
        PredicateComparisonOperator.LessThan => "<",
        PredicateComparisonOperator.LessThanOrEqual => "<=",
        PredicateComparisonOperator.GreaterThan => ">",
        PredicateComparisonOperator.GreaterThanOrEqual => ">=",
        _ => throw new ArgumentOutOfRangeException(nameof(comparison)),
    };
}

/// <summary>A predicate requiring a named link to have a target.</summary>
public sealed class LinkExistsPredicate : ActionPredicate
{
    internal LinkExistsPredicate(string linkName)
    {
        LinkName = linkName;
        Expression = $"link({PredicateLiteral.Quote(linkName)}) exists";
        CanonicalToken = "link-exists:" + PredicateLiteral.Segment(linkName);
        ReferencedResources = ImmutableArray.Create(ActionResource.Link(linkName));
    }

    public string LinkName { get; }

    public override string Expression { get; }

    public override string CanonicalToken { get; }

    public override bool ContainsCustom => false;

    public override bool ContainsRelation => false;

    public override ImmutableArray<ActionResource> ReferencedResources { get; }
}

/// <summary>A predicate requiring the principal to hold a named relation.</summary>
public sealed class RelationHoldsPredicate : ActionPredicate
{
    internal RelationHoldsPredicate(string relationName, ImmutableArray<string> linkPath)
    {
        RelationName = relationName;
        LinkPath = linkPath;
        var path = linkPath.IsEmpty ? "target" : string.Join("/", linkPath);
        Expression = $"principal -[{relationName}]-> {path}";
        CanonicalToken = "relation:" + PredicateLiteral.Segment(relationName) + ":"
            + string.Concat(linkPath.Select(PredicateLiteral.Segment));
        ReferencedResources = CanonicalResources(linkPath
            .Select(ActionResource.Link)
            .Append(ActionResource.Link(relationName)), nameof(linkPath));
    }

    public string RelationName { get; }

    public ImmutableArray<string> LinkPath { get; }

    public override string Expression { get; }

    public override string CanonicalToken { get; }

    public override bool ContainsCustom => false;

    public override bool ContainsRelation => true;

    public override ImmutableArray<ActionResource> ReferencedResources { get; }
}

/// <summary>A canonical conjunction.</summary>
public sealed class AllPredicate : ActionPredicate
{
    internal AllPredicate(ImmutableArray<ActionPredicate> operands)
    {
        Operands = operands;
        Expression = "(" + string.Join(" && ", operands.Select(operand => operand.Expression)) + ")";
        CanonicalToken = AggregateToken("all", operands);
        ReferencedResources = UnionResources(operands);
        ContainsCustom = operands.Any(operand => operand.ContainsCustom);
        ContainsRelation = operands.Any(operand => operand.ContainsRelation);
    }

    public ImmutableArray<ActionPredicate> Operands { get; }

    public override string Expression { get; }

    public override string CanonicalToken { get; }

    public override bool ContainsCustom { get; }

    public override bool ContainsRelation { get; }

    public override ImmutableArray<ActionResource> ReferencedResources { get; }
}

/// <summary>A canonical disjunction.</summary>
public sealed class AnyPredicate : ActionPredicate
{
    internal AnyPredicate(ImmutableArray<ActionPredicate> operands)
    {
        Operands = operands;
        Expression = "(" + string.Join(" || ", operands.Select(operand => operand.Expression)) + ")";
        CanonicalToken = AggregateToken("any", operands);
        ReferencedResources = UnionResources(operands);
        ContainsCustom = operands.Any(operand => operand.ContainsCustom);
        ContainsRelation = operands.Any(operand => operand.ContainsRelation);
    }

    public ImmutableArray<ActionPredicate> Operands { get; }

    public override string Expression { get; }

    public override string CanonicalToken { get; }

    public override bool ContainsCustom { get; }

    public override bool ContainsRelation { get; }

    public override ImmutableArray<ActionResource> ReferencedResources { get; }
}

/// <summary>A canonical logical negation.</summary>
public sealed class NotPredicate : ActionPredicate
{
    internal NotPredicate(ActionPredicate operand)
    {
        Operand = operand;
        Expression = "!(" + operand.Expression + ")";
        CanonicalToken = "not:" + PredicateLiteral.Segment(operand.CanonicalToken);
    }

    public ActionPredicate Operand { get; }

    public override string Expression { get; }

    public override string CanonicalToken { get; }

    public override bool ContainsCustom => Operand.ContainsCustom;

    public override bool ContainsRelation => Operand.ContainsRelation;

    public override ImmutableArray<ActionResource> ReferencedResources => Operand.ReferencedResources;
}

/// <summary>An explicitly opaque predicate evaluated only at runtime.</summary>
public sealed class CustomPredicate : ActionPredicate
{
    internal CustomPredicate(
        string evaluatorKey,
        ImmutableArray<PredicateLiteral> arguments,
        ImmutableArray<ActionResource> readSet)
    {
        EvaluatorKey = evaluatorKey;
        Arguments = arguments;
        ReadSet = readSet;
        Expression = $"custom({PredicateLiteral.Quote(evaluatorKey)}"
            + (arguments.IsEmpty ? string.Empty : ", " + string.Join(", ", arguments)) + ")";
        CanonicalToken = "custom:" + PredicateLiteral.Segment(evaluatorKey) + ":"
            + string.Concat(arguments.Select(argument => PredicateLiteral.Segment(argument.CanonicalToken))) + ":"
            + string.Concat(readSet.Select(resource =>
                $"{(int)resource.Kind}:{PredicateLiteral.Segment(resource.Name)}"));
    }

    public string EvaluatorKey { get; }

    public ImmutableArray<PredicateLiteral> Arguments { get; }

    public ImmutableArray<ActionResource> ReadSet { get; }

    public override string Expression { get; }

    public override string CanonicalToken { get; }

    public override bool ContainsCustom => true;

    public override bool ContainsRelation => false;

    public override ImmutableArray<ActionResource> ReferencedResources => ReadSet;
}
