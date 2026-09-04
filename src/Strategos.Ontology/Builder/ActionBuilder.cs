using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class ActionBuilder(string name) : IActionBuilder
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

    public IActionBuilder Description(string description)
    {
        _description = description;
        return this;
    }

    public IActionBuilder Accepts<T>()
    {
        _acceptsType = typeof(T);
        return this;
    }

    public IActionBuilder Returns<T>()
    {
        _returnsType = typeof(T);
        return this;
    }

    public IActionBuilder BoundToWorkflow(string workflowName)
    {
        _bindingType = ActionBindingType.Workflow;
        _boundWorkflowName = workflowName;
        return this;
    }

    public IActionBuilder BoundToTool(string toolName, string methodName)
    {
        _bindingType = ActionBindingType.Tool;
        _boundToolName = toolName;
        _boundToolMethod = methodName;
        return this;
    }

    public IActionBuilder ReadOnly()
    {
        _isReadOnly = true;
        _idempotent = true;
        return this;
    }

    public IActionBuilder Idempotent()
    {
        _idempotent = true;
        return this;
    }

    public IActionBuilder RequiresAuthority(string authorityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityName);
        _requiredAuthority = authorityName;
        return this;
    }

    public IActionBuilder Touches(ActionResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _touchedResources.Add(resource);
        return this;
    }

    public IActionBuilder CompensatedBy(string actionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        _compensatingActionName = actionName;
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
        };
}
