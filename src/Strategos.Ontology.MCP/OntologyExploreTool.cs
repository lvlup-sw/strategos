using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.MCP;

/// <summary>
/// MCP tool for ontology schema discovery.
/// Routes scope parameters to OntologyGraph queries and returns structured results.
/// </summary>
public sealed class OntologyExploreTool
{
    private readonly OntologyGraph _graph;

    public OntologyExploreTool(OntologyGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Explores the ontology schema based on the given scope and optional filters.
    /// </summary>
    public ExploreResult Explore(
        string scope,
        string? domain = null,
        string? objectType = null,
        string? traverseFrom = null,
        int maxDepth = 2)
    {
        if (traverseFrom is not null && domain is not null)
        {
            return ExploreTraversal(domain, traverseFrom, maxDepth);
        }

        return scope switch
        {
            "domains" => ExploreDomains(),
            "objectTypes" => ExploreObjectTypes(domain),
            "actions" => ExploreActions(domain, objectType),
            "links" => ExploreLinks(domain, objectType),
            "events" => ExploreEvents(domain, objectType),
            "interfaces" => ExploreInterfaces(),
            "workflowChains" => ExploreWorkflowChains(),
            _ => new ExploreResult(scope, []),
        };
    }

    private ExploreResult ExploreDomains()
    {
        var items = _graph.Domains.Select(d => new Dictionary<string, object?>
        {
            ["domainName"] = d.DomainName,
            ["objectTypeCount"] = d.ObjectTypes.Count,
        }).ToList();

        return new ExploreResult("domains", items);
    }

    private ExploreResult ExploreObjectTypes(string? domain)
    {
        var types = domain is null
            ? _graph.ObjectTypes
            : _graph.ObjectTypes.Where(t => t.DomainName == domain).ToList();

        var items = types.Select(t => new Dictionary<string, object?>
        {
            ["name"] = t.Name,
            ["domain"] = t.DomainName,
            ["propertyCount"] = t.Properties.Count,
            ["linkCount"] = t.Links.Count,
            ["actionCount"] = t.Actions.Count,
            ["eventCount"] = t.Events.Count,
        }).ToList();

        return new ExploreResult("objectTypes", items);
    }

    private ExploreResult ExploreActions(string? domain, string? objectType)
    {
        var type = ResolveObjectType(domain, objectType);
        if (type is null)
        {
            return new ExploreResult("actions", []);
        }

        var items = type.Actions.Select(a => new Dictionary<string, object?>
        {
            ["name"] = a.Name,
            ["description"] = a.Description,
            ["acceptsType"] = a.AcceptsType?.Name,
            ["returnsType"] = a.ReturnsType?.Name,
            ["bindingType"] = a.BindingType.ToString(),
        }).ToList();

        return new ExploreResult("actions", items);
    }

    private ExploreResult ExploreLinks(string? domain, string? objectType)
    {
        var type = ResolveObjectType(domain, objectType);
        if (type is null)
        {
            return new ExploreResult("links", []);
        }

        var items = type.Links.Select(l => new Dictionary<string, object?>
        {
            ["name"] = l.Name,
            ["targetTypeName"] = l.TargetTypeName,
            ["cardinality"] = l.Cardinality.ToString(),
            ["edgePropertyCount"] = l.EdgeProperties.Count,
        }).ToList();

        return new ExploreResult("links", items);
    }

    private ExploreResult ExploreEvents(string? domain, string? objectType)
    {
        var type = ResolveObjectType(domain, objectType);
        if (type is null)
        {
            return new ExploreResult("events", []);
        }

        var items = type.Events.Select(e => new Dictionary<string, object?>
        {
            ["eventType"] = e.EventType.Name,
            ["description"] = e.Description,
            ["severity"] = e.Severity.ToString(),
            ["materializedLinks"] = e.MaterializedLinks,
            ["updatedProperties"] = e.UpdatedProperties,
        }).ToList();

        return new ExploreResult("events", items);
    }

    private ExploreResult ExploreInterfaces()
    {
        var items = _graph.Interfaces.Select(i => new Dictionary<string, object?>
        {
            ["name"] = i.Name,
            ["propertyCount"] = i.Properties.Count,
        }).ToList();

        return new ExploreResult("interfaces", items);
    }

    private ExploreResult ExploreWorkflowChains()
    {
        var items = _graph.WorkflowChains.Select(w => new Dictionary<string, object?>
        {
            ["workflowName"] = w.WorkflowName,
            ["consumedType"] = w.ConsumedType.Name,
            ["producedType"] = w.ProducedType.Name,
        }).ToList();

        return new ExploreResult("workflowChains", items);
    }

    private ExploreResult ExploreTraversal(string domain, string traverseFrom, int maxDepth)
    {
        var results = _graph.TraverseLinks(domain, traverseFrom, maxDepth);

        var items = results.Select(r => new Dictionary<string, object?>
        {
            ["objectType"] = r.ObjectType.Name,
            ["linkName"] = r.LinkName,
            ["depth"] = r.Depth,
        }).ToList();

        return new ExploreResult("links", items);
    }

    private ObjectTypeDescriptor? ResolveObjectType(string? domain, string? objectType)
    {
        if (domain is null || objectType is null)
        {
            return null;
        }

        return _graph.GetObjectType(domain, objectType);
    }
}
