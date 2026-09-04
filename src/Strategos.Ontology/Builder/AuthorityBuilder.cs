using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class AuthorityBuilder(string name) : IAuthorityBuilder
{
    private readonly Dictionary<string, string> _coordinates = new(StringComparer.Ordinal);
    private readonly HashSet<string> _implications = new(StringComparer.Ordinal);

    public IAuthorityBuilder At(string axisName, string levelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(axisName);
        ArgumentException.ThrowIfNullOrWhiteSpace(levelName);
        _coordinates[axisName] = levelName;
        return this;
    }

    public IAuthorityBuilder Implies(string weakerAuthority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(weakerAuthority);
        _implications.Add(weakerAuthority);
        return this;
    }

    internal AuthorityDescriptor Build() => new(name)
    {
        Coordinates = _coordinates.ToImmutableDictionary(StringComparer.Ordinal),
        ExplicitImplications = _implications.Order(StringComparer.Ordinal).ToImmutableArray(),
    };
}
