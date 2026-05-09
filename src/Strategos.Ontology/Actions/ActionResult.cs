namespace Strategos.Ontology.Actions;

/// <summary>
/// Result of dispatching an ontology action.
/// </summary>
public sealed record ActionResult(
    bool IsSuccess,
    object? Result = null,
    string? Error = null,
    ConstraintViolationReport? Violations = null)
{
    /// <summary>
    /// Initializes a new instance of <see cref="ActionResult"/> without a
    /// <see cref="Violations"/> report.
    /// </summary>
    /// <remarks>
    /// Preserves the pre-2.5.0 three-parameter constructor signature so
    /// already-compiled consumers continue to bind. C# encodes optional
    /// parameter defaults at the call site, so adding the new
    /// <see cref="Violations"/> parameter to the primary constructor would
    /// otherwise break binary compatibility with assemblies linked against
    /// the old shape.
    /// </remarks>
    /// <param name="isSuccess">Whether the dispatch succeeded.</param>
    /// <param name="result">Payload returned by the action; pass null for none.</param>
    /// <param name="error">Error message describing failure; pass null for none.</param>
    public ActionResult(bool isSuccess, object? result, string? error)
        : this(isSuccess, result, error, null)
    {
    }
}
