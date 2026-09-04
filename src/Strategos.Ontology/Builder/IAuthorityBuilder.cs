namespace Strategos.Ontology.Builder;

/// <summary>
/// Positions one named authority in a domain's product lattice.
/// </summary>
public interface IAuthorityBuilder
{
    /// <summary>
    /// Sets the authority's level on an independent axis.
    /// </summary>
    IAuthorityBuilder At(string axisName, string levelName);

    /// <summary>
    /// Declares an expected implication. Graph construction verifies that the
    /// product coordinates make this implication true.
    /// </summary>
    IAuthorityBuilder Implies(string weakerAuthority);
}
