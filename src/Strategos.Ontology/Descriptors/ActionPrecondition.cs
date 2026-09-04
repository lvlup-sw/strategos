using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

public sealed record ActionPrecondition
{
    public required string Expression { get; init; }

    public required string Description { get; init; }

    public required PreconditionKind Kind { get; init; }

    public string? LinkName { get; init; }

    /// <summary>
    /// Gets the final relation that must connect the resource selected by
    /// <see cref="LinkPath"/> to the calling principal.
    /// </summary>
    public string? RelationName { get; init; }

    /// <summary>
    /// Gets the ordered link path from the action target to the resource on
    /// which <see cref="RelationName"/> must hold. An empty path evaluates the
    /// relation directly on the action target.
    /// </summary>
    public ImmutableArray<string> LinkPath { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets whether this precondition is opaque to build-time composition.
    /// Opaque preconditions remain runtime concerns and are not treated as
    /// members of the decidable predicate fragment.
    /// </summary>
    public bool IsOpaque => Kind == PreconditionKind.Custom;

    public ConstraintStrength Strength { get; init; } = ConstraintStrength.Hard;
}
