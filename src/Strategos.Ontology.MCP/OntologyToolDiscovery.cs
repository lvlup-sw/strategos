using System.Diagnostics.CodeAnalysis;
using System.Text;

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.MCP.Internal;

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
    /// <remarks>
    /// Reflective schema generation via JsonSchemaExporter requires unreferenced code
    /// and dynamic code; consumers calling Discover() from trim/AOT-published projects
    /// will see IL2026/IL3050 warnings. Suppress at call site or precompute schemas.
    /// </remarks>
    [RequiresUnreferencedCode("OutputSchema generation reflects over result-record types; not safe under trimming.")]
    [RequiresDynamicCode("OutputSchema generation may require runtime code generation.")]
    public IReadOnlyList<OntologyToolDescriptor> Discover()
    {
        var domainNames = string.Join(", ", _graph.Domains.Select(d => d.DomainName));
        var objectTypeCount = _graph.ObjectTypes.Count;
        var constraintSummaries = BuildConstraintSummaries();

        return
        [
            BuildExploreDescriptor(domainNames, objectTypeCount),
            BuildQueryDescriptor(domainNames, objectTypeCount),
            BuildActionDescriptor(domainNames, objectTypeCount, constraintSummaries),
        ];
    }

    [RequiresUnreferencedCode("OutputSchema generation reflects over ExploreResult.")]
    [RequiresDynamicCode("OutputSchema generation may require runtime code generation.")]
    private static OntologyToolDescriptor BuildExploreDescriptor(string domainNames, int objectTypeCount) =>
        new(
            "ontology_explore",
            BuildExploreDescription(domainNames, objectTypeCount))
        {
            Title = "Explore Ontology Schema",
            OutputSchema = JsonSchemaHelper.JsonSchemaFor<ExploreResult>(),
            Annotations = new ToolAnnotations(
                ReadOnlyHint: true,
                DestructiveHint: false,
                IdempotentHint: true,
                OpenWorldHint: false),
        };

    [RequiresUnreferencedCode("OutputSchema generation reflects over QueryResultUnion.")]
    [RequiresDynamicCode("OutputSchema generation may require runtime code generation.")]
    private static OntologyToolDescriptor BuildQueryDescriptor(string domainNames, int objectTypeCount) =>
        new(
            "ontology_query",
            BuildQueryDescription(domainNames, objectTypeCount))
        {
            Title = "Query Ontology Objects",
            // QueryResultUnion's [JsonPolymorphic] attributes drive a oneOf schema
            // covering both QueryResult ("filter") and SemanticQueryResult ("semantic").
            // JsonSchemaForUnion validates polymorphism and asserts the rewrite produced
            // a top-level oneOf, so a JsonSchemaExporter output-shape regression fails loudly.
            OutputSchema = JsonSchemaHelper.JsonSchemaForUnion<QueryResultUnion>(),
            Annotations = new ToolAnnotations(
                ReadOnlyHint: true,
                DestructiveHint: false,
                IdempotentHint: true,
                OpenWorldHint: false),
        };

    [RequiresUnreferencedCode("OutputSchema generation reflects over ActionToolResult.")]
    [RequiresDynamicCode("OutputSchema generation may require runtime code generation.")]
    private static OntologyToolDescriptor BuildActionDescriptor(
        string domainNames,
        int objectTypeCount,
        IReadOnlyList<ActionConstraintSummary> constraintSummaries) =>
        new(
            "ontology_action",
            BuildActionDescription(domainNames, objectTypeCount, constraintSummaries))
        {
            Title = "Execute Ontology Action",
            OutputSchema = JsonSchemaHelper.JsonSchemaFor<ActionToolResult>(),
            Annotations = new ToolAnnotations(
                ReadOnlyHint: false,
                DestructiveHint: true,
                IdempotentHint: false,
                OpenWorldHint: false),
            ConstraintSummaries = constraintSummaries,
        };

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
        sb.Append("Scopes: domains, objectTypes, actions, links, interfaces, events, workflowChains, vectorProperties.");
        return sb.ToString();
    }

    private static string BuildQueryDescription(string domainNames, int objectTypeCount)
    {
        var sb = new StringBuilder();
        sb.Append("Query ontology objects. ");
        sb.Append($"Domains: {domainNames}. ");
        sb.Append($"{objectTypeCount} object type(s) available. ");
        sb.Append("Supports filter, traverseLink, interface narrowing, include, and semantic search (semanticQuery, topK, minRelevance, distanceMetric).");
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
