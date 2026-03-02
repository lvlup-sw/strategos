namespace Strategos.Ontology.MCP;

/// <summary>
/// Result of a semantic (vector similarity) query operation.
/// Extends <see cref="QueryResult"/> with relevance scores and search metadata.
/// </summary>
public sealed record SemanticQueryResult(
    string ObjectType,
    IReadOnlyList<object> Items) : QueryResult(ObjectType, Items)
{
    public IReadOnlyList<double> Scores { get; init; } = [];
    public string? SemanticQuery { get; init; }
    public int TopK { get; init; }
    public double MinRelevance { get; init; }
}
