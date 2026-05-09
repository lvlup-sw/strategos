namespace Strategos.Ontology.Query;

public interface IOntologyCoverageProvider
{
    CoverageReport? GetCoverage(DesignIntent intent);
}
