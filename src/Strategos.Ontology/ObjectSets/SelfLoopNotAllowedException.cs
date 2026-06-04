namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Thrown when a relate operation would connect an object to itself along a
/// link whose <see cref="Descriptors.LinkDescriptor.AllowsSelfLoop"/> is
/// <c>false</c>.
/// </summary>
/// <remarks>
/// DR-8 (self-loop policy). The DR-2 enforcement is runtime-only; promotion to
/// a compile-time analyzer is a later task.
/// </remarks>
public sealed class SelfLoopNotAllowedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelfLoopNotAllowedException"/> class,
    /// naming the descriptor, the self-related id, and the link so the message is actionable.
    /// </summary>
    /// <param name="descriptorName">Descriptor of the self-related endpoint.</param>
    /// <param name="id">Projected id related to itself.</param>
    /// <param name="linkName">Link the self-loop was attempted along.</param>
    public SelfLoopNotAllowedException(string descriptorName, string id, string linkName)
        : base($"Self-loop not allowed: cannot relate '{descriptorName}' instance '{id}' to itself "
            + $"along link '{linkName}'. Set LinkDescriptor.AllowsSelfLoop to permit self-loops on this link.")
    {
        DescriptorName = descriptorName;
        Id = id;
        LinkName = linkName;
    }

    /// <summary>Descriptor name of the self-related endpoint.</summary>
    public string DescriptorName { get; }

    /// <summary>Projected id that was related to itself.</summary>
    public string Id { get; }

    /// <summary>Name of the link the self-loop was attempted along.</summary>
    public string LinkName { get; }
}
