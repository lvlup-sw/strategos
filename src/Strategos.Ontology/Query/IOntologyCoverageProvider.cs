namespace Strategos.Ontology.Query;

/// <summary>
/// Optional service that supplies a <see cref="CoverageReport"/> for a given
/// <see cref="DesignIntent"/>. Implementations decide what "coverage" means in
/// their host application (registered actions, descriptors, doc references,
/// etc.); validation tooling treats a null return as "coverage information not
/// available".
/// </summary>
public interface IOntologyCoverageProvider
{
    /// <summary>
    /// Computes a coverage report for the supplied design intent.
    /// </summary>
    /// <param name="intent">The design intent to evaluate.</param>
    /// <returns>
    /// A <see cref="CoverageReport"/> describing covered and uncovered nodes,
    /// or <c>null</c> if coverage cannot be determined for this intent.
    /// </returns>
    CoverageReport? GetCoverage(DesignIntent intent);
}
