namespace Strategos.Ontology.Descriptors;

public sealed record LinkDescriptor(
    string Name,
    string TargetTypeName,
    LinkCardinality Cardinality,
    IReadOnlyList<PropertyDescriptor>? EdgeProperties = null)
{
    public IReadOnlyList<PropertyDescriptor> EdgeProperties { get; init; } =
        EdgeProperties ?? [];

    public string? InverseLinkName { get; init; }
}
