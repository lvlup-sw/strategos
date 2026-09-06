using System;
using System.Globalization;
using System.Numerics;

namespace Strategos.Ontology.Descriptors;

/// <summary>Closed set of literal families supported by action predicates.</summary>
public enum PredicateLiteralKind
{
    Null,
    Boolean,
    Integer,
    Decimal,
    String,
    Enum,
    Symbol,
}

/// <summary>
/// A lossless, language-neutral literal in the decidable action-predicate
/// fragment. Equality is based on its kind, optional type name, and canonical
/// invariant value.
/// </summary>
public sealed record PredicateLiteral
{
    private PredicateLiteral(PredicateLiteralKind kind, string canonicalValue, string? typeName = null)
    {
        Kind = kind;
        CanonicalValue = canonicalValue;
        TypeName = typeName;
    }

    /// <summary>Gets the literal family.</summary>
    public PredicateLiteralKind Kind { get; }

    /// <summary>Gets the lossless invariant representation of the value.</summary>
    public string CanonicalValue { get; }

    /// <summary>Gets the language-neutral enum type name, when applicable.</summary>
    public string? TypeName { get; }

    /// <summary>Gets the singleton null literal.</summary>
    public static PredicateLiteral Null { get; } = new(PredicateLiteralKind.Null, string.Empty);

    public static PredicateLiteral Boolean(bool value) =>
        new(PredicateLiteralKind.Boolean, value ? "true" : "false");

    public static PredicateLiteral Integer(BigInteger value) =>
        new(PredicateLiteralKind.Integer, value.ToString(CultureInfo.InvariantCulture));

    public static PredicateLiteral Decimal(PredicateDecimal value) =>
        new(PredicateLiteralKind.Decimal, value.ToString());

    public static PredicateLiteral Decimal(decimal value) => Decimal(PredicateDecimal.FromDecimal(value));

    public static PredicateLiteral Decimal(string value) => Decimal(PredicateDecimal.Parse(value));

    public static PredicateLiteral String(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new PredicateLiteral(PredicateLiteralKind.String, value);
    }

    public static PredicateLiteral Enum(string typeName, string memberName)
    {
        RequireName(typeName, nameof(typeName));
        RequireName(memberName, nameof(memberName));
        return new PredicateLiteral(PredicateLiteralKind.Enum, memberName, typeName);
    }

    public static PredicateLiteral Enum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var matches = System.Enum.GetValues<TEnum>()
            .Count(candidate => EqualityComparer<TEnum>.Default.Equals(candidate, value));
        if (matches != 1)
        {
            throw new ArgumentException(
                "Enum predicates require an unaliased named value.",
                nameof(value));
        }

        var memberName = System.Enum.GetName(value);
        if (memberName is null)
        {
            throw new ArgumentException("Enum predicates require a named value.", nameof(value));
        }

        return Enum(typeof(TEnum).Name, memberName);
    }

    public static PredicateLiteral Symbol(string value)
    {
        RequireName(value, nameof(value));
        return new PredicateLiteral(PredicateLiteralKind.Symbol, value);
    }

    public bool BooleanValue => Kind == PredicateLiteralKind.Boolean
        ? CanonicalValue == "true"
        : throw WrongKind(PredicateLiteralKind.Boolean);

    public BigInteger IntegerValue => Kind == PredicateLiteralKind.Integer
        ? BigInteger.Parse(CanonicalValue, CultureInfo.InvariantCulture)
        : throw WrongKind(PredicateLiteralKind.Integer);

    public PredicateDecimal DecimalValue => Kind == PredicateLiteralKind.Decimal
        ? PredicateDecimal.Parse(CanonicalValue)
        : throw WrongKind(PredicateLiteralKind.Decimal);

    public string StringValue => Kind is PredicateLiteralKind.String or PredicateLiteralKind.Symbol
        ? CanonicalValue
        : throw WrongKind(PredicateLiteralKind.String);

    public string EnumMemberName => Kind == PredicateLiteralKind.Enum
        ? CanonicalValue
        : throw WrongKind(PredicateLiteralKind.Enum);

    /// <summary>Gets an unambiguous token suitable for hashes and caches.</summary>
    public string CanonicalToken =>
        $"literal:{(int)Kind}:{Segment(TypeName ?? string.Empty)}:{Segment(CanonicalValue)}";

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        PredicateLiteralKind.Null => "null",
        PredicateLiteralKind.Boolean => CanonicalValue,
        PredicateLiteralKind.Integer => CanonicalValue,
        PredicateLiteralKind.Decimal => CanonicalValue,
        PredicateLiteralKind.String => Quote(CanonicalValue),
        PredicateLiteralKind.Enum => $"{TypeName}.{CanonicalValue}",
        PredicateLiteralKind.Symbol => $"symbol({Quote(CanonicalValue)})",
        _ => throw new InvalidOperationException($"Unknown literal kind '{Kind}'."),
    };

    internal static string Segment(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;

    internal static string Quote(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(character); break;
            }
        }

        return builder.Append('"').ToString();
    }

    private static void RequireName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Predicate names cannot be empty.", parameterName);
        }
    }

    private InvalidOperationException WrongKind(PredicateLiteralKind expected) =>
        new($"Literal kind '{Kind}' does not expose a {expected} value.");
}
