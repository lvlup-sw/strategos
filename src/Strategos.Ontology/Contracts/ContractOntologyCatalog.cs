using System.Collections.Immutable;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Contracts;

/// <summary>
/// Contract-authored ontology descriptors generated from TypeSpec operations.
/// </summary>
public static class ContractOntologyCatalog
{
    /// <summary>
    /// Gets the immutable descriptors emitted from the canonical contract
    /// sources. Consumers compose them through
    /// <see cref="Builder.IOntologyBuilder.ObjectTypeFromDescriptor"/>.
    /// </summary>
    public static ImmutableArray<ObjectTypeDescriptor> ObjectTypes =>
        GeneratedContractOntology.ObjectTypes;
}
