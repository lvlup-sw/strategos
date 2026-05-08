namespace Strategos.Ontology.MCP;

/// <summary>
/// Exposes server initialization metadata that MCP-server hosts (Basileus AgentHost,
/// test harnesses) consume when populating the <c>initialize</c> response's
/// <c>capabilities._meta.ontologyVersion</c> field.
/// </summary>
public sealed class OntologyServerCapabilitiesProvider
{
    private readonly OntologyGraph _graph;

    public OntologyServerCapabilitiesProvider(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    /// <summary>
    /// Returns initialize-time capability metadata about the ontology layer.
    /// MCP-server hosts surface this in their <c>initialize</c> response's
    /// <c>capabilities._meta.ontologyVersion</c> field.
    /// </summary>
    public OntologyServerCapabilities GetServerCapabilities() =>
        new(ResponseMeta.ForGraph(_graph).OntologyVersion);
}
