namespace Strategos.Ontology.MCP;

/// <summary>
/// Result of a semantic (vector similarity) query operation.
/// Extends <see cref="QueryResult"/> with relevance scores and search metadata.
/// </summary>
public sealed record SemanticQueryResult(
    string ObjectType,
    IReadOnlyList<object> Items,
    ResponseMeta Meta) : QueryResult(ObjectType, Items, Meta)
{
    /// <summary>Relevance scores corresponding to each item in <see cref="QueryResult.Items"/>.</summary>
    public IReadOnlyList<double> Scores { get; init; } = [];

    /// <summary>The natural-language query used for the semantic search.</summary>
    public string? SemanticQuery { get; init; }

    /// <summary>Maximum number of results requested.</summary>
    public int TopK { get; init; }

    /// <summary>Minimum relevance threshold applied to filter results.</summary>
    public double MinRelevance { get; init; }
}
