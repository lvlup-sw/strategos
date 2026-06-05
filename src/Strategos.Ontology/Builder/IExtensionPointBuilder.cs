namespace Strategos.Ontology.Builder;

public interface IExtensionPointBuilder
{
    IExtensionPointBuilder FromInterface<T>();

    IExtensionPointBuilder FromDomain(string domain);

    IExtensionPointBuilder Description(string description);

    IExtensionPointBuilder MaxLinks(int max);
}
