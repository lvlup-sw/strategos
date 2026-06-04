namespace Strategos.Ontology.Identity;

/// <summary>
/// Thrown by <see cref="IObjectIdentityProjector.ProjectId"/> when the descriptor
/// exposes no <see cref="Descriptors.ObjectTypeDescriptor.IdAccessor"/>.
/// </summary>
/// <remarks>
/// DR-1 (FIX-E). This is a CONFIGURATION error — the descriptor was assembled
/// without an id accessor (a hand-authored descriptor missing its <c>Key(...)</c>
/// selector, or an ingested descriptor whose <c>IOntologySource</c> supplied
/// none). It is distinct from <see cref="NullKeyValueException"/>, which is a
/// DATA error (the accessor ran but yielded null), so callers can tell a
/// misconfigured descriptor from bad instance data.
/// </remarks>
public sealed class MissingIdAccessorException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingIdAccessorException"/> class,
    /// naming the descriptor that lacks an id accessor.
    /// </summary>
    /// <param name="descriptorName">Descriptor that exposes no id accessor.</param>
    public MissingIdAccessorException(string descriptorName)
        : base($"Cannot project id for descriptor '{descriptorName}': descriptor '{descriptorName}' has no id accessor. "
            + "Hand-authored descriptors derive it from the Key(...) selector; "
            + "ingested descriptors must have one supplied by their IOntologySource.")
    {
        DescriptorName = descriptorName;
    }

    /// <summary>Descriptor name that exposes no id accessor.</summary>
    public string DescriptorName { get; }
}
