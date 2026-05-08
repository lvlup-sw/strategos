using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Result of an ontology query operation.
/// </summary>
public record QueryResult(
    string ObjectType,
    IReadOnlyList<object> Items,
    [property: JsonPropertyName("_meta")] ResponseMeta Meta) : QueryResultUnion
{
    public string? Filter { get; init; }
    public string? TraverseLink { get; init; }
    public string? InterfaceName { get; init; }
    public string? Include { get; init; }
}
