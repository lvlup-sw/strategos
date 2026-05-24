namespace Strategos.Ontology.MCP;

/// <summary>
/// Internal parameter object bundling the per-call shape of a semantic
/// (<c>semanticQuery is not null</c>) query as it flows through
/// <see cref="OntologyQueryTool"/> and <see cref="HybridQueryCoordinator"/>.
/// </summary>
/// <remarks>
/// This collapses the 11–12-argument private-helper signatures that the additive
/// 2.6.0 hybrid surface grew (issue #78). It is deliberately <c>internal</c>: the
/// public <see cref="OntologyQueryTool.QueryAsync"/> signature stays flat and
/// positional so the 2.5.0 backward-compatibility gate (design §6.2/§6.3, DIM-3)
/// is untouched. Every field mirrors a <c>QueryAsync</c> parameter that the
/// semantic branch consumes; <see cref="DistanceMetric"/> is the raw caller string,
/// parsed once when the <c>SimilarityExpression</c> is built.
/// </remarks>
internal sealed record SemanticQueryRequest(
    string ObjectType,
    string SemanticQuery,
    int TopK,
    double MinRelevance,
    string? DistanceMetric,
    string? Filter,
    string? TraverseLink,
    string? InterfaceName,
    string? Include);
