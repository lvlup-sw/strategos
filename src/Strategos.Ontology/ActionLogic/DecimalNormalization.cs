using System.Collections.Generic;
using System.Numerics;

namespace Strategos.Ontology.ActionLogic;

/// <summary>
/// Canonicalizes exact base-10 values held as a significand and a scale.
/// Source-linked into the analyzer package, so it stays netstandard2.0-safe.
/// </summary>
internal static class DecimalNormalization
{
    /// <summary>
    /// Removes as many trailing base-10 zeros from <paramref name="significand"/>
    /// as <paramref name="scale"/> allows, decrementing the scale to match.
    /// </summary>
    /// <remarks>
    /// Dividing by ten once per zero costs a full-width division per digit,
    /// which is quadratic in the literal length. This divides by 10^(2^i) for
    /// descending i, so the number of divisions is logarithmic in the zero count.
    /// </remarks>
    internal static BigInteger StripTrailingZeros(BigInteger significand, ref int scale)
    {
        if (significand.IsZero)
        {
            scale = 0;
            return significand;
        }

        if (scale <= 0 || !(significand % 10).IsZero)
        {
            return significand;
        }

        var magnitude = BigInteger.Abs(significand);
        var powers = new List<BigInteger>();
        var power = new BigInteger(10);
        var step = 1;
        while (step <= scale && power <= magnitude)
        {
            powers.Add(power);
            if (step > scale / 2)
            {
                break;
            }

            power *= power;
            step *= 2;
        }

        for (var i = powers.Count - 1; i >= 0; i--)
        {
            step = 1 << i;
            if (step > scale)
            {
                continue;
            }

            var quotient = BigInteger.DivRem(significand, powers[i], out var remainder);
            if (remainder.IsZero)
            {
                significand = quotient;
                scale -= step;
            }
        }

        return significand;
    }
}
