namespace Strategos.Ontology.Actions;

/// <summary>
/// Result of dispatching an ontology action.
/// </summary>
public sealed record ActionResult(
    bool IsSuccess,
    object? Result = null,
    string? Error = null,
    ConstraintViolationReport? Violations = null);
