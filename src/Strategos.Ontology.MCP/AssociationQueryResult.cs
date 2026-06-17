using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// The <c>resultKind: "association"</c> branch of <see cref="QueryResultUnion"/>
/// (DR-15): an instance-anchored edge/association result. Carries
/// <see cref="AssociationEdgeRow"/> endpoints rather than the plain object items a
/// <see cref="QueryResult"/> carries, so MCP clients dispatch the edge view on the
/// <c>resultKind</c> discriminator and read endpoint identity + edge attributes
/// directly.
/// </summary>
/// <remarks>
/// INV-3: every result record carries the <c>_meta</c> envelope. The branch is
/// registered on <see cref="QueryResultUnion"/> so the union's
/// <c>OutputSchema</c> covers it as a <c>oneOf</c> alternative.
/// </remarks>
public sealed record AssociationQueryResult(
    string AssociationType,
    IReadOnlyList<AssociationEdgeRow> Edges,
    [property: JsonPropertyName("_meta")] ResponseMeta Meta) : QueryResultUnion;
