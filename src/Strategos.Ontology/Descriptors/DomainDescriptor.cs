using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

public sealed record DomainDescriptor(
    string DomainName)
{
    private ImmutableArray<ObjectTypeDescriptor> _objectTypes = [];
    private ImmutableArray<AuthorityAxisDescriptor> _authorityAxes = [];
    private ImmutableArray<AuthorityDescriptor> _authorities = [];

    public IReadOnlyList<ObjectTypeDescriptor> ObjectTypes
    {
        get => _objectTypes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _objectTypes = value.ToImmutableArray();
        }
    }

    public IReadOnlyList<AuthorityAxisDescriptor> AuthorityAxes
    {
        get => _authorityAxes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _authorityAxes = value.ToImmutableArray();
        }
    }

    public IReadOnlyList<AuthorityDescriptor> Authorities
    {
        get => _authorities;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _authorities = value.ToImmutableArray();
        }
    }
}
