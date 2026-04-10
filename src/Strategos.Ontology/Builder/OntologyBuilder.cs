using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class OntologyBuilder(string domainName) : IOntologyBuilder
{
    private readonly List<ObjectTypeDescriptor> _objectTypes = [];
    private readonly List<InterfaceDescriptor> _interfaces = [];
    private readonly List<CrossDomainLinkBuilder> _crossDomainLinkBuilders = [];

    public IReadOnlyList<ObjectTypeDescriptor> ObjectTypes => _objectTypes.AsReadOnly();

    public IReadOnlyList<InterfaceDescriptor> Interfaces => _interfaces.AsReadOnly();

    public IReadOnlyList<CrossDomainLinkDescriptor> CrossDomainLinks =>
        _crossDomainLinkBuilders.ConvertAll(b => b.Build()).AsReadOnly();

    public void Object<T>(Action<IObjectTypeBuilder<T>> configure)
        where T : class
        => Object<T>(name: null, configure);

    public void Object<T>(string? name, Action<IObjectTypeBuilder<T>> configure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ObjectTypeBuilder<T>(domainName, explicitName: name);
        configure(builder);
        _objectTypes.Add(builder.Build());
    }

    public void Interface<T>(string name, Action<IInterfaceBuilder<T>> configure)
        where T : class
    {
        var builder = new InterfaceBuilder<T>(name);
        configure(builder);
        _interfaces.Add(builder.Build());
    }

    public ICrossDomainLinkBuilder CrossDomainLink(string name)
    {
        var builder = new CrossDomainLinkBuilder(name);
        _crossDomainLinkBuilders.Add(builder);
        return builder;
    }
}
