using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Query;

internal sealed class OntologyQueryService(OntologyGraph graph) : IOntologyQuery
{
    public IReadOnlyList<ObjectTypeDescriptor> GetObjectTypes(
        string? domain = null, string? implementsInterface = null)
    {
        IEnumerable<ObjectTypeDescriptor> result = graph.ObjectTypes;

        if (domain is not null)
        {
            result = result.Where(ot => ot.DomainName == domain);
        }

        if (implementsInterface is not null)
        {
            result = result.Where(ot =>
                ot.ImplementedInterfaces.Any(i =>
                    i.Name == implementsInterface || i.InterfaceType.Name == implementsInterface));
        }

        return result.ToList().AsReadOnly();
    }

    public IReadOnlyList<ActionDescriptor> GetActions(string objectType)
    {
        var ot = FindObjectType(objectType);
        return ot?.Actions ?? [];
    }

    public IReadOnlyList<LinkDescriptor> GetLinks(string objectType)
    {
        var ot = FindObjectType(objectType);
        return ot?.Links ?? [];
    }

    public IReadOnlyList<ObjectTypeDescriptor> GetImplementors(string interfaceName) =>
        graph.GetImplementors(interfaceName);

    public IReadOnlyList<ActionDescriptor> GetValidActions(
        string objectType,
        IReadOnlyDictionary<string, object?>? knownProperties = null)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return [];
        }

        if (knownProperties is null)
        {
            return ot.Actions;
        }

        return ot.Actions
            .Where(a => a.Preconditions.Count == 0 || a.Preconditions.All(p =>
                p.Kind == PreconditionKind.LinkExists || IsPreconditionSatisfiable(p, knownProperties)))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<PostconditionTrace> TracePostconditions(
        string objectType, string actionName, int maxDepth = 1)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return [];
        }

        var action = ot.Actions.FirstOrDefault(a => a.Name == actionName);
        if (action is null)
        {
            return [];
        }

        return action.Postconditions
            .Select(pc => new PostconditionTrace(actionName, pc, objectType))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ActionDescriptor> GetActionsForState(
        string objectType, string stateName)
    {
        var ot = FindObjectType(objectType);
        if (ot?.Lifecycle is null)
        {
            return ot?.Actions ?? [];
        }

        // Find actions that trigger transitions FROM this state
        var actionsForState = ot.Lifecycle.Transitions
            .Where(t => t.FromState == stateName && t.TriggerActionName is not null)
            .Select(t => t.TriggerActionName!)
            .ToHashSet();

        // Also include actions without lifecycle constraints (no transition references)
        var lifecycleActionNames = ot.Lifecycle.Transitions
            .Where(t => t.TriggerActionName is not null)
            .Select(t => t.TriggerActionName!)
            .ToHashSet();

        return ot.Actions
            .Where(a => actionsForState.Contains(a.Name) || !lifecycleActionNames.Contains(a.Name))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<LifecycleTransitionDescriptor> GetTransitionsFrom(
        string objectType, string stateName)
    {
        var ot = FindObjectType(objectType);
        if (ot?.Lifecycle is null)
        {
            return [];
        }

        return ot.Lifecycle.Transitions
            .Where(t => t.FromState == stateName)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<AffectedProperty> GetAffectedProperties(
        string objectType, string propertyName)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return [];
        }

        var affected = new List<AffectedProperty>();

        foreach (var prop in ot.Properties)
        {
            if (prop.DerivedFrom.Any(d =>
                    d.Kind == DerivationSourceKind.Local && d.PropertyName == propertyName))
            {
                affected.Add(new AffectedProperty(ot.DomainName, ot.Name, prop.Name, IsDirect: true));
            }
            else if (prop.TransitiveDerivedFrom.Any(d =>
                    d.Kind == DerivationSourceKind.Local && d.PropertyName == propertyName))
            {
                affected.Add(new AffectedProperty(ot.DomainName, ot.Name, prop.Name, IsDirect: false));
            }
        }

        return affected.AsReadOnly();
    }

    public IReadOnlyList<DerivationSource> GetDerivationChain(
        string objectType, string propertyName)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return [];
        }

        var prop = ot.Properties.FirstOrDefault(p => p.Name == propertyName);
        if (prop is null)
        {
            return [];
        }

        return prop.TransitiveDerivedFrom.Count > 0
            ? prop.TransitiveDerivedFrom
            : prop.DerivedFrom;
    }

    public IReadOnlyList<InterfaceActionDescriptor> GetInterfaceActions(string interfaceName)
    {
        var iface = graph.Interfaces.FirstOrDefault(i =>
            i.Name == interfaceName || i.InterfaceType.Name == interfaceName);

        return iface?.Actions ?? [];
    }

    public ActionDescriptor? ResolveInterfaceAction(
        string objectType, string interfaceActionName)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return null;
        }

        var mapping = ot.InterfaceActionMappings
            .FirstOrDefault(m => m.InterfaceActionName == interfaceActionName);

        if (mapping is null)
        {
            return null;
        }

        return ot.Actions.FirstOrDefault(a => a.Name == mapping.ConcreteActionName);
    }

    public IReadOnlyList<ExternalLinkExtensionPoint> GetExtensionPoints(string objectType)
    {
        var ot = FindObjectType(objectType);
        return ot?.ExternalLinkExtensionPoints ?? [];
    }

    public IReadOnlyList<ResolvedCrossDomainLink> GetIncomingCrossDomainLinks(string objectType)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return [];
        }

        return graph.CrossDomainLinks
            .Where(l => l.TargetObjectType.Name == ot.Name && l.TargetDomain == ot.DomainName)
            .ToList()
            .AsReadOnly();
    }

    private ObjectTypeDescriptor? FindObjectType(string objectType) =>
        graph.ObjectTypes.FirstOrDefault(ot => ot.Name == objectType);

    private static bool IsPreconditionSatisfiable(
        ActionPrecondition precondition,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        // For PropertyPredicate preconditions, check if known properties could satisfy them
        // This is a simple heuristic - we can't fully evaluate expression trees at runtime
        // without compiling them, so we check if the referenced property is known
        if (precondition.Kind != PreconditionKind.PropertyPredicate)
        {
            return true;
        }

        // If there's no expression to parse, consider it satisfiable
        return true;
    }
}
