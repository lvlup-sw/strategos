namespace Strategos.Ontology.MCP;

/// <summary>
/// The direction an instance-anchored traversal hops across a reified association
/// (DR-15). A closed-vocabulary input — the MCP traversal tool accepts only these
/// values, never a free-text role name.
/// </summary>
public enum TraversalDirection
{
    /// <summary>
    /// Hop to the association's DESTINATION endpoint (the right-hand role of
    /// <c>Between(left).And(right)</c>). The default traversal direction.
    /// </summary>
    ToDestination,

    /// <summary>
    /// Hop to the association's SOURCE endpoint (the left-hand role of
    /// <c>Between(left).And(right)</c>) — surfacing the originating node.
    /// </summary>
    ToSource,
}
