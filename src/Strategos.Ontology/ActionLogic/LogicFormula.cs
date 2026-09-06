using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Strategos.Ontology.ActionLogic;

internal enum LogicFormulaKind
{
    False,
    True,
    Comparison,
    BooleanAtom,
    All,
    Any,
    Not,
    Opaque,
}

internal enum LogicScalarKind
{
    Boolean,
    Integer,
    Decimal,
    String,
    Enum,
    Symbol,
}

internal enum LogicLiteralKind
{
    Null,
    Boolean,
    Integer,
    Decimal,
    String,
    Enum,
    Symbol,
}

internal enum LogicComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
}

internal enum LogicTruthValue
{
    False,
    Unknown,
    True,
}

internal sealed class LogicResource : IEquatable<LogicResource>
{
    internal LogicResource(
        string key,
        LogicScalarKind scalarKind,
        bool isNullable,
        string? scalarTypeName = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A predicate resource key cannot be empty.", nameof(key));
        }

        Key = key;
        ScalarKind = scalarKind;
        IsNullable = isNullable;
        ScalarTypeName = scalarTypeName;
        StableKey = FormattableString.Invariant(
            $"{key.Length}:{key}|{(int)scalarKind}|{(isNullable ? 1 : 0)}|{(scalarTypeName ?? string.Empty).Length}:{scalarTypeName ?? string.Empty}");
    }

    internal string Key { get; }

    internal LogicScalarKind ScalarKind { get; }

    internal bool IsNullable { get; }

    internal string? ScalarTypeName { get; }

    internal string StableKey { get; }

    public bool Equals(LogicResource? other) =>
        other is not null && string.Equals(StableKey, other.StableKey, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as LogicResource);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(StableKey);
}

internal sealed class LogicLiteral : IEquatable<LogicLiteral>
{
    internal LogicLiteral(
        LogicLiteralKind kind,
        string canonicalValue,
        string? scalarTypeName = null)
    {
        if (canonicalValue is null)
        {
            throw new ArgumentNullException(nameof(canonicalValue));
        }

        if (kind == LogicLiteralKind.Enum && string.IsNullOrWhiteSpace(scalarTypeName))
        {
            throw new ArgumentException("An enum literal requires a scalar type name.", nameof(scalarTypeName));
        }

        if (kind != LogicLiteralKind.Enum && scalarTypeName is not null)
        {
            throw new ArgumentException("Only enum literals carry a scalar type name.", nameof(scalarTypeName));
        }

        Kind = kind;
        CanonicalValue = canonicalValue;
        ScalarTypeName = scalarTypeName;
        StableKey = FormattableString.Invariant(
            $"{(int)kind}|{(scalarTypeName ?? string.Empty).Length}:{scalarTypeName ?? string.Empty}|{canonicalValue.Length}:{canonicalValue}");
    }

    internal static LogicLiteral Null { get; } = new(LogicLiteralKind.Null, string.Empty);

    internal LogicLiteralKind Kind { get; }

    internal string CanonicalValue { get; }

    internal string? ScalarTypeName { get; }

    internal string StableKey { get; }

    public bool Equals(LogicLiteral? other) =>
        other is not null && string.Equals(StableKey, other.StableKey, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as LogicLiteral);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(StableKey);
}

internal sealed class LogicFormula : IEquatable<LogicFormula>
{
    private LogicFormula(
        LogicFormulaKind kind,
        string stableKey,
        LogicResource? resource = null,
        LogicComparisonOperator comparisonOperator = LogicComparisonOperator.Equal,
        LogicLiteral? literal = null,
        ImmutableArray<LogicFormula> operands = default,
        string? opaqueKey = null)
    {
        Kind = kind;
        StableKey = stableKey;
        Resource = resource;
        ComparisonOperator = comparisonOperator;
        Literal = literal;
        Operands = operands.IsDefault ? ImmutableArray<LogicFormula>.Empty : operands;
        OpaqueKey = opaqueKey;
    }

    internal static LogicFormula False { get; } = new(LogicFormulaKind.False, "false");

    internal static LogicFormula True { get; } = new(LogicFormulaKind.True, "true");

    internal LogicFormulaKind Kind { get; }

    internal LogicResource? Resource { get; }

    internal LogicComparisonOperator ComparisonOperator { get; }

    internal LogicLiteral? Literal { get; }

    internal ImmutableArray<LogicFormula> Operands { get; }

    internal string? OpaqueKey { get; }

    internal string StableKey { get; }

    internal bool ContainsOpaque =>
        Kind == LogicFormulaKind.Opaque || Operands.Any(operand => operand.ContainsOpaque);

    internal static LogicFormula Comparison(
        LogicResource resource,
        LogicComparisonOperator comparisonOperator,
        LogicLiteral literal)
    {
        if (resource is null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        if (literal is null)
        {
            throw new ArgumentNullException(nameof(literal));
        }

        var stableKey = FormattableString.Invariant(
            $"cmp|{resource.StableKey.Length}:{resource.StableKey}|{(int)comparisonOperator}|{literal.StableKey}");
        return new LogicFormula(
            LogicFormulaKind.Comparison,
            stableKey,
            resource,
            comparisonOperator,
            literal);
    }

    internal static LogicFormula BooleanAtom(LogicResource resource)
    {
        if (resource is null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        return new LogicFormula(
            LogicFormulaKind.BooleanAtom,
            $"atom|{resource.StableKey.Length}:{resource.StableKey}",
            resource);
    }

    internal static LogicFormula Opaque(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("An opaque predicate key cannot be empty.", nameof(key));
        }

        return new LogicFormula(LogicFormulaKind.Opaque, $"opaque|{key.Length}:{key}", opaqueKey: key);
    }

    internal static LogicFormula Not(LogicFormula operand)
    {
        if (operand is null)
        {
            throw new ArgumentNullException(nameof(operand));
        }

        return operand.Kind switch
        {
            LogicFormulaKind.True => False,
            LogicFormulaKind.False => True,
            LogicFormulaKind.Not => operand.Operands[0],
            _ => new LogicFormula(
                LogicFormulaKind.Not,
                $"not|{operand.StableKey.Length}:{operand.StableKey}",
                operands: ImmutableArray.Create(operand)),
        };
    }

    internal static LogicFormula All(params LogicFormula[] operands) => All((IEnumerable<LogicFormula>)operands);

    internal static LogicFormula All(IEnumerable<LogicFormula> operands) =>
        Junction(LogicFormulaKind.All, operands);

    internal static LogicFormula Any(params LogicFormula[] operands) => Any((IEnumerable<LogicFormula>)operands);

    internal static LogicFormula Any(IEnumerable<LogicFormula> operands) =>
        Junction(LogicFormulaKind.Any, operands);

    public bool Equals(LogicFormula? other) =>
        other is not null && string.Equals(StableKey, other.StableKey, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as LogicFormula);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(StableKey);

    public override string ToString() => StableKey;

    private static LogicFormula Junction(
        LogicFormulaKind kind,
        IEnumerable<LogicFormula> operands)
    {
        if (operands is null)
        {
            throw new ArgumentNullException(nameof(operands));
        }

        var flattened = new List<LogicFormula>();
        foreach (var operand in operands)
        {
            if (operand is null)
            {
                throw new ArgumentException("A predicate junction cannot contain null.", nameof(operands));
            }

            if (kind == LogicFormulaKind.All && operand.Kind == LogicFormulaKind.False)
            {
                return False;
            }

            if (kind == LogicFormulaKind.Any && operand.Kind == LogicFormulaKind.True)
            {
                return True;
            }

            if ((kind == LogicFormulaKind.All && operand.Kind == LogicFormulaKind.True)
                || (kind == LogicFormulaKind.Any && operand.Kind == LogicFormulaKind.False))
            {
                continue;
            }

            if (operand.Kind == kind)
            {
                flattened.AddRange(operand.Operands);
            }
            else
            {
                flattened.Add(operand);
            }
        }

        var canonical = flattened
            .GroupBy(operand => operand.StableKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(operand => operand.StableKey, StringComparer.Ordinal)
            .ToImmutableArray();

        if (canonical.Length == 0)
        {
            return kind == LogicFormulaKind.All ? True : False;
        }

        if (canonical.Length == 1)
        {
            return canonical[0];
        }

        var prefix = kind == LogicFormulaKind.All ? "all" : "any";
        var stableKey = prefix + "|" + string.Concat(
            canonical.Select(operand => $"{operand.StableKey.Length}:{operand.StableKey}"));
        return new LogicFormula(kind, stableKey, operands: canonical);
    }
}
