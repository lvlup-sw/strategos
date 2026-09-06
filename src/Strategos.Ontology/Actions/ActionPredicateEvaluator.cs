using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>Evaluates the closed action-predicate language over runtime facts.</summary>
internal sealed class ActionPredicateEvaluator
{
    private readonly IActionRelationResolver? relationResolver;
    private readonly IReadOnlyDictionary<string, ICustomActionPredicateEvaluator> customEvaluators;
    private readonly ILogger logger;

    internal ActionPredicateEvaluator(
        IActionRelationResolver? relationResolver = null,
        IEnumerable<ICustomActionPredicateEvaluator>? customEvaluators = null,
        ILogger? logger = null)
    {
        this.relationResolver = relationResolver;
        this.logger = logger ?? NullLogger.Instance;

        var byKey = new Dictionary<string, ICustomActionPredicateEvaluator>(StringComparer.Ordinal);
        foreach (var evaluator in customEvaluators ?? [])
        {
            ArgumentNullException.ThrowIfNull(evaluator);
            ArgumentException.ThrowIfNullOrWhiteSpace(evaluator.EvaluatorKey);
            if (!byKey.TryAdd(evaluator.EvaluatorKey, evaluator))
            {
                throw new InvalidOperationException(
                    $"More than one custom action-predicate evaluator is registered for key '{evaluator.EvaluatorKey}'.");
            }
        }

        this.customEvaluators = byKey;
    }

    internal PredicateTruthValue Evaluate(ActionPredicate predicate, ActionFacts facts)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(facts);
        return EvaluateLocal(predicate, facts);
    }

    internal EvaluationSession BeginEvaluation(
        ActionFacts facts,
        ActionContext context,
        object? request)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(context);
        return new EvaluationSession(this, facts, context, request);
    }

    internal ValueTask<PredicateTruthValue> EvaluateAsync(
        ActionPredicate predicate,
        ActionFacts facts,
        ActionContext context,
        object? request,
        CancellationToken ct = default)
    {
        return BeginEvaluation(facts, context, request).EvaluateAsync(predicate, ct);
    }

    private static PredicateTruthValue EvaluateLocal(ActionPredicate predicate, ActionFacts facts) => predicate switch
    {
        ConstantActionPredicate constant => constant.Value
            ? PredicateTruthValue.Satisfied
            : PredicateTruthValue.Unsatisfied,
        PropertyComparisonPredicate comparison => EvaluateComparison(comparison, facts),
        LinkExistsPredicate link => facts.Links.TryGetValue(link.LinkName, out var present)
            ? present ? PredicateTruthValue.Satisfied : PredicateTruthValue.Unsatisfied
            : PredicateTruthValue.Indeterminate,
        RelationHoldsPredicate => PredicateTruthValue.Indeterminate,
        CustomPredicate => PredicateTruthValue.Indeterminate,
        AllPredicate all => EvaluateAllLocal(all.Operands, facts),
        AnyPredicate any => EvaluateAnyLocal(any.Operands, facts),
        NotPredicate not => Negate(EvaluateLocal(not.Operand, facts)),
        _ => PredicateTruthValue.Indeterminate,
    };

    private async ValueTask<PredicateTruthValue> EvaluateCoreAsync(
        ActionPredicate predicate,
        ActionFacts facts,
        ActionContext context,
        object? request,
        IDictionary<string, PredicateTruthValue> atomValues,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        switch (predicate)
        {
            case ConstantActionPredicate or PropertyComparisonPredicate or LinkExistsPredicate:
                return EvaluateLocal(predicate, facts);

            case RelationHoldsPredicate relation:
                return await EvaluateRelationAtomAsync(
                    relation,
                    context,
                    atomValues,
                    ct).ConfigureAwait(false);

            case CustomPredicate custom:
                return await EvaluateCustomAtomAsync(
                    custom,
                    facts,
                    context,
                    request,
                    atomValues,
                    ct).ConfigureAwait(false);

            case NotPredicate not:
                return Negate(await EvaluateCoreAsync(
                    not.Operand,
                    facts,
                    context,
                    request,
                    atomValues,
                    ct).ConfigureAwait(false));

            case AllPredicate all:
                return await EvaluateAllAsync(
                    all,
                    facts,
                    context,
                    request,
                    atomValues,
                    ct).ConfigureAwait(false);

            case AnyPredicate any:
                return await EvaluateAnyAsync(
                    any,
                    facts,
                    context,
                    request,
                    atomValues,
                    ct).ConfigureAwait(false);

            default:
                return PredicateTruthValue.Indeterminate;
        }
    }

    private async ValueTask<PredicateTruthValue> EvaluateAllAsync(
        AllPredicate predicate,
        ActionFacts facts,
        ActionContext context,
        object? request,
        IDictionary<string, PredicateTruthValue> atomValues,
        CancellationToken ct)
    {
        var result = PredicateTruthValue.Satisfied;
        foreach (var operand in predicate.Operands)
        {
            var current = await EvaluateCoreAsync(
                operand,
                facts,
                context,
                request,
                atomValues,
                ct).ConfigureAwait(false);
            if (current == PredicateTruthValue.Unsatisfied)
            {
                return PredicateTruthValue.Unsatisfied;
            }

            if (current == PredicateTruthValue.Indeterminate)
            {
                result = PredicateTruthValue.Indeterminate;
            }
        }

        return result;
    }

    private async ValueTask<PredicateTruthValue> EvaluateAnyAsync(
        AnyPredicate predicate,
        ActionFacts facts,
        ActionContext context,
        object? request,
        IDictionary<string, PredicateTruthValue> atomValues,
        CancellationToken ct)
    {
        var result = PredicateTruthValue.Unsatisfied;
        foreach (var operand in predicate.Operands)
        {
            var current = await EvaluateCoreAsync(
                operand,
                facts,
                context,
                request,
                atomValues,
                ct).ConfigureAwait(false);
            if (current == PredicateTruthValue.Satisfied)
            {
                return PredicateTruthValue.Satisfied;
            }

            if (current == PredicateTruthValue.Indeterminate)
            {
                result = PredicateTruthValue.Indeterminate;
            }
        }

        return result;
    }

    private static PredicateTruthValue EvaluateAllLocal(
        IEnumerable<ActionPredicate> operands,
        ActionFacts facts)
    {
        var result = PredicateTruthValue.Satisfied;
        foreach (var operand in operands)
        {
            var current = EvaluateLocal(operand, facts);
            if (current == PredicateTruthValue.Unsatisfied)
            {
                return PredicateTruthValue.Unsatisfied;
            }

            if (current == PredicateTruthValue.Indeterminate)
            {
                result = PredicateTruthValue.Indeterminate;
            }
        }

        return result;
    }

    private static PredicateTruthValue EvaluateAnyLocal(
        IEnumerable<ActionPredicate> operands,
        ActionFacts facts)
    {
        var result = PredicateTruthValue.Unsatisfied;
        foreach (var operand in operands)
        {
            var current = EvaluateLocal(operand, facts);
            if (current == PredicateTruthValue.Satisfied)
            {
                return PredicateTruthValue.Satisfied;
            }

            if (current == PredicateTruthValue.Indeterminate)
            {
                result = PredicateTruthValue.Indeterminate;
            }
        }

        return result;
    }

    private async ValueTask<PredicateTruthValue> EvaluateRelationAsync(
        RelationHoldsPredicate predicate,
        ActionContext context,
        CancellationToken ct)
    {
        if (relationResolver is null)
        {
            logger.LogWarning(
                "Action relation resolver is not registered for {Domain}/{ObjectType}/{ActionName}; relation {RelationName} is indeterminate.",
                context.Domain,
                context.ObjectType,
                context.ActionName,
                predicate.RelationName);
            return PredicateTruthValue.Indeterminate;
        }

        try
        {
            var precondition = CreateRelationPrecondition(predicate);
            return await relationResolver.HoldsAsync(context, precondition, ct).ConfigureAwait(false)
                ? PredicateTruthValue.Satisfied
                : PredicateTruthValue.Unsatisfied;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Relation predicate evaluator returned an indeterminate result for {Domain}/{ObjectType}/{ActionName} and relation {RelationName}.",
                context.Domain,
                context.ObjectType,
                context.ActionName,
                predicate.RelationName);
            return PredicateTruthValue.Indeterminate;
        }
    }

    private async ValueTask<PredicateTruthValue> EvaluateRelationAtomAsync(
        RelationHoldsPredicate predicate,
        ActionContext context,
        IDictionary<string, PredicateTruthValue> atomValues,
        CancellationToken ct)
    {
        if (atomValues.TryGetValue(predicate.CanonicalToken, out var cached))
        {
            return cached;
        }

        var result = await EvaluateRelationAsync(predicate, context, ct).ConfigureAwait(false);
        atomValues[predicate.CanonicalToken] = result;
        return result;
    }

    private async ValueTask<PredicateTruthValue> EvaluateCustomAtomAsync(
        CustomPredicate predicate,
        ActionFacts facts,
        ActionContext context,
        object? request,
        IDictionary<string, PredicateTruthValue> atomValues,
        CancellationToken ct)
    {
        if (atomValues.TryGetValue(predicate.CanonicalToken, out var cached))
        {
            return cached;
        }

        var result = await EvaluateCustomAsync(predicate, facts, context, request, ct).ConfigureAwait(false);
        atomValues[predicate.CanonicalToken] = result;
        return result;
    }

    private async ValueTask<PredicateTruthValue> EvaluateCustomAsync(
        CustomPredicate predicate,
        ActionFacts facts,
        ActionContext context,
        object? request,
        CancellationToken ct)
    {
        if (!customEvaluators.TryGetValue(predicate.EvaluatorKey, out var evaluator))
        {
            logger.LogWarning(
                "Custom action-predicate evaluator {EvaluatorKey} is not registered for {Domain}/{ObjectType}/{ActionName}; the predicate is indeterminate.",
                predicate.EvaluatorKey,
                context.Domain,
                context.ObjectType,
                context.ActionName);
            return PredicateTruthValue.Indeterminate;
        }

        if (!TryProjectCustomFacts(predicate, facts, out var projectedFacts, out var missingResources))
        {
            logger.LogWarning(
                "Custom action-predicate evaluator {EvaluatorKey} is missing declared target facts {MissingResources} for {Domain}/{ObjectType}/{ActionName}; the predicate is indeterminate.",
                predicate.EvaluatorKey,
                missingResources,
                context.Domain,
                context.ObjectType,
                context.ActionName);
            return PredicateTruthValue.Indeterminate;
        }

        try
        {
            var result = await evaluator.EvaluateAsync(
                new CustomActionPredicateContext(context, predicate, projectedFacts, request),
                ct).ConfigureAwait(false);
            if (Enum.IsDefined(result))
            {
                return result;
            }

            logger.LogWarning(
                "Custom action-predicate evaluator {EvaluatorKey} returned invalid truth value {TruthValue} for {Domain}/{ObjectType}/{ActionName}; the predicate is indeterminate.",
                predicate.EvaluatorKey,
                (int)result,
                context.Domain,
                context.ObjectType,
                context.ActionName);
            return PredicateTruthValue.Indeterminate;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Custom action-predicate evaluator {EvaluatorKey} returned an indeterminate result for {Domain}/{ObjectType}/{ActionName}.",
                predicate.EvaluatorKey,
                context.Domain,
                context.ObjectType,
                context.ActionName);
            return PredicateTruthValue.Indeterminate;
        }
    }

    private static bool TryProjectCustomFacts(
        CustomPredicate predicate,
        ActionFacts facts,
        out ActionFacts projectedFacts,
        out string missingResources)
    {
        List<KeyValuePair<string, PredicateLiteral>>? properties = null;
        List<KeyValuePair<string, bool>>? links = null;
        List<string>? missing = null;

        foreach (var resource in predicate.ReadSet)
        {
            switch (resource.Kind)
            {
                case ActionResourceKind.Property:
                    if (facts.Properties.TryGetValue(resource.Name, out var propertyValue))
                    {
                        (properties ??= []).Add(KeyValuePair.Create(resource.Name, propertyValue));
                    }
                    else
                    {
                        (missing ??= []).Add($"Property:{resource.Name}");
                    }

                    break;

                case ActionResourceKind.Link:
                    if (facts.Links.TryGetValue(resource.Name, out var linkPresent))
                    {
                        (links ??= []).Add(KeyValuePair.Create(resource.Name, linkPresent));
                    }
                    else
                    {
                        (missing ??= []).Add($"Link:{resource.Name}");
                    }

                    break;
            }
        }

        if (missing is { Count: > 0 })
        {
            projectedFacts = ActionFacts.Empty;
            missingResources = string.Join(", ", missing);
            return false;
        }

        projectedFacts = properties is null && links is null
            ? ActionFacts.Empty
            : new ActionFacts(properties, links);
        missingResources = string.Empty;
        return true;
    }

    private static PredicateTruthValue EvaluateComparison(
        PropertyComparisonPredicate predicate,
        ActionFacts facts)
    {
        if (!facts.Properties.TryGetValue(predicate.PropertyReference.Name, out var actual))
        {
            return PredicateTruthValue.Indeterminate;
        }

        if (!IsCompatible(predicate.PropertyReference, actual))
        {
            return PredicateTruthValue.Indeterminate;
        }

        if (actual.Kind == PredicateLiteralKind.Null || predicate.Value.Kind == PredicateLiteralKind.Null)
        {
            var equal = actual.Kind == predicate.Value.Kind;
            return FromBoolean(predicate.Operator switch
            {
                PredicateComparisonOperator.Equal => equal,
                PredicateComparisonOperator.NotEqual => !equal,
                _ => false,
            });
        }

        var comparison = Compare(actual, predicate.Value);
        if (comparison is null)
        {
            return PredicateTruthValue.Indeterminate;
        }

        return FromBoolean(predicate.Operator switch
        {
            PredicateComparisonOperator.Equal => comparison.Value == 0,
            PredicateComparisonOperator.NotEqual => comparison.Value != 0,
            PredicateComparisonOperator.LessThan => comparison.Value < 0,
            PredicateComparisonOperator.LessThanOrEqual => comparison.Value <= 0,
            PredicateComparisonOperator.GreaterThan => comparison.Value > 0,
            PredicateComparisonOperator.GreaterThanOrEqual => comparison.Value >= 0,
            _ => false,
        });
    }

    private static bool IsCompatible(PredicatePropertyReference property, PredicateLiteral actual)
    {
        if (actual.Kind == PredicateLiteralKind.Null)
        {
            return property.IsNullable;
        }

        return (property.ScalarKind, actual.Kind) switch
        {
            (PredicateScalarKind.Boolean, PredicateLiteralKind.Boolean) => true,
            (PredicateScalarKind.Integer, PredicateLiteralKind.Integer) => true,
            (PredicateScalarKind.Decimal, PredicateLiteralKind.Decimal) => true,
            (PredicateScalarKind.String, PredicateLiteralKind.String) => true,
            (PredicateScalarKind.Enum, PredicateLiteralKind.Enum) => true,
            (PredicateScalarKind.Symbol, PredicateLiteralKind.Symbol) => true,
            _ => false,
        };
    }

    private static int? Compare(PredicateLiteral left, PredicateLiteral right)
    {
        if (left.Kind != right.Kind)
        {
            return null;
        }

        return left.Kind switch
        {
            PredicateLiteralKind.Boolean => left.BooleanValue.CompareTo(right.BooleanValue),
            PredicateLiteralKind.Integer => left.IntegerValue.CompareTo(right.IntegerValue),
            PredicateLiteralKind.Decimal => left.DecimalValue.CompareTo(right.DecimalValue),
            PredicateLiteralKind.String or PredicateLiteralKind.Symbol =>
                string.Compare(left.CanonicalValue, right.CanonicalValue, StringComparison.Ordinal),
            PredicateLiteralKind.Enum when string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal) =>
                string.Compare(left.EnumMemberName, right.EnumMemberName, StringComparison.Ordinal),
            _ => null,
        };
    }

    private static PredicateTruthValue Negate(PredicateTruthValue value) => value switch
    {
        PredicateTruthValue.Satisfied => PredicateTruthValue.Unsatisfied,
        PredicateTruthValue.Unsatisfied => PredicateTruthValue.Satisfied,
        _ => PredicateTruthValue.Indeterminate,
    };

    private static PredicateTruthValue FromBoolean(bool value) => value
        ? PredicateTruthValue.Satisfied
        : PredicateTruthValue.Unsatisfied;

    private static ActionPrecondition CreateRelationPrecondition(RelationHoldsPredicate predicate) =>
        new(predicate, predicate.Expression, ConstraintStrength.Hard);

    internal sealed class EvaluationSession
    {
        private readonly ActionPredicateEvaluator evaluator;
        private readonly ActionFacts facts;
        private readonly ActionContext context;
        private readonly object? request;
        private readonly Dictionary<string, PredicateTruthValue> atomValues = new(StringComparer.Ordinal);

        internal EvaluationSession(
            ActionPredicateEvaluator evaluator,
            ActionFacts facts,
            ActionContext context,
            object? request)
        {
            this.evaluator = evaluator;
            this.facts = facts;
            this.context = context;
            this.request = request;
        }

        internal ValueTask<PredicateTruthValue> EvaluateAsync(
            ActionPredicate predicate,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            return evaluator.EvaluateCoreAsync(predicate, facts, context, request, atomValues, ct);
        }
    }
}
