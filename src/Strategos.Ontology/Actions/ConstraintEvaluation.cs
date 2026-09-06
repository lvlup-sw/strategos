// Copyright (c) Levelup Software. All rights reserved.

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Evaluation outcome for a single action precondition produced by
/// <c>IOntologyQuery.GetActionConstraintReport</c> and friends.
/// </summary>
/// <param name="Precondition">The precondition that was evaluated.</param>
/// <param name="TruthValue">The three-valued outcome of evaluating the precondition.</param>
/// <param name="Strength">Constraint strength (hard or soft) of the precondition.</param>
/// <param name="FailureReason">
/// Optional human-readable explanation when the precondition is unsatisfied or indeterminate;
/// null when satisfied.
/// </param>
/// <param name="ExpectedShape">
/// Optional expected data shape for corrective guidance (e.g. property values
/// the caller would need to satisfy the precondition); null when not applicable.
/// </param>
public sealed record ConstraintEvaluation(
    ActionPrecondition Precondition,
    PredicateTruthValue TruthValue,
    ConstraintStrength Strength,
    string? FailureReason,
    IReadOnlyDictionary<string, object?>? ExpectedShape)
{
    /// <summary>Gets whether the precondition is known to be satisfied.</summary>
    public bool IsSatisfied => TruthValue == PredicateTruthValue.Satisfied;
}
