using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

public interface IOntologyBuilder
{
    /// <summary>
    /// Registers a CLR type <typeparamref name="T"/> with its default
    /// descriptor name (<c>typeof(T).Name</c>).
    /// </summary>
    /// <typeparam name="T">CLR type the descriptor is bound to.</typeparam>
    /// <param name="configure">Configuration callback for the object type builder.</param>
    /// <remarks>
    /// The fluent <c>Object&lt;T&gt;</c> / <c>Interface&lt;T&gt;</c> surface
    /// stays CLR-generic. The first-class CLR-free path is
    /// <see cref="ObjectTypeFromDescriptor"/> /
    /// <see cref="ApplyDelta"/>.
    /// </remarks>
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

    /// <summary>
    /// Registers a polymorphic interface backed by the C# interface
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">CLR interface the descriptor is bound to.</typeparam>
    /// <param name="name">Descriptor name of the interface.</param>
    /// <param name="configure">Configuration callback for the interface builder.</param>
    /// <remarks>
    /// <c>Interface&lt;T&gt;</c> stays CLR-generic: an interface descriptor
    /// carries a CLR type, so a SymbolKey-only interface fan-out is not
    /// expressible. CLR-free object types use
    /// <see cref="ObjectTypeFromDescriptor"/> /
    /// <see cref="ApplyDelta"/> and remain monomorphic.
    /// </remarks>
    void Interface<T>(string name, Action<IInterfaceBuilder<T>> configure)
        where T : class;

    /// <summary>
    /// Registers a reified association (DR-4): a standalone
    /// object-with-two-endpoints. Unlike per-source links (<c>ManyToMany</c> et
    /// al. on <see cref="IObjectTypeBuilder{T}"/>), an association is a
    /// first-class object type that owns its own key and edge attributes and
    /// links two endpoints declared via
    /// <see cref="IAssociationBuilder{TRel}.Between{TLeft}"/> +
    /// <see cref="IAssociationEndpointBuilder{TRel}.And{TRight}"/>. It produces an
    /// <see cref="ObjectTypeDescriptor"/> with
    /// <see cref="ObjectTypeDescriptor.Kind"/> = <see cref="ObjectKind.Association"/>.
    /// </summary>
    /// <typeparam name="TRel">CLR type backing the association object.</typeparam>
    /// <param name="name">Descriptor name of the association.</param>
    /// <param name="configure">Configuration callback for the association builder.</param>
    void Association<TRel>(string name, Action<IAssociationBuilder<TRel>> configure)
        where TRel : class;

    ICrossDomainLinkBuilder CrossDomainLink(string name);

    /// <summary>
    /// Registers an <see cref="ObjectTypeDescriptor"/> directly, bypassing
    /// the expression-tree DSL. This is the first-class CLR-free path —
    /// the mechanism <see cref="IOntologySource"/> contributions reach the
    /// graph when ingested types are known only by <c>SymbolKey</c>, with
    /// no loaded CLR type.
    /// </summary>
    /// <remarks>
    /// DR-5 (Task 9). The descriptor's <see cref="ObjectTypeDescriptor.Source"/>
    /// is preserved unchanged so provenance flows through to graph-freeze.
    /// The fluent <c>Object&lt;T&gt;</c> / <c>Interface&lt;T&gt;</c> surface
    /// stays CLR-generic; do not treat those overloads as a CLR-free
    /// authoring path.
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
    /// through this entry point — the first-class CLR-free companion to
    /// <see cref="ObjectTypeFromDescriptor"/>. The fluent
    /// <c>Object&lt;T&gt;</c> / <c>Interface&lt;T&gt;</c> surface stays
    /// CLR-generic.
    /// </remarks>
    void ApplyDelta(OntologyDelta delta);
}
