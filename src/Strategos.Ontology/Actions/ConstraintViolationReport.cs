// Copyright (c) Levelup Software. All rights reserved.

namespace Strategos.Ontology.Actions;

public sealed record ConstraintViolationReport(
    string ActionName,
    IReadOnlyList<ConstraintEvaluation> Hard,
    IReadOnlyList<ConstraintEvaluation> Soft,
    string? SuggestedCorrection);
