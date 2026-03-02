using System.Linq.Expressions;
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
    private readonly List<ActionPrecondition> _preconditions = [];
    private readonly List<ActionPostcondition> _postconditions = [];

    IActionBuilder IActionBuilder.Description(string description) => Description(description);
    IActionBuilder IActionBuilder.Accepts<TAccepts>() => Accepts<TAccepts>();
    IActionBuilder IActionBuilder.Returns<TReturns>() => Returns<TReturns>();
    IActionBuilder IActionBuilder.BoundToWorkflow(string workflowName) => BoundToWorkflow(workflowName);
    IActionBuilder IActionBuilder.BoundToTool(string toolName, string methodName) => BoundToTool(toolName, methodName);

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

    public IActionBuilder<T> BoundToTool<TTool>(Expression<Func<TTool, Delegate>> methodSelector)
    {
        var methodName = ExpressionHelper.ExtractMethodName(methodSelector);
        return BoundToTool(typeof(TTool).Name, methodName);
    }

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

    public IActionBuilder<T> Modifies(Expression<Func<T, object>> propertySelector)
    {
        var memberName = ExpressionHelper.ExtractMemberName(propertySelector);
        _postconditions.Add(new ActionPostcondition
        {
            Kind = PostconditionKind.ModifiesProperty,
            PropertyName = memberName,
        });
        return this;
    }

    public IActionBuilder<T> CreatesLinked<TTarget>(string linkName)
    {
        _postconditions.Add(new ActionPostcondition
        {
            Kind = PostconditionKind.CreatesLink,
            LinkName = linkName,
        });
        return this;
    }

    public IActionBuilder<T> EmitsEvent<TEvent>()
    {
        _postconditions.Add(new ActionPostcondition
        {
            Kind = PostconditionKind.EmitsEvent,
            EventTypeName = typeof(TEvent).Name,
        });
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
            Preconditions = _preconditions.AsReadOnly(),
            Postconditions = _postconditions.AsReadOnly(),
        };
}
