using System.Text;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Discovers and enriches MCP tool descriptors from the ontology graph.
/// </summary>
public sealed class OntologyToolDiscovery
{
    private readonly OntologyGraph _graph;

    public OntologyToolDiscovery(OntologyGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Discovers the three ontology MCP tools, enriched with semantic metadata from the graph.
    /// </summary>
    public IReadOnlyList<OntologyToolDescriptor> Discover()
    {
        var domainNames = string.Join(", ", _graph.Domains.Select(d => d.DomainName));
        var objectTypeCount = _graph.ObjectTypes.Count;

        return
        [
            new OntologyToolDescriptor(
                "ontology_explore",
                BuildExploreDescription(domainNames, objectTypeCount)),
            new OntologyToolDescriptor(
                "ontology_query",
                BuildQueryDescription(domainNames, objectTypeCount)),
            new OntologyToolDescriptor(
                "ontology_action",
                BuildActionDescription(domainNames, objectTypeCount)),
        ];
    }

    private static string BuildExploreDescription(string domainNames, int objectTypeCount)
    {
        var sb = new StringBuilder();
        sb.Append("Explore the ontology schema. ");
        sb.Append($"Domains: {domainNames}. ");
        sb.Append($"{objectTypeCount} object type(s) available. ");
        sb.Append("Scopes: domains, objectTypes, actions, links, interfaces, events, workflowChains.");
        return sb.ToString();
    }

    private static string BuildQueryDescription(string domainNames, int objectTypeCount)
    {
        var sb = new StringBuilder();
        sb.Append("Query ontology objects. ");
        sb.Append($"Domains: {domainNames}. ");
        sb.Append($"{objectTypeCount} object type(s) available. ");
        sb.Append("Supports filter, traverseLink, interface narrowing, and include.");
        return sb.ToString();
    }

    private static string BuildActionDescription(string domainNames, int objectTypeCount)
    {
        var sb = new StringBuilder();
        sb.Append("Execute ontology actions. ");
        sb.Append($"Domains: {domainNames}. ");
        sb.Append($"{objectTypeCount} object type(s) available. ");
        sb.Append("Dispatches actions to objects by type and optional filter.");
        return sb.ToString();
    }
}
