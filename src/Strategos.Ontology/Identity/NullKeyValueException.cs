namespace Strategos.Ontology.Identity;

/// <summary>
/// Thrown by <see cref="IObjectIdentityProjector.ProjectId"/> when the
/// descriptor's id accessor runs but yields a null key value.
/// </summary>
/// <remarks>
/// DR-1 (FIX-E). This is a DATA error — the descriptor is correctly configured
/// (an accessor is present) but the instance carries a null key. It is distinct
/// from <see cref="MissingIdAccessorException"/>, which is a CONFIGURATION error
/// (no accessor at all), so callers can tell bad instance data from a
/// misconfigured descriptor.
/// </remarks>
public sealed class NullKeyValueException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullKeyValueException"/> class,
    /// naming the descriptor whose key value resolved to null.
    /// </summary>
    /// <param name="descriptorName">Descriptor whose accessor yielded a null key value.</param>
    public NullKeyValueException(string descriptorName)
        : base($"Cannot project id for descriptor '{descriptorName}': the key value resolved to null.")
    {
        DescriptorName = descriptorName;
    }

    /// <summary>Descriptor name whose accessor yielded a null key value.</summary>
    public string DescriptorName { get; }
}
