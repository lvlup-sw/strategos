using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Resolves an ontology relation precondition against persisted object and
/// relation state.
/// </summary>
public interface IActionRelationResolver
{
    /// <summary>
    /// Determines whether the calling principal holds the relation declared by
    /// <paramref name="precondition"/> for the target in <paramref name="context"/>.
    /// </summary>
    Task<bool> HoldsAsync(
        ActionContext context,
        ActionPrecondition precondition,
        CancellationToken ct = default);
}
