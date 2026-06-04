using System.Globalization;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Identity;

/// <summary>
/// Default <see cref="IObjectIdentityProjector"/>. Resolves an instance's id
/// solely through the descriptor's <see cref="ObjectTypeDescriptor.IdAccessor"/>,
/// performing no per-call reflection on the instance type (INV-8).
/// </summary>
public sealed class ObjectIdentityProjector : IObjectIdentityProjector
{
    /// <inheritdoc />
    public string ProjectId(ObjectTypeDescriptor descriptor, object instance)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(instance);

        var keyValue = descriptor.IdAccessor!(instance);
        if (keyValue is null)
        {
            throw new InvalidOperationException(
                $"Cannot project id for descriptor '{descriptor.Name}': the key value resolved to null.");
        }

        return Convert.ToString(keyValue, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
