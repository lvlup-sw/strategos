using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Declares one independent, totally ordered axis in an authority product lattice.
/// Levels are ordered from weakest to strongest.
/// </summary>
public sealed record AuthorityAxisDescriptor(
    string Name,
    ImmutableArray<string> Levels);
