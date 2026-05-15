namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Describes a property of an object type.
/// </summary>
/// <remarks>
/// DR-1 polyglot: <see cref="PropertyType"/> remains the canonical CLR
/// type used by hand-authored paths. For reference-typed properties
/// whose target type is identified by SCIP rather than CLR (ingestion
/// path), <see cref="ReferenceSymbolKey"/> rides alongside.
/// DR-6 provenance: <see cref="Source"/> tags whether this property
/// arrived hand-authored or via an <c>IOntologySource</c>.
/// </remarks>
public sealed record PropertyDescriptor(
    string Name,
    Type PropertyType,
    bool IsRequired = false,
    bool IsComputed = false,
    string? ExpressionPath = null)
{
    public PropertyKind Kind { get; init; } = PropertyKind.Scalar;

    public int? VectorDimensions { get; init; }

    public IReadOnlyList<DerivationSource> DerivedFrom { get; init; } = [];

    public IReadOnlyList<DerivationSource> TransitiveDerivedFrom { get; init; } = [];

    /// <summary>
    /// SCIP moniker identifying the target type of a reference property,
    /// when the target type is not available as a loaded CLR
    /// <see cref="Type"/>. Parallel to <see cref="PropertyType"/>.
    /// </summary>
    public string? ReferenceSymbolKey { get; init; }

    /// <summary>Field-level provenance — hand-authored vs. ingested.</summary>
    public DescriptorSource Source { get; init; } = DescriptorSource.HandAuthored;
}
