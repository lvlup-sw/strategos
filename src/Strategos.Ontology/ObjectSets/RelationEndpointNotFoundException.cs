namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Thrown when a relate operation names an endpoint (source or target) for
/// which no instance is stored under the given descriptor.
/// </summary>
/// <remarks>
/// DR-8 (eager endpoint validation). The in-memory provider validates both
/// endpoints BEFORE writing any row, so a failed relate never leaves a
/// dangling relation behind. This eager posture is the contract the future
/// Npgsql provider mirrors via foreign-key constraints — the same caller
/// error surfaces identically across backends.
/// </remarks>
public sealed class RelationEndpointNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationEndpointNotFoundException"/> class,
    /// naming the missing endpoint's descriptor and id so the message is actionable.
    /// </summary>
    /// <param name="descriptorName">Descriptor of the missing endpoint.</param>
    /// <param name="id">Projected id that resolved to no stored instance.</param>
    public RelationEndpointNotFoundException(string descriptorName, string id)
        : base($"Relation endpoint not found: no instance with id '{id}' is stored under descriptor '{descriptorName}'. "
            + "Both endpoints must be stored before they can be related (eager validation).")
    {
        ArgumentNullException.ThrowIfNull(descriptorName);
        ArgumentNullException.ThrowIfNull(id);
        DescriptorName = descriptorName;
        Id = id;
    }

    /// <summary>Descriptor name of the endpoint that could not be resolved.</summary>
    public string DescriptorName { get; }

    /// <summary>Projected id that resolved to no stored instance.</summary>
    public string Id { get; }
}
