namespace Strategos.Ontology.MCP;

/// <summary>
/// Describes an MCP tool exposed by the ontology layer.
/// </summary>
public sealed record OntologyToolDescriptor(
    string Name,
    string Description);
