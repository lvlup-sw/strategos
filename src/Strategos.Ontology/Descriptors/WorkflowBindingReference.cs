namespace Strategos.Ontology.Descriptors;

/// <summary>Stable workflow-catalog identity used to bind an ontology action.</summary>
public sealed record WorkflowBindingReference
{
    /// <summary>Initializes a reference to the workflow with the supplied catalog identifier.</summary>
    public WorkflowBindingReference(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        WorkflowId = workflowId;
    }

    /// <summary>Gets the workflow identifier exactly as declared in the workflow catalog.</summary>
    public string WorkflowId { get; }
}
