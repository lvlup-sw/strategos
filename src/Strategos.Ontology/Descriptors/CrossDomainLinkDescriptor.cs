namespace Strategos.Ontology.Descriptors;

public sealed record CrossDomainLinkDescriptor(
    string Name,
    Type SourceType,
    string TargetDomain,
    string TargetTypeName,
    LinkCardinality Cardinality)
{
    public string? Description { get; init; }
}
