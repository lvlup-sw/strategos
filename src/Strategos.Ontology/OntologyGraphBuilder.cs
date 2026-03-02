using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Extensions;

namespace Strategos.Ontology;

public sealed class OntologyGraphBuilder
{
    private readonly List<DomainOntology> _domainOntologies = [];
    private readonly List<WorkflowMetadataBuilder> _workflowMetadata = [];

    public OntologyGraphBuilder AddDomain<T>()
        where T : DomainOntology, new()
    {
        _domainOntologies.Add(new T());
        return this;
    }

    internal OntologyGraphBuilder AddDomain(DomainOntology domain)
    {
        _domainOntologies.Add(domain);
        return this;
    }

    internal OntologyGraphBuilder AddWorkflowMetadata(IEnumerable<WorkflowMetadataBuilder> metadata)
    {
        _workflowMetadata.AddRange(metadata);
        return this;
    }

    public OntologyGraph Build()
    {
        var domains = new List<DomainDescriptor>();
        var allObjectTypes = new List<ObjectTypeDescriptor>();
        var allInterfaces = new List<InterfaceDescriptor>();
        var allCrossDomainLinkDescriptors = new List<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)>();

        foreach (var domainOntology in _domainOntologies)
        {
            var ontologyBuilder = new OntologyBuilder(domainOntology.DomainName);
            domainOntology.Build(ontologyBuilder);

            var domainDescriptor = new DomainDescriptor(domainOntology.DomainName)
            {
                ObjectTypes = ontologyBuilder.ObjectTypes.ToArray(),
            };

            domains.Add(domainDescriptor);
            allObjectTypes.AddRange(ontologyBuilder.ObjectTypes);
            allInterfaces.AddRange(ontologyBuilder.Interfaces);

            foreach (var crossDomainLink in ontologyBuilder.CrossDomainLinks)
            {
                allCrossDomainLinkDescriptors.Add((domainOntology.DomainName, crossDomainLink));
            }
        }

        var domainLookup = domains.ToDictionary(d => d.DomainName);
        var objectTypeLookup = allObjectTypes
            .GroupBy(ot => ot.DomainName)
            .ToDictionary(g => g.Key, g => g.ToDictionary(ot => ot.Name));

        var resolvedLinks = ResolveCrossDomainLinks(
            allCrossDomainLinkDescriptors, domainLookup, objectTypeLookup, allObjectTypes);

        ValidateIsAHierarchy(allObjectTypes);
        ValidateInterfaceImplementations(allObjectTypes, allInterfaces);
        ValidateInterfaceActionMappings(allObjectTypes, allInterfaces);
        ValidateLifecycles(allObjectTypes);
        ComputeTransitiveDerivationChains(allObjectTypes);
        InferPropertyKinds(allObjectTypes);
        ValidateInverseLinks(allObjectTypes);

        var warnings = new List<string>();
        MatchExtensionPoints(allObjectTypes, resolvedLinks, warnings);

        var workflowChains = BuildWorkflowChains(allObjectTypes, _workflowMetadata);

        return new OntologyGraph(
            domains: domains.ToArray(),
            objectTypes: allObjectTypes.ToArray(),
            interfaces: allInterfaces.ToArray(),
            crossDomainLinks: resolvedLinks.ToArray(),
            workflowChains: workflowChains.ToArray(),
            warnings: warnings.AsReadOnly());
    }

    private static List<ResolvedCrossDomainLink> ResolveCrossDomainLinks(
        List<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)> linkDescriptors,
        Dictionary<string, DomainDescriptor> domainLookup,
        Dictionary<string, Dictionary<string, ObjectTypeDescriptor>> objectTypeLookup,
        List<ObjectTypeDescriptor> allObjectTypes)
    {
        var resolved = new List<ResolvedCrossDomainLink>();

        foreach (var (sourceDomain, descriptor) in linkDescriptors)
        {
            if (!domainLookup.ContainsKey(descriptor.TargetDomain))
            {
                throw new OntologyCompositionException(
                    $"Cross-domain link '{descriptor.Name}' references unresolvable domain '{descriptor.TargetDomain}'.");
            }

            if (!objectTypeLookup.TryGetValue(descriptor.TargetDomain, out var targetDomainTypes)
                || !targetDomainTypes.TryGetValue(descriptor.TargetTypeName, out var targetObjectType))
            {
                throw new OntologyCompositionException(
                    $"Cross-domain link '{descriptor.Name}' references unresolvable object type '{descriptor.TargetTypeName}' in domain '{descriptor.TargetDomain}'.");
            }

            var sourceObjectType = allObjectTypes.FirstOrDefault(
                ot => ot.DomainName == sourceDomain && ot.ClrType == descriptor.SourceType);

            if (sourceObjectType is null)
            {
                throw new OntologyCompositionException(
                    $"Cross-domain link '{descriptor.Name}' references unresolvable source type '{descriptor.SourceType.Name}' in domain '{sourceDomain}'.");
            }

            resolved.Add(new ResolvedCrossDomainLink(
                Name: descriptor.Name,
                SourceDomain: sourceDomain,
                SourceObjectType: sourceObjectType,
                TargetDomain: descriptor.TargetDomain,
                TargetObjectType: targetObjectType,
                Cardinality: descriptor.Cardinality,
                EdgeProperties: descriptor.EdgeProperties));
        }

        return resolved;
    }

    private static void MatchExtensionPoints(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<ResolvedCrossDomainLink> resolvedLinks,
        List<string> warnings)
    {
        for (var i = 0; i < allObjectTypes.Count; i++)
        {
            var objectType = allObjectTypes[i];
            if (objectType.ExternalLinkExtensionPoints.Count == 0)
            {
                continue;
            }

            // Find incoming cross-domain links targeting this object type
            var incomingLinks = resolvedLinks
                .Where(l => l.TargetObjectType.Name == objectType.Name
                         && l.TargetDomain == objectType.DomainName)
                .ToList();

            var updatedExtensionPoints = new List<ExternalLinkExtensionPoint>();

            foreach (var extensionPoint in objectType.ExternalLinkExtensionPoints)
            {
                var matchedNames = new List<string>();

                foreach (var link in incomingLinks)
                {
                    var isMatch = true;

                    // Check domain constraint
                    if (extensionPoint.RequiredSourceDomain is not null
                        && link.SourceDomain != extensionPoint.RequiredSourceDomain)
                    {
                        isMatch = false;
                    }

                    // Check interface constraint
                    if (isMatch && extensionPoint.RequiredSourceInterface is not null)
                    {
                        var sourceImplementsInterface = link.SourceObjectType.ImplementedInterfaces
                            .Any(iface => iface.Name == extensionPoint.RequiredSourceInterface
                                       || iface.InterfaceType.Name == extensionPoint.RequiredSourceInterface);
                        if (!sourceImplementsInterface)
                        {
                            isMatch = false;
                            warnings.Add(
                                $"Cross-domain link '{link.Name}' targets extension point '{extensionPoint.Name}' on '{objectType.Name}' but source type '{link.SourceObjectType.Name}' does not implement required interface '{extensionPoint.RequiredSourceInterface}'.");
                        }
                    }

                    // Check edge property constraints
                    if (isMatch)
                    {
                        foreach (var requiredEdgeProp in extensionPoint.RequiredEdgeProperties)
                        {
                            var hasEdgeProp = link.EdgeProperties
                                .Any(ep => ep.Name == requiredEdgeProp.Name);
                            if (!hasEdgeProp)
                            {
                                warnings.Add(
                                    $"Cross-domain link '{link.Name}' matched extension point '{extensionPoint.Name}' on '{objectType.Name}' but is missing required edge property '{requiredEdgeProp.Name}'.");
                            }
                        }
                    }

                    if (isMatch)
                    {
                        matchedNames.Add(link.Name);
                    }
                }

                updatedExtensionPoints.Add(extensionPoint with
                {
                    MatchedLinkNames = matchedNames.AsReadOnly(),
                });
            }

            allObjectTypes[i] = objectType with
            {
                ExternalLinkExtensionPoints = updatedExtensionPoints.AsReadOnly(),
            };
        }
    }

    private static void ValidateIsAHierarchy(List<ObjectTypeDescriptor> allObjectTypes)
    {
        var typesByName = allObjectTypes.ToDictionary(ot => ot.Name);

        foreach (var objectType in allObjectTypes)
        {
            if (objectType.ParentTypeName is null)
            {
                continue;
            }

            if (!typesByName.ContainsKey(objectType.ParentTypeName))
            {
                throw new OntologyCompositionException(
                    $"Object type '{objectType.Name}' declares IS-A relationship with unregistered parent type '{objectType.ParentTypeName}'.");
            }
        }

        // Detect cycles using DFS
        foreach (var objectType in allObjectTypes)
        {
            if (objectType.ParentTypeName is null)
            {
                continue;
            }

            var visited = new HashSet<string>();
            var current = objectType.Name;

            while (current is not null)
            {
                if (!visited.Add(current))
                {
                    throw new OntologyCompositionException(
                        $"IS-A hierarchy cycle detected involving type '{current}'.");
                }

                if (typesByName.TryGetValue(current, out var currentType))
                {
                    current = currentType.ParentTypeName;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private static void ValidateInterfaceImplementations(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<InterfaceDescriptor> allInterfaces)
    {
        var interfaceByName = allInterfaces
            .GroupBy(i => i.Name)
            .ToDictionary(g => g.Key, g => g.First());

        var interfaceByType = allInterfaces
            .GroupBy(i => i.InterfaceType)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var objectType in allObjectTypes)
        {
            foreach (var implementedInterface in objectType.ImplementedInterfaces)
            {
                // Try name-based lookup first, then fall back to type-based
                if (!interfaceByName.TryGetValue(implementedInterface.Name, out var interfaceDescriptor))
                {
                    if (!interfaceByType.TryGetValue(implementedInterface.InterfaceType, out interfaceDescriptor))
                    {
                        continue;
                    }
                }

                var objectPropertyLookup = objectType.Properties
                    .ToDictionary(p => p.Name, p => p.PropertyType);

                // Also include the key property in the lookup (keys are valid interface targets)
                if (objectType.KeyProperty is not null)
                {
                    objectPropertyLookup.TryAdd(objectType.KeyProperty.Name, objectType.KeyProperty.PropertyType);
                }

                // Build a set of interface property names covered by Via() mappings
                var viaMappedTargets = objectType.InterfacePropertyMappings
                    .Where(m => m.InterfaceName == implementedInterface.Name)
                    .ToDictionary(m => m.TargetPropertyName, m => m.SourcePropertyName);

                foreach (var interfaceProperty in interfaceDescriptor.Properties)
                {
                    // Check direct name match first
                    if (objectPropertyLookup.TryGetValue(interfaceProperty.Name, out var objectPropertyType))
                    {
                        if (!interfaceProperty.PropertyType.IsAssignableFrom(objectPropertyType))
                        {
                            throw new OntologyCompositionException(
                                $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but property '{interfaceProperty.Name}' has incompatible type. Expected '{interfaceProperty.PropertyType.Name}', found '{objectPropertyType.Name}'.");
                        }

                        continue;
                    }

                    // Check Via() mapping
                    if (viaMappedTargets.TryGetValue(interfaceProperty.Name, out var sourcePropName))
                    {
                        if (objectPropertyLookup.TryGetValue(sourcePropName, out var sourcePropertyType))
                        {
                            if (!interfaceProperty.PropertyType.IsAssignableFrom(sourcePropertyType))
                            {
                                throw new OntologyCompositionException(
                                    $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but Via() mapped property '{sourcePropName}' has incompatible type for interface property '{interfaceProperty.Name}'. Expected '{interfaceProperty.PropertyType.Name}', found '{sourcePropertyType.Name}'.");
                            }

                            continue;
                        }
                    }

                    throw new OntologyCompositionException(
                        $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but is missing property '{interfaceProperty.Name}'.");
                }
            }
        }
    }

    private static void ValidateInterfaceActionMappings(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<InterfaceDescriptor> allInterfaces)
    {
        // Build lookup by InterfaceType since ObjectTypeBuilder uses typeof(TInterface).Name
        // while InterfaceBuilder uses the user-provided name
        var interfaceByType = allInterfaces
            .GroupBy(i => i.InterfaceType)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var objectType in allObjectTypes)
        {
            foreach (var implementedInterface in objectType.ImplementedInterfaces)
            {
                if (!interfaceByType.TryGetValue(implementedInterface.InterfaceType, out var interfaceDescriptor))
                {
                    continue;
                }

                if (interfaceDescriptor.Actions.Count == 0)
                {
                    continue;
                }

                var mappedActionNames = objectType.InterfaceActionMappings
                    .Select(m => m.InterfaceActionName)
                    .ToHashSet();

                foreach (var interfaceAction in interfaceDescriptor.Actions)
                {
                    if (!mappedActionNames.Contains(interfaceAction.Name))
                    {
                        throw new OntologyCompositionException(
                            $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but does not map interface action '{interfaceAction.Name}'. Use ActionVia() or ActionDefault() in the Implements<> mapping.");
                    }
                }
            }
        }
    }

    private static void ValidateLifecycles(List<ObjectTypeDescriptor> allObjectTypes)
    {
        foreach (var objectType in allObjectTypes)
        {
            if (objectType.Lifecycle is null)
            {
                continue;
            }

            var lifecycle = objectType.Lifecycle;

            // Exactly one initial state
            var initialStates = lifecycle.States.Count(s => s.IsInitial);
            if (initialStates != 1)
            {
                throw new OntologyCompositionException(
                    $"Object type '{objectType.Name}' lifecycle must have exactly 1 initial state, found {initialStates}.");
            }

            // At least one terminal state
            var terminalStates = lifecycle.States.Count(s => s.IsTerminal);
            if (terminalStates < 1)
            {
                throw new OntologyCompositionException(
                    $"Object type '{objectType.Name}' lifecycle must have at least 1 terminal state, found 0.");
            }

            // Validate transition state references
            var stateNames = lifecycle.States.Select(s => s.Name).ToHashSet();
            foreach (var transition in lifecycle.Transitions)
            {
                if (!stateNames.Contains(transition.FromState))
                {
                    throw new OntologyCompositionException(
                        $"Object type '{objectType.Name}' lifecycle transition references undeclared state '{transition.FromState}'.");
                }

                if (!stateNames.Contains(transition.ToState))
                {
                    throw new OntologyCompositionException(
                        $"Object type '{objectType.Name}' lifecycle transition references undeclared state '{transition.ToState}'.");
                }
            }
        }
    }

    private static void ComputeTransitiveDerivationChains(List<ObjectTypeDescriptor> allObjectTypes)
    {
        for (var i = 0; i < allObjectTypes.Count; i++)
        {
            var objectType = allObjectTypes[i];
            var computedProperties = objectType.Properties
                .Where(p => p.IsComputed && p.DerivedFrom.Count > 0)
                .ToList();

            if (computedProperties.Count == 0)
            {
                continue;
            }

            var propertyLookup = objectType.Properties.ToDictionary(p => p.Name);
            var updatedProperties = new List<PropertyDescriptor>();

            foreach (var prop in objectType.Properties)
            {
                if (!prop.IsComputed || prop.DerivedFrom.Count == 0)
                {
                    updatedProperties.Add(prop);
                    continue;
                }

                var transitive = new HashSet<DerivationSource>();
                var visited = new HashSet<string>();
                ComputeTransitiveSources(prop.Name, propertyLookup, transitive, visited);

                updatedProperties.Add(prop with
                {
                    TransitiveDerivedFrom = transitive.ToList().AsReadOnly(),
                });
            }

            allObjectTypes[i] = objectType with
            {
                Properties = updatedProperties.AsReadOnly(),
            };
        }
    }

    private static void ComputeTransitiveSources(
        string propertyName,
        Dictionary<string, PropertyDescriptor> propertyLookup,
        HashSet<DerivationSource> transitive,
        HashSet<string> visited)
    {
        if (!visited.Add(propertyName))
        {
            throw new OntologyCompositionException(
                $"Derivation cycle detected involving property '{propertyName}'.");
        }

        if (!propertyLookup.TryGetValue(propertyName, out var prop))
        {
            return;
        }

        foreach (var source in prop.DerivedFrom)
        {
            transitive.Add(source);

            if (source.Kind == Descriptors.DerivationSourceKind.Local && source.PropertyName is not null)
            {
                ComputeTransitiveSources(source.PropertyName, propertyLookup, transitive, visited);
            }
        }

        visited.Remove(propertyName);
    }

    private static void ValidateInverseLinks(List<ObjectTypeDescriptor> allObjectTypes)
    {
        var typesByName = allObjectTypes.ToDictionary(ot => ot.Name);

        foreach (var objectType in allObjectTypes)
        {
            foreach (var link in objectType.Links)
            {
                if (link.InverseLinkName is null)
                {
                    continue;
                }

                if (!typesByName.TryGetValue(link.TargetTypeName, out var targetType))
                {
                    continue; // Target type not found; other validations handle this
                }

                var inverseLink = targetType.Links.FirstOrDefault(l => l.Name == link.InverseLinkName);
                if (inverseLink is null)
                {
                    throw new OntologyCompositionException(
                        $"Link '{link.Name}' on '{objectType.Name}' declares inverse '{link.InverseLinkName}' but target type '{link.TargetTypeName}' has no link named '{link.InverseLinkName}'.");
                }

                // If the inverse link also declares an inverse, verify symmetry
                if (inverseLink.InverseLinkName is not null && inverseLink.InverseLinkName != link.Name)
                {
                    throw new OntologyCompositionException(
                        $"Asymmetric inverse declaration: '{objectType.Name}.{link.Name}' declares inverse '{link.InverseLinkName}', but '{link.TargetTypeName}.{link.InverseLinkName}' declares inverse '{inverseLink.InverseLinkName}' instead of '{link.Name}'.");
                }
            }
        }
    }

    private static void InferPropertyKinds(List<ObjectTypeDescriptor> allObjectTypes)
    {
        var registeredClrTypes = allObjectTypes
            .Select(ot => ot.ClrType)
            .ToHashSet();

        for (var i = 0; i < allObjectTypes.Count; i++)
        {
            var objectType = allObjectTypes[i];
            var updatedProperties = new List<PropertyDescriptor>();
            var hasChanges = false;

            foreach (var prop in objectType.Properties)
            {
                if (prop.Kind == PropertyKind.Vector)
                {
                    updatedProperties.Add(prop);
                    continue;
                }

                PropertyKind kind;

                if (prop.IsComputed)
                {
                    kind = PropertyKind.Computed;
                }
                else if (IsReferenceType(prop.PropertyType, registeredClrTypes))
                {
                    kind = PropertyKind.Reference;
                }
                else
                {
                    kind = PropertyKind.Scalar;
                }

                if (kind != prop.Kind)
                {
                    hasChanges = true;
                    updatedProperties.Add(prop with { Kind = kind });
                }
                else
                {
                    updatedProperties.Add(prop);
                }
            }

            if (hasChanges)
            {
                allObjectTypes[i] = objectType with
                {
                    Properties = updatedProperties.AsReadOnly(),
                };
            }
        }
    }

    private static bool IsReferenceType(Type propertyType, HashSet<Type> registeredClrTypes)
    {
        // Check the direct type
        if (registeredClrTypes.Contains(propertyType))
        {
            return true;
        }

        // Check for Nullable<T>
        var underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying is not null && registeredClrTypes.Contains(underlying))
        {
            return true;
        }

        return false;
    }

    private static List<WorkflowChain> BuildWorkflowChains(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<WorkflowMetadataBuilder> workflowMetadata)
    {
        var chains = new List<WorkflowChain>();
        var objectTypeByName = allObjectTypes.ToDictionary(ot => ot.Name);

        foreach (var metadata in workflowMetadata)
        {
            if (metadata.ConsumedTypeName is null || metadata.ProducedTypeName is null)
            {
                continue;
            }

            if (!objectTypeByName.TryGetValue(metadata.ConsumedTypeName, out var consumedType))
            {
                continue;
            }

            if (!objectTypeByName.TryGetValue(metadata.ProducedTypeName, out var producedType))
            {
                continue;
            }

            chains.Add(new WorkflowChain(metadata.WorkflowName, consumedType, producedType));
        }

        return chains;
    }
}
