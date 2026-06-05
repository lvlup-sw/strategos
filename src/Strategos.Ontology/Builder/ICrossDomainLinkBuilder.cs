namespace Strategos.Ontology.Builder;

public interface ICrossDomainLinkBuilder
{
    ICrossDomainLinkBuilder From<T>();

    ICrossDomainLinkBuilder ToExternal(string domain, string typeName);

    ICrossDomainLinkBuilder ManyToMany();

    ICrossDomainLinkBuilder Description(string description);
}
