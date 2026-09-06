using System;
using System.Globalization;
using System.Numerics;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// An arbitrary-precision, exact base-10 value used by the action-predicate
/// language. The value is stored as an integer significand and a decimal scale.
/// </summary>
public readonly struct PredicateDecimal :
    IComparable<PredicateDecimal>,
    IEquatable<PredicateDecimal>
{
    /// <summary>Initializes and canonically normalizes an exact decimal.</summary>
    /// <param name="unscaledValue">The signed integer significand.</param>
    /// <param name="scale">The number of base-10 fractional digits.</param>
    public PredicateDecimal(BigInteger unscaledValue, int scale)
    {
        if (scale < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Decimal scale cannot be negative.");
        }

        if (unscaledValue.IsZero)
        {
            UnscaledValue = BigInteger.Zero;
            Scale = 0;
            return;
        }

        while (scale > 0 && unscaledValue % 10 == 0)
        {
            unscaledValue /= 10;
            scale--;
        }

        UnscaledValue = unscaledValue;
        Scale = scale;
    }

    /// <summary>Gets the signed integer significand.</summary>
    public BigInteger UnscaledValue { get; }

    /// <summary>Gets the number of base-10 fractional digits.</summary>
    public int Scale { get; }

    /// <summary>Parses a non-exponent, invariant-culture decimal literal.</summary>
    /// <param name="text">Text in the form <c>[+-]digits[.digits]</c>.</param>
    /// <returns>The exact decimal value.</returns>
    public static PredicateDecimal Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (!TryParse(text, out var value))
        {
            throw new FormatException($"'{text}' is not an exact base-10 decimal literal.");
        }

        return value;
    }

    /// <summary>Attempts to parse a non-exponent invariant decimal literal.</summary>
    public static bool TryParse(string? text, out PredicateDecimal value)
    {
        value = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var index = 0;
        var negative = false;
        if (text[0] == '+' || text[0] == '-')
        {
            negative = text[0] == '-';
            index++;
        }

        if (index == text.Length)
        {
            return false;
        }

        var point = -1;
        for (var i = index; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '.')
            {
                if (point >= 0)
                {
                    return false;
                }

                point = i;
                continue;
            }

            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        if (point == index || point == text.Length - 1)
        {
            return false;
        }

        var digits = point < 0
            ? text.Substring(index)
            : text.Substring(index, point - index) + text.Substring(point + 1);
        var scale = point < 0 ? 0 : text.Length - point - 1;
        if (!BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var unscaled))
        {
            return false;
        }

        value = new PredicateDecimal(negative ? -unscaled : unscaled, scale);
        return true;
    }

    /// <summary>Creates an exact predicate decimal from a CLR decimal.</summary>
    public static PredicateDecimal FromDecimal(decimal value) =>
        Parse(value.ToString("G29", CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public int CompareTo(PredicateDecimal other)
    {
        if (Scale == other.Scale)
        {
            return UnscaledValue.CompareTo(other.UnscaledValue);
        }

        var commonScale = Math.Max(Scale, other.Scale);
        var left = UnscaledValue * BigInteger.Pow(10, commonScale - Scale);
        var right = other.UnscaledValue * BigInteger.Pow(10, commonScale - other.Scale);
        return left.CompareTo(right);
    }

    /// <inheritdoc />
    public bool Equals(PredicateDecimal other) =>
        UnscaledValue.Equals(other.UnscaledValue) && Scale == other.Scale;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PredicateDecimal other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (UnscaledValue.GetHashCode() * 397) ^ Scale;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Scale == 0)
        {
            return UnscaledValue.ToString(CultureInfo.InvariantCulture);
        }

        var negative = UnscaledValue.Sign < 0;
        var digits = BigInteger.Abs(UnscaledValue).ToString(CultureInfo.InvariantCulture);
        if (digits.Length <= Scale)
        {
            digits = new string('0', Scale - digits.Length + 1) + digits;
        }

        var point = digits.Length - Scale;
        return (negative ? "-" : string.Empty)
            + digits.Substring(0, point)
            + "."
            + digits.Substring(point);
    }

    public static bool operator ==(PredicateDecimal left, PredicateDecimal right) => left.Equals(right);

    public static bool operator !=(PredicateDecimal left, PredicateDecimal right) => !left.Equals(right);

    public static bool operator <(PredicateDecimal left, PredicateDecimal right) => left.CompareTo(right) < 0;

    public static bool operator <=(PredicateDecimal left, PredicateDecimal right) => left.CompareTo(right) <= 0;

    public static bool operator >(PredicateDecimal left, PredicateDecimal right) => left.CompareTo(right) > 0;

    public static bool operator >=(PredicateDecimal left, PredicateDecimal right) => left.CompareTo(right) >= 0;
}
