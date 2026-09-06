namespace Strategos.Ontology.Actions;

/// <summary>Loads authoritative state facts for an action target.</summary>
public interface IActionFactResolver
{
    /// <summary>
    /// Resolves facts for the target identified by <paramref name="context"/>.
    /// Returning <see langword="null"/> means the target facts are unavailable.
    /// </summary>
    ValueTask<ActionFacts?> ResolveAsync(
        ActionContext context,
        CancellationToken ct = default);
}
