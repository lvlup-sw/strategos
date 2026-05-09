namespace Strategos.Ontology.Query;

public sealed record CoverageReport(
    int CoveredNodes,
    int TotalNodes,
    IReadOnlyList<OntologyNodeRef> Uncovered);
