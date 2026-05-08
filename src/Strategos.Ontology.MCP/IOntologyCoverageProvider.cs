namespace Strategos.Ontology.MCP;

public interface IOntologyCoverageProvider
{
    CoverageReport? GetCoverage(DesignIntent intent);
}
