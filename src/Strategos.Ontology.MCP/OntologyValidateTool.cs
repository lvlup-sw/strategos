using System.Globalization;
using System.Numerics;
using System.Text.Json;

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP;

/// <summary>
/// MCP tool that validates a <see cref="DesignIntent"/> against the ontology
/// graph. Aggregates hard/soft constraint evaluations from
/// <see cref="IOntologyQuery.GetActionConstraintReport"/> for each proposed
/// action, the blast-radius estimate, structural pattern violations, and
/// (if <see cref="IOntologyCoverageProvider"/> is registered) a coverage
/// report into a single <see cref="ValidationVerdict"/>.
/// </summary>
public sealed class OntologyValidateTool
{
    private readonly IOntologyQuery _query;
    private readonly IOntologyCoverageProvider? _coverage;

    /// <summary>
    /// Initializes a new instance of the <see cref="OntologyValidateTool"/> class.
    /// </summary>
    /// <param name="query">Ontology query surface used to evaluate the design intent.</param>
    /// <param name="coverage">
    /// Optional coverage provider; when null, the resulting verdict's
    /// <see cref="ValidationVerdict.Coverage"/> is also null.
    /// </param>
    public OntologyValidateTool(IOntologyQuery query, IOntologyCoverageProvider? coverage = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        _query = query;
        _coverage = coverage;
    }

    /// <summary>
    /// Validates a <paramref name="intent"/> against the ontology graph and
    /// returns a verdict aggregating constraint violations, blast radius,
    /// pattern violations, and (if available) coverage.
    /// </summary>
    /// <param name="intent">The design intent to validate.</param>
    /// <returns>A <see cref="ValidationVerdict"/> describing the validation outcome.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="intent"/> is null.
    /// </exception>
    public ValidationVerdict Validate(DesignIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var (hard, soft) = CollectConstraintViolations(intent);
        var blastRadius = _query.EstimateBlastRadius(intent.AffectedNodes);
        var patternViolations = _query.DetectPatternViolations(intent.AffectedNodes, intent);
        var coverage = _coverage?.GetCoverage(intent);

        var passed = hard.Count == 0
            && patternViolations.All(p => p.Severity == ViolationSeverity.Warning);

        return new ValidationVerdict(
            Passed: passed,
            HardViolations: hard,
            SoftWarnings: soft,
            BlastRadius: blastRadius,
            PatternViolations: patternViolations,
            Coverage: coverage);
    }

    private (IReadOnlyList<ConstraintEvaluation> Hard, IReadOnlyList<ConstraintEvaluation> Soft)
        CollectConstraintViolations(DesignIntent intent)
    {
        var hard = new List<ConstraintEvaluation>();
        var soft = new List<ConstraintEvaluation>();

        foreach (var action in intent.Actions)
        {
            // Merge intent.KnownProperties with this action's Arguments so
            // preconditions that read argument values (e.g. "quantity > 0")
            // are evaluated. Both fields share the same key/value shape
            // expected by the constraint evaluator; action arguments win on
            // key collisions because they describe the *invocation* rather
            // than the prior subject state.
            var facts = MergeKnownPropsAndArgs(intent.KnownProperties, action.Arguments);

            // Domain-qualified lookup so a graph with two same-named types in
            // different domains (e.g. trading.Order vs fulfillment.Order)
            // returns the constraint set for the actual subject's type.
            var reports = _query.GetActionConstraintReport(
                action.Subject.Domain,
                action.Subject.ObjectTypeName,
                facts);

            // The query returns a report per registered action on the object type.
            // Narrow to the proposed action so we don't report constraints from
            // other unrelated actions on the same object type.
            var report = reports.FirstOrDefault(r => r.Action.Name == action.ActionName);
            if (report is null)
            {
                // Don't silently swallow unknown/misspelled actions — that
                // would let a typo produce Passed=true on what is actually
                // an invalid intent. Surface as a hard violation.
                hard.Add(BuildUnknownActionViolation(action));
                continue;
            }

            foreach (var evaluation in report.Constraints)
            {
                if (evaluation.IsSatisfied)
                {
                    continue;
                }

                if (evaluation.Strength == ConstraintStrength.Hard)
                {
                    hard.Add(evaluation);
                }
                else
                {
                    soft.Add(evaluation);
                }
            }
        }

        return (hard, soft);
    }

    private static ConstraintEvaluation BuildUnknownActionViolation(ProposedAction action)
    {
        var synthetic = new ActionPrecondition(
            ActionPredicate.Custom(
                "strategos.action-registered",
                [PredicateLiteral.String(action.ActionName)]),
            description:
                $"Action '{action.ActionName}' must be registered on " +
                $"'{action.Subject.Domain}.{action.Subject.ObjectTypeName}'.",
            ConstraintStrength.Hard);

        return new ConstraintEvaluation(
            Precondition: synthetic,
            TruthValue: PredicateTruthValue.Unsatisfied,
            Strength: ConstraintStrength.Hard,
            FailureReason:
                $"Action '{action.ActionName}' is not registered on " +
                $"'{action.Subject.Domain}.{action.Subject.ObjectTypeName}'.",
            ExpectedShape: null);
    }

    private static ActionFacts MergeKnownPropsAndArgs(
        IReadOnlyDictionary<string, object?>? knownProperties,
        IReadOnlyDictionary<string, object?>? actionArguments)
    {
        var properties = new Dictionary<string, PredicateLiteral>(StringComparer.Ordinal);
        AddFacts(properties, knownProperties);
        AddFacts(properties, actionArguments);
        return properties.Count == 0 ? ActionFacts.Empty : new ActionFacts(properties);
    }

    private static void AddFacts(
        IDictionary<string, PredicateLiteral> destination,
        IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var pair in values)
        {
            if (TryCreateLiteral(pair.Value, out var literal))
            {
                destination[pair.Key] = literal;
            }
            else
            {
                // An invocation argument still overrides a same-named known
                // property. Unsupported values therefore make that fact
                // unknown instead of leaking the earlier value through.
                destination.Remove(pair.Key);
            }
        }
    }

    private static bool TryCreateLiteral(object? value, out PredicateLiteral literal)
    {
        switch (value)
        {
            case null:
                literal = PredicateLiteral.Null;
                return true;
            case PredicateLiteral typed:
                literal = typed;
                return true;
            case bool boolean:
                literal = PredicateLiteral.Boolean(boolean);
                return true;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                literal = PredicateLiteral.Integer(BigInteger.Parse(
                    Convert.ToString(value, CultureInfo.InvariantCulture)!,
                    CultureInfo.InvariantCulture));
                return true;
            case BigInteger integer:
                literal = PredicateLiteral.Integer(integer);
                return true;
            case decimal exactDecimal:
                literal = PredicateLiteral.Decimal(exactDecimal);
                return true;
            case PredicateDecimal exactDecimal:
                literal = PredicateLiteral.Decimal(exactDecimal);
                return true;
            case string text:
                literal = PredicateLiteral.String(text);
                return true;
            case Enum enumValue when Enum.GetName(enumValue.GetType(), enumValue) is { } memberName:
                literal = PredicateLiteral.Enum(enumValue.GetType().Name, memberName);
                return true;
            case JsonElement element:
                return TryCreateJsonLiteral(element, out literal);
            default:
                literal = null!;
                return false;
        }
    }

    private static bool TryCreateJsonLiteral(JsonElement value, out PredicateLiteral literal)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                literal = PredicateLiteral.Null;
                return true;
            case JsonValueKind.True:
            case JsonValueKind.False:
                literal = PredicateLiteral.Boolean(value.GetBoolean());
                return true;
            case JsonValueKind.String:
                literal = PredicateLiteral.String(value.GetString()!);
                return true;
            case JsonValueKind.Number:
            {
                var text = value.GetRawText();
                if (BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
                {
                    literal = PredicateLiteral.Integer(integer);
                    return true;
                }

                if (PredicateDecimal.TryParse(text, out var exactDecimal))
                {
                    literal = PredicateLiteral.Decimal(exactDecimal);
                    return true;
                }

                break;
            }
        }

        literal = null!;
        return false;
    }
}
