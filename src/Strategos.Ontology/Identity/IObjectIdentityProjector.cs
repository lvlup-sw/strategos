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
    /// <paramref name="descriptor"/>. Throws <see cref="System.InvalidOperationException"/>
    /// (naming the descriptor) when the descriptor exposes no id accessor or
    /// the accessor yields a null key value — never silently, never via a
    /// reflection fallback.
    /// </summary>
    string ProjectId(ObjectTypeDescriptor descriptor, object instance);
}
