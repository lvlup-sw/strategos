namespace Strategos.Ontology.Extensions;

/// <summary>
/// Collects workflow metadata (consumed/produced types) for ontology graph integration.
/// </summary>
public sealed class WorkflowMetadataBuilder
{
    public string WorkflowName { get; }

    public string? ConsumedTypeName { get; private set; }

    public string? ProducedTypeName { get; private set; }

    /// <summary>
    /// The domain name to use for resolving consumed and produced types. When set,
    /// <c>BuildWorkflowChains</c> performs a domain-keyed <c>(DomainName, Name)</c> lookup
    /// instead of a simple-name lookup. This allows two domains that share a simple type name
    /// to each have workflow chains resolved unambiguously. See #33 Finding 4.
    /// </summary>
    public string? DomainName { get; private set; }

    public WorkflowMetadataBuilder(string workflowName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        WorkflowName = workflowName;
    }

    public WorkflowMetadataBuilder InDomain(string domainName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);
        DomainName = domainName;
        return this;
    }

    public WorkflowMetadataBuilder Consumes<T>()
        where T : class
    {
        ConsumedTypeName = typeof(T).Name;
        return this;
    }

    public WorkflowMetadataBuilder Produces<T>()
        where T : class
    {
        ProducedTypeName = typeof(T).Name;
        return this;
    }
}
