using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// One reified-association edge in an MCP result (DR-15): the association object
/// projected as a directed edge between two endpoint instances, plus its
/// edge-attribute values. Distinct from a plain object row — a plain query yields
/// opaque object items, whereas an edge row names both endpoints and carries the
/// edge's own attributes.
/// </summary>
/// <remarks>
/// INV-8 (polyglot identity): endpoints are named by descriptor name (a string),
/// never by a CLR <see cref="System.Type"/>; a SymbolKey-only endpoint is
/// representable identically to a CLR one. INV-7 (immutable): an edge row is an
/// immutable record.
/// </remarks>
/// <param name="AssociationName">Descriptor name of the reified association object.</param>
/// <param name="SourceDescriptor">Descriptor name of the source (left) endpoint.</param>
/// <param name="SourceId">Projected id of the source endpoint instance.</param>
/// <param name="DestinationDescriptor">Descriptor name of the destination (right) endpoint.</param>
/// <param name="DestinationId">Projected id of the destination endpoint instance.</param>
public sealed record AssociationEdgeRow(
    [property: JsonPropertyName("associationName")] string AssociationName,
    [property: JsonPropertyName("sourceDescriptor")] string SourceDescriptor,
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("destinationDescriptor")] string DestinationDescriptor,
    [property: JsonPropertyName("destinationId")] string DestinationId)
{
    /// <summary>
    /// The association object's id, when the edge was materialized via an
    /// attributed (DR-4) relate; null for a plain (unattributed) relation.
    /// </summary>
    [JsonPropertyName("associationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssociationId { get; init; }

    /// <summary>
    /// Edge-attribute values carried by the association object, keyed by property
    /// name. Empty for a plain relation with no association object.
    /// </summary>
    [JsonPropertyName("edgeAttributes")]
    public IReadOnlyDictionary<string, object?> EdgeAttributes { get; init; } =
        new Dictionary<string, object?>();
}
