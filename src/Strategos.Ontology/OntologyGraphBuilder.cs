using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Extensions;

namespace Strategos.Ontology;

public sealed class OntologyGraphBuilder
{
    private readonly List<DomainOntology> _domainOntologies = [];
    private readonly List<WorkflowMetadataBuilder> _workflowMetadata = [];
    private readonly List<IOntologySource> _sources = [];

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

    /// <summary>
    /// DR-3 (Task 13): registers <see cref="IOntologySource"/> instances
    /// to be drained at <see cref="Build"/> time. Each source's
    /// <c>LoadAsync</c> is iterated and emitted deltas applied to the
    /// matching per-domain builder. Sources contribute before composition
    /// so all subsequent validation observes a unified descriptor set.
    /// </summary>
    public OntologyGraphBuilder AddSources(IEnumerable<IOntologySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources.AddRange(sources);
        return this;
    }

    public OntologyGraph Build()
    {
        var domains = new List<DomainDescriptor>();
        var allObjectTypes = new List<ObjectTypeDescriptor>();
        var allInterfaces = new List<InterfaceDescriptor>();
        var allCrossDomainLinkDescriptors = new List<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)>();

        // Per-domain builders are kept so source-emitted deltas land in
        // the same OntologyBuilder that the hand-authored Define() pass
        // populated. New domains contributed exclusively by sources get
        // their own builder created on demand below.
        var buildersByDomain = new Dictionary<string, OntologyBuilder>();

        foreach (var domainOntology in _domainOntologies)
        {
            var ontologyBuilder = new OntologyBuilder(domainOntology.DomainName);
            domainOntology.Build(ontologyBuilder);
            buildersByDomain[domainOntology.DomainName] = ontologyBuilder;
        }

        // DR-3 (Task 13): drain registered IOntologySource instances and
        // apply their deltas via the existing per-domain OntologyBuilder.
        // Runs after the hand-authored pass so hand descriptors are in
        // place when ingested deltas reference them (e.g. AddProperty
        // against a hand-defined ObjectType). Sources whose deltas
        // reference a domain unseen by the hand pass cause a new
        // per-domain builder to be created on demand.
        // DR-10 (Task 17): wrap each source's drain in try/catch so the
        // SourceId is surfaced in any propagated composition exception.
        DrainSources(buildersByDomain);

        foreach (var domainOntology in _domainOntologies)
        {
            var ontologyBuilder = buildersByDomain[domainOntology.DomainName];

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

        // Source-only domains (no DomainOntology subclass): fold into the
        // top-level lists so AONT040 et al. see their descriptors.
        foreach (var (domainName, ontologyBuilder) in buildersByDomain)
        {
            if (_domainOntologies.Any(d => d.DomainName == domainName))
            {
                continue; // already handled in the hand-authored loop above
            }

            var domainDescriptor = new DomainDescriptor(domainName)
            {
                ObjectTypes = ontologyBuilder.ObjectTypes.ToArray(),
            };

            domains.Add(domainDescriptor);
            allObjectTypes.AddRange(ontologyBuilder.ObjectTypes);
            allInterfaces.AddRange(ontologyBuilder.Interfaces);

            foreach (var crossDomainLink in ontologyBuilder.CrossDomainLinks)
            {
                allCrossDomainLinkDescriptors.Add((domainName, crossDomainLink));
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
                        $"First registration: CLR type '{existing.ClrType?.FullName ?? existing.SymbolKey ?? "<unknown>"}'. " +
                        $"Second registration: CLR type '{descriptor.ClrType?.FullName ?? descriptor.SymbolKey ?? "<unknown>"}'. " +
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
        // DR-1 polyglot: descriptors with null ClrType (ingested-only) are excluded
        // from this CLR-keyed index; the (DomainName, Name)-keyed retarget arrives
        // with DR-8 (AONT041 retarget) in a later task.
        var namesByType = allObjectTypes
            .Where(ot => ot.ClrType is not null)
            .GroupBy(ot => ot.ClrType!)
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
    /// Enforces the Option X freeze invariant that a descriptor identity (CLR type or
    /// SCIP SymbolKey) registered under more than one descriptor name cannot participate
    /// in structural links — neither as a link target nor as a link source, and neither
    /// in intra-domain <see cref="LinkDescriptor"/>s nor in <see cref="CrossDomainLinkDescriptor"/>s.
    /// The Basileus happy path is a multi-registered leaf type with no links anywhere —
    /// that remains legal.
    /// </summary>
    /// <remarks>
    /// DR-8 (Task 31) retarget: the lookup is now polyglot-aware. The "identity" of a
    /// descriptor is its <see cref="ObjectTypeDescriptor.ClrType"/> when non-null, else
    /// its <see cref="ObjectTypeDescriptor.SymbolKey"/>. Link targets resolve by
    /// <c>(DomainName, Name)</c> directly — the previous CLR-simple-name pre-filter
    /// produced false negatives for ingested-only descriptors. Cross-domain links keep
    /// their CLR source-type wiring because the From&lt;T&gt;() builder requires a CLR
    /// type, but the target-side scan is now polyglot.
    /// </remarks>
    private static void ValidateMultiRegisteredTypesNotInLinks(
        IReadOnlyList<ObjectTypeDescriptor> allObjectTypes,
        IReadOnlyList<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)> crossDomainLinks,
        IReadOnlyDictionary<Type, IReadOnlyList<string>> namesByType)
    {
        // Polyglot identity reverse index: identity (ClrType-or-SymbolKey, as object) →
        // list of (DomainName, Name) registrations. A descriptor is "multi-registered"
        // when its identity bucket holds more than one entry. Ingested-only descriptors
        // (ClrType == null) participate via SymbolKey identity; descriptors with neither
        // a ClrType nor a SymbolKey are skipped — there is nothing to key against.
        var identityByDescriptor = new Dictionary<(string DomainName, string Name), object>();
        var registrationsByIdentity = new Dictionary<object, List<(string DomainName, string Name)>>();
        foreach (var ot in allObjectTypes)
        {
            object? identity = ot.ClrType is not null ? ot.ClrType : ot.SymbolKey;
            if (identity is null)
            {
                continue;
            }

            identityByDescriptor[(ot.DomainName, ot.Name)] = identity;
            if (!registrationsByIdentity.TryGetValue(identity, out var list))
            {
                list = new List<(string DomainName, string Name)>();
                registrationsByIdentity[identity] = list;
            }

            list.Add((ot.DomainName, ot.Name));
        }

        // Direct (DomainName, Name) → descriptor index. Replaces the prior
        // (DomainName, ClrSimpleName) map and covers polyglot descriptors. Includes
        // every descriptor regardless of whether ClrType is set.
        var descriptorByDomainAndName = allObjectTypes
            .GroupBy(ot => (ot.DomainName, ot.Name))
            .ToDictionary(g => g.Key, g => g.First());

        // CLR-simple-name fallback: HasMany<TLinked>(name) writes typeof(TLinked).Name
        // into LinkDescriptor.TargetTypeName, which need not match any registered
        // descriptor name when the underlying type was registered under one or more
        // explicit names (e.g., "orders" / "open_orders" for TradeOrder). To preserve
        // the legacy multi-registered-CLR-type-as-link-target diagnostic we keep a
        // sidecar index from CLR simple name → registered CLR type for types whose
        // identity bucket holds more than one (Domain, Name) registration.
        var multiRegisteredClrByName = namesByType
            .Where(kvp => kvp.Value.Count > 1)
            .GroupBy(kvp => kvp.Key.Name)
            .ToDictionary(g => g.Key, g => g.First().Key);

        // Sidecar index for the explicit-name sub-rule: a link whose TargetTypeName is a
        // CLR simple name that resolves to a descriptor registered under an *explicit*
        // (non-default) name. The descriptor identity is keyed under its registered name
        // in descriptorByDomainAndName above, but the link target — which carries the CLR
        // simple name from HasMany<TLinked> — needs (DomainName, ClrSimpleName) to find
        // it. Only descriptors with a non-null ClrType participate.
        var descriptorByDomainAndClrSimpleName = allObjectTypes
            .Where(ot => ot.ClrType is not null)
            .GroupBy(ot => (ot.DomainName, ot.ClrType!.Name))
            .ToDictionary(g => g.Key, g => g.First());

        bool IsMultiRegistered(string domain, string name)
        {
            return identityByDescriptor.TryGetValue((domain, name), out var identity)
                && registrationsByIdentity.TryGetValue(identity, out var regs)
                && regs.Count > 1;
        }

        IReadOnlyList<(string DomainName, string Name)> RegistrationsFor(string domain, string name)
        {
            if (identityByDescriptor.TryGetValue((domain, name), out var identity)
                && registrationsByIdentity.TryGetValue(identity, out var regs))
            {
                return regs;
            }

            return Array.Empty<(string, string)>();
        }

        foreach (var descriptor in allObjectTypes)
        {
            // Check: does this descriptor declare an outgoing link whose target resolves
            // (within the same domain) to a multi-registered identity? Intra-domain links
            // identify their target by simple name, so we resolve (descriptor.DomainName,
            // link.TargetTypeName) and consult the polyglot identity index.
            foreach (var link in descriptor.Links)
            {
                // CLR-simple-name fallback first: when no descriptor is registered under
                // link.TargetTypeName in this domain, but the CLR simple name matches a
                // multi-registered CLR type, the HasMany<TLinked>() target would resolve
                // ambiguously at runtime — surface AONT041 instead.
                if (!descriptorByDomainAndName.ContainsKey((descriptor.DomainName, link.TargetTypeName))
                    && multiRegisteredClrByName.TryGetValue(link.TargetTypeName, out var multiClrType))
                {
                    var names = namesByType[multiClrType];
                    throw new OntologyCompositionException(
                        $"AONT041: CLR type '{multiClrType.FullName}' has multiple registrations " +
                        $"({string.Join(", ", names.Select(n => $"'{n}'"))}) but is also referenced as a link target " +
                        $"in '{descriptor.Name}.{link.Name}'. Multi-registered types cannot participate in structural " +
                        $"links. See #32 for a future relaxation path.");
                }

                if (descriptorByDomainAndName.TryGetValue(
                        (descriptor.DomainName, link.TargetTypeName), out var targetDescriptor)
                    && IsMultiRegistered(targetDescriptor.DomainName, targetDescriptor.Name))
                {
                    var regs = RegistrationsFor(targetDescriptor.DomainName, targetDescriptor.Name);
                    var identityLabel = targetDescriptor.ClrType?.FullName
                        ?? $"SymbolKey '{targetDescriptor.SymbolKey}'";
                    throw new OntologyCompositionException(
                        $"AONT041: descriptor identity '{identityLabel}' has multiple registrations " +
                        $"({string.Join(", ", regs.Select(r => $"'{r.DomainName}.{r.Name}'"))}) but is also " +
                        $"referenced as a link target in '{descriptor.Name}.{link.Name}'. " +
                        $"Multi-registered types cannot participate in structural links. " +
                        $"See #32 for a future relaxation path.");
                }

                // AONT041 extension: reject a link whose target type is registered with an
                // explicit (non-default) descriptor name. HasMany<TLinked> writes the CLR
                // simple name into TargetTypeName, so if the registered descriptor name
                // differs from the CLR simple name, reads and writes would diverge silently
                // across two table names. Enforcing single-registration with default name
                // is the Option X minimum-invasiveness fix (see #33 Finding 1).
                // Polyglot note: only meaningful for CLR-typed descriptors — ingested-only
                // descriptors have no CLR-simple-name baseline to diverge from.
                // We consult the CLR-simple-name index here because link.TargetTypeName
                // is the CLR simple name; the explicit-name divergence shows up precisely
                // when that name does not match any registered descriptor by name.
                if (descriptorByDomainAndClrSimpleName.TryGetValue(
                        (descriptor.DomainName, link.TargetTypeName), out var explicitNameTarget)
                    && explicitNameTarget.ClrType is not null
                    && explicitNameTarget.Name != explicitNameTarget.ClrType.Name)
                {
                    throw new OntologyCompositionException(
                        $"AONT041: Link '{descriptor.Name}.{link.Name}' targets CLR type " +
                        $"'{explicitNameTarget.ClrType.FullName}' which is registered with explicit " +
                        $"descriptor name '{explicitNameTarget.Name}' (default would be '{explicitNameTarget.ClrType.Name}'). " +
                        $"A link target registered under a non-default name causes read/write table-name " +
                        $"divergence. Either remove the explicit name or use the default registration. " +
                        $"See #33 Finding 1.");
                }
            }

            // Check: does this descriptor's own identity have multiple registrations AND
            // declare outgoing links? The source side is just as invalid as the target side.
            // Polyglot: works for both CLR-typed and ingested-only descriptors.
            if (descriptor.Links.Count > 0
                && IsMultiRegistered(descriptor.DomainName, descriptor.Name))
            {
                var regs = RegistrationsFor(descriptor.DomainName, descriptor.Name);
                var identityLabel = descriptor.ClrType?.FullName
                    ?? $"SymbolKey '{descriptor.SymbolKey}'";
                throw new OntologyCompositionException(
                    $"AONT041: descriptor identity '{identityLabel}' has multiple registrations " +
                    $"({string.Join(", ", regs.Select(r => $"'{r.DomainName}.{r.Name}'"))}) but also " +
                    $"declares outgoing links ({string.Join(", ", descriptor.Links.Select(l => $"'{l.Name}'"))}). " +
                    $"Multi-registered types cannot participate in structural links. " +
                    $"See #32 for a future relaxation path.");
            }
        }

        // Cross-domain link checks. Carries the source CLR type on the descriptor and
        // identifies the target by (TargetDomain, TargetTypeName). We deliberately reject
        // ANY cross-domain link whose source or target identity appears more than once
        // in the polyglot reverse index, even if the (sourceDomain, ClrType) lookup would
        // itself be unambiguous — Option X says multi-registered types are leaf-only,
        // full stop.
        foreach (var (sourceDomain, link) in crossDomainLinks)
        {
            // Source side: link.SourceType is the CLR type the From<T>() builder set.
            // Cross-domain link sources are still strictly CLR-typed (the From<T>() API
            // requires it), so the source check stays CLR-keyed via namesByType.
            if (namesByType.TryGetValue(link.SourceType, out var srcNames) && srcNames.Count > 1)
            {
                throw new OntologyCompositionException(
                    $"AONT041: CLR type '{link.SourceType.FullName}' has multiple registrations " +
                    $"({string.Join(", ", srcNames.Select(n => $"'{n}'"))}) but is declared as the source " +
                    $"of cross-domain link '{link.Name}' in domain '{sourceDomain}'. Multi-registered " +
                    $"types cannot participate in structural links. See #32 for a future relaxation path.");
            }

            // Target side: resolve the target descriptor by (TargetDomain, TargetTypeName)
            // and check whether its identity (CLR or SymbolKey) is multi-registered. If
            // the target is unresolvable here, leave the diagnostic to
            // ResolveCrossDomainLinks below.
            var targetDescriptor = allObjectTypes.FirstOrDefault(
                ot => ot.DomainName == link.TargetDomain && ot.Name == link.TargetTypeName);

            if (targetDescriptor is not null
                && IsMultiRegistered(targetDescriptor.DomainName, targetDescriptor.Name))
            {
                var tgtRegs = RegistrationsFor(targetDescriptor.DomainName, targetDescriptor.Name);
                var identityLabel = targetDescriptor.ClrType?.FullName
                    ?? $"SymbolKey '{targetDescriptor.SymbolKey}'";
                throw new OntologyCompositionException(
                    $"AONT041: descriptor identity '{identityLabel}' has multiple registrations " +
                    $"({string.Join(", ", tgtRegs.Select(r => $"'{r.DomainName}.{r.Name}'"))}) but is the target of cross-domain link " +
                    $"'{link.Name}' from domain '{sourceDomain}' to '{link.TargetDomain}.{link.TargetTypeName}'. " +
                    $"Multi-registered types cannot participate in structural links. See #32 for a future relaxation path.");
            }
        }
    }

    private static void InferPropertyKinds(List<ObjectTypeDescriptor> allObjectTypes)
    {
        // DR-1 polyglot: ingested-only descriptors (null ClrType) don't participate
        // in CLR-keyed reference-property inference; they reach reference kinds via
        // PropertyDescriptor.ReferenceSymbolKey instead (DR-1, Task 4+).
        var registeredClrTypes = allObjectTypes
            .Select(ot => ot.ClrType)
            .Where(t => t is not null)
            .Select(t => t!)
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
        // See #33 Finding 4. DR-1 polyglot: ingested-only types skipped here.
        var objectTypesByDomainAndClrName = allObjectTypes
            .Where(ot => ot.ClrType is not null)
            .GroupBy(ot => (ot.DomainName, ot.ClrType!.Name))
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

    /// <summary>
    /// DR-3 + DR-10 (Tasks 13, 17): drains each registered
    /// <see cref="IOntologySource"/>'s <c>LoadAsync</c> and applies each
    /// emitted delta to the per-domain <see cref="OntologyBuilder"/>.
    /// Synchronous bridging of the async stream is intentional —
    /// <see cref="Build"/> is sync and Strategos 2.5.0 ships only the
    /// startup drain. Source-raised exceptions are wrapped in
    /// <see cref="OntologyCompositionException"/> with the offending
    /// <see cref="IOntologySource.SourceId"/> in the message so logs and
    /// incident reports attribute the failure to the right ingester.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "VSTHRD002:Synchronously waiting on tasks or awaiters",
        Justification =
            "Intentional sync bridge at the OntologyGraphBuilder startup boundary. " +
            "Build() is sync by design and Strategos 2.5.0 only drains LoadAsync " +
            "once at composition; the async surface exists for forward compatibility " +
            "(live invalidation lands in v2.6.0+ via SubscribeAsync). Task.Run + " +
            "ConfigureAwait(false) protects callers running under a captured " +
            "SynchronizationContext (e.g. WPF/UI).")]
    private void DrainSources(Dictionary<string, OntologyBuilder> buildersByDomain)
    {
        foreach (var source in _sources)
        {
            try
            {
                Task.Run(async () =>
                    await DrainSourceCoreAsync(source, buildersByDomain).ConfigureAwait(false))
                    .GetAwaiter().GetResult();
            }
            catch (OntologyCompositionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new OntologyCompositionException(
                    $"IOntologySource '{source.SourceId}' raised during LoadAsync: {ex.Message}",
                    ex);
            }
        }
    }

    private static async Task DrainSourceCoreAsync(
        IOntologySource source,
        Dictionary<string, OntologyBuilder> buildersByDomain)
    {
        await foreach (var delta in source.LoadAsync(CancellationToken.None))
        {
            var domainName = ResolveDeltaDomain(delta);
            if (domainName is null)
            {
                continue;
            }

            if (!buildersByDomain.TryGetValue(domainName, out var ontologyBuilder))
            {
                ontologyBuilder = new OntologyBuilder(domainName);
                buildersByDomain[domainName] = ontologyBuilder;
            }

            ontologyBuilder.ApplyDelta(delta);
        }
    }

    private static string? ResolveDeltaDomain(OntologyDelta delta) => delta switch
    {
        OntologyDelta.AddObjectType a => a.Descriptor.DomainName,
        OntologyDelta.UpdateObjectType u => u.Descriptor.DomainName,
        OntologyDelta.RemoveObjectType r => r.DomainName,
        OntologyDelta.AddProperty ap => ap.DomainName,
        OntologyDelta.RenameProperty rp => rp.DomainName,
        OntologyDelta.RemoveProperty rp => rp.DomainName,
        OntologyDelta.AddLink al => al.DomainName,
        OntologyDelta.RemoveLink rl => rl.DomainName,
        _ => null,
    };

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
