using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// A named authority literal positioned on every axis of its domain's authority lattice.
/// </summary>
public sealed record AuthorityDescriptor(string Name)
{
    /// <summary>
    /// Maps each authority-axis name to the literal's level on that axis.
    /// </summary>
    public ImmutableDictionary<string, string> Coordinates { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Optional human-readable order assertions. Graph construction verifies each
    /// assertion agrees with the product order derived from <see cref="Coordinates"/>.
    /// </summary>
    public ImmutableArray<string> ExplicitImplications { get; init; } = [];
}
