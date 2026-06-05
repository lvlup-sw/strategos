namespace Strategos.Ontology.Descriptors;

/// <summary>
/// One endpoint of a reified association (DR-4). An association is a standalone
/// object type (<see cref="ObjectKind.Association"/>) that links two endpoints
/// and may carry its own edge attributes.
/// </summary>
/// <remarks>
/// INV-8 (polyglot identity): an endpoint references its object type by
/// <see cref="DescriptorName"/> — a string — never by a CLR
/// <see cref="System.Type"/>. The authoring builder accepts a generic
/// <c>&lt;L&gt;</c>/<c>&lt;R&gt;</c> as sugar, but the resulting descriptor
/// stores only the descriptor name (so a SymbolKey-only ingested endpoint is
/// representable identically to a CLR one).
/// INV-7 (immutable): this is an immutable record carrying no mutation surface.
/// </remarks>
/// <param name="Role">
/// The property on the association object that carries this endpoint (e.g.
/// <c>Employee</c> / <c>Employer</c>). Distinguishes the left and right
/// endpoints when they share an object type (a self-association).
/// </param>
/// <param name="DescriptorName">
/// The descriptor name of the endpoint's object type (e.g. <c>AssocPerson</c>).
/// </param>
/// <param name="Cardinality">
/// The multiplicity of this endpoint relative to the association object
/// (DR-6). A valid reified relation requires <see cref="EndpointCardinality.ManyToOne"/>
/// on both endpoints — many association rows folding INTO one endpoint object.
/// The authoring default is <see cref="EndpointCardinality.ManyToOne"/>; the
/// analyzer rule <c>AONT210</c> flags any other value.
/// </param>
public sealed record AssociationEndpoint(
    string Role,
    string DescriptorName,
    EndpointCardinality Cardinality = EndpointCardinality.ManyToOne);
