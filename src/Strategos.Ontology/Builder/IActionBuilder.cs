using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

public interface IActionBuilder
{
    IActionBuilder Description(string description);

    IActionBuilder Accepts<T>();

    IActionBuilder Returns<T>();

    IActionBuilder BoundToWorkflow(string workflowName);

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

    /// <summary>Names the action that restores this action's declared frame.</summary>
    IActionBuilder CompensatedBy(string actionName);
}
