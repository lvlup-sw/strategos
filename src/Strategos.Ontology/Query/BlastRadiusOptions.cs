namespace Strategos.Ontology.Query;

/// <summary>
/// Options controlling blast-radius traversal.
/// </summary>
public sealed record BlastRadiusOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlastRadiusOptions"/> class.
    /// </summary>
    /// <param name="MaxExpansionDegree">
    /// Maximum graph expansion degree applied while walking neighbors. Must be
    /// greater than or equal to 1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="MaxExpansionDegree"/> is less than 1.
    /// </exception>
    public BlastRadiusOptions(int MaxExpansionDegree = 16)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxExpansionDegree, 1);
        this.MaxExpansionDegree = MaxExpansionDegree;
    }

    /// <summary>
    /// Gets the maximum graph expansion degree.
    /// </summary>
    public int MaxExpansionDegree { get; }
}
