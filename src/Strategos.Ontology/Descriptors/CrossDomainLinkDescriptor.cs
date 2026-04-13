namespace Strategos.Ontology.Descriptors;

public sealed record CrossDomainLinkDescriptor(
    string Name,
    Type SourceType,
    string TargetDomain,
    string TargetTypeName,
    LinkCardinality Cardinality)
{
    public IReadOnlyList<PropertyDescriptor> EdgeProperties { get; init; } = [];

    public string? Description { get; init; }
}
