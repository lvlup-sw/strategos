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
            var reports = _query.GetActionConstraintReport(
                action.Subject.ObjectTypeName,
                intent.KnownProperties);

            // The query returns a report per registered action on the object type.
            // Narrow to the proposed action so we don't report constraints from
            // other unrelated actions on the same object type.
            var report = reports.FirstOrDefault(r => r.Action.Name == action.ActionName);
            if (report is null)
            {
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
}
