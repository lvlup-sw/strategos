// Copyright (c) Levelup Software. All rights reserved.

namespace Strategos.Ontology.Actions;

/// <summary>
/// Aggregates unsatisfied precondition evaluations for a single dispatched
/// action. Attached to <see cref="ActionResult.Violations"/> by the
/// constraint-reporting dispatcher decorator when constraints are present.
/// </summary>
/// <param name="ActionName">The action whose preconditions were evaluated.</param>
/// <param name="Hard">Unsatisfied hard preconditions that block the dispatch.</param>
/// <param name="Soft">Unsatisfied soft preconditions surfaced as advisory warnings.</param>
/// <param name="SuggestedCorrection">
/// Optional corrective guidance for the caller; null when none is suggested.
/// </param>
public sealed record ConstraintViolationReport(
    string ActionName,
    IReadOnlyList<ConstraintEvaluation> Hard,
    IReadOnlyList<ConstraintEvaluation> Soft,
    string? SuggestedCorrection);
