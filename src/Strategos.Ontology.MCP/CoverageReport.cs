using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP;

public sealed record CoverageReport(
    int CoveredNodes,
    int TotalNodes,
    IReadOnlyList<OntologyNodeRef> Uncovered);
