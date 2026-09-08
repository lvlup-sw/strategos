using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class ActionBuilder(string name, ActionSubject? subject = null) : IActionBuilder
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

    public IActionBuilder BoundToWorkflow(string workflowName) =>
        BoundToWorkflow(new WorkflowBindingReference(workflowName));

    public IActionBuilder BoundToWorkflow(WorkflowBindingReference workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        _bindingType = ActionBindingType.Workflow;
        _boundWorkflow = workflow;
        _boundToolName = null;
        _boundToolMethod = null;
        return this;
    }

    public IActionBuilder BoundToTool(string toolName, string methodName)
    {
        _bindingType = ActionBindingType.Tool;
        _boundToolName = toolName;
        _boundToolMethod = methodName;
        _boundWorkflow = null;
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

    public IActionBuilder Requires(ActionPredicate predicate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _preconditions.Add(new ActionPrecondition(
            predicate,
            description ?? predicate.Expression,
            ConstraintStrength.Hard));
        return this;
    }

    public IActionBuilder RequiresSoft(ActionPredicate predicate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _preconditions.Add(new ActionPrecondition(
            predicate,
            description ?? predicate.Expression,
            ConstraintStrength.Soft));
        return this;
    }

    public IActionBuilder Ensures(ActionPredicate predicate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _ensures.Add(new ActionGuarantee(predicate, description));
        return this;
    }

    public IActionBuilder RequiresLink(string linkName) =>
        Requires(
            ActionPredicate.LinkExists(linkName),
            $"Requires link '{linkName}' to have at least one target");

    public IActionBuilder RequiresLinkSoft(string linkName) =>
        RequiresSoft(
            ActionPredicate.LinkExists(linkName),
            $"Prefers link '{linkName}' to have at least one target");

    public IActionBuilder RequiresRelation(string relationName, params string[] linkPath) =>
        Requires(
            ActionPredicate.RelationHolds(relationName, linkPath),
            $"Requires the caller to hold relation '{relationName}' via {FormatPath(linkPath)}");

    public IActionBuilder EnsuresLink(string linkName) =>
        Ensures(ActionPredicate.LinkExists(linkName));

    public IActionBuilder EnsuresRelation(string relationName, params string[] linkPath) =>
        Ensures(ActionPredicate.RelationHolds(relationName, linkPath));

    public ActionDescriptor Build(ActionSubject? subjectOverride = null) =>
        new(subjectOverride ?? subject ?? throw new InvalidOperationException(
            $"Executable action '{name}' must be built for an ActionSubject."), name, _description)
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
        };

    internal InterfaceActionDescriptor BuildInterfaceAction() =>
        new()
        {
            Name = name,
            Description = string.IsNullOrEmpty(_description) ? null : _description,
            AcceptsTypeName = _acceptsType?.Name,
            ReturnsTypeName = _returnsType?.Name,
        };

    private static string FormatPath(IReadOnlyList<string> linkPath) =>
        linkPath.Count == 0 ? "target" : string.Join("/", linkPath);
}
