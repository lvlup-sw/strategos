using System.Diagnostics;
using System.Numerics;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

/// <summary>
/// The trailing-zero loop in <see cref="PredicateDecimal"/> does a full-width
/// division per digit. These tests fail while that cost is quadratic and pass
/// once a literal with this many fractional zeros is rejected or normalized
/// in linear time.
/// </summary>
public class PredicateDecimalNormalizationCostTests
{
    // Measured on a debug build: 40_000 trailing zeros took ~971ms, and the
    // 20_000 case was ~308ms, so 50_000 zeros is about 1.5s. A trim, a length
    // cap, or one divide by 10^k finishes in milliseconds.
    private const int TrailingFractionalZeros = 50_000;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(250);

    [Test]
    public async Task TryParse_TrailingFractionalZeros_IsBoundedAndCanonicalOrRejected()
    {
        var literal = TrailingZeroLiteral(TrailingFractionalZeros);

        var parsed = false;
        PredicateDecimal value = default;
        var elapsed = Time(() => parsed = PredicateDecimal.TryParse(literal, out value));

        if (parsed)
        {
            await AssertCanonicalOne(value);
        }

        await Assert.That(elapsed).IsLessThan(Budget);
    }

    [Test]
    public async Task TryParse_NegativeTrailingFractionalZeros_IsBoundedAndCanonicalOrRejected()
    {
        var literal = "-" + TrailingZeroLiteral(TrailingFractionalZeros);

        var parsed = false;
        PredicateDecimal value = default;
        var elapsed = Time(() => parsed = PredicateDecimal.TryParse(literal, out value));

        if (parsed)
        {
            await Assert.That(value.Scale).IsEqualTo(0);
            await Assert.That(value.UnscaledValue).IsEqualTo(BigInteger.MinusOne);
            await Assert.That(value.ToString()).IsEqualTo("-1");
        }

        await Assert.That(elapsed).IsLessThan(Budget);
    }

    [Test]
    public async Task Parse_ShortTrailingZeros_StaysCanonical()
    {
        var value = PredicateDecimal.Parse("1.2500");

        await Assert.That(value.ToString()).IsEqualTo("1.25");
        await Assert.That(value.Scale).IsEqualTo(2);
    }

    [Test]
    public async Task Constructor_StripsTrailingZerosInBoundedTime()
    {
        var zeros = TrailingFractionalZeros;
        var unscaled = BigInteger.Parse("1" + new string('0', zeros));

        PredicateDecimal value = default;
        var elapsed = Time(() => value = new PredicateDecimal(unscaled, zeros));

        await AssertCanonicalOne(value);
        await Assert.That(elapsed).IsLessThan(Budget);
    }

    [Test]
    public async Task PredicateLiteral_DecimalString_TrailingZeros_IsBounded()
    {
        var literal = TrailingZeroLiteral(TrailingFractionalZeros);

        string? canonical = null;
        var rejected = false;
        var elapsed = Time(() =>
        {
            try
            {
                canonical = PredicateLiteral.Decimal(literal).ToString();
            }
            catch (FormatException)
            {
                rejected = true;
            }
        });

        await Assert.That(rejected || canonical == "1").IsTrue();
        await Assert.That(elapsed).IsLessThan(Budget);
    }

    private static async Task AssertCanonicalOne(PredicateDecimal value)
    {
        await Assert.That(value.Scale).IsEqualTo(0);
        await Assert.That(value.UnscaledValue).IsEqualTo(BigInteger.One);
        await Assert.That(value.ToString()).IsEqualTo("1");
    }

    private static string TrailingZeroLiteral(int zeros) => "1." + new string('0', zeros);

    private static TimeSpan Time(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }
}
