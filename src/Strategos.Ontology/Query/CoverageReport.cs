namespace Strategos.Ontology.Query;

/// <summary>
/// Reports how much of a <see cref="DesignIntent"/>'s affected node set is
/// covered by registered actions, descriptors, or other ontology surfaces.
/// </summary>
/// <param name="CoveredNodes">Number of affected nodes that are covered.</param>
/// <param name="TotalNodes">Total number of affected nodes inspected.</param>
/// <param name="Uncovered">The subset of affected nodes that lacks coverage.</param>
public sealed record CoverageReport(
    int CoveredNodes,
    int TotalNodes,
    IReadOnlyList<OntologyNodeRef> Uncovered);
