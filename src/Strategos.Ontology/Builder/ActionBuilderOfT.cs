using System.Linq.Expressions;
using System.Collections.Immutable;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class ActionBuilder<T>(string name) : IActionBuilder<T>
    where T : class
{
    private string _description = string.Empty;
    private Type? _acceptsType;
    private Type? _returnsType;
    private ActionBindingType _bindingType = ActionBindingType.Unbound;
    private string? _boundWorkflowName;
    private string? _boundToolName;
    private string? _boundToolMethod;
    private bool _isReadOnly;
    private bool _idempotent;
    private string? _requiredAuthority;
    private string? _compensatingActionName;
    private readonly HashSet<ActionResource> _touchedResources = [];
    private readonly List<ActionPrecondition> _preconditions = [];
    private readonly List<ActionPostcondition> _postconditions = [];
    private readonly List<string> _validFromStates = [];

    internal string Name => name;

    internal IReadOnlyList<string> ValidFromStates => _validFromStates;

    IActionBuilder IActionBuilder.Description(string description) => Description(description);
    IActionBuilder IActionBuilder.Accepts<TAccepts>() => Accepts<TAccepts>();
    IActionBuilder IActionBuilder.Returns<TReturns>() => Returns<TReturns>();
    IActionBuilder IActionBuilder.BoundToWorkflow(string workflowName) => BoundToWorkflow(workflowName);
    IActionBuilder IActionBuilder.BoundToTool(string toolName, string methodName) => BoundToTool(toolName, methodName);
    IActionBuilder IActionBuilder.ReadOnly() => ReadOnly();
    IActionBuilder IActionBuilder.Idempotent() => Idempotent();
    IActionBuilder IActionBuilder.RequiresAuthority(string authorityName) => RequiresAuthority(authorityName);
    IActionBuilder IActionBuilder.Touches(ActionResource resource) => Touches(resource);
    IActionBuilder IActionBuilder.CompensatedBy(string actionName) => CompensatedBy(actionName);

    public IActionBuilder<T> Description(string description)
    {
        _description = description;
        return this;
    }

    public IActionBuilder<T> Accepts<TAccepts>()
    {
        _acceptsType = typeof(TAccepts);
        return this;
    }

    public IActionBuilder<T> Returns<TReturns>()
    {
        _returnsType = typeof(TReturns);
        return this;
    }

    public IActionBuilder<T> BoundToWorkflow(string workflowName)
    {
        _bindingType = ActionBindingType.Workflow;
        _boundWorkflowName = workflowName;
        return this;
    }

    public IActionBuilder<T> BoundToTool(string toolName, string methodName)
    {
        _bindingType = ActionBindingType.Tool;
        _boundToolName = toolName;
        _boundToolMethod = methodName;
        return this;
    }

    public IActionBuilder<T> ReadOnly()
    {
        _isReadOnly = true;
        _idempotent = true;
        return this;
    }

    public IActionBuilder<T> Idempotent()
    {
        _idempotent = true;
        return this;
    }

    public IActionBuilder<T> RequiresAuthority(string authorityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityName);
        _requiredAuthority = authorityName;
        return this;
    }

    public IActionBuilder<T> Touches(ActionResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _touchedResources.Add(resource);
        return this;
    }

    public IActionBuilder<T> CompensatedBy(string actionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        _compensatingActionName = actionName;
        return this;
    }

    public IActionBuilder<T> BoundToTool<TTool>(Expression<Func<TTool, Delegate>> methodSelector)
    {
        var methodName = ExpressionHelper.ExtractMethodName(methodSelector);
        return BoundToTool(typeof(TTool).Name, methodName);
    }

    [Obsolete("Use ActionDescriptor.Preconditions to declare action preconditions. There is no fluent successor.")]
    public IActionBuilder<T> Requires(Expression<Func<T, bool>> predicate)
    {
        var expressionString = predicate.Body.ToString();
        var description = ExpressionHelper.ExtractPredicateString(predicate);

        _preconditions.Add(new ActionPrecondition
        {
            Expression = expressionString,
            Description = description,
            Kind = PreconditionKind.PropertyPredicate,
            Strength = ConstraintStrength.Hard,
        });
        return this;
    }

    public IActionBuilder<T> RequiresSoft(Expression<Func<T, bool>> predicate)
    {
        var expressionString = predicate.Body.ToString();
        var description = ExpressionHelper.ExtractPredicateString(predicate);

        _preconditions.Add(new ActionPrecondition
        {
            Expression = expressionString,
            Description = description,
            Kind = PreconditionKind.PropertyPredicate,
            Strength = ConstraintStrength.Soft,
        });
        return this;
    }

    public IActionBuilder<T> RequiresLink(string linkName)
    {
        _preconditions.Add(new ActionPrecondition
        {
            Expression = $"Link '{linkName}' exists",
            Description = $"Requires link '{linkName}' to have at least one target",
            Kind = PreconditionKind.LinkExists,
            LinkName = linkName,
            Strength = ConstraintStrength.Hard,
        });
        return this;
    }

    public IActionBuilder<T> RequiresLinkSoft(string linkName)
    {
        _preconditions.Add(new ActionPrecondition
        {
            Expression = $"Link '{linkName}' exists",
            Description = $"Prefers link '{linkName}' to have at least one target",
            Kind = PreconditionKind.LinkExists,
            LinkName = linkName,
            Strength = ConstraintStrength.Soft,
        });
        return this;
    }

    public IActionBuilder<T> RequiresRelation(string relationName, params string[] linkPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationName);
        ArgumentNullException.ThrowIfNull(linkPath);
        if (linkPath.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Relation paths cannot contain empty link names.", nameof(linkPath));
        }

        _preconditions.Add(new ActionPrecondition
        {
            Expression = BuildRelationExpression(relationName, linkPath),
            Description = $"Requires the caller to hold relation '{relationName}' via {FormatPath(linkPath)}",
            Kind = PreconditionKind.RelationHolds,
            RelationName = relationName,
            LinkPath = linkPath.ToImmutableArray(),
            Strength = ConstraintStrength.Hard,
        });
        return this;
    }

    public IActionBuilder<T> Modifies(Expression<Func<T, object>> propertySelector)
    {
        var memberName = ExpressionHelper.ExtractMemberName(propertySelector);
        _postconditions.Add(new ActionPostcondition
        {
            Kind = PostconditionKind.ModifiesProperty,
            PropertyName = memberName,
        });
        _touchedResources.Add(ActionResource.Property(memberName));
        return this;
    }

    public IActionBuilder<T> CreatesLinked<TTarget>(string linkName)
    {
        _postconditions.Add(new ActionPostcondition
        {
            Kind = PostconditionKind.CreatesLink,
            LinkName = linkName,
            TargetTypeName = typeof(TTarget).Name,
        });
        _touchedResources.Add(ActionResource.Link(linkName));
        return this;
    }

    public IActionBuilder<T> EmitsEvent<TEvent>()
    {
        _postconditions.Add(new ActionPostcondition
        {
            Kind = PostconditionKind.EmitsEvent,
            EventTypeName = typeof(TEvent).Name,
        });
        _touchedResources.Add(ActionResource.Event(typeof(TEvent).Name));
        return this;
    }

    public IActionBuilder<T> ValidFromState<TEnum>(TEnum state) where TEnum : struct, Enum
    {
        _validFromStates.Add(state.ToString());
        return this;
    }

    public ActionDescriptor Build() =>
        new(name, _description)
        {
            AcceptsType = _acceptsType,
            ReturnsType = _returnsType,
            BindingType = _bindingType,
            BoundWorkflowName = _boundWorkflowName,
            BoundToolName = _boundToolName,
            BoundToolMethod = _boundToolMethod,
            IsReadOnly = _isReadOnly,
            Idempotent = _idempotent,
            RequiredAuthority = _requiredAuthority,
            TouchedResources = _touchedResources.ToArray(),
            CompensatingActionName = _compensatingActionName,
            Preconditions = _preconditions.ToList().AsReadOnly(),
            Postconditions = _postconditions.ToList().AsReadOnly(),
        };

    private static string BuildRelationExpression(string relationName, IReadOnlyList<string> linkPath) =>
        $"principal -[{relationName}]-> {FormatPath(linkPath)}";

    private static string FormatPath(IReadOnlyList<string> linkPath) =>
        linkPath.Count == 0 ? "target" : string.Join("/", linkPath);
}
