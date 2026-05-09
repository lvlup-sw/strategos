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
    private readonly Func<IOntologyQuery> _queryAccessor;
    private readonly ILogger<ConstraintReportingActionDispatcher> _logger;

    internal IActionDispatcher Inner => _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstraintReportingActionDispatcher"/> class.
    /// </summary>
    /// <param name="inner">Inner dispatcher to delegate dispatch to.</param>
    /// <param name="query">
    /// Ontology query used to resolve constraint reports for the dispatched action.
    /// </param>
    /// <param name="logger">Logger used to record query lookup failures.</param>
    public ConstraintReportingActionDispatcher(
        IActionDispatcher inner,
        IOntologyQuery query,
        ILogger<ConstraintReportingActionDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _queryAccessor = () => query;
        _logger = logger;
    }

    private ConstraintReportingActionDispatcher(
        IActionDispatcher inner,
        Func<IOntologyQuery> queryAccessor,
        ILogger<ConstraintReportingActionDispatcher> logger)
    {
        _inner = inner;
        _queryAccessor = queryAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Creates an instance that resolves <see cref="IOntologyQuery"/> lazily on first
    /// dispatch. Used by DI to break the cycle between the dispatcher chain factory
    /// and the query factory (which itself injects <see cref="IActionDispatcher"/>).
    /// </summary>
    internal static ConstraintReportingActionDispatcher CreateDeferred(
        IActionDispatcher inner,
        Lazy<IOntologyQuery> query,
        ILogger<ConstraintReportingActionDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(logger);

        return new ConstraintReportingActionDispatcher(inner, () => query.Value, logger);
    }

    public async Task<ActionResult> DispatchAsync(
        ActionContext context, object request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

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
            reports = _queryAccessor().GetActionConstraintReport(context.ObjectType, knownProperties: null);
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
