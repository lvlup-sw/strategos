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
}
