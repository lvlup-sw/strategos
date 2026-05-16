namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Describes a structural link from an object type to another.
/// </summary>
/// <remarks>
/// DR-1 polyglot: <see cref="TargetTypeName"/> remains the canonical
/// string-keyed target name used by hand-authored paths. For ingested
/// links whose target is identified by SCIP rather than CLR, set
/// <see cref="TargetSymbolKey"/> alongside.
/// DR-6 provenance: <see cref="Source"/> tags whether this link arrived
/// hand-authored or via an <c>IOntologySource</c>.
/// </remarks>
public sealed record LinkDescriptor(
    string Name,
    string TargetTypeName,
    LinkCardinality Cardinality,
    IReadOnlyList<PropertyDescriptor>? EdgeProperties = null)
{
    public IReadOnlyList<PropertyDescriptor> EdgeProperties { get; init; } =
        EdgeProperties ?? [];

    public string? InverseLinkName { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// SCIP moniker of the link target, when known. Parallel to
    /// <see cref="TargetTypeName"/>; populated by ingestion paths whose
    /// target type is not loadable as a CLR <see cref="Type"/>.
    /// </summary>
    public string? TargetSymbolKey { get; init; }

    /// <summary>Field-level provenance — hand-authored vs. ingested.</summary>
    public DescriptorSource Source { get; init; } = DescriptorSource.HandAuthored;
}
