namespace Strategos.Ontology.Descriptors;

public sealed record ActionPostcondition
{
    public required PostconditionKind Kind { get; init; }

    public string? PropertyName { get; init; }

    public string? LinkName { get; init; }

    public string? EventTypeName { get; init; }

    public string? TargetTypeName { get; init; }
}
