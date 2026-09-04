using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// The pointwise join of one or more named authorities.
/// </summary>
public sealed record AuthorityRequirement
{
    /// <summary>
    /// Gets the strongest required level on each independent authority axis.
    /// </summary>
    public ImmutableDictionary<string, string> Coordinates { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Gets the authority literals whose join produced this requirement.
    /// </summary>
    public ImmutableArray<string> SourceAuthorities { get; init; } = [];
}
