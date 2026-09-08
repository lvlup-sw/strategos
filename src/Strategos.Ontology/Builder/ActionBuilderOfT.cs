using System.Linq.Expressions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class ActionBuilder<T>(string name, ActionSubject subject) : IActionBuilder<T>
    where T : class
{
    private string _description = string.Empty;
    private Type? _acceptsType;
    private Type? _returnsType;
    private ActionBindingType _bindingType = ActionBindingType.Unbound;
    private WorkflowBindingReference? _boundWorkflow;
    private string? _boundToolName;
    private string? _boundToolMethod;
    private bool _isReadOnly;
    private bool _idempotent;
    private string? _requiredAuthority;
    private string? _compensatingActionName;
    private readonly HashSet<ActionResource> _touchedResources = [];
    private readonly List<ActionPrecondition> _preconditions = [];
    private readonly List<ActionGuarantee> _ensures = [];
    private readonly List<ActionPostcondition> _postconditions = [];
    private readonly List<string> _validFromStates = [];

    internal string Name => name;

    internal IReadOnlyList<string> ValidFromStates => _validFromStates;

    IActionBuilder IActionBuilder.Description(string description) => Description(description);
    IActionBuilder IActionBuilder.Accepts<TAccepts>() => Accepts<TAccepts>();
    IActionBuilder IActionBuilder.Returns<TReturns>() => Returns<TReturns>();
    IActionBuilder IActionBuilder.BoundToWorkflow(string workflowName) => BoundToWorkflow(workflowName);
    IActionBuilder IActionBuilder.BoundToWorkflow(WorkflowBindingReference workflow) => BoundToWorkflow(workflow);
    IActionBuilder IActionBuilder.BoundToTool(string toolName, string methodName) => BoundToTool(toolName, methodName);
    IActionBuilder IActionBuilder.ReadOnly() => ReadOnly();
    IActionBuilder IActionBuilder.Idempotent() => Idempotent();
    IActionBuilder IActionBuilder.RequiresAuthority(string authorityName) => RequiresAuthority(authorityName);
    IActionBuilder IActionBuilder.Touches(ActionResource resource) => Touches(resource);
    IActionBuilder IActionBuilder.CompensatedBy(string actionName) => CompensatedBy(actionName);
    IActionBuilder IActionBuilder.Requires(ActionPredicate predicate, string? description) => Requires(predicate, description);
    IActionBuilder IActionBuilder.RequiresSoft(ActionPredicate predicate, string? description) => RequiresSoft(predicate, description);
    IActionBuilder IActionBuilder.Ensures(ActionPredicate predicate, string? description) => Ensures(predicate, description);
    IActionBuilder IActionBuilder.RequiresLink(string linkName) => RequiresLink(linkName);
    IActionBuilder IActionBuilder.RequiresLinkSoft(string linkName) => RequiresLinkSoft(linkName);
    IActionBuilder IActionBuilder.RequiresRelation(string relationName, params string[] linkPath) => RequiresRelation(relationName, linkPath);
    IActionBuilder IActionBuilder.EnsuresLink(string linkName) => EnsuresLink(linkName);
    IActionBuilder IActionBuilder.EnsuresRelation(string relationName, params string[] linkPath) => EnsuresRelation(relationName, linkPath);

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

    public IActionBuilder<T> BoundToWorkflow(string workflowName) =>
        BoundToWorkflow(new WorkflowBindingReference(workflowName));

    public IActionBuilder<T> BoundToWorkflow(WorkflowBindingReference workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        _bindingType = ActionBindingType.Workflow;
        _boundWorkflow = workflow;
        _boundToolName = null;
        _boundToolMethod = null;
        return this;
    }

    public IActionBuilder<T> BoundToTool(string toolName, string methodName)
    {
        _bindingType = ActionBindingType.Tool;
        _boundToolName = toolName;
        _boundToolMethod = methodName;
        _boundWorkflow = null;
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

    public IActionBuilder<T> Requires(ActionPredicate predicate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _preconditions.Add(new ActionPrecondition(
            predicate,
            description ?? predicate.Expression,
            ConstraintStrength.Hard));
        return this;
    }

    public IActionBuilder<T> Requires(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var translated = ActionPredicateExpressionTranslator.Translate(predicate);
        return Requires(translated, translated.Expression);
    }

    public IActionBuilder<T> RequiresSoft(ActionPredicate predicate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _preconditions.Add(new ActionPrecondition(
            predicate,
            description ?? predicate.Expression,
            ConstraintStrength.Soft));
        return this;
    }

    public IActionBuilder<T> RequiresSoft(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var translated = ActionPredicateExpressionTranslator.Translate(predicate);
        return RequiresSoft(translated, translated.Expression);
    }

    public IActionBuilder<T> Ensures(ActionPredicate predicate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _ensures.Add(new ActionGuarantee(predicate, description));
        return this;
    }

    public IActionBuilder<T> Ensures(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var translated = ActionPredicateExpressionTranslator.Translate(predicate);
        return Ensures(translated, translated.Expression);
    }

    public IActionBuilder<T> RequiresLink(string linkName)
    {
        return Requires(
            ActionPredicate.LinkExists(linkName),
            $"Requires link '{linkName}' to have at least one target");
    }

    public IActionBuilder<T> RequiresLinkSoft(string linkName)
    {
        return RequiresSoft(
            ActionPredicate.LinkExists(linkName),
            $"Prefers link '{linkName}' to have at least one target");
    }

    public IActionBuilder<T> RequiresRelation(string relationName, params string[] linkPath)
    {
        return Requires(
            ActionPredicate.RelationHolds(relationName, linkPath),
            $"Requires the caller to hold relation '{relationName}' via {FormatPath(linkPath)}");
    }

    public IActionBuilder<T> EnsuresLink(string linkName)
    {
        return Ensures(ActionPredicate.LinkExists(linkName));
    }

    public IActionBuilder<T> EnsuresRelation(string relationName, params string[] linkPath)
    {
        return Ensures(ActionPredicate.RelationHolds(relationName, linkPath));
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
        new(subject, name, _description)
        {
            AcceptsType = _acceptsType,
            ReturnsType = _returnsType,
            BindingType = _bindingType,
            BoundWorkflow = _boundWorkflow,
            BoundToolName = _boundToolName,
            BoundToolMethod = _boundToolMethod,
            IsReadOnly = _isReadOnly,
            Idempotent = _idempotent,
            RequiredAuthority = _requiredAuthority,
            TouchedResources = _touchedResources.ToArray(),
            CompensatingActionName = _compensatingActionName,
            Preconditions = _preconditions,
            Ensures = _ensures,
            Postconditions = _postconditions,
        };

    private static string FormatPath(IReadOnlyList<string> linkPath) =>
        linkPath.Count == 0 ? "target" : string.Join("/", linkPath);
}
