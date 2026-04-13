using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology;

public sealed record LinkTraversalResult(
    ObjectTypeDescriptor ObjectType,
    string LinkName,
    int Depth,
    string? Description = null);
