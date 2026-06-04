namespace Strategos.Ontology;

/// <summary>
/// A reified association projected as a directed graph edge (DR-4). The
/// association object's two endpoints become the edge's source and
/// destination; the association itself supplies the edge identity
/// (<see cref="AssociationName"/>) and may carry edge attributes on its
/// underlying descriptor.
/// </summary>
/// <remarks>
/// INV-8 (polyglot identity): source and destination are named by descriptor
/// (<see cref="SourceDescriptorName"/> / <see cref="DestinationDescriptorName"/>),
/// never by a CLR <see cref="System.Type"/>. INV-7 (immutable): an edge is an
/// immutable record. Edge direction follows the authoring order:
/// <c>Between(left).And(right)</c> ⇒ left is the source, right is the
/// destination.
/// </remarks>
/// <param name="AssociationName">Descriptor name of the association object.</param>
/// <param name="DomainName">Owning domain of the association.</param>
/// <param name="SourceDescriptorName">Descriptor name of the left endpoint (edge source).</param>
/// <param name="DestinationDescriptorName">Descriptor name of the right endpoint (edge destination).</param>
public sealed record AssociationEdge(
    string AssociationName,
    string DomainName,
    string SourceDescriptorName,
    string DestinationDescriptorName);
