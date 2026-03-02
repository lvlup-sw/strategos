namespace Strategos.Ontology.MCP;

/// <summary>
/// Describes an MCP tool exposed by the ontology layer.
/// </summary>
public sealed record OntologyToolDescriptor(
    string Name,
    string Description)
{
    /// <summary>
    /// Constraint summaries for actions discovered from the ontology graph.
    /// Populated only for the ontology_action tool descriptor.
    /// </summary>
    public IReadOnlyList<ActionConstraintSummary> ConstraintSummaries { get; init; } = [];
}
