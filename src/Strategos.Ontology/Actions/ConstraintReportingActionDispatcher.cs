using Microsoft.Extensions.Logging;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Decorator that calls <see cref="IOntologyQuery.GetActionConstraintReport"/>
/// for the dispatched action and attaches a <see cref="ConstraintViolationReport"/>
/// to the inner dispatcher's <see cref="ActionResult"/> when constraints are present.
/// Hard violations populate <see cref="ConstraintViolationReport.Hard"/>; soft
/// violations populate <see cref="ConstraintViolationReport.Soft"/>.
/// </summary>
public sealed class ConstraintReportingActionDispatcher : IActionDispatcher
{
    private readonly IActionDispatcher _inner;
    private readonly IOntologyQuery _query;
    private readonly ILogger<ConstraintReportingActionDispatcher> _logger;

    public ConstraintReportingActionDispatcher(
        IActionDispatcher inner,
        IOntologyQuery query,
        ILogger<ConstraintReportingActionDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _query = query;
        _logger = logger;
    }

    public async Task<ActionResult> DispatchAsync(
        ActionContext context, object request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await _inner.DispatchAsync(context, request, ct).ConfigureAwait(false);
        return AppendViolationsIfAny(context, result);
    }

    private ActionResult AppendViolationsIfAny(ActionContext context, ActionResult result)
    {
        var descriptor = context.ActionDescriptor;
        if (descriptor is null)
        {
            return result;
        }

        IReadOnlyList<ActionConstraintReport> reports;
        try
        {
            reports = _query.GetActionConstraintReport(context.ObjectType, knownProperties: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Constraint report lookup failed for {ObjectType}.{ActionName}",
                context.ObjectType,
                context.ActionName);
            return result;
        }

        var report = reports.FirstOrDefault(r => r.Action.Name == descriptor.Name);
        if (report is null || report.Constraints.Count == 0)
        {
            return result;
        }

        var hard = new List<ConstraintEvaluation>();
        var soft = new List<ConstraintEvaluation>();
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

        if (hard.Count == 0 && soft.Count == 0)
        {
            return result;
        }

        var violations = new ConstraintViolationReport(
            ActionName: descriptor.Name,
            Hard: hard,
            Soft: soft,
            SuggestedCorrection: null);

        return result with { Violations = violations };
    }
}
