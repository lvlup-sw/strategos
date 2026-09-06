using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>Evaluates one registered family of opaque action predicates.</summary>
public interface ICustomActionPredicateEvaluator
{
    /// <summary>Gets the stable key named by authored custom predicates.</summary>
    string EvaluatorKey { get; }

    /// <summary>Evaluates a custom predicate using its declared inputs.</summary>
    ValueTask<PredicateTruthValue> EvaluateAsync(
        CustomActionPredicateContext context,
        CancellationToken ct = default);
}

/// <summary>Runtime inputs supplied to a custom predicate evaluator.</summary>
/// <param name="ActionContext">The principal and target of the evaluation.</param>
/// <param name="Predicate">The authored custom predicate.</param>
/// <param name="Facts">
/// The authoritative property and link facts named by the predicate's declared read set.
/// External and event resources remain evaluator-owned.
/// </param>
/// <param name="Request">The dispatch request, or null during discovery.</param>
public sealed record CustomActionPredicateContext(
    ActionContext ActionContext,
    CustomPredicate Predicate,
    ActionFacts Facts,
    object? Request);
