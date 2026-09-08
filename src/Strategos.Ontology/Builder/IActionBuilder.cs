using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

public interface IActionBuilder
{
    IActionBuilder Description(string description);

    IActionBuilder Accepts<T>();

    IActionBuilder Returns<T>();

    IActionBuilder BoundToWorkflow(string workflowName);

    IActionBuilder BoundToWorkflow(WorkflowBindingReference workflow);

    IActionBuilder BoundToTool(string toolName, string methodName);

    /// <summary>
    /// Marks the action as read-only. Read-only actions are dispatchable via
    /// <see cref="Strategos.Ontology.Actions.IActionDispatcher.DispatchReadOnlyAsync"/>
    /// and may not declare write postconditions; the analyzer enforces both
    /// invariants at compile time.
    /// </summary>
    /// <returns>The same builder instance for fluent chaining.</returns>
    IActionBuilder ReadOnly();

    /// <summary>
    /// Marks the action as safe to repeat without changing its externally
    /// observable effect.
    /// </summary>
    /// <returns>The same builder instance for fluent chaining.</returns>
    IActionBuilder Idempotent();

    /// <summary>
    /// Requires the named domain authority to invoke this action.
    /// </summary>
    IActionBuilder RequiresAuthority(string authorityName);

    /// <summary>Adds a resource to the action's declared frame.</summary>
    IActionBuilder Touches(ActionResource resource);

    /// <summary>Names the action declared to implement this action's contract inverse.</summary>
    IActionBuilder CompensatedBy(string actionName);

    /// <summary>Adds a hard typed precondition.</summary>
    IActionBuilder Requires(ActionPredicate predicate, string? description = null);

    /// <summary>Adds a soft typed precondition.</summary>
    IActionBuilder RequiresSoft(ActionPredicate predicate, string? description = null);

    /// <summary>Adds an explicit typed post-state guarantee.</summary>
    IActionBuilder Ensures(ActionPredicate predicate, string? description = null);

    /// <summary>Adds a hard link-existence precondition.</summary>
    IActionBuilder RequiresLink(string linkName);

    /// <summary>Adds a soft link-existence precondition.</summary>
    IActionBuilder RequiresLinkSoft(string linkName);

    /// <summary>Adds a hard principal-relation precondition.</summary>
    IActionBuilder RequiresRelation(string relationName, params string[] linkPath);

    /// <summary>Adds an explicit link-existence guarantee.</summary>
    IActionBuilder EnsuresLink(string linkName);

    /// <summary>Adds an explicit principal-relation guarantee.</summary>
    IActionBuilder EnsuresRelation(string relationName, params string[] linkPath);
}
