namespace Strategos.Ontology.Descriptors;

/// <summary>Stable ontology-name identity of the object an action operates on.</summary>
public sealed record ActionSubject
{
    /// <summary>Initializes a subject from its owning domain and object descriptor names.</summary>
    public ActionSubject(string domainName, string objectTypeName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new ArgumentException("Action subject domain name cannot be empty.", nameof(domainName));
        }

        if (string.IsNullOrWhiteSpace(objectTypeName))
        {
            throw new ArgumentException("Action subject object type name cannot be empty.", nameof(objectTypeName));
        }

        DomainName = domainName;
        ObjectTypeName = objectTypeName;
    }

    /// <summary>Gets the owning ontology domain name.</summary>
    public string DomainName { get; }

    /// <summary>Gets the object descriptor name within the domain.</summary>
    public string ObjectTypeName { get; }

    /// <inheritdoc />
    public override string ToString() => DomainName + "/" + ObjectTypeName;
}
