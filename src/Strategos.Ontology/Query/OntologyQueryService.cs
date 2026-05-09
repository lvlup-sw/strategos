using System.Text.RegularExpressions;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Query;

internal sealed class OntologyQueryService : IOntologyQuery
{
    private readonly OntologyGraph graph;
    private readonly IObjectSetProvider? _objectSetProvider;
    private readonly IActionDispatcher? _actionDispatcher;
    private readonly IEventStreamProvider? _eventStreamProvider;
    private readonly IReadOnlyList<IPatternDetector> _patternDetectors;

    /// <summary>
    /// Creates an <see cref="OntologyQueryService"/> for read-only graph queries.
    /// <see cref="GetObjectSet{T}"/> will throw <see cref="InvalidOperationException"/>
    /// because no provider/dispatcher/event stream were supplied.
    /// </summary>
    public OntologyQueryService(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        this.graph = graph;
        _patternDetectors = BuildPatternDetectors();
    }

    /// <summary>
    /// Creates an <see cref="OntologyQueryService"/> with full object-set materialization
    /// support. <see cref="GetObjectSet{T}"/> requires the provider/dispatcher/event stream
    /// dependencies, which are forwarded into <see cref="ObjectSet{T}"/>.
    /// </summary>
    public OntologyQueryService(
        OntologyGraph graph,
        IObjectSetProvider objectSetProvider,
        IActionDispatcher actionDispatcher,
        IEventStreamProvider eventStreamProvider)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(objectSetProvider);
        ArgumentNullException.ThrowIfNull(actionDispatcher);
        ArgumentNullException.ThrowIfNull(eventStreamProvider);

        this.graph = graph;
        _objectSetProvider = objectSetProvider;
        _actionDispatcher = actionDispatcher;
        _eventStreamProvider = eventStreamProvider;
        _patternDetectors = BuildPatternDetectors();
    }

    // Detectors are registered internally so future v2 patterns can extend the
    // surface without changing the public IOntologyQuery contract.
    private static IReadOnlyList<IPatternDetector> BuildPatternDetectors() =>
        [
            new ComputedWritePatternDetector(),
            new MissingExtensionPointPatternDetector(),
            new MissingPreconditionPropertyPatternDetector(),
            new UnreachableInitialPatternDetector(),
        ];

    public ObjectSet<T> GetObjectSet<T>(string objectType) where T : class
    {
        ArgumentNullException.ThrowIfNull(objectType);
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            throw new KeyNotFoundException(
                $"Object type '{objectType}' is not registered in the ontology.");
        }

        if (_objectSetProvider is null || _actionDispatcher is null || _eventStreamProvider is null)
        {
            throw new InvalidOperationException(
                $"GetObjectSet<{typeof(T).Name}> requires {nameof(IObjectSetProvider)}, " +
                $"{nameof(IActionDispatcher)}, and {nameof(IEventStreamProvider)} to be supplied " +
                "when constructing the OntologyQueryService.");
        }

        // Thread the resolved descriptor name (ot.Name) — which may differ from
        // typeof(T).Name when the caller registered the type under an explicit
        // name via Object<T>(name, ...) — into the RootExpression so providers
        // dispatch against the correct descriptor partition.
        return new ObjectSet<T>(
            descriptorName: ot.Name,
            _objectSetProvider,
            _actionDispatcher,
            _eventStreamProvider);
    }

    public IReadOnlyList<string> GetObjectTypeNames<T>() where T : class
    {
        return graph.ObjectTypeNamesByType.TryGetValue(typeof(T), out var names)
            ? names
            : Array.Empty<string>();
    }

    public IReadOnlyList<ObjectTypeDescriptor> GetObjectTypes(
        string? domain = null,
        string? implementsInterface = null,
        bool includeSubtypes = false)
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

        var matched = result.ToList();

        if (includeSubtypes && matched.Count > 0)
        {
            var matchedNames = matched.Select(ot => ot.Name).ToHashSet();
            var subtypes = graph.ObjectTypes
                .Where(ot => ot.ParentTypeName is not null && matchedNames.Contains(ot.ParentTypeName))
                .Where(ot => !matchedNames.Contains(ot.Name));

            matched.AddRange(subtypes);
        }

        return matched.AsReadOnly();
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
            .Where(a => a.Preconditions.Count == 0 || a.Preconditions
                .Where(p => p.Strength == Descriptors.ConstraintStrength.Hard)
                .All(p => IsPreconditionSatisfiable(p, knownProperties)))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ActionConstraintReport> GetActionConstraintReport(
        string objectType,
        IReadOnlyDictionary<string, object?>? knownProperties = null)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return [];
        }

        var props = knownProperties ?? new Dictionary<string, object?>();
        var reports = new List<ActionConstraintReport>(ot.Actions.Count);

        foreach (var action in ot.Actions)
        {
            var constraints = EvaluateConstraints(action.Preconditions, props);
            var isAvailable = constraints
                .Where(c => c.Strength == Descriptors.ConstraintStrength.Hard)
                .All(c => c.IsSatisfied);

            reports.Add(new ActionConstraintReport(action, isAvailable, constraints));
        }

        return reports.AsReadOnly();
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

    public IReadOnlyList<LinkDescriptor> GetInverseLinks(string objectType, string linkName)
    {
        var ot = FindObjectType(objectType);
        if (ot is null)
        {
            return [];
        }

        var link = ot.Links.FirstOrDefault(l => l.Name == linkName);
        if (link?.InverseLinkName is null)
        {
            return [];
        }

        var targetOt = FindObjectType(link.TargetTypeName);
        if (targetOt is null)
        {
            return [];
        }

        var inverseLink = targetOt.Links.FirstOrDefault(l => l.Name == link.InverseLinkName);
        if (inverseLink is null)
        {
            return [];
        }

        return [inverseLink];
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

    public BlastRadius EstimateBlastRadius(
        IReadOnlyList<OntologyNodeRef> touchedNodes,
        BlastRadiusOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(touchedNodes);

        if (touchedNodes.Count == 0)
        {
            return new BlastRadius([], [], [], BlastRadiusScope.Local);
        }

        var opts = options ?? new BlastRadiusOptions();

        var directlyAffected = touchedNodes.ToArray();
        var seen = new HashSet<OntologyNodeRef>(directlyAffected);
        var frontier = new Queue<(OntologyNodeRef Node, int Depth)>();
        foreach (var node in directlyAffected)
        {
            frontier.Enqueue((node, 0));
        }

        var transitivelyAffected = new List<OntologyNodeRef>();
        var crossHops = new List<CrossDomainHop>();

        while (frontier.Count > 0)
        {
            var (current, depth) = frontier.Dequeue();
            if (depth >= opts.MaxExpansionDegree)
            {
                continue;
            }

            foreach (var (neighbor, hop) in ExpandNeighbors(current))
            {
                if (!seen.Add(neighbor))
                {
                    continue;
                }

                transitivelyAffected.Add(neighbor);
                if (hop is not null)
                {
                    crossHops.Add(hop);
                }

                frontier.Enqueue((neighbor, depth + 1));
            }
        }

        var directlyOrdered = directlyAffected
            .OrderBy(n => n.Domain, StringComparer.Ordinal)
            .ThenBy(n => n.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(n => n.Key ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var transitivelyOrdered = transitivelyAffected
            .OrderBy(n => n.Domain, StringComparer.Ordinal)
            .ThenBy(n => n.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(n => n.Key ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        // Normalize crossHops too — leaving BFS discovery order makes the
        // result vary with seed ordering and graph registration order, which
        // breaks the determinism contract documented on EstimateBlastRadius.
        var crossHopsOrdered = crossHops
            .OrderBy(h => h.FromDomain, StringComparer.Ordinal)
            .ThenBy(h => h.ToDomain, StringComparer.Ordinal)
            .ThenBy(h => h.SourceNode.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(h => h.SourceNode.Key ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(h => h.TargetNode.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(h => h.TargetNode.Key ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var scope = ClassifyScope(directlyOrdered, transitivelyOrdered, crossHopsOrdered);
        return new BlastRadius(directlyOrdered, transitivelyOrdered, crossHopsOrdered, scope);
    }

    private IEnumerable<(OntologyNodeRef Neighbor, CrossDomainHop? Hop)> ExpandNeighbors(
        OntologyNodeRef current)
    {
        // Strict domain-qualified lookup only: a domain-agnostic fallback can
        // resolve to an object type from a different domain that happens to
        // share a simple name, corrupting blast-radius traversal across
        // multi-domain graphs.
        var ot = graph.GetObjectType(current.Domain, current.ObjectTypeName);

        if (ot is not null)
        {
            foreach (var link in ot.Links)
            {
                yield return (new OntologyNodeRef(ot.DomainName, link.TargetTypeName), null);
            }

            // Derivation edges (design §4.6 step 2): for each property on the
            // current type, every external derivation source is a neighbor.
            // Local sources stay within the current ObjectType so they don't
            // produce a node-level neighbor.
            foreach (var prop in ot.Properties)
            {
                var sources = prop.TransitiveDerivedFrom.Count > 0
                    ? prop.TransitiveDerivedFrom
                    : prop.DerivedFrom;

                foreach (var src in sources)
                {
                    if (src.Kind != DerivationSourceKind.External
                        || src.ExternalDomain is null
                        || src.ExternalObjectType is null)
                    {
                        continue;
                    }

                    var derivedNeighbor = new OntologyNodeRef(src.ExternalDomain, src.ExternalObjectType);
                    var hop = !string.Equals(src.ExternalDomain, current.Domain, StringComparison.Ordinal)
                        ? new CrossDomainHop(
                            FromDomain: current.Domain,
                            ToDomain: src.ExternalDomain,
                            SourceNode: new OntologyNodeRef(current.Domain, current.ObjectTypeName),
                            TargetNode: derivedNeighbor)
                        : null;
                    yield return (derivedNeighbor, hop);
                }
            }

            // Postcondition edges (design §4.6 step 3): each action's
            // postconditions that name a TargetTypeName produce an
            // intra-domain neighbor (cross-domain postcondition propagation
            // is already covered by the CrossDomainLinks pass below).
            foreach (var action in ot.Actions)
            {
                foreach (var post in action.Postconditions)
                {
                    if (string.IsNullOrEmpty(post.TargetTypeName))
                    {
                        continue;
                    }

                    yield return (
                        new OntologyNodeRef(ot.DomainName, post.TargetTypeName),
                        null);
                }
            }
        }

        foreach (var link in graph.CrossDomainLinks)
        {
            if (link.SourceDomain == current.Domain
                && link.SourceObjectType.Name == current.ObjectTypeName)
            {
                var target = new OntologyNodeRef(link.TargetDomain, link.TargetObjectType.Name);
                yield return (target, new CrossDomainHop(
                    FromDomain: link.SourceDomain,
                    ToDomain: link.TargetDomain,
                    SourceNode: new OntologyNodeRef(link.SourceDomain, link.SourceObjectType.Name),
                    TargetNode: target));
            }

            if (link.TargetDomain == current.Domain
                && link.TargetObjectType.Name == current.ObjectTypeName)
            {
                var source = new OntologyNodeRef(link.SourceDomain, link.SourceObjectType.Name);
                yield return (source, new CrossDomainHop(
                    FromDomain: link.TargetDomain,
                    ToDomain: link.SourceDomain,
                    SourceNode: new OntologyNodeRef(link.TargetDomain, link.TargetObjectType.Name),
                    TargetNode: source));
            }
        }
    }

    public IReadOnlyList<PatternViolation> DetectPatternViolations(
        IReadOnlyList<OntologyNodeRef> affectedNodes,
        DesignIntent intent)
    {
        ArgumentNullException.ThrowIfNull(affectedNodes);
        ArgumentNullException.ThrowIfNull(intent);

        var collected = new List<PatternViolation>();
        foreach (var detector in _patternDetectors)
        {
            collected.AddRange(detector.Detect(graph, intent, affectedNodes));
        }

        return collected
            .OrderBy(v => v.PatternName, StringComparer.Ordinal)
            .ThenBy(v => v.Subject.Domain, StringComparer.Ordinal)
            .ThenBy(v => v.Subject.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(v => v.Subject.Key ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    // Classification follows plan §C1 test expectations: a single-seed/single-domain
    // call is "Local" even when expansion reaches a 2nd type (test 1), but a multi-type
    // SEED set in one domain is "Domain" (test 2). Cross-domain and Global thresholds
    // include all affected nodes plus crossHop endpoints to capture the full reach.
    private static BlastRadiusScope ClassifyScope(
        IReadOnlyList<OntologyNodeRef> directlyAffected,
        IReadOnlyList<OntologyNodeRef> transitivelyAffected,
        IReadOnlyList<CrossDomainHop> crossHops)
    {
        var seedDomains = directlyAffected.Select(n => n.Domain).Distinct().Count();
        var seedTypes = directlyAffected.Select(n => n.ObjectTypeName).Distinct().Count();

        if (crossHops.Count > 0)
        {
            var allDomains = directlyAffected.Select(n => n.Domain)
                .Concat(transitivelyAffected.Select(n => n.Domain))
                .Concat(crossHops.Select(h => h.FromDomain))
                .Concat(crossHops.Select(h => h.ToDomain))
                .Distinct()
                .Count();

            return allDomains > 3 ? BlastRadiusScope.Global : BlastRadiusScope.CrossDomain;
        }

        if (seedDomains == 1 && seedTypes == 1)
        {
            return BlastRadiusScope.Local;
        }

        return BlastRadiusScope.Domain;
    }

    private ObjectTypeDescriptor? FindObjectType(string objectType) =>
        graph.ObjectTypes.FirstOrDefault(ot => ot.Name == objectType);

    private static IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(
        IReadOnlyList<ActionPrecondition> preconditions,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        if (preconditions.Count == 0)
        {
            return [];
        }

        var evaluations = new List<ConstraintEvaluation>(preconditions.Count);

        foreach (var precondition in preconditions)
        {
            var isSatisfied = IsPreconditionSatisfiable(precondition, knownProperties);
            string? failureReason = null;
            IReadOnlyDictionary<string, object?>? expectedShape = null;

            if (!isSatisfied)
            {
                failureReason = BuildFailureReason(precondition, knownProperties);
                expectedShape = BuildExpectedShape(precondition);
            }

            evaluations.Add(new ConstraintEvaluation(
                precondition,
                isSatisfied,
                precondition.Strength,
                failureReason,
                expectedShape));
        }

        return evaluations.AsReadOnly();
    }

    private static string BuildFailureReason(
        ActionPrecondition precondition,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        return precondition.Kind switch
        {
            PreconditionKind.LinkExists => BuildLinkFailureReason(precondition, knownProperties),
            PreconditionKind.PropertyPredicate => BuildPropertyFailureReason(precondition, knownProperties),
            _ => $"Custom precondition not satisfied: {precondition.Description}",
        };
    }

    private static string BuildLinkFailureReason(
        ActionPrecondition precondition,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        if (precondition.LinkName is null)
        {
            return "Link precondition has no link name specified";
        }

        if (!knownProperties.TryGetValue(precondition.LinkName, out var value))
        {
            return $"Link '{precondition.LinkName}' is not present in known properties";
        }

        return $"Link '{precondition.LinkName}' has value '{value}' but requires a truthy value";
    }

    private static string BuildPropertyFailureReason(
        ActionPrecondition precondition,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        var expression = precondition.Expression;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return $"Property precondition not satisfied: {precondition.Description}";
        }

        var parsed = TryParseComparison(expression);
        if (parsed is null)
        {
            return $"Property precondition not satisfied: {precondition.Description}";
        }

        var (propertyName, op, rightSide) = parsed.Value;

        if (!knownProperties.TryGetValue(propertyName, out var knownValue) || knownValue is null)
        {
            return $"Property '{propertyName}' is not known but requires '{op} {rightSide}'";
        }

        return $"Property '{propertyName}' has value '{knownValue}' but requires '{op} {rightSide}'";
    }

    private static IReadOnlyDictionary<string, object?>? BuildExpectedShape(
        ActionPrecondition precondition)
    {
        if (precondition.Kind == PreconditionKind.LinkExists)
        {
            if (precondition.LinkName is null)
            {
                return null;
            }

            return new Dictionary<string, object?> { [precondition.LinkName] = true };
        }

        if (precondition.Kind != PreconditionKind.PropertyPredicate)
        {
            return null;
        }

        var parsed = TryParseComparison(precondition.Expression);
        if (parsed is null)
        {
            return null;
        }

        var (propertyName, op, rightSide) = parsed.Value;
        return new Dictionary<string, object?> { [propertyName] = $"{op} {rightSide}" };
    }

    private static (string PropertyName, string Op, string RightSide)? TryParseComparison(
        string expression)
    {
        var convertMatch = ConvertPropertyPattern.Match(expression);
        if (convertMatch.Success)
        {
            return (convertMatch.Groups[2].Value, convertMatch.Groups[3].Value, convertMatch.Groups[4].Value);
        }

        var simpleMatch = SimplePropertyPattern.Match(expression);
        if (simpleMatch.Success)
        {
            return (simpleMatch.Groups[2].Value, simpleMatch.Groups[3].Value, simpleMatch.Groups[4].Value);
        }

        return null;
    }

    private static bool IsPreconditionSatisfiable(
        ActionPrecondition precondition,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        return precondition.Kind switch
        {
            PreconditionKind.LinkExists => IsLinkSatisfiable(precondition, knownProperties),
            PreconditionKind.PropertyPredicate => IsPropertyPredicateSatisfiable(precondition, knownProperties),
            _ => true, // Custom or unknown kinds are optimistically satisfiable
        };
    }

    private static bool IsLinkSatisfiable(
        ActionPrecondition precondition,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        if (precondition.LinkName is null)
        {
            return true;
        }

        if (!knownProperties.TryGetValue(precondition.LinkName, out var value))
        {
            return false;
        }

        return value is true or (not null and not false);
    }

    private static bool IsPropertyPredicateSatisfiable(
        ActionPrecondition precondition,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        var expression = precondition.Expression;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        // Try to evaluate simple binary comparisons from expression tree ToString() output.
        // Expression format examples:
        //   (p.Quantity > 0)
        //   (Convert(p.Status, Int32) == 1)
        return TryEvaluateSimpleComparison(expression, knownProperties) ?? true;
    }

    private static readonly Regex SimplePropertyPattern = new(
        @"\((\w+)\.(\w+)\s*(>=|<=|==|!=|>|<)\s*(.+?)\)",
        RegexOptions.Compiled);

    private static readonly Regex ConvertPropertyPattern = new(
        @"\(Convert\((\w+)\.(\w+),\s*\w+\)\s*(>=|<=|==|!=|>|<)\s*(.+?)\)",
        RegexOptions.Compiled);

    private static bool? TryEvaluateSimpleComparison(
        string expression,
        IReadOnlyDictionary<string, object?> knownProperties)
    {
        var parsed = TryParseComparison(expression);
        if (parsed is null)
        {
            return null; // Can't parse — optimistically satisfiable
        }

        var (propertyName, op, rightSide) = parsed.Value;

        if (!knownProperties.TryGetValue(propertyName, out var knownValue) || knownValue is null)
        {
            return null; // Property not known — optimistically satisfiable
        }

        return EvaluateComparison(knownValue, op, rightSide);
    }

    private static bool? EvaluateComparison(object knownValue, string op, string rightSide)
    {
        // Convert both sides to comparable decimals for numeric comparison
        if (TryConvertToDecimal(knownValue, out var leftNum) &&
            decimal.TryParse(rightSide, out var rightNum))
        {
            return op switch
            {
                "==" => leftNum == rightNum,
                "!=" => leftNum != rightNum,
                ">" => leftNum > rightNum,
                "<" => leftNum < rightNum,
                ">=" => leftNum >= rightNum,
                "<=" => leftNum <= rightNum,
                _ => null,
            };
        }

        // For enum types, convert to int and compare
        if (knownValue is Enum enumValue && int.TryParse(rightSide, out var rightInt))
        {
            var leftInt = Convert.ToInt32(enumValue);
            return op switch
            {
                "==" => leftInt == rightInt,
                "!=" => leftInt != rightInt,
                _ => null,
            };
        }

        // String equality comparison
        var leftStr = knownValue.ToString() ?? string.Empty;
        return op switch
        {
            "==" => string.Equals(leftStr, rightSide, StringComparison.Ordinal),
            "!=" => !string.Equals(leftStr, rightSide, StringComparison.Ordinal),
            _ => null,
        };
    }

    private static bool TryConvertToDecimal(object value, out decimal result)
    {
        if (value is byte or sbyte or short or ushort or int or uint
            or long or ulong or float or double or decimal)
        {
            result = Convert.ToDecimal(value);
            return true;
        }

        result = 0;
        return false;
    }

    private interface IPatternDetector
    {
        IEnumerable<PatternViolation> Detect(
            OntologyGraph graph,
            DesignIntent intent,
            IReadOnlyList<OntologyNodeRef> affectedNodes);
    }

    // Computed.Write — Error. AONT023 catches the build-time conflict; this is
    // runtime defense-in-depth for ProposedActions whose Arguments name a
    // computed property on the subject's ObjectType.
    private sealed class ComputedWritePatternDetector : IPatternDetector
    {
        public IEnumerable<PatternViolation> Detect(
            OntologyGraph graph,
            DesignIntent intent,
            IReadOnlyList<OntologyNodeRef> affectedNodes)
        {
            foreach (var action in intent.Actions)
            {
                if (action.Arguments is null || action.Arguments.Count == 0)
                {
                    continue;
                }

                var ot = ResolveSubject(graph, action.Subject);
                if (ot is null)
                {
                    continue;
                }

                foreach (var argName in action.Arguments.Keys)
                {
                    var prop = ot.Properties.FirstOrDefault(p =>
                        string.Equals(p.Name, argName, StringComparison.Ordinal));

                    if (prop is { IsComputed: true })
                    {
                        yield return new PatternViolation(
                            PatternName: "Computed.Write",
                            Description: $"Action '{action.ActionName}' on '{ot.Name}' attempts to write computed property '{prop.Name}'.",
                            Subject: action.Subject,
                            Severity: ViolationSeverity.Error);
                    }
                }
            }
        }
    }

    // Link.MissingExtensionPoint — Error. A ProposedAction whose underlying
    // ActionDescriptor declares a CreatesLink postcondition pointing at a
    // target ObjectType which advertises ExternalLinkExtensionPoints requiring
    // a source interface that the action's Subject does not implement.
    private sealed class MissingExtensionPointPatternDetector : IPatternDetector
    {
        public IEnumerable<PatternViolation> Detect(
            OntologyGraph graph,
            DesignIntent intent,
            IReadOnlyList<OntologyNodeRef> affectedNodes)
        {
            foreach (var action in intent.Actions)
            {
                var ot = ResolveSubject(graph, action.Subject);
                if (ot is null)
                {
                    continue;
                }

                var descriptor = ot.Actions.FirstOrDefault(a =>
                    string.Equals(a.Name, action.ActionName, StringComparison.Ordinal));

                if (descriptor is null)
                {
                    continue;
                }

                foreach (var post in descriptor.Postconditions)
                {
                    if (post.Kind != PostconditionKind.CreatesLink || post.LinkName is null)
                    {
                        continue;
                    }

                    var link = ot.Links.FirstOrDefault(l =>
                        string.Equals(l.Name, post.LinkName, StringComparison.Ordinal));

                    var targetTypeName = link?.TargetTypeName ?? post.TargetTypeName;
                    if (targetTypeName is null)
                    {
                        continue;
                    }

                    var target = graph.ObjectTypes.FirstOrDefault(t =>
                        string.Equals(t.Name, targetTypeName, StringComparison.Ordinal));

                    if (target is null || target.ExternalLinkExtensionPoints.Count == 0)
                    {
                        continue;
                    }

                    var sourceInterfaces = ot.ImplementedInterfaces
                        .Select(i => i.Name)
                        .ToHashSet(StringComparer.Ordinal);

                    var matches = target.ExternalLinkExtensionPoints.Any(ep =>
                        ep.RequiredSourceInterface is null
                        || sourceInterfaces.Contains(ep.RequiredSourceInterface));

                    if (!matches)
                    {
                        yield return new PatternViolation(
                            PatternName: "Link.MissingExtensionPoint",
                            Description: $"Action '{action.ActionName}' creates link '{post.LinkName}' to '{target.Name}' but source '{ot.Name}' does not satisfy any ExternalLinkExtensionPoint.",
                            Subject: action.Subject,
                            Severity: ViolationSeverity.Error);
                    }
                }
            }
        }
    }

    // Action.PreconditionPropertyMissing — Error. The action descriptor's
    // precondition Expression names a property that is not on AcceptsType.
    private sealed class MissingPreconditionPropertyPatternDetector : IPatternDetector
    {
        // Reflection on AcceptsType is bounded to public properties of a user-supplied
        // type. AOT consumers register their AcceptsType via the DSL builder, so the
        // type's properties are always preserved as part of the user's compiled code.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
            "Trimming",
            "IL2075:DynamicallyAccessedMembers",
            Justification = "AcceptsType is user-registered and always present in the trimmed graph.")]
        public IEnumerable<PatternViolation> Detect(
            OntologyGraph graph,
            DesignIntent intent,
            IReadOnlyList<OntologyNodeRef> affectedNodes)
        {
            foreach (var action in intent.Actions)
            {
                var ot = ResolveSubject(graph, action.Subject);
                if (ot is null)
                {
                    continue;
                }

                var descriptor = ot.Actions.FirstOrDefault(a =>
                    string.Equals(a.Name, action.ActionName, StringComparison.Ordinal));

                if (descriptor is null || descriptor.AcceptsType is null)
                {
                    continue;
                }

                var acceptsProps = descriptor.AcceptsType
                    .GetProperties()
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var pre in descriptor.Preconditions)
                {
                    var propName = ExtractPropertyName(pre);
                    if (propName is null)
                    {
                        continue;
                    }

                    if (!acceptsProps.Contains(propName))
                    {
                        yield return new PatternViolation(
                            PatternName: "Action.PreconditionPropertyMissing",
                            Description: $"Action '{action.ActionName}' precondition references property '{propName}' which is not present on AcceptsType '{descriptor.AcceptsType.Name}'.",
                            Subject: action.Subject,
                            Severity: ViolationSeverity.Error);
                    }
                }
            }
        }

        private static string? ExtractPropertyName(ActionPrecondition pre)
        {
            if (pre.Kind == PreconditionKind.LinkExists)
            {
                return pre.LinkName;
            }

            if (pre.Kind != PreconditionKind.PropertyPredicate)
            {
                return null;
            }

            var parsed = TryParseComparison(pre.Expression);
            return parsed?.PropertyName;
        }
    }

    // Lifecycle.UnreachableInitial — Warning. The descriptor declares an
    // Initial state but no transition produces it. Severity is Warning rather
    // than Error because an unreachable Initial may be intentional (e.g. seed
    // state for newly constructed entities) — the agent should be warned but
    // not blocked.
    private sealed class UnreachableInitialPatternDetector : IPatternDetector
    {
        public IEnumerable<PatternViolation> Detect(
            OntologyGraph graph,
            DesignIntent intent,
            IReadOnlyList<OntologyNodeRef> affectedNodes)
        {
            // Scope to the affected node set so we don't surface lifecycle
            // violations for object types that are unrelated to the design
            // intent being validated. Other detectors (ComputedWrite,
            // MissingExtensionPoint, MissingPrecondition) follow the same
            // contract.
            var inScope = new HashSet<(string Domain, string Name)>(affectedNodes
                .Select(n => (n.Domain, n.ObjectTypeName)));

            foreach (var ot in graph.ObjectTypes)
            {
                if (!inScope.Contains((ot.DomainName, ot.Name)))
                {
                    continue;
                }

                if (ot.Lifecycle is null)
                {
                    continue;
                }

                var initial = ot.Lifecycle.States.FirstOrDefault(s => s.IsInitial);
                if (initial is null)
                {
                    continue;
                }

                var hasIncoming = ot.Lifecycle.Transitions.Any(t =>
                    string.Equals(t.ToState, initial.Name, StringComparison.Ordinal));

                if (!hasIncoming)
                {
                    yield return new PatternViolation(
                        PatternName: "Lifecycle.UnreachableInitial",
                        Description: $"Lifecycle '{ot.Name}' declares initial state '{initial.Name}' but no transition produces it.",
                        Subject: new OntologyNodeRef(ot.DomainName, ot.Name),
                        Severity: ViolationSeverity.Warning);
                }
            }
        }
    }

    private static ObjectTypeDescriptor? ResolveSubject(OntologyGraph graph, OntologyNodeRef subject)
        // Strict domain-qualified lookup only. A simple-name fallback would
        // reintroduce the same multi-domain ambiguity that was removed from
        // ExpandNeighbors — running detectors against the wrong descriptor
        // is worse than returning null and skipping the subject.
        => graph.GetObjectType(subject.Domain, subject.ObjectTypeName);
}
