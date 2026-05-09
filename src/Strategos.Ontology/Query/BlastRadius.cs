namespace Strategos.Ontology.Query;

/// <summary>
/// Result of estimating the blast radius of a proposed ontology change,
/// capturing directly and transitively affected nodes, cross-domain hops,
/// and the classified scope.
/// </summary>
/// <param name="DirectlyAffected">Nodes immediately impacted by the change.</param>
/// <param name="TransitivelyAffected">
/// Nodes reachable through derivation chains, postconditions, or link
/// traversal during BFS expansion.
/// </param>
/// <param name="CrossDomainHops">Traversals that cross domain boundaries during expansion.</param>
/// <param name="Scope">Classified scope of the blast radius.</param>
public sealed record BlastRadius(
    IReadOnlyList<OntologyNodeRef> DirectlyAffected,
    IReadOnlyList<OntologyNodeRef> TransitivelyAffected,
    IReadOnlyList<CrossDomainHop> CrossDomainHops,
    BlastRadiusScope Scope);
