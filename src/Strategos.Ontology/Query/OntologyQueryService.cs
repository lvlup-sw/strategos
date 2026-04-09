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

    /// <summary>
    /// Creates an <see cref="OntologyQueryService"/> for read-only graph queries.
    /// <see cref="GetObjectSet{T}"/> will throw <see cref="InvalidOperationException"/>
    /// because no provider/dispatcher/event stream were supplied.
    /// </summary>
    public OntologyQueryService(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        this.graph = graph;
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
    }

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
}
