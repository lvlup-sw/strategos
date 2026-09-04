using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>Contract computed from an ordered action composition.</summary>
public sealed record CompositeActionContract(
    ImmutableArray<ActionDescriptor> Actions,
    AuthorityRequirement RequiredAuthority,
    ActionFrame Frame);
