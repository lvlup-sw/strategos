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
        };
}
