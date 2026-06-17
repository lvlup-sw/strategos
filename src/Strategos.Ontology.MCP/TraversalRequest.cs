namespace Strategos.Ontology.MCP;

/// <summary>
/// A validated, closed-vocabulary instance-anchored traversal request (DR-15).
/// The MCP traversal tool accepts only these structured inputs — a source object
/// type + instance id, a link name resolved against the graph, an integer depth
/// bounded by <see cref="OntologyTraversalLimits.MaxDepth"/>, a
/// <see cref="TraversalDirection"/> enum, and an optional edge-attribute equality
/// filter — never a free-text traversal expression.
/// </summary>
/// <param name="ObjectType">Descriptor name of the source object type to anchor on.</param>
/// <param name="ObjectId">Projected id of the source instance to traverse from.</param>
/// <param name="LinkName">Name of the link to traverse; must exist on the source descriptor.</param>
/// <param name="Direction">Which association endpoint to hop to on the far side.</param>
/// <param name="Depth">Traversal depth; must be in <c>1..<see cref="OntologyTraversalLimits.MaxDepth"/></c>.</param>
/// <param name="Domain">Optional domain qualifier for the source object type.</param>
/// <param name="EdgeFilter">
/// Optional edge-attribute equality filter applied to the association objects
/// BEFORE the far hop, keyed by edge-attribute property name. Mirrors the
/// in-process <c>ObjectSet.Where(e =&gt; e.Prop == value)</c> edge filter.
/// </param>
/// <param name="Cursor">Opaque pagination cursor from a prior page, or null for the first page.</param>
public sealed record TraversalRequest(
    string ObjectType,
    string ObjectId,
    string LinkName,
    TraversalDirection Direction,
    int Depth,
    string? Domain = null,
    IReadOnlyDictionary<string, string>? EdgeFilter = null,
    string? Cursor = null);
