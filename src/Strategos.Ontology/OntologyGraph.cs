using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology;

public sealed class OntologyGraph
{
    private readonly Dictionary<(string Domain, string Name), ObjectTypeDescriptor> _objectTypeLookup;
    private readonly Dictionary<string, List<ObjectTypeDescriptor>> _implementorsLookup;
    private readonly Dictionary<string, List<WorkflowChain>> _workflowChainLookup;

    public IReadOnlyList<DomainDescriptor> Domains { get; }
    public IReadOnlyList<ObjectTypeDescriptor> ObjectTypes { get; }
    public IReadOnlyList<InterfaceDescriptor> Interfaces { get; }
    public IReadOnlyList<ResolvedCrossDomainLink> CrossDomainLinks { get; }
    public IReadOnlyList<WorkflowChain> WorkflowChains { get; }
    public IReadOnlyList<string> Warnings { get; }

    internal OntologyGraph(
        IReadOnlyList<DomainDescriptor> domains,
        IReadOnlyList<ObjectTypeDescriptor> objectTypes,
        IReadOnlyList<InterfaceDescriptor> interfaces,
        IReadOnlyList<ResolvedCrossDomainLink> crossDomainLinks,
        IReadOnlyList<WorkflowChain> workflowChains,
        IReadOnlyList<string>? warnings = null)
    {
        Domains = domains;
        ObjectTypes = objectTypes;
        Interfaces = interfaces;
        CrossDomainLinks = crossDomainLinks;
        WorkflowChains = workflowChains;
        Warnings = warnings ?? [];

        _objectTypeLookup = BuildObjectTypeLookup(objectTypes);
        _implementorsLookup = BuildImplementorsLookup(objectTypes);
        _workflowChainLookup = BuildWorkflowChainLookup(workflowChains);
    }

    public ObjectTypeDescriptor? GetObjectType(string domain, string name) =>
        _objectTypeLookup.GetValueOrDefault((domain, name));

    public IReadOnlyList<ObjectTypeDescriptor> GetImplementors(string interfaceName) =>
        _implementorsLookup.TryGetValue(interfaceName, out var implementors)
            ? implementors
            : [];

    public IReadOnlyList<LinkTraversalResult> TraverseLinks(
        string domain, string objectTypeName, int maxDepth = 2)
    {
        var startType = GetObjectType(domain, objectTypeName);
        if (startType is null)
        {
            return [];
        }

        var results = new List<LinkTraversalResult>();
        var visited = new HashSet<(string Domain, string Name)> { (domain, objectTypeName) };
        var queue = new Queue<(ObjectTypeDescriptor ObjectType, int Depth)>();
        queue.Enqueue((startType, 0));

        while (queue.Count > 0)
        {
            var (currentType, currentDepth) = queue.Dequeue();

            if (currentDepth >= maxDepth)
            {
                continue;
            }

            foreach (var link in currentType.Links)
            {
                var targetType = FindObjectTypeByName(currentType.DomainName, link.TargetTypeName);
                if (targetType is null)
                {
                    continue;
                }

                var targetKey = (targetType.DomainName, targetType.Name);
                if (visited.Contains(targetKey))
                {
                    continue;
                }

                visited.Add(targetKey);
                results.Add(new LinkTraversalResult(targetType, link.Name, currentDepth + 1));
                queue.Enqueue((targetType, currentDepth + 1));
            }
        }

        return results;
    }

    public IReadOnlyList<ObjectTypeDescriptor> GetSubtypes(string objectType) =>
        ObjectTypes.Where(ot => ot.ParentTypeName == objectType).ToList().AsReadOnly();

    public IReadOnlyList<WorkflowChain> FindWorkflowChains(string targetWorkflow) =>
        _workflowChainLookup.TryGetValue(targetWorkflow, out var chains)
            ? chains
            : [];

    private ObjectTypeDescriptor? FindObjectTypeByName(string preferredDomain, string typeName)
    {
        // First try the preferred domain
        if (_objectTypeLookup.TryGetValue((preferredDomain, typeName), out var result))
        {
            return result;
        }

        // Fall back to searching all domains
        foreach (var objectType in ObjectTypes)
        {
            if (objectType.Name == typeName)
            {
                return objectType;
            }
        }

        return null;
    }

    private static Dictionary<(string Domain, string Name), ObjectTypeDescriptor> BuildObjectTypeLookup(
        IReadOnlyList<ObjectTypeDescriptor> objectTypes)
    {
        var lookup = new Dictionary<(string, string), ObjectTypeDescriptor>();
        foreach (var objectType in objectTypes)
        {
            lookup[(objectType.DomainName, objectType.Name)] = objectType;
        }

        return lookup;
    }

    private static Dictionary<string, List<ObjectTypeDescriptor>> BuildImplementorsLookup(
        IReadOnlyList<ObjectTypeDescriptor> objectTypes)
    {
        var lookup = new Dictionary<string, List<ObjectTypeDescriptor>>();
        foreach (var objectType in objectTypes)
        {
            foreach (var implementedInterface in objectType.ImplementedInterfaces)
            {
                if (!lookup.TryGetValue(implementedInterface.Name, out var list))
                {
                    list = [];
                    lookup[implementedInterface.Name] = list;
                }

                list.Add(objectType);
            }
        }

        return lookup;
    }

    private static Dictionary<string, List<WorkflowChain>> BuildWorkflowChainLookup(
        IReadOnlyList<WorkflowChain> workflowChains)
    {
        var lookup = new Dictionary<string, List<WorkflowChain>>();
        foreach (var chain in workflowChains)
        {
            if (!lookup.TryGetValue(chain.WorkflowName, out var list))
            {
                list = [];
                lookup[chain.WorkflowName] = list;
            }

            list.Add(chain);
        }

        return lookup;
    }
}
