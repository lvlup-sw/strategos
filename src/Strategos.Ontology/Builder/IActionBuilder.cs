namespace Strategos.Ontology.Builder;

public interface IActionBuilder
{
    IActionBuilder Description(string description);

    IActionBuilder Accepts<T>();

    IActionBuilder Returns<T>();

    IActionBuilder BoundToWorkflow(string workflowName);

    IActionBuilder BoundToTool(string toolName, string methodName);

    IActionBuilder ReadOnly();
}
