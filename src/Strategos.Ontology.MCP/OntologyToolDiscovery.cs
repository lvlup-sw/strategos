using System.Text;
using Strategos.Ontology.Descriptors;

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
        var constraintSummaries = BuildConstraintSummaries();

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
                BuildActionDescription(domainNames, objectTypeCount, constraintSummaries))
            {
                ConstraintSummaries = constraintSummaries,
            },
        ];
    }

    private IReadOnlyList<ActionConstraintSummary> BuildConstraintSummaries()
    {
        var summaries = new List<ActionConstraintSummary>();

        foreach (var objectType in _graph.ObjectTypes)
        {
            foreach (var action in objectType.Actions)
            {
                if (action.Preconditions.Count == 0)
                {
                    continue;
                }

                var hardCount = action.Preconditions
                    .Count(p => p.Strength == ConstraintStrength.Hard);
                var softCount = action.Preconditions
                    .Count(p => p.Strength == ConstraintStrength.Soft);
                var descriptions = action.Preconditions
                    .Select(p => p.Description)
                    .ToList()
                    .AsReadOnly();

                summaries.Add(new ActionConstraintSummary(
                    objectType.Name,
                    action.Name,
                    hardCount,
                    softCount,
                    descriptions));
            }
        }

        return summaries.AsReadOnly();
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

    private static string BuildActionDescription(
        string domainNames,
        int objectTypeCount,
        IReadOnlyList<ActionConstraintSummary> constraintSummaries)
    {
        var sb = new StringBuilder();
        sb.Append("Execute ontology actions. ");
        sb.Append($"Domains: {domainNames}. ");
        sb.Append($"{objectTypeCount} object type(s) available. ");
        sb.Append("Dispatches actions to objects by type and optional filter.");

        if (constraintSummaries.Count > 0)
        {
            var actionCount = constraintSummaries.Count;
            var typeCount = constraintSummaries.Select(s => s.ObjectTypeName).Distinct().Count();
            var totalConstraints = constraintSummaries.Sum(s => s.HardConstraintCount + s.SoftConstraintCount);
            sb.Append($" {actionCount} action(s) across {typeCount} type(s) with {totalConstraints} constraint rule(s).");
        }

        return sb.ToString();
    }
}
