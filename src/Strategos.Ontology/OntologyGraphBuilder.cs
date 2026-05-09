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

        // AONT040 — DuplicateObjectTypeName (Track C1)
        // Enforce per-domain uniqueness of descriptor names with an explicit diagnostic,
        // before the ToDictionary call below would otherwise throw a cryptic ArgumentException.
        foreach (var group in allObjectTypes.GroupBy(ot => ot.DomainName))
        {
            var seen = new Dictionary<string, ObjectTypeDescriptor>();
            foreach (var descriptor in group)
            {
                if (seen.TryGetValue(descriptor.Name, out var existing))
                {
                    throw new OntologyCompositionException(
                        $"AONT040: Object type name '{descriptor.Name}' is registered twice in domain '{group.Key}'. " +
                        $"First registration: CLR type '{existing.ClrType.FullName}'. " +
                        $"Second registration: CLR type '{descriptor.ClrType.FullName}'. " +
                        $"Either remove one registration, or specify distinct names via Object<T>(\"name\", ...).");
                }

                seen[descriptor.Name] = descriptor;
            }
        }

        var objectTypeLookup = allObjectTypes
            .GroupBy(ot => ot.DomainName)
            .ToDictionary(g => g.Key, g => g.ToDictionary(ot => ot.Name));

        // Track C2 — reverse index from CLR type → descriptor names in registration order.
        // Built after the AONT040 check so callers can trust name uniqueness-per-domain,
        // and used by C3 below to detect multi-registered types in link positions.
        var namesByType = allObjectTypes
            .GroupBy(ot => ot.ClrType)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(ot => ot.Name).ToList().AsReadOnly());

        // AONT041 must run before ResolveCrossDomainLinks: that resolver does a
        // first-wins ClrType match on (sourceDomain, ClrType) which would silently
        // bind a multi-registered source type to one descriptor without surfacing
        // the violation. Fail fast here so the diagnostic points at the original
        // multi-registration rather than a downstream "unresolvable" error.
        ValidateMultiRegisteredTypesNotInLinks(
            allObjectTypes, allCrossDomainLinkDescriptors, namesByType);

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

        var workflowChains = BuildWorkflowChains(allObjectTypes, _workflowMetadata, warnings);

        return new OntologyGraph(
            domains: domains.ToArray(),
            objectTypes: allObjectTypes.ToArray(),
            interfaces: allInterfaces.ToArray(),
            crossDomainLinks: resolvedLinks.ToArray(),
            workflowChains: workflowChains.ToArray(),
            objectTypeNamesByType: namesByType,
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
                EdgeProperties: descriptor.EdgeProperties,
                Description: descriptor.Description));
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
        // Keys are (DomainName, Name) so that two domains can legally host descriptors
        // that share a simple name (enforced by the AONT040 per-domain check above).
        var typesByKey = allObjectTypes.ToDictionary(ot => (ot.DomainName, ot.Name));

        foreach (var objectType in allObjectTypes)
        {
            if (objectType.ParentTypeName is null)
            {
                continue;
            }

            if (!typesByKey.ContainsKey((objectType.DomainName, objectType.ParentTypeName)))
            {
                throw new OntologyCompositionException(
                    $"Object type '{objectType.Name}' declares IS-A relationship with unregistered parent type '{objectType.ParentTypeName}'.");
            }
        }

        // Detect cycles using DFS (domain-scoped)
        foreach (var objectType in allObjectTypes)
        {
            if (objectType.ParentTypeName is null)
            {
                continue;
            }

            var visited = new HashSet<string>();
            var currentName = objectType.Name;
            var currentDomain = objectType.DomainName;

            while (currentName is not null)
            {
                if (!visited.Add(currentName))
                {
                    throw new OntologyCompositionException(
                        $"IS-A hierarchy cycle detected involving type '{currentName}'.");
                }

                if (typesByKey.TryGetValue((currentDomain, currentName), out var currentType))
                {
                    currentName = currentType.ParentTypeName;
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
        // Keyed by (DomainName, Name) because descriptor names are unique only within a
        // domain — cross-domain name collisions are legal under the AONT040 invariant.
        var typesByKey = allObjectTypes.ToDictionary(ot => (ot.DomainName, ot.Name));

        foreach (var objectType in allObjectTypes)
        {
            foreach (var link in objectType.Links)
            {
                if (link.InverseLinkName is null)
                {
                    continue;
                }

                if (!typesByKey.TryGetValue((objectType.DomainName, link.TargetTypeName), out var targetType))
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

    /// <summary>
    /// AONT041 — MultiRegisteredTypeInLink (Track C3).
    /// Enforces the Option X freeze invariant that a CLR type registered under more than
    /// one descriptor name cannot participate in structural links — neither as a link
    /// target nor as a link source, and neither in intra-domain <see cref="LinkDescriptor"/>s
    /// nor in <see cref="CrossDomainLinkDescriptor"/>s. The Basileus happy path is a
    /// multi-registered leaf type with no links anywhere — that remains legal.
    /// </summary>
    /// <remarks>
    /// Intra-domain link targets carry only a <see cref="LinkDescriptor.TargetTypeName"/>
    /// string. Under the Track B builder, <c>HasMany&lt;TLinked&gt;(name)</c> writes
    /// <c>typeof(TLinked).Name</c> into that field, so we match multi-registered CLR types
    /// against the link target by simple type name. Cross-domain links carry the source
    /// CLR <see cref="Type"/> directly on the descriptor and identify their target by
    /// <c>(TargetDomain, TargetTypeName)</c>, so we resolve those positionally. Future
    /// relaxation (see #32) can carry the source CLR <see cref="Type"/> on intra-domain
    /// links too.
    /// </remarks>
    private static void ValidateMultiRegisteredTypesNotInLinks(
        IReadOnlyList<ObjectTypeDescriptor> allObjectTypes,
        IReadOnlyList<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)> crossDomainLinks,
        IReadOnlyDictionary<Type, IReadOnlyList<string>> namesByType)
    {
        var multiRegistered = namesByType
            .Where(kvp => kvp.Value.Count > 1)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Map simple CLR-type names → CLR Type, restricted to multi-registered types.
        // Simple names match what HasMany<TLinked>(name) writes into LinkDescriptor.TargetTypeName.
        var multiRegisteredByClrSimpleName = multiRegistered
            .GroupBy(kvp => kvp.Key.Name)
            .ToDictionary(g => g.Key, g => g.First().Key);

        // Index descriptors by (DomainName, ClrSimpleName) for the explicit-name check below.
        // A link's TargetTypeName carries the CLR simple name of the linked type — use this
        // to find the registered descriptor and verify its name matches the CLR simple name.
        var descriptorByDomainAndClrSimpleName = allObjectTypes
            .GroupBy(ot => (ot.DomainName, ot.ClrType.Name))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var descriptor in allObjectTypes)
        {
            // Check: does this descriptor declare an outgoing link whose target
            // resolves to a multi-registered CLR type?
            foreach (var link in descriptor.Links)
            {
                if (multiRegisteredByClrSimpleName.TryGetValue(link.TargetTypeName, out var targetClrType))
                {
                    var names = multiRegistered[targetClrType];
                    throw new OntologyCompositionException(
                        $"AONT041: CLR type '{targetClrType.FullName}' has multiple registrations " +
                        $"({string.Join(", ", names.Select(n => $"'{n}'"))}) but is also referenced as a link target " +
                        $"in '{descriptor.Name}.{link.Name}'. Multi-registered types cannot participate in structural " +
                        $"links. See #32 for a future relaxation path.");
                }

                // AONT041 extension: reject a link whose target type is registered with an
                // explicit (non-default) descriptor name. HasMany<TLinked> writes the CLR
                // simple name into TargetTypeName, so if the registered descriptor name
                // differs from the CLR simple name, reads and writes would diverge silently
                // across two table names. Enforcing single-registration with default name
                // is the Option X minimum-invasiveness fix (see #33 Finding 1).
                if (descriptorByDomainAndClrSimpleName.TryGetValue(
                        (descriptor.DomainName, link.TargetTypeName), out var targetDescriptor)
                    && targetDescriptor.Name != targetDescriptor.ClrType.Name)
                {
                    throw new OntologyCompositionException(
                        $"AONT041: Link '{descriptor.Name}.{link.Name}' targets CLR type " +
                        $"'{targetDescriptor.ClrType.FullName}' which is registered with explicit " +
                        $"descriptor name '{targetDescriptor.Name}' (default would be '{targetDescriptor.ClrType.Name}'). " +
                        $"A link target registered under a non-default name causes read/write table-name " +
                        $"divergence. Either remove the explicit name or use the default registration. " +
                        $"See #33 Finding 1.");
                }
            }

            // Check: does this descriptor's own CLR type have multiple registrations
            // AND declare outgoing links? The source side is just as invalid as the target side.
            if (descriptor.Links.Count > 0 && multiRegistered.TryGetValue(descriptor.ClrType, out var ownNames))
            {
                throw new OntologyCompositionException(
                    $"AONT041: CLR type '{descriptor.ClrType.FullName}' has multiple registrations " +
                    $"({string.Join(", ", ownNames.Select(n => $"'{n}'"))}) but also declares outgoing links " +
                    $"({string.Join(", ", descriptor.Links.Select(l => $"'{l.Name}'"))}). Multi-registered types cannot " +
                    $"participate in structural links. See #32 for a future relaxation path.");
            }
        }

        // Cross-domain link checks. Carries the source CLR type on the descriptor and
        // identifies the target by (TargetDomain, TargetTypeName). We deliberately reject
        // ANY cross-domain link whose source or target CLR type appears more than once
        // in the reverse index, even if the (sourceDomain, ClrType) lookup would itself
        // be unambiguous — Option X says multi-registered types are leaf-only, full stop.
        foreach (var (sourceDomain, link) in crossDomainLinks)
        {
            // Source side: descriptor.SourceType is the CLR type the From<T>() builder set.
            if (multiRegistered.TryGetValue(link.SourceType, out var srcNames))
            {
                throw new OntologyCompositionException(
                    $"AONT041: CLR type '{link.SourceType.FullName}' has multiple registrations " +
                    $"({string.Join(", ", srcNames.Select(n => $"'{n}'"))}) but is declared as the source " +
                    $"of cross-domain link '{link.Name}' in domain '{sourceDomain}'. Multi-registered " +
                    $"types cannot participate in structural links. See #32 for a future relaxation path.");
            }

            // Target side: resolve the target descriptor by (TargetDomain, TargetTypeName)
            // and check whether its CLR type is multi-registered. If the target is
            // unresolvable here, leave the diagnostic to ResolveCrossDomainLinks below.
            var targetDescriptor = allObjectTypes.FirstOrDefault(
                ot => ot.DomainName == link.TargetDomain && ot.Name == link.TargetTypeName);

            if (targetDescriptor is not null
                && multiRegistered.TryGetValue(targetDescriptor.ClrType, out var tgtNames))
            {
                throw new OntologyCompositionException(
                    $"AONT041: CLR type '{targetDescriptor.ClrType.FullName}' has multiple registrations " +
                    $"({string.Join(", ", tgtNames.Select(n => $"'{n}'"))}) but is the target of cross-domain link " +
                    $"'{link.Name}' from domain '{sourceDomain}' to '{link.TargetDomain}.{link.TargetTypeName}'. " +
                    $"Multi-registered types cannot participate in structural links. See #32 for a future relaxation path.");
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
        List<WorkflowMetadataBuilder> workflowMetadata,
        List<string> warnings)
    {
        var chains = new List<WorkflowChain>();

        // Workflow metadata uses unqualified type names; under the AONT040 invariant the
        // same simple name can legitimately appear in two domains. Build a multi-valued
        // index so we can detect ambiguity rather than silently first-wins-binding to
        // one descriptor and producing a wrong workflow chain.
        var objectTypesByName = allObjectTypes
            .GroupBy(ot => ot.Name)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Domain-keyed index for metadata that carries a DomainName via InDomain().
        // Keyed by (DomainName, ClrType.Name) so that Consumes<T>()/Produces<T>() — which
        // store typeof(T).Name — resolve to the right descriptor in the specified domain
        // even when the descriptor was registered with an explicit (non-CLR) name.
        // See #33 Finding 4.
        var objectTypesByDomainAndClrName = allObjectTypes
            .GroupBy(ot => (ot.DomainName, ot.ClrType.Name))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var metadata in workflowMetadata)
        {
            if (metadata.ConsumedTypeName is null || metadata.ProducedTypeName is null)
            {
                warnings.Add(
                    $"Workflow '{metadata.WorkflowName}' is missing consumed or produced type metadata; skipping.");
                continue;
            }

            ObjectTypeDescriptor consumedType;
            ObjectTypeDescriptor producedType;

            if (metadata.DomainName is not null)
            {
                if (!TryResolveDomainKeyed(
                        objectTypesByDomainAndClrName, metadata.DomainName, metadata.ConsumedTypeName,
                        metadata.WorkflowName, "consumed", warnings, out consumedType))
                {
                    continue;
                }

                if (!TryResolveDomainKeyed(
                        objectTypesByDomainAndClrName, metadata.DomainName, metadata.ProducedTypeName,
                        metadata.WorkflowName, "produced", warnings, out producedType))
                {
                    continue;
                }
            }
            else
            {
                if (!TryResolveUnambiguous(
                        objectTypesByName, metadata.ConsumedTypeName, metadata.WorkflowName, "consumed", warnings, out consumedType))
                {
                    continue;
                }

                if (!TryResolveUnambiguous(
                        objectTypesByName, metadata.ProducedTypeName, metadata.WorkflowName, "produced", warnings, out producedType))
                {
                    continue;
                }
            }

            chains.Add(new WorkflowChain(metadata.WorkflowName, consumedType, producedType));
        }

        return chains;
    }

    private static bool TryResolveDomainKeyed(
        Dictionary<(string DomainName, string ClrName), ObjectTypeDescriptor> objectTypesByDomainAndClrName,
        string domainName,
        string typeName,
        string workflowName,
        string role,
        List<string> warnings,
        out ObjectTypeDescriptor resolved)
    {
        resolved = null!;

        if (!objectTypesByDomainAndClrName.TryGetValue((domainName, typeName), out resolved!))
        {
            warnings.Add(
                $"Workflow '{workflowName}' references unknown {role} type '{typeName}' in domain '{domainName}'; skipping.");
            return false;
        }

        return true;
    }

    private static bool TryResolveUnambiguous(
        Dictionary<string, List<ObjectTypeDescriptor>> objectTypesByName,
        string typeName,
        string workflowName,
        string role,
        List<string> warnings,
        out ObjectTypeDescriptor resolved)
    {
        resolved = null!;

        if (!objectTypesByName.TryGetValue(typeName, out var matches) || matches.Count == 0)
        {
            warnings.Add(
                $"Workflow '{workflowName}' references unknown {role} type '{typeName}'; skipping.");
            return false;
        }

        if (matches.Count > 1)
        {
            var domains = string.Join(", ", matches.Select(m => $"'{m.DomainName}'"));
            warnings.Add(
                $"Workflow '{workflowName}' references {role} type '{typeName}' which is ambiguous across domains ({domains}); skipping.");
            return false;
        }

        resolved = matches[0];
        return true;
    }
}
