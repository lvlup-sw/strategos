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

    /// <summary>
    /// Assembles a <see cref="SemanticQueryResult"/> from a
    /// <see cref="SemanticQueryRequest"/>, the projected items/scores, an optional
    /// <see cref="HybridMeta"/>, and the base <see cref="ResponseMeta"/>. Single
    /// construction site shared by <see cref="OntologyQueryTool"/>'s dense-only
    /// semantic path and <see cref="HybridQueryCoordinator"/>'s hybrid/degraded
    /// paths so the wire shape cannot diverge between them (DIM-5).
    /// </summary>
    internal static SemanticQueryResult FromRequest(
        SemanticQueryRequest request,
        IReadOnlyList<object> items,
        IReadOnlyList<double> scores,
        HybridMeta? hybridMeta,
        ResponseMeta baseMeta) =>
        new(request.ObjectType, items, hybridMeta is null ? baseMeta : baseMeta with { Hybrid = hybridMeta })
        {
            Scores = scores,
            SemanticQuery = request.SemanticQuery,
            TopK = request.TopK,
            MinRelevance = request.MinRelevance,
            Filter = request.Filter,
            TraverseLink = request.TraverseLink,
            InterfaceName = request.InterfaceName,
            Include = request.Include,
        };
}
