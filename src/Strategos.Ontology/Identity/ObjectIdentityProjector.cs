using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

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
    /// Reserved delimiter separating composite-key components. The ASCII Unit
    /// Separator (U+001F) is a non-printable control character; it delimits the
    /// length-prefixed encoding emitted by <see cref="Format"/> but, because each
    /// component is length-prefixed, the encoding stays injective even when a
    /// component's own text happens to contain this character.
    /// </summary>
    public const string CompositeKeySeparator = "";

    // Discriminators emitted ahead of each composite-key component so the
    // encoding is injective. A null element emits NullComponentMarker alone; a
    // non-null element emits ValueComponentMarker, the component's char length,
    // a colon, then the raw component text. Length-prefixing means a separator
    // (or any other character) appearing INSIDE a component can never be
    // mistaken for a component boundary, and the null marker is distinct from
    // ValueComponentMarker + "0:" (the empty string), so null and "" never
    // collide.
    private const char NullComponentMarker = 'N';
    private const char ValueComponentMarker = 'V';

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
        // Composite keys (ValueTuple / Tuple) encode each element with a
        // length-prefix and a null/value discriminator, joined by the reserved
        // separator, deterministically and in declaration order. The encoding is
        // injective: a component whose own text contains the separator cannot be
        // confused with a boundary, and a null element is distinguishable from
        // the empty string (see NullComponentMarker / ValueComponentMarker).
        // Single-value keys are formatted invariantly via ToString().
        if (keyValue is ITuple tuple)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < tuple.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(CompositeKeySeparator);
                }

                AppendComponent(builder, tuple[i]);
            }

            return builder.ToString();
        }

        return FormatScalar(keyValue);
    }

    private static void AppendComponent(StringBuilder builder, object? value)
    {
        if (value is null)
        {
            builder.Append(NullComponentMarker);
            return;
        }

        var text = FormatScalar(value);
        builder.Append(ValueComponentMarker)
            .Append(text.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text);
    }

    private static string FormatScalar(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
