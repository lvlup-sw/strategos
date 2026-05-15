using Strategos.Ontology.Descriptors;

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
    /// <typeparam name="T">CLR type the descriptor is bound to.</typeparam>
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

    /// <summary>
    /// Registers an <see cref="ObjectTypeDescriptor"/> directly, bypassing
    /// the expression-tree DSL. This is the mechanism
    /// <see cref="IOntologySource"/> contributions reach the graph —
    /// necessary because ingested types may only be known by
    /// <c>SymbolKey</c>, with no loaded CLR type.
    /// </summary>
    /// <remarks>
    /// DR-5 (Task 9). The descriptor's <see cref="ObjectTypeDescriptor.Source"/>
    /// is preserved unchanged so provenance flows through to graph-freeze.
    /// </remarks>
    void ObjectTypeFromDescriptor(ObjectTypeDescriptor descriptor);

    /// <summary>
    /// Applies an <see cref="OntologyDelta"/> against the current builder
    /// state. Dispatches by variant; the
    /// <see cref="OntologyDelta.AddObjectType"/> branch routes to
    /// <see cref="ObjectTypeFromDescriptor"/>. Unknown variants throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    /// <remarks>
    /// DR-5 (Tasks 10 + 11). Polyglot ingestion deltas reach the graph
    /// through this entry point.
    /// </remarks>
    void ApplyDelta(OntologyDelta delta);
}
