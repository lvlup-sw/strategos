using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class ExtensionPointBuilder(string name) : IExtensionPointBuilder
{
    private string? _description;
    private string? _requiredSourceInterface;
    private string? _requiredSourceDomain;
    private int? _maxLinks;

    public IExtensionPointBuilder FromInterface<T>()
    {
        _requiredSourceInterface = typeof(T).Name;
        return this;
    }

    public IExtensionPointBuilder FromDomain(string domain)
    {
        _requiredSourceDomain = domain;
        return this;
    }

    public IExtensionPointBuilder Description(string description)
    {
        _description = description;
        return this;
    }

    public IExtensionPointBuilder MaxLinks(int max)
    {
        _maxLinks = max;
        return this;
    }

    public ExternalLinkExtensionPoint Build() =>
        new()
        {
            Name = name,
            Description = _description,
            RequiredSourceInterface = _requiredSourceInterface,
            RequiredSourceDomain = _requiredSourceDomain,
            MaxLinks = _maxLinks,
        };
}
