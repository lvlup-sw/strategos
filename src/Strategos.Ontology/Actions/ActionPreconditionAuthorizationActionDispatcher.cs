using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Resolves action contracts and authoritative facts at the dispatch boundary,
/// then fails closed for hard requirements that are false or unknown.
/// </summary>
internal sealed class ActionPreconditionAuthorizationActionDispatcher : IActionDispatcher
{
    private readonly IActionDispatcher inner;
    private readonly OntologyGraph graph;
    private readonly IActionFactResolver? factResolver;
    private readonly ActionPredicateEvaluator predicateEvaluator;
    private readonly ILogger logger;
    private readonly bool reportSoftConstraints;

    internal ActionPreconditionAuthorizationActionDispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionFactResolver? factResolver,
        IActionRelationResolver? relationResolver,
        IEnumerable<ICustomActionPredicateEvaluator>? customEvaluators,
        ILogger? logger = null,
        bool reportSoftConstraints = false)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(graph);

        this.inner = inner;
        this.graph = graph;
        this.factResolver = factResolver;
        this.logger = logger ?? NullLogger.Instance;
        this.reportSoftConstraints = reportSoftConstraints;
        predicateEvaluator = new ActionPredicateEvaluator(
            relationResolver,
            customEvaluators,
            this.logger);
    }

    public async Task<ActionResult> DispatchAsync(
        ActionContext context,
        object request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = ResolveAction(context);
        if (descriptor is null ||
            (context.ActionDescriptor is not null && !ReferenceEquals(context.ActionDescriptor, descriptor)))
        {
            return DeniedUnknownAction(context);
        }

        var authorizedContext = ReferenceEquals(descriptor, context.ActionDescriptor)
            ? context
            : context with { ActionDescriptor = descriptor };
        var enforceAll = context.Options?.EnforcePreconditions == true;
        var enforcedHard = descriptor.Preconditions
            .Where(candidate => candidate.Strength == ConstraintStrength.Hard)
            .Where(candidate => enforceAll || candidate.Predicate.ContainsRelation)
            .ToArray();
        var advisoryHard = reportSoftConstraints
            ? descriptor.Preconditions
                .Where(candidate => candidate.Strength == ConstraintStrength.Hard)
                .Where(candidate => !enforceAll && !candidate.Predicate.ContainsRelation)
                .ToArray()
            : [];
        var reportableSoft = reportSoftConstraints
            ? descriptor.Preconditions
                .Where(candidate => candidate.Strength == ConstraintStrength.Soft)
                .ToArray()
            : [];
        if (enforcedHard.Length == 0 && advisoryHard.Length == 0 && reportableSoft.Length == 0)
        {
            return await inner.DispatchAsync(authorizedContext, request, ct).ConfigureAwait(false);
        }

        var needsFacts = enforcedHard
            .Concat(advisoryHard)
            .Concat(reportableSoft)
            .Any(candidate => RequiresTargetFacts(candidate.Predicate));
        var facts = needsFacts
            ? await ResolveAuthoritativeFactsAsync(authorizedContext, ct).ConfigureAwait(false)
            : ActionFacts.Empty;

        var evaluation = predicateEvaluator.BeginEvaluation(facts, authorizedContext, request);
        var enforcedHardViolations = await EvaluateConstraintsAsync(
            enforcedHard,
            evaluation,
            ct).ConfigureAwait(false);
        var advisoryHardViolations = await EvaluateConstraintsAsync(
            advisoryHard,
            evaluation,
            ct).ConfigureAwait(false);
        var softViolations = await EvaluateConstraintsAsync(
            reportableSoft,
            evaluation,
            ct).ConfigureAwait(false);

        if (enforcedHardViolations.Count > 0)
        {
            enforcedHardViolations.AddRange(advisoryHardViolations);
            return new ActionResult(
                false,
                Error: $"Action '{descriptor.Name}' was denied because one or more hard requirements were unsatisfied or indeterminate.",
                Violations: new ConstraintViolationReport(
                    descriptor.Name,
                    enforcedHardViolations.AsReadOnly(),
                    softViolations.AsReadOnly(),
                    SuggestedCorrection: null));
        }

        var result = await inner.DispatchAsync(authorizedContext, request, ct).ConfigureAwait(false);
        if (result.Violations is not null ||
            (advisoryHardViolations.Count == 0 && softViolations.Count == 0))
        {
            return result;
        }

        return result with
        {
            Violations = new ConstraintViolationReport(
                descriptor.Name,
                advisoryHardViolations.AsReadOnly(),
                softViolations.AsReadOnly(),
                SuggestedCorrection: null),
        };
    }

    private static async ValueTask<List<ConstraintEvaluation>> EvaluateConstraintsAsync(
        IReadOnlyList<ActionPrecondition> constraints,
        ActionPredicateEvaluator.EvaluationSession evaluation,
        CancellationToken ct)
    {
        var violations = new List<ConstraintEvaluation>();
        foreach (var precondition in constraints)
        {
            var truthValue = await evaluation.EvaluateAsync(
                precondition.Predicate,
                ct).ConfigureAwait(false);
            if (truthValue == PredicateTruthValue.Satisfied)
            {
                continue;
            }

            violations.Add(new ConstraintEvaluation(
                precondition,
                truthValue,
                precondition.Strength,
                truthValue == PredicateTruthValue.Unsatisfied
                    ? $"Requirement '{precondition.Expression}' is not satisfied."
                    : $"Could not determine whether requirement '{precondition.Expression}' is satisfied from authoritative facts.",
                BuildExpectedShape(precondition.Predicate)));
        }

        return violations;
    }

    private async ValueTask<ActionFacts> ResolveAuthoritativeFactsAsync(
        ActionContext context,
        CancellationToken ct)
    {
        if (factResolver is null)
        {
            logger.LogWarning(
                "Action fact resolver is not registered for {Domain}/{ObjectType}/{ActionName}; target facts are indeterminate.",
                context.Domain,
                context.ObjectType,
                context.ActionName);
            return ActionFacts.Empty;
        }

        try
        {
            var facts = await factResolver.ResolveAsync(context, ct).ConfigureAwait(false);
            if (facts is not null)
            {
                return facts;
            }

            logger.LogWarning(
                "Action fact resolver returned no facts for {Domain}/{ObjectType}/{ObjectId}/{ActionName}; target facts are indeterminate.",
                context.Domain,
                context.ObjectType,
                context.ObjectId,
                context.ActionName);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Action fact resolver failed for {Domain}/{ObjectType}/{ObjectId}/{ActionName}; target facts are indeterminate.",
                context.Domain,
                context.ObjectType,
                context.ObjectId,
                context.ActionName);
        }

        return ActionFacts.Empty;
    }

    private static bool RequiresTargetFacts(ActionPredicate predicate) => predicate switch
    {
        PropertyComparisonPredicate or LinkExistsPredicate => true,
        CustomPredicate custom => custom.ReadSet.Any(resource =>
            resource.Kind is ActionResourceKind.Property or ActionResourceKind.Link),
        AllPredicate all => all.Operands.Any(RequiresTargetFacts),
        AnyPredicate any => any.Operands.Any(RequiresTargetFacts),
        NotPredicate not => RequiresTargetFacts(not.Operand),
        _ => false,
    };

    private static IReadOnlyDictionary<string, object?>? BuildExpectedShape(ActionPredicate predicate) =>
        predicate switch
        {
            PropertyComparisonPredicate comparison => new Dictionary<string, object?>
            {
                [comparison.PropertyReference.Name] = comparison.Value,
            },
            LinkExistsPredicate link => new Dictionary<string, object?> { [link.LinkName] = true },
            _ => null,
        };

    private ActionDescriptor? ResolveAction(ActionContext context)
    {
        var objectType = graph.GetObjectType(context.Domain, context.ObjectType);
        return objectType?.Actions.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, context.ActionName, StringComparison.Ordinal));
    }

    private static ActionResult DeniedUnknownAction(ActionContext context) => new(
        false,
        Error: $"Action '{context.Domain}/{context.ObjectType}/{context.ActionName}' is not present in the authoritative ontology graph.");
}
