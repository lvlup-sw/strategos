namespace Strategos.Ontology.Descriptors;

public sealed record DomainDescriptor(
    string DomainName)
{
    public IReadOnlyList<ObjectTypeDescriptor> ObjectTypes { get; init; } = [];

    public IReadOnlyList<AuthorityAxisDescriptor> AuthorityAxes { get; init; } = [];

    public IReadOnlyList<AuthorityDescriptor> Authorities { get; init; } = [];
}
