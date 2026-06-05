using System.Linq.Expressions;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

/// <summary>
/// Fluent authoring surface for a reified association (DR-4). An association is
/// a standalone object-with-two-endpoints declared at the ontology level via
/// <see cref="IOntologyBuilder.Association{TRel}(string, System.Action{IAssociationBuilder{TRel}})"/>.
/// It carries its own key (DR-1 identity path), two typed endpoints, and its
/// own edge-attribute properties.
/// </summary>
/// <typeparam name="TRel">The CLR type backing the association object.</typeparam>
/// <remarks>
/// INV-8: <see cref="Between{TLeft}"/>/<see cref="IAssociationEndpointBuilder{TRel}.And{TRight}"/>
/// accept a generic endpoint type as authoring sugar, but the produced
/// <see cref="AssociationEndpoint"/> stores only the endpoint's descriptor
/// name — no CLR <see cref="System.Type"/> survives onto the descriptor as
/// endpoint identity.
/// </remarks>
public interface IAssociationBuilder<TRel>
    where TRel : class
{
    /// <summary>
    /// Declares the association's key selector. Mirrors
    /// <c>IObjectTypeBuilder&lt;T&gt;.Key</c>; the id flows through the DR-1
    /// <see cref="ObjectTypeDescriptor.IdAccessor"/> path like any object type.
    /// </summary>
    void Key(Expression<Func<TRel, object>> keySelector);

    /// <summary>
    /// Declares the LEFT endpoint of the association via a selector onto the
    /// property carrying it, then returns the step to declare the right
    /// endpoint with <see cref="IAssociationEndpointBuilder{TRel}.And{TRight}"/>.
    /// </summary>
    /// <typeparam name="TLeft">The left endpoint's object type (authoring sugar).</typeparam>
    IAssociationEndpointBuilder<TRel> Between<TLeft>(Expression<Func<TRel, TLeft>> endpoint);

    /// <summary>
    /// Declares an edge-attribute property on the association by selector.
    /// Mirrors <c>IObjectTypeBuilder&lt;T&gt;.Property</c>.
    /// </summary>
    IPropertyBuilder<TRel> Property(Expression<Func<TRel, object>> propertySelector);

    /// <summary>
    /// Declares an edge-attribute property on the association by name and
    /// CLR type, for callers that prefer the by-name form
    /// (parity with <see cref="IEdgeBuilder.Property{TProp}"/>).
    /// </summary>
    IAssociationBuilder<TRel> Property<TProp>(string name);
}

/// <summary>
/// The second step of the <see cref="IAssociationBuilder{TRel}.Between{TLeft}"/>
/// fluent chain: declares the RIGHT endpoint, and optionally the LEFT
/// endpoint's cardinality before doing so.
/// </summary>
/// <typeparam name="TRel">The CLR type backing the association object.</typeparam>
public interface IAssociationEndpointBuilder<TRel>
    where TRel : class
{
    /// <summary>
    /// Declares the cardinality of the LEFT endpoint relative to the
    /// association object (DR-6). Omitting this defaults to
    /// <see cref="EndpointCardinality.ManyToOne"/>, the only shape that forms
    /// a valid reified relation; any other value is flagged by <c>AONT210</c>.
    /// </summary>
    IAssociationEndpointBuilder<TRel> WithCardinality(EndpointCardinality cardinality);

    /// <summary>
    /// Declares the RIGHT endpoint of the association via a selector onto the
    /// property carrying it.
    /// </summary>
    /// <typeparam name="TRight">The right endpoint's object type (authoring sugar).</typeparam>
    IAssociationRightEndpointBuilder<TRel> And<TRight>(Expression<Func<TRel, TRight>> endpoint);
}

/// <summary>
/// The terminal step of the
/// <see cref="IAssociationBuilder{TRel}.Between{TLeft}"/> chain reached after
/// the RIGHT endpoint is declared: optionally declares the RIGHT endpoint's
/// cardinality (DR-6).
/// </summary>
/// <typeparam name="TRel">The CLR type backing the association object.</typeparam>
public interface IAssociationRightEndpointBuilder<TRel>
    where TRel : class
{
    /// <summary>
    /// Declares the cardinality of the RIGHT endpoint relative to the
    /// association object (DR-6). Omitting this defaults to
    /// <see cref="EndpointCardinality.ManyToOne"/>; any other value is flagged
    /// by <c>AONT210</c>.
    /// </summary>
    IAssociationRightEndpointBuilder<TRel> WithCardinality(EndpointCardinality cardinality);
}
