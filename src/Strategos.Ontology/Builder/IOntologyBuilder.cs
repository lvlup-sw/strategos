namespace Strategos.Ontology.Builder;

public interface IOntologyBuilder
{
    void Object<T>(Action<IObjectTypeBuilder<T>> configure)
        where T : class;

    /// <summary>
    /// Registers an object type with an explicit descriptor name, allowing the same CLR
    /// type to be registered under multiple logical descriptor names (e.g. one CLR type
    /// backing multiple object sets).
    /// </summary>
    /// <param name="name">
    /// Explicit descriptor name. When <c>null</c>, falls back to <c>typeof(T).Name</c>
    /// (parity with the parameterless overload). When non-null, must match
    /// <c>^[a-zA-Z_][a-zA-Z0-9_]*$</c>.
    /// </param>
    /// <param name="configure">Configuration callback for the object type builder.</param>
    void Object<T>(string? name, Action<IObjectTypeBuilder<T>> configure)
        where T : class;

    void Interface<T>(string name, Action<IInterfaceBuilder<T>> configure)
        where T : class;

    ICrossDomainLinkBuilder CrossDomainLink(string name);
}
