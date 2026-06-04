using System.Globalization;
using System.Runtime.CompilerServices;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Identity;

/// <summary>
/// Default <see cref="IObjectIdentityProjector"/>. Resolves an instance's id
/// solely through the descriptor's <see cref="ObjectTypeDescriptor.IdAccessor"/>,
/// performing no per-call reflection on the instance type (INV-8).
/// </summary>
public sealed class ObjectIdentityProjector : IObjectIdentityProjector
{
    /// <summary>
    /// Reserved delimiter joining composite-key elements. The ASCII Unit
    /// Separator (U+001F) is a non-printable control character that does not
    /// occur in well-formed key text, so the joined id is unambiguously
    /// reversible and collision-free for tuple keys.
    /// </summary>
    public const string CompositeKeySeparator = "\u001F";

    /// <inheritdoc />
    public string ProjectId(ObjectTypeDescriptor descriptor, object instance)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(instance);

        var accessor = descriptor.IdAccessor;
        if (accessor is null)
        {
            throw new InvalidOperationException(
                $"Cannot project id for descriptor '{descriptor.Name}': descriptor '{descriptor.Name}' has no id accessor. "
                + "Hand-authored descriptors derive it from the Key(...) selector; "
                + "ingested descriptors must have one supplied by their IOntologySource.");
        }

        var keyValue = accessor(instance);
        if (keyValue is null)
        {
            throw new InvalidOperationException(
                $"Cannot project id for descriptor '{descriptor.Name}': the key value resolved to null.");
        }

        return Format(keyValue);
    }

    private static string Format(object keyValue)
    {
        // Composite keys (ValueTuple / Tuple) join their elements with the
        // reserved separator, deterministically and in declaration order.
        // Single-value keys are formatted invariantly via ToString().
        if (keyValue is ITuple tuple)
        {
            var parts = new string[tuple.Length];
            for (var i = 0; i < tuple.Length; i++)
            {
                parts[i] = FormatScalar(tuple[i]);
            }

            return string.Join(CompositeKeySeparator, parts);
        }

        return FormatScalar(keyValue);
    }

    private static string FormatScalar(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
