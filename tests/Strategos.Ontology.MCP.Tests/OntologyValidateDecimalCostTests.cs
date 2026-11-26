using System.Diagnostics;
using System.Text.Json;

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// <see cref="OntologyValidateTool"/> turns caller JSON numbers into
/// <see cref="PredicateDecimal"/> values. A number written as <c>1</c> followed
/// by a long run of fractional zeros must not occupy the caller for a
/// quadratic normalization, whether it arrives as a known property or as an
/// action argument.
/// </summary>
public class OntologyValidateDecimalCostTests
{
    private const int TrailingFractionalZeros = 50_000;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(250);

    [Test]
    public async Task Validate_ShortJsonDecimal_BecomesCanonicalFact()
    {
        var captured = CaptureFacts(JsonNumber("1.2500"), propertyOnArguments: false);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Properties["price"].ToString()).IsEqualTo("1.25");
    }

    [Test]
    public async Task Validate_KnownPropertyTrailingZeros_IsBounded()
    {
        ActionFacts? captured = null;
        var elapsed = Time(() =>
            captured = CaptureFacts(JsonNumber(TrailingZeroLiteral(TrailingFractionalZeros)), propertyOnArguments: false));

        await AssertBoundedFact(elapsed, captured, "price", "1");
    }

    [Test]
    public async Task Validate_ActionArgumentTrailingZeros_IsBounded()
    {
        ActionFacts? captured = null;
        var elapsed = Time(() =>
            captured = CaptureFacts(JsonNumber(TrailingZeroLiteral(TrailingFractionalZeros)), propertyOnArguments: true));

        await AssertBoundedFact(elapsed, captured, "price", "1");
    }

    [Test]
    public async Task Validate_NegativeActionArgumentTrailingZeros_IsBounded()
    {
        ActionFacts? captured = null;
        var elapsed = Time(() =>
            captured = CaptureFacts(
                JsonNumber("-" + TrailingZeroLiteral(TrailingFractionalZeros)),
                propertyOnArguments: true));

        await AssertBoundedFact(elapsed, captured, "price", "-1");
    }

    private static async Task AssertBoundedFact(
        TimeSpan elapsed,
        ActionFacts? captured,
        string key,
        string canonical)
    {
        await Assert.That(captured).IsNotNull();
        if (captured!.Properties.TryGetValue(key, out var literal))
        {
            await Assert.That(literal.Kind is PredicateLiteralKind.Decimal or PredicateLiteralKind.Integer).IsTrue();
            await Assert.That(literal.ToString()).IsEqualTo(canonical);
        }

        await Assert.That(elapsed).IsLessThan(Budget);
    }

    private static ActionFacts? CaptureFacts(JsonElement number, bool propertyOnArguments)
    {
        ActionFacts? captured = null;
        var query = Substitute.For<IOntologyQuery>();
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<ActionFacts?>())
            .Returns(call =>
            {
                captured = call.ArgAt<ActionFacts?>(1);
                return Array.Empty<ActionConstraintReport>();
            });
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActionFacts?>())
            .Returns(call =>
            {
                captured = call.ArgAt<ActionFacts?>(2);
                return Array.Empty<ActionConstraintReport>();
            });
        query
            .EstimateBlastRadius(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<BlastRadiusOptions?>())
            .Returns(new BlastRadius(
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<CrossDomainHop>(),
                BlastRadiusScope.Local));
        query
            .DetectPatternViolations(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<DesignIntent>())
            .Returns(Array.Empty<PatternViolation>());

        var subject = new OntologyNodeRef("trading", "Order", "ord-1");
        Dictionary<string, object?>? known = propertyOnArguments
            ? null
            : new Dictionary<string, object?> { ["price"] = number };
        Dictionary<string, object?>? arguments = propertyOnArguments
            ? new Dictionary<string, object?> { ["price"] = number }
            : null;
        var intent = new DesignIntent(
            new List<OntologyNodeRef> { subject },
            new List<ProposedAction> { new("Ship", subject, arguments) },
            known);

        _ = new OntologyValidateTool(query).Validate(intent);
        return captured;
    }

    private static JsonElement JsonNumber(string literal)
    {
        using var document = JsonDocument.Parse("{\"v\":" + literal + "}");
        return document.RootElement.GetProperty("v").Clone();
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
