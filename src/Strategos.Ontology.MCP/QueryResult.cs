namespace Strategos.Ontology.MCP;

/// <summary>
/// Result of an ontology query operation.
/// </summary>
public sealed record QueryResult(
    string ObjectType,
    IReadOnlyList<object> Items)
{
    public string? Filter { get; init; }
    public string? TraverseLink { get; init; }
    public string? InterfaceName { get; init; }
    public string? Include { get; init; }
}
