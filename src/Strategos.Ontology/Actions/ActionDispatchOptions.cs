namespace Strategos.Ontology.Actions;

/// <summary>
/// Options controlling action dispatch behavior. General precondition enforcement
/// is opt-in; hard formulas containing a relation are always enforced.
/// </summary>
public sealed record ActionDispatchOptions
{
    /// <summary>
    /// Default options with no enforcement.
    /// </summary>
    public static readonly ActionDispatchOptions Default = new();

    /// <summary>
    /// When true, the dispatcher requires every hard precondition to evaluate
    /// satisfied before dispatch. Unsatisfied and indeterminate results fail closed.
    /// </summary>
    public bool EnforcePreconditions { get; init; }
}
