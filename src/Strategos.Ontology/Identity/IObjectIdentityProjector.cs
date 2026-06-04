using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Identity;

/// <summary>
/// Projects a domain instance to its stable, deterministic string id using
/// the descriptor's reflection-free <see cref="ObjectTypeDescriptor.IdAccessor"/>.
/// </summary>
/// <remarks>
/// DR-1 (Ontology Edge Foundation). Replaces the ad-hoc
/// <c>item?.ToString()</c> notion of instance identity with a real,
/// polyglot-capable projector. The projector NEVER reflects over the
/// instance's CLR type to locate the key (INV-8): both CLR and
/// <c>SymbolKey</c>-only descriptors resolve through the same supplied
/// <see cref="ObjectTypeDescriptor.IdAccessor"/>. No member of this
/// namespace accepts or returns <see cref="System.Type"/>.
/// </remarks>
public interface IObjectIdentityProjector
{
    /// <summary>
    /// Projects <paramref name="instance"/> to a deterministic id under
    /// <paramref name="descriptor"/> — never silently, never via a reflection
    /// fallback.
    /// </summary>
    /// <param name="descriptor">The descriptor supplying the id accessor.</param>
    /// <param name="instance">The instance to project.</param>
    /// <returns>The deterministic projected id.</returns>
    /// <exception cref="MissingIdAccessorException">
    /// The descriptor exposes no id accessor — a CONFIGURATION error (the
    /// descriptor was assembled without a <c>Key(...)</c> selector or an
    /// <c>IOntologySource</c>-supplied accessor).
    /// </exception>
    /// <exception cref="NullKeyValueException">
    /// The accessor ran but yielded a null key value — a DATA error (the
    /// descriptor is configured, but the instance carries a null key).
    /// </exception>
    /// <remarks>
    /// Both exceptions name the descriptor and both derive from
    /// <see cref="System.InvalidOperationException"/>, so existing callers that
    /// catch the latter keep working while callers that need to distinguish a
    /// config error from a data error can catch the two specific types.
    /// </remarks>
    string ProjectId(ObjectTypeDescriptor descriptor, object instance);
}
