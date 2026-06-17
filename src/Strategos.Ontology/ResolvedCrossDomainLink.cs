using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology;

public sealed record ResolvedCrossDomainLink(
    string Name,
    string SourceDomain,
    ObjectTypeDescriptor SourceObjectType,
    string TargetDomain,
    ObjectTypeDescriptor TargetObjectType,
    LinkCardinality Cardinality,
    string? Description = null);
