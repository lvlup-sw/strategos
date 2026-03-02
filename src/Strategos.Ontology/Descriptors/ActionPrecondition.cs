namespace Strategos.Ontology.Descriptors;

public sealed record ActionPrecondition
{
    public required string Expression { get; init; }

    public required string Description { get; init; }

    public required PreconditionKind Kind { get; init; }

    public string? LinkName { get; init; }

    public ConstraintStrength Strength { get; init; } = ConstraintStrength.Hard;
}
